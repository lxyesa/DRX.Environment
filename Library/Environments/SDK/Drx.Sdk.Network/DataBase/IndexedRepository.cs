using Drx.Sdk.Network.DataBase.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections.Concurrent;

namespace Drx.Sdk.Network.DataBase
{
    /// <summary>
    /// Provides a simplified, generic repository for storing and retrieving IIndexable and IXmlSerializable objects
    /// using the indexed file system. This class encapsulates the complexities of database configuration and file paths.
    /// </summary>
    /// <typeparam name="T">The type of object to be stored. Must implement IIndexable, IXmlSerializable, and have a parameterless constructor.</typeparam>
    public class IndexedRepository<T> where T : IIndexable, IXmlSerializable, new()
    {
        private readonly XmlDatabase _database;
        private readonly string _repositoryPath;
        private readonly string _indexFilePath;
        private readonly IndexSystemConfig _config;
        private readonly string _keyPrefix;
        
        // 添加内存缓存，提高读写性能
        private readonly ConcurrentDictionary<string, T> _cache;
        private bool _useCache = true;
        private int _cacheSize = 1000; // 默认缓存大小
        private int _saveInterval = 10; // 默认每10次Save操作进行一次实际保存
        private int _saveCounter = 0;
        private bool _hasPendingChanges = false;

        /// <summary>
        /// 获取仓库的根目录路径
        /// </summary>
        public string RepositoryPath => _repositoryPath;

        /// <summary>
        /// Initializes a new instance of the IndexedRepository class.
        /// </summary>
        /// <param name="repositoryPath">The root directory path where the data and index file will be stored.</param>
        /// <param name="keyPrefix">An optional prefix to ensure that the generated keys are valid XML tag names (e.g., "user_").</param>
        public IndexedRepository(string repositoryPath, string keyPrefix = "")
        {
            _database = new XmlDatabase();
            _repositoryPath = repositoryPath;
            _indexFilePath = Path.Combine(_repositoryPath, "index.xml");
            _keyPrefix = keyPrefix;
            _cache = new ConcurrentDictionary<string, T>();

            _config = new IndexSystemConfig
            {
                RootPath = _repositoryPath,
                UseHashForFilenames = false, // Use predictable filenames based on keys
                AutoCreateDirectories = true
            };
            
            // 确保存储库目录存在
            if (!Directory.Exists(_repositoryPath))
            {
                Directory.CreateDirectory(_repositoryPath);
            }
            
            // 确保索引文件目录存在（以防路径中包含子目录）
            string indexFileDirectory = Path.GetDirectoryName(_indexFilePath);
            if (!string.IsNullOrEmpty(indexFileDirectory) && !Directory.Exists(indexFileDirectory))
            {
                Directory.CreateDirectory(indexFileDirectory);
            }
            
            // 检查索引文件是否存在，如果不存在则创建一个空的索引文件
            if (!File.Exists(_indexFilePath))
            {
                // 创建一个空的根节点
                var rootNode = _database.CreateRoot(_indexFilePath);
                
                // 初始化必要的结构
                rootNode.GetOrCreateNode("items");
                
                // 保存更改
                _database.SaveChanges();
            }
        }

        /// <summary>
        /// 配置缓存和保存策略
        /// </summary>
        /// <param name="useCache">是否使用缓存</param>
        /// <param name="cacheSize">最大缓存项数</param>
        /// <param name="saveInterval">Save操作的批处理间隔</param>
        public void ConfigureCache(bool useCache = true, int cacheSize = 1000, int saveInterval = 10)
        {
            _useCache = useCache;
            _cacheSize = Math.Max(10, cacheSize);
            _saveInterval = Math.Max(1, saveInterval);
            
            if (!_useCache)
            {
                _cache.Clear();
            }
        }

        /// <summary>
        /// Retrieves a single object from the repository by its ID.
        /// </summary>
        /// <param name="id">The unique ID of the object.</param>
        /// <returns>The deserialized object, or null if not found.</returns>
        public T Get(string id)
        {
            string prefixedId = _keyPrefix + id;
            
            // 如果启用了缓存，先检查缓存
            if (_useCache && _cache.TryGetValue(prefixedId, out T cachedItem))
            {
                return cachedItem;
            }
            
            try
            {
                var item = _database.LoadSingleFromIndexSystem<T>(_indexFilePath, prefixedId);
                
                // 如果找到了项目且启用了缓存，则添加到缓存
                if (item != null && _useCache)
                {
                    // 如果缓存接近容量上限，移除一些旧项目
                    if (_cache.Count >= _cacheSize)
                    {
                        // 简单策略：移除10%的缓存项
                        int removeCount = _cacheSize / 10;
                        var keysToRemove = _cache.Keys.Take(removeCount).ToList();
                        foreach (var key in keysToRemove)
                        {
                            _cache.TryRemove(key, out _);
                        }
                    }
                    
                    _cache[prefixedId] = item;
                }
                
                return item;
            }
            catch (FileNotFoundException)
            {
                // 如果索引文件不存在，返回默认值（null）
                return default(T);
            }
            catch (Exception ex)
            {
                // 记录其他异常并返回默认值
                Console.WriteLine($"从索引系统加载对象 {id} 时出错: {ex.Message}");
                return default(T);
            }
        }

        /// <summary>
        /// Retrieves all objects from the repository.
        /// </summary>
        /// <returns>A list of all deserialized objects.</returns>
        public List<T> GetAll()
        {
            try
            {
                // 如果启用了缓存且缓存中有数据，先检查是否有未保存的更改
                if (_useCache && _cache.Count > 0 && _hasPendingChanges)
                {
                    // 确保先保存所有更改，以便获取最新数据
                    SaveChanges();
                }
                
                var items = _database.LoadFromIndexSystem<T>(_indexFilePath) ?? new List<T>();
                
                // 如果启用了缓存，更新缓存
                if (_useCache && items.Count <= _cacheSize)
                {
                    _cache.Clear();
                    foreach (var item in items)
                    {
                        _cache[_keyPrefix + item.Id] = item;
                    }
                }
                
                return items;
            }
            catch (FileNotFoundException)
            {
                // 如果索引文件不存在，返回一个空列表
                return new List<T>();
            }
            catch (Exception ex)
            {
                // 记录其他异常并返回一个空列表
                Console.WriteLine($"从索引系统加载对象时出错: {ex.Message}");
                return new List<T>();
            }
        }

        /// <summary>
        /// Saves or updates a single object in the repository.
        /// </summary>
        /// <param name="item">The object to save.</param>
        public void Save(T item)
        {
            string prefixedId = _keyPrefix + item.Id;
            
            // 更新缓存
            if (_useCache)
            {
                _cache[prefixedId] = item;
            }
            
            // 更新内存中的数据
            _database.UpdateInIndexSystem(item, _config, i => _keyPrefix + i.Id);
            _hasPendingChanges = true;
            
            // 增加计数器
            _saveCounter++;
            
            // 如果达到保存间隔或未使用缓存，则执行实际保存
            if (!_useCache || _saveCounter >= _saveInterval)
            {
                _database.SaveChanges();
                _saveCounter = 0;
                _hasPendingChanges = false;
            }
        }

        /// <summary>
        /// 更新单个对象，但不立即保存更改。
        /// 此方法适用于需要批量更新多个对象时提高性能。
        /// </summary>
        /// <param name="item">要更新的对象</param>
        public void Update(T item)
        {
            string prefixedId = _keyPrefix + item.Id;
            
            // 更新缓存
            if (_useCache)
            {
                _cache[prefixedId] = item;
            }
            
            _database.UpdateInIndexSystem(item, _config, i => _keyPrefix + i.Id);
            _hasPendingChanges = true;
        }

        /// <summary>
        /// 批量保存多个对象（高效）
        /// </summary>
        /// <param name="items">要保存的对象集合</param>
        /// <param name="maxBatchSize">每批次最大处理数量，默认为100</param>
        public void BatchSave(IEnumerable<T> items, int maxBatchSize = 100)
        {
            int count = 0;
            foreach (var item in items)
            {
                Update(item);
                count++;
                
                // 每处理maxBatchSize个对象，保存一次更改
                if (count % maxBatchSize == 0)
                {
                    _database.SaveChanges();
                    _hasPendingChanges = false;
                }
            }
            
            // 保存最后的更改
            if (count % maxBatchSize != 0)
            {
                _database.SaveChanges();
                _hasPendingChanges = false;
            }
        }

        /// <summary>
        /// 从存储库中删除指定ID的对象。
        /// </summary>
        /// <param name="id">要删除的对象ID</param>
        /// <returns>如果对象存在并被成功删除则返回true，否则返回false</returns>
        public bool Remove(string id)
        {
            string prefixedId = _keyPrefix + id;
            
            // 从缓存中移除
            if (_useCache)
            {
                _cache.TryRemove(prefixedId, out _);
            }
            
            try
            {
                // 从索引系统中移除对象
                bool removed = _database.RemoveFromIndexSystem<T>(_indexFilePath, prefixedId);
                
                if (removed)
                {
                    try
                    {
                        // 获取对象文件路径
                        string objectFileName = prefixedId + ".xml";
                        string objectFilePath = Path.Combine(_repositoryPath, objectFileName);
                        
                        // 删除对象文件
                        if (File.Exists(objectFilePath))
                        {
                            File.Delete(objectFilePath);
                        }
                        
                        _database.SaveChanges();
                        _hasPendingChanges = false;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"清理删除对象 {id} 的相关资源时出错: {ex.Message}");
                        // 即使清理失败也认为删除成功，因为对象已从索引中移除
                    }
                }
                
                return removed;
            }
            catch (FileNotFoundException)
            {
                // 如果索引文件不存在，则无法删除对象
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"删除对象 {id} 时出错: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 保存所有挂起的更改到磁盘。
        /// 在调用多个Update方法后使用此方法一次性保存所有更改。
        /// </summary>
        public void SaveChanges()
        {
            if (_hasPendingChanges)
            {
                _database.SaveChanges();
                _saveCounter = 0;
                _hasPendingChanges = false;
            }
        }

        /// <summary>
        /// Saves a collection of objects to the repository, overwriting any existing index.
        /// This is typically used for initial data seeding.
        /// </summary>
        /// <param name="items">The collection of objects to save.</param>
        public void SaveAll(IEnumerable<T> items)
        {
            // 清空缓存
            if (_useCache)
            {
                _cache.Clear();
            }
            
            // This method completely overwrites the old index, so use with caution.
            if(File.Exists(_indexFilePath))
            {
                File.Delete(_indexFilePath);
                var dataFiles = Directory.GetFiles(_repositoryPath, "*.xml").Where(f => !f.EndsWith("index.xml"));
                foreach(var file in dataFiles)
                {
                    File.Delete(file);
                }
            }
            
            _database.SaveToIndexSystem(items, _config, i => _keyPrefix + i.Id);
            _database.SaveChanges();
            _hasPendingChanges = false;
            
            // 更新缓存
            if (_useCache)
            {
                foreach (var item in items)
                {
                    // 只缓存到达上限前的项目
                    if (_cache.Count < _cacheSize)
                    {
                        _cache[_keyPrefix + item.Id] = item;
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// 根据条件查询对象
        /// </summary>
        /// <param name="predicate">查询条件</param>
        /// <returns>匹配条件的对象列表</returns>
        public List<T> Find(Func<T, bool> predicate)
        {
            try
            {
                // 如果启用了缓存且缓存中有数据，检查是否可以直接从缓存查询
                if (_useCache && _cache.Count > 0)
                {
                    // 如果有未保存的更改，先保存
                    if (_hasPendingChanges)
                    {
                        SaveChanges();
                    }
                    
                    // 判断缓存是否包含所有数据
                    int totalCount = GetAll().Count;
                    if (_cache.Count >= totalCount)
                    {
                        // 缓存包含所有数据，直接从缓存查询
                        return _cache.Values.Where(predicate).ToList();
                    }
                }
                
                // 缓存不完整或未启用，从磁盘加载所有数据
                var allItems = GetAll();
                return allItems.Where(predicate).ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"查询对象时出错: {ex.Message}");
                return new List<T>();
            }
        }

        /// <summary>
        /// 关闭数据库连接
        /// </summary>
        public void Close()
        {
            // 确保所有更改都已保存
            if (_hasPendingChanges)
            {
                SaveChanges();
            }
            
            _database.CloseAllFiles();
            
            // 清空缓存
            if (_useCache)
            {
                _cache.Clear();
            }
        }
    }
} 