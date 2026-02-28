using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Shared;

namespace Drx.Sdk.Network.Http.ResourceManagement
{
    /// <summary>
    /// 资源索引管理器：负责资源目录的扫描、索引的持久化、验证和实时监控
    /// 核心逻辑：
    ///   - 无索引文件时全量扫描并生成索引
    ///   - 有索引文件时仅验证有效性并清理失效条目，增量补充新文件
    ///   - 支持子索引：当子目录配置了子索引时，主索引只追踪子索引引用，不追踪其内部文件
    ///   - FileSystemWatcher 实时监控文件系统变化
    ///   - 后台周期性刷新（可配置）
    /// </summary>
    public class ResourceIndexManager : IDisposable
    {
        #region 常量与字段

        private const string MainIndexFileName = ".drx_resource_index.json";
        private const string SubIndexPrefix = ".drx_resource_index_";
        private const string SubIndexSuffix = ".json";
        private const int FileHashBufferSize = 1024 * 1024;

        private readonly string _resourceRoot;
        private readonly string _mainIndexPath;
        private readonly HashSet<string> _excludePatterns;
        private readonly string _version;

        private ResourceIndexDocument _mainIndex;
        private readonly ConcurrentDictionary<string, ResourceIndexDocument> _subIndexes = new();
        private readonly ConcurrentDictionary<string, ResourceIndexEntry> _memoryIndex = new();
        private readonly SemaphoreSlim _indexLock = new(1, 1);
        private readonly SemaphoreSlim _saveLock = new(1, 1);

        private FileSystemWatcher? _fileWatcher;
        private Timer? _periodicRefreshTimer;
        private bool _disposed;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        #endregion

        /// <summary>
        /// 创建资源索引管理器实例
        /// </summary>
        /// <param name="resourceRoot">资源根目录的绝对路径</param>
        /// <param name="excludePatterns">要排除的目录或文件模式（如 ".temp", "node_modules", ".git"）</param>
        /// <param name="version">索引版本号，版本不匹配时会重建索引</param>
        public ResourceIndexManager(string resourceRoot, IEnumerable<string>? excludePatterns = null, string version = "1.0")
        {
            if (string.IsNullOrEmpty(resourceRoot))
                throw new ArgumentNullException(nameof(resourceRoot));

            _resourceRoot = Path.GetFullPath(resourceRoot);
            _mainIndexPath = Path.Combine(_resourceRoot, MainIndexFileName);
            _excludePatterns = new HashSet<string>(excludePatterns ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            _version = version;
            _mainIndex = new ResourceIndexDocument { RootPath = _resourceRoot, Version = version };
        }

        #region 公开属性

        /// <summary>
        /// 资源根目录
        /// </summary>
        public string ResourceRoot => _resourceRoot;

        /// <summary>
        /// 内存中的索引条目总数
        /// </summary>
        public int EntryCount => _memoryIndex.Count;

        /// <summary>
        /// 主索引文档
        /// </summary>
        public ResourceIndexDocument MainIndex => _mainIndex;

        #endregion

        #region 初始化与启动

        /// <summary>
        /// 初始化索引系统：加载或构建索引并启动文件监控
        /// </summary>
        public async Task InitializeAsync(bool enableFileWatcher = true, int periodicRefreshSeconds = 0)
        {
            Directory.CreateDirectory(_resourceRoot);

            if (File.Exists(_mainIndexPath))
            {
                var loadSuccess = await LoadIndexFromDiskAsync().ConfigureAwait(false);
                if (loadSuccess && _mainIndex.Version == _version)
                {
                    await ValidateAndCleanIndexAsync().ConfigureAwait(false);
                    await ScanForNewFilesAsync().ConfigureAwait(false);
                }
                else
                {
                    Logger.Warn("[ResourceIndex] 索引版本不匹配或加载失败，将重建索引");
                    await FullScanAsync().ConfigureAwait(false);
                }
            }
            else
            {
                Logger.Info("ResourceIndex", "未发现索引文件，执行全量扫描");
                await FullScanAsync().ConfigureAwait(false);
            }

            if (enableFileWatcher)
                StartFileWatcher();

            if (periodicRefreshSeconds > 0)
                StartPeriodicRefresh(periodicRefreshSeconds);

            Logger.Info("ResourceIndex", $"索引初始化完成，共 {_memoryIndex.Count} 个条目");
        }

        #endregion

        #region 全量扫描

        /// <summary>
        /// 全量扫描 resources 目录并构建索引
        /// </summary>
        private async Task FullScanAsync()
        {
            await _indexLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _memoryIndex.Clear();
                _mainIndex = new ResourceIndexDocument
                {
                    RootPath = _resourceRoot,
                    Version = _version,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                await ScanDirectoryAsync(_resourceRoot, _mainIndex).ConfigureAwait(false);
                await SaveIndexToDiskAsync().ConfigureAwait(false);

                Logger.Info("ResourceIndex", $"全量扫描完成，发现 {_memoryIndex.Count} 个文件");
            }
            finally
            {
                _indexLock.Release();
            }
        }

        /// <summary>
        /// 递归扫描目录，构建索引条目
        /// </summary>
        private async Task ScanDirectoryAsync(string directory, ResourceIndexDocument targetIndex)
        {
            if (!Directory.Exists(directory)) return;

            var subIndexFile = FindSubIndexFile(directory);
            if (subIndexFile != null && directory != _resourceRoot)
            {
                await LoadSubIndexAsync(directory, subIndexFile, targetIndex).ConfigureAwait(false);
                return;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(directory);
            }
            catch (UnauthorizedAccessException)
            {
                Logger.Warn($"[ResourceIndex] 无权限访问目录: {directory}");
                return;
            }

            var tasks = new List<Task>();

            foreach (var filePath in files)
            {
                var fileName = Path.GetFileName(filePath);
                if (IsIndexFile(fileName) || ShouldExclude(fileName)) continue;

                tasks.Add(Task.Run(async () =>
                {
                    var entry = await CreateEntryFromFileAsync(filePath).ConfigureAwait(false);
                    if (entry != null)
                    {
                        targetIndex.Entries[entry.Id] = entry;
                        _memoryIndex[entry.Id] = entry;
                    }
                }));
            }

            if (tasks.Count > 0)
                await Task.WhenAll(tasks).ConfigureAwait(false);

            string[] subdirs;
            try
            {
                subdirs = Directory.GetDirectories(directory);
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            foreach (var subdir in subdirs)
            {
                var dirName = Path.GetFileName(subdir);
                if (ShouldExclude(dirName)) continue;

                await ScanDirectoryAsync(subdir, targetIndex).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// 查找目录中的子索引文件
        /// </summary>
        private string? FindSubIndexFile(string directory)
        {
            try
            {
                var files = Directory.GetFiles(directory, $"{SubIndexPrefix}*{SubIndexSuffix}");
                return files.Length > 0 ? files[0] : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 加载子索引并注册到主索引
        /// </summary>
        private async Task LoadSubIndexAsync(string directory, string subIndexPath, ResourceIndexDocument parentIndex)
        {
            try
            {
                var json = await File.ReadAllTextAsync(subIndexPath).ConfigureAwait(false);
                var subIndex = JsonSerializer.Deserialize<ResourceIndexDocument>(json, JsonOptions);
                if (subIndex == null) return;

                var subIndexName = Path.GetFileNameWithoutExtension(subIndexPath)
                    .Replace(SubIndexPrefix.TrimEnd('_'), "")
                    .TrimStart('_');

                var relativePath = GetRelativePath(directory);
                _subIndexes[subIndexName] = subIndex;

                parentIndex.SubIndexes[subIndexName] = relativePath;

                var subIndexEntry = new ResourceIndexEntry
                {
                    Id = Guid.NewGuid().ToString("N"),
                    RelativePath = relativePath,
                    IsSubIndex = true,
                    SubIndexName = subIndexName
                };
                parentIndex.Entries[subIndexEntry.Id] = subIndexEntry;

                foreach (var entry in subIndex.Entries.Values)
                {
                    _memoryIndex[entry.Id] = entry;
                }

                Logger.Info("ResourceIndex", $"加载子索引: {subIndexName}，包含 {subIndex.Entries.Count} 个条目");
            }
            catch (Exception ex)
            {
                Logger.Error($"[ResourceIndex] 加载子索引失败: {subIndexPath}，错误: {ex.Message}");
                await ScanDirectoryAsync(directory, parentIndex).ConfigureAwait(false);
            }
        }

        #endregion

        #region 验证与增量扫描

        /// <summary>
        /// 验证现有索引的有效性，清理不存在的文件条目
        /// 优化策略：批量并行验证文件存在性，基于 LastModified + Size 快速判断
        /// </summary>
        private async Task ValidateAndCleanIndexAsync()
        {
            await _indexLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var invalidIds = new ConcurrentBag<string>();
                var entriesToValidate = _mainIndex.Entries.ToArray();

                await Parallel.ForEachAsync(entriesToValidate, new ParallelOptions
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                },
                async (kvp, ct) =>
                {
                    var entry = kvp.Value;

                    if (entry.IsSubIndex)
                    {
                        var subDir = Path.Combine(_resourceRoot, entry.RelativePath);
                        if (!Directory.Exists(subDir))
                        {
                            invalidIds.Add(kvp.Key);
                            if (entry.SubIndexName != null)
                                _subIndexes.TryRemove(entry.SubIndexName, out _);
                        }
                        return;
                    }

                    var fullPath = Path.Combine(_resourceRoot, entry.RelativePath);
                    if (!File.Exists(fullPath))
                    {
                        invalidIds.Add(kvp.Key);
                        return;
                    }

                    try
                    {
                        var fileInfo = new FileInfo(fullPath);
                        if (fileInfo.Length != entry.Size ||
                            fileInfo.LastWriteTimeUtc.Ticks != entry.LastModifiedTicks)
                        {
                            var updatedEntry = await CreateEntryFromFileAsync(fullPath).ConfigureAwait(false);
                            if (updatedEntry != null)
                            {
                                updatedEntry.Id = entry.Id;
                                _mainIndex.Entries[kvp.Key] = updatedEntry;
                                _memoryIndex[entry.Id] = updatedEntry;
                            }
                        }
                        else
                        {
                            _memoryIndex[entry.Id] = entry;
                        }
                    }
                    catch
                    {
                        invalidIds.Add(kvp.Key);
                    }
                }).ConfigureAwait(false);

                var removedCount = 0;
                foreach (var id in invalidIds)
                {
                    _mainIndex.Entries.Remove(id);
                    _memoryIndex.TryRemove(id, out _);
                    removedCount++;
                }

                var invalidSubIndexes = _mainIndex.SubIndexes
                    .Where(kvp => !Directory.Exists(Path.Combine(_resourceRoot, kvp.Value)))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in invalidSubIndexes)
                {
                    _mainIndex.SubIndexes.Remove(key);
                    _subIndexes.TryRemove(key, out _);
                }

                if (removedCount > 0 || invalidSubIndexes.Count > 0)
                {
                    _mainIndex.UpdatedAt = DateTime.UtcNow;
                    await SaveIndexToDiskAsync().ConfigureAwait(false);
                    Logger.Info("ResourceIndex", $"清理了 {removedCount} 个无效条目和 {invalidSubIndexes.Count} 个无效子索引");
                }
            }
            finally
            {
                _indexLock.Release();
            }
        }

        /// <summary>
        /// 扫描新文件：查找索引中尚未收录的文件并追加
        /// </summary>
        private async Task ScanForNewFilesAsync()
        {
            var existingPaths = new HashSet<string>(
                _mainIndex.Entries.Values
                    .Where(e => !e.IsSubIndex)
                    .Select(e => e.RelativePath),
                StringComparer.OrdinalIgnoreCase);

            var newEntries = new ConcurrentBag<ResourceIndexEntry>();

            await ScanForNewFilesInDirectoryAsync(_resourceRoot, existingPaths, newEntries).ConfigureAwait(false);

            if (!newEntries.IsEmpty)
            {
                await _indexLock.WaitAsync().ConfigureAwait(false);
                try
                {
                    foreach (var entry in newEntries)
                    {
                        _mainIndex.Entries[entry.Id] = entry;
                        _memoryIndex[entry.Id] = entry;
                    }
                    _mainIndex.UpdatedAt = DateTime.UtcNow;
                    await SaveIndexToDiskAsync().ConfigureAwait(false);
                    Logger.Info("ResourceIndex", $"增量扫描新增 {newEntries.Count} 个文件");
                }
                finally
                {
                    _indexLock.Release();
                }
            }
        }

        /// <summary>
        /// 递归扫描目录中的新文件
        /// </summary>
        private async Task ScanForNewFilesInDirectoryAsync(string directory, HashSet<string> existingPaths, ConcurrentBag<ResourceIndexEntry> newEntries)
        {
            if (!Directory.Exists(directory)) return;

            if (directory != _resourceRoot && FindSubIndexFile(directory) != null)
                return;

            try
            {
                foreach (var filePath in Directory.GetFiles(directory))
                {
                    var fileName = Path.GetFileName(filePath);
                    if (IsIndexFile(fileName) || ShouldExclude(fileName)) continue;

                    var relativePath = GetRelativePath(filePath);
                    if (!existingPaths.Contains(relativePath))
                    {
                        var entry = await CreateEntryFromFileAsync(filePath).ConfigureAwait(false);
                        if (entry != null) newEntries.Add(entry);
                    }
                }

                foreach (var subdir in Directory.GetDirectories(directory))
                {
                    var dirName = Path.GetFileName(subdir);
                    if (ShouldExclude(dirName)) continue;

                    await ScanForNewFilesInDirectoryAsync(subdir, existingPaths, newEntries).ConfigureAwait(false);
                }
            }
            catch (UnauthorizedAccessException)
            {
                Logger.Warn($"[ResourceIndex] 增量扫描时无权限访问: {directory}");
            }
        }

        #endregion

        #region 索引操作 API

        /// <summary>
        /// 异步追加文件到索引（上传完成后调用，不阻塞上传响应）
        /// </summary>
        public async Task<ResourceIndexEntry?> AddFileAsync(string filePath)
        {
            if (!File.Exists(filePath)) return null;

            var entry = await CreateEntryFromFileAsync(filePath).ConfigureAwait(false);
            if (entry == null) return null;

            await _indexLock.WaitAsync().ConfigureAwait(false);
            try
            {
                _mainIndex.Entries[entry.Id] = entry;
                _memoryIndex[entry.Id] = entry;
                _mainIndex.UpdatedAt = DateTime.UtcNow;
            }
            finally
            {
                _indexLock.Release();
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await SaveIndexToDiskAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Error($"[ResourceIndex] 保存索引时出错: {ex.Message}");
                }
            });

            return entry;
        }

        /// <summary>
        /// 异步追加文件到子索引
        /// </summary>
        public async Task<ResourceIndexEntry?> AddFileToSubIndexAsync(string filePath, string subIndexName)
        {
            if (!File.Exists(filePath) || string.IsNullOrEmpty(subIndexName)) return null;

            var entry = await CreateEntryFromFileAsync(filePath).ConfigureAwait(false);
            if (entry == null) return null;

            await _indexLock.WaitAsync().ConfigureAwait(false);
            try
            {
                if (_subIndexes.TryGetValue(subIndexName, out var subIndex))
                {
                    subIndex.Entries[entry.Id] = entry;
                    subIndex.UpdatedAt = DateTime.UtcNow;
                    _memoryIndex[entry.Id] = entry;
                }
                else
                {
                    var directory = Path.GetDirectoryName(filePath) ?? _resourceRoot;
                    subIndex = new ResourceIndexDocument
                    {
                        RootPath = directory,
                        Version = _version,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    subIndex.Entries[entry.Id] = entry;
                    _subIndexes[subIndexName] = subIndex;
                    _memoryIndex[entry.Id] = entry;

                    var relativePath = GetRelativePath(directory);
                    _mainIndex.SubIndexes[subIndexName] = relativePath;

                    var subIndexEntry = new ResourceIndexEntry
                    {
                        Id = Guid.NewGuid().ToString("N"),
                        RelativePath = relativePath,
                        IsSubIndex = true,
                        SubIndexName = subIndexName
                    };
                    _mainIndex.Entries[subIndexEntry.Id] = subIndexEntry;
                }
            }
            finally
            {
                _indexLock.Release();
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await SaveSubIndexToDiskAsync(subIndexName).ConfigureAwait(false);
                    await SaveIndexToDiskAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Error($"[ResourceIndex] 保存子索引时出错: {ex.Message}");
                }
            });

            return entry;
        }

        /// <summary>
        /// 根据资源 ID 查询索引条目
        /// </summary>
        public ResourceIndexEntry? GetById(string id)
        {
            _memoryIndex.TryGetValue(id, out var entry);
            return entry;
        }

        /// <summary>
        /// 根据相对路径查询索引条目
        /// </summary>
        public ResourceIndexEntry? GetByPath(string relativePath)
        {
            return _memoryIndex.Values.FirstOrDefault(e =>
                string.Equals(e.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 根据文件名查询索引条目（可能返回多个同名文件）
        /// </summary>
        public IEnumerable<ResourceIndexEntry> GetByFileName(string fileName)
        {
            return _memoryIndex.Values.Where(e =>
                string.Equals(Path.GetFileName(e.RelativePath), fileName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 获取文件的完整磁盘路径
        /// </summary>
        public string? ResolveFullPath(string id)
        {
            if (_memoryIndex.TryGetValue(id, out var entry))
            {
                var fullPath = Path.Combine(_resourceRoot, entry.RelativePath);
                return File.Exists(fullPath) ? fullPath : null;
            }
            return null;
        }

        /// <summary>
        /// 手动刷新索引（重新验证并增量扫描）
        /// </summary>
        public async Task RefreshAsync()
        {
            Logger.Info("ResourceIndex", "开始手动刷新索引...");
            await ValidateAndCleanIndexAsync().ConfigureAwait(false);
            await ScanForNewFilesAsync().ConfigureAwait(false);
            Logger.Info("ResourceIndex", $"手动刷新完成，当前 {_memoryIndex.Count} 个条目");
        }

        /// <summary>
        /// 创建子索引：为指定目录配置独立的子索引文件
        /// </summary>
        public async Task CreateSubIndexAsync(string directoryRelativePath, string subIndexName)
        {
            var fullDir = Path.Combine(_resourceRoot, directoryRelativePath);
            Directory.CreateDirectory(fullDir);

            var subIndex = new ResourceIndexDocument
            {
                RootPath = fullDir,
                Version = _version,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _subIndexes[subIndexName] = subIndex;
            _mainIndex.SubIndexes[subIndexName] = directoryRelativePath;

            await SaveSubIndexToDiskAsync(subIndexName).ConfigureAwait(false);
            await SaveIndexToDiskAsync().ConfigureAwait(false);

            Logger.Info("ResourceIndex", $"创建子索引: {subIndexName} -> {directoryRelativePath}");
        }

        #endregion

        #region FileSystemWatcher 实时监控

        /// <summary>
        /// 启动文件系统监控
        /// </summary>
        private void StartFileWatcher()
        {
            try
            {
                _fileWatcher = new FileSystemWatcher(_resourceRoot)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                                   NotifyFilters.Size | NotifyFilters.LastWrite,
                    EnableRaisingEvents = true
                };

                _fileWatcher.Created += OnFileCreated;
                _fileWatcher.Deleted += OnFileDeleted;
                _fileWatcher.Renamed += OnFileRenamed;
                _fileWatcher.Changed += OnFileChanged;
                _fileWatcher.Error += OnWatcherError;

                Logger.Info("ResourceIndex", "文件系统监控已启动");
            }
            catch (Exception ex)
            {
                Logger.Error($"[ResourceIndex] 启动文件监控失败: {ex.Message}");
            }
        }

        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            if (IsIndexFile(Path.GetFileName(e.FullPath))) return;
            if (ShouldExclude(Path.GetFileName(e.FullPath))) return;

            _ = Task.Run(async () =>
            {
                await Task.Delay(200).ConfigureAwait(false);
                try
                {
                    if (File.Exists(e.FullPath))
                    {
                        await AddFileAsync(e.FullPath).ConfigureAwait(false);
                        Logger.Debug($"[ResourceIndex] 检测到新文件: {e.Name}");
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[ResourceIndex] 处理新文件事件失败: {ex.Message}");
                }
            });
        }

        private void OnFileDeleted(object sender, FileSystemEventArgs e)
        {
            if (IsIndexFile(Path.GetFileName(e.FullPath))) return;

            _ = Task.Run(async () =>
            {
                var relativePath = GetRelativePath(e.FullPath);
                var entry = _memoryIndex.Values.FirstOrDefault(en =>
                    string.Equals(en.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    _memoryIndex.TryRemove(entry.Id, out _);
                    await _indexLock.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        _mainIndex.Entries.Remove(entry.Id);
                        _mainIndex.UpdatedAt = DateTime.UtcNow;
                    }
                    finally
                    {
                        _indexLock.Release();
                    }
                    await SaveIndexToDiskAsync().ConfigureAwait(false);
                    Logger.Debug($"[ResourceIndex] 检测到文件删除: {e.Name}");
                }
            });
        }

        private void OnFileRenamed(object sender, RenamedEventArgs e)
        {
            if (IsIndexFile(Path.GetFileName(e.FullPath))) return;

            _ = Task.Run(async () =>
            {
                var oldRelativePath = GetRelativePath(e.OldFullPath);
                var entry = _memoryIndex.Values.FirstOrDefault(en =>
                    string.Equals(en.RelativePath, oldRelativePath, StringComparison.OrdinalIgnoreCase));

                if (entry != null)
                {
                    _memoryIndex.TryRemove(entry.Id, out _);
                    await _indexLock.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        _mainIndex.Entries.Remove(entry.Id);
                    }
                    finally
                    {
                        _indexLock.Release();
                    }
                }

                if (File.Exists(e.FullPath))
                {
                    await AddFileAsync(e.FullPath).ConfigureAwait(false);
                    Logger.Debug($"[ResourceIndex] 检测到文件重命名: {e.OldName} -> {e.Name}");
                }
            });
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (IsIndexFile(Path.GetFileName(e.FullPath))) return;
            if (ShouldExclude(Path.GetFileName(e.FullPath))) return;

            _ = Task.Run(async () =>
            {
                await Task.Delay(300).ConfigureAwait(false);
                try
                {
                    if (!File.Exists(e.FullPath)) return;

                    var relativePath = GetRelativePath(e.FullPath);
                    var existing = _memoryIndex.Values.FirstOrDefault(en =>
                        string.Equals(en.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase));

                    if (existing != null)
                    {
                        var fileInfo = new FileInfo(e.FullPath);
                        if (fileInfo.Length != existing.Size || fileInfo.LastWriteTimeUtc.Ticks != existing.LastModifiedTicks)
                        {
                            var updatedEntry = await CreateEntryFromFileAsync(e.FullPath).ConfigureAwait(false);
                            if (updatedEntry != null)
                            {
                                updatedEntry.Id = existing.Id;
                                _memoryIndex[existing.Id] = updatedEntry;
                                await _indexLock.WaitAsync().ConfigureAwait(false);
                                try
                                {
                                    _mainIndex.Entries[existing.Id] = updatedEntry;
                                    _mainIndex.UpdatedAt = DateTime.UtcNow;
                                }
                                finally
                                {
                                    _indexLock.Release();
                                }
                                await SaveIndexToDiskAsync().ConfigureAwait(false);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"[ResourceIndex] 处理文件变更事件失败: {ex.Message}");
                }
            });
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            Logger.Error($"[ResourceIndex] 文件系统监控出错: {e.GetException()?.Message}");
            try
            {
                _fileWatcher?.Dispose();
                StartFileWatcher();
            }
            catch (Exception ex)
            {
                Logger.Error($"[ResourceIndex] 重启文件监控失败: {ex.Message}");
            }
        }

        #endregion

        #region 周期性刷新

        /// <summary>
        /// 启动后台周期刷新定时器
        /// </summary>
        private void StartPeriodicRefresh(int intervalSeconds)
        {
            _periodicRefreshTimer = new Timer(async _ =>
            {
                try
                {
                    await RefreshAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Error($"[ResourceIndex] 周期刷新失败: {ex.Message}");
                }
            }, null, TimeSpan.FromSeconds(intervalSeconds), TimeSpan.FromSeconds(intervalSeconds));

            Logger.Info("ResourceIndex", $"后台周期刷新已启动，间隔 {intervalSeconds} 秒");
        }

        #endregion

        #region 持久化

        /// <summary>
        /// 从磁盘加载主索引
        /// </summary>
        private async Task<bool> LoadIndexFromDiskAsync()
        {
            try
            {
                var json = await File.ReadAllTextAsync(_mainIndexPath).ConfigureAwait(false);
                var document = JsonSerializer.Deserialize<ResourceIndexDocument>(json, JsonOptions);
                if (document == null) return false;

                _mainIndex = document;

                foreach (var entry in document.Entries.Values)
                {
                    _memoryIndex[entry.Id] = entry;
                }

                foreach (var kvp in document.SubIndexes)
                {
                    var subDir = Path.Combine(_resourceRoot, kvp.Value);
                    var subIndexFile = FindSubIndexFile(subDir);
                    if (subIndexFile != null)
                    {
                        try
                        {
                            var subJson = await File.ReadAllTextAsync(subIndexFile).ConfigureAwait(false);
                            var subIndex = JsonSerializer.Deserialize<ResourceIndexDocument>(subJson, JsonOptions);
                            if (subIndex != null)
                            {
                                _subIndexes[kvp.Key] = subIndex;
                                foreach (var entry in subIndex.Entries.Values)
                                    _memoryIndex[entry.Id] = entry;
                            }
                        }
                        catch (Exception ex)
                        {
                            Logger.Warn($"[ResourceIndex] 加载子索引 {kvp.Key} 失败: {ex.Message}");
                        }
                    }
                }

                Logger.Info("ResourceIndex", $"从磁盘加载索引: {document.Entries.Count} 个主条目");
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error($"[ResourceIndex] 加载索引文件失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 将主索引保存到磁盘
        /// </summary>
        private async Task SaveIndexToDiskAsync()
        {
            await _saveLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var json = JsonSerializer.Serialize(_mainIndex, JsonOptions);
                var tempFile = _mainIndexPath + ".tmp";
                await File.WriteAllTextAsync(tempFile, json).ConfigureAwait(false);

                if (File.Exists(_mainIndexPath))
                    File.Replace(tempFile, _mainIndexPath, null);
                else
                    File.Move(tempFile, _mainIndexPath);
            }
            catch (Exception ex)
            {
                Logger.Error($"[ResourceIndex] 保存索引失败: {ex.Message}");
            }
            finally
            {
                _saveLock.Release();
            }
        }

        /// <summary>
        /// 将子索引保存到磁盘
        /// </summary>
        private async Task SaveSubIndexToDiskAsync(string subIndexName)
        {
            if (!_subIndexes.TryGetValue(subIndexName, out var subIndex)) return;
            if (!_mainIndex.SubIndexes.TryGetValue(subIndexName, out var relativePath)) return;

            await _saveLock.WaitAsync().ConfigureAwait(false);
            try
            {
                var subDir = Path.Combine(_resourceRoot, relativePath);
                Directory.CreateDirectory(subDir);

                var subIndexPath = Path.Combine(subDir, $"{SubIndexPrefix}{subIndexName}{SubIndexSuffix}");
                var json = JsonSerializer.Serialize(subIndex, JsonOptions);
                var tempFile = subIndexPath + ".tmp";
                await File.WriteAllTextAsync(tempFile, json).ConfigureAwait(false);

                if (File.Exists(subIndexPath))
                    File.Replace(tempFile, subIndexPath, null);
                else
                    File.Move(tempFile, subIndexPath);
            }
            catch (Exception ex)
            {
                Logger.Error($"[ResourceIndex] 保存子索引 {subIndexName} 失败: {ex.Message}");
            }
            finally
            {
                _saveLock.Release();
            }
        }

        #endregion

        #region 工具方法

        /// <summary>
        /// 从文件创建索引条目（计算哈希、获取元数据）
        /// </summary>
        private async Task<ResourceIndexEntry?> CreateEntryFromFileAsync(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists) return null;

                var entry = new ResourceIndexEntry
                {
                    RelativePath = GetRelativePath(filePath),
                    Size = fileInfo.Length,
                    LastModifiedTicks = fileInfo.LastWriteTimeUtc.Ticks
                };

                entry.Hash = await ComputeFileHashAsync(filePath).ConfigureAwait(false);

                return entry;
            }
            catch (Exception ex)
            {
                Logger.Warn($"[ResourceIndex] 创建索引条目失败: {filePath}，错误: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 计算文件哈希（SHA256 前 16 字节的十六进制表示，兼顾安全性和性能）
        /// </summary>
        private static async Task<string> ComputeFileHashAsync(string filePath)
        {
            try
            {
                using var sha256 = SHA256.Create();
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
                    FileHashBufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

                var hashBytes = await sha256.ComputeHashAsync(stream).ConfigureAwait(false);
                return Convert.ToHexString(hashBytes, 0, 16).ToLowerInvariant();
            }
            catch
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 获取文件的相对路径（相对于 resources 根目录，使用 / 分隔符）
        /// </summary>
        private string GetRelativePath(string fullPath)
        {
            var relative = Path.GetRelativePath(_resourceRoot, fullPath);
            return relative.Replace('\\', '/');
        }

        /// <summary>
        /// 判断是否为索引文件
        /// </summary>
        private static bool IsIndexFile(string fileName)
        {
            return fileName.StartsWith(".drx_resource_index", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 判断是否应排除该文件/目录
        /// </summary>
        private bool ShouldExclude(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;

            foreach (var pattern in _excludePatterns)
            {
                if (name.Equals(pattern, StringComparison.OrdinalIgnoreCase)) return true;
                if (name.StartsWith(pattern, StringComparison.OrdinalIgnoreCase)) return true;
                if (pattern.StartsWith(".") && name.EndsWith(pattern, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _fileWatcher?.Dispose();
            _periodicRefreshTimer?.Dispose();
            _indexLock.Dispose();
            _saveLock.Dispose();
        }

        #endregion
    }
}
