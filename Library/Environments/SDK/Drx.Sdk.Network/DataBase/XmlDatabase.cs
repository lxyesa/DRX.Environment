using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Drx.Sdk.Network.DataBase
{
    /// <summary>
    /// XML数据库类，提供对XML文件的基本操作功能
    /// </summary>
    public class XmlDatabase
    {
        private readonly ConcurrentDictionary<string, object> _fileLocks = new ConcurrentDictionary<string, object>();
        
        // 文件句柄管理结构
        private class FileData
        {
            public XmlDocument Document { get; set; }
            public XmlNode RootNode { get; set; }
        }
        
        // 使用ConcurrentDictionary来管理文件句柄，确保线程安全
        private readonly ConcurrentDictionary<string, FileData> _fileHandles = new ConcurrentDictionary<string, FileData>();
        
        // 统计信息
        private int _savedFileCount = 0;
        private int _failedFileCount = 0;
        private DateTime _lastSaveTime = DateTime.MinValue;
        
        public XmlDatabase()
        {
        }

        // 文件锁获取逻辑
        private object GetFileLock(string filePath)
        {
            return _fileLocks.GetOrAdd(filePath, _ => new object());
        }
        
        /// <summary>
        /// 打开XML文件并返回根节点
        /// </summary>
        /// <param name="filePath">XML文件路径</param>
        /// <returns>根节点</returns>
        public XmlNode OpenRoot(string filePath)
        {
            // 如果文件已经打开，先尝试关闭它
            if (_fileHandles.TryGetValue(filePath, out _))
            {
                CloseFile(filePath);
            }
            
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException("要打开的XML文件不存在", filePath);
                }

                var doc = new XmlDocument();
                
                // 使用文件锁来确保文件被正确加载，同时避免其他进程的干扰
                using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    doc.Load(fileStream);
                }

                var rootElement = doc.DocumentElement;
                if (rootElement == null)
                {
                    throw new InvalidOperationException("XML文档没有根元素");
                }

                var xmlNode = new XmlNode(rootElement, doc, filePath, this);
                _fileHandles[filePath] = new FileData { Document = doc, RootNode = xmlNode };
                return xmlNode;
            }
            catch (Exception ex) when (
                ex is FileNotFoundException || 
                ex is DirectoryNotFoundException || 
                ex is IOException || 
                ex is UnauthorizedAccessException)
            {
                throw new FileNotFoundException($"无法打开XML文件: {ex.Message}", filePath, ex);
            }
            catch (XmlException ex)
            {
                throw new InvalidOperationException($"解析XML失败: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"打开文件时发生未知错误: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 创建新的XML根节点
        /// </summary>
        /// <param name="filePath">XML文件路径</param>
        /// <returns>创建的根节点</returns>
        public XmlNode CreateRoot(string filePath)
        {
            try
            {
                // 如果文件已经打开，先关闭它
                if (_fileHandles.TryGetValue(filePath, out _))
                {
                    CloseFile(filePath);
                }
                
                // 创建目录（如果不存在）
                string directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // 创建新XML文档
                var doc = new XmlDocument();
                var declaration = doc.CreateXmlDeclaration("1.0", "UTF-8", null);
                doc.AppendChild(declaration);
                
                var rootElement = doc.CreateElement("Root");
                doc.AppendChild(rootElement);
                
                // 保存到文件
                var xmlNode = new XmlNode(rootElement, doc, filePath, this);
                _fileHandles[filePath] = new FileData { Document = doc, RootNode = xmlNode };
                
                // 立即保存文件
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    doc.Save(fileStream);
                }
                
                return xmlNode;
            }
            catch (Exception ex)
            {
                throw new IOException($"创建XML文件失败: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 保存指定文件的更改
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public void SaveFile(string filePath)
        {
            if (!_fileHandles.TryGetValue(filePath, out var fileData))
            {
                throw new InvalidOperationException("尝试保存未打开的文件");
            }

            try
            {
                // 创建目录（如果不存在）
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 使用临时文件写入，然后替换原文件，这样可以减少文件损坏的风险
                var tempFilePath = filePath + ".tmp";
                
                // 使用文件锁确保文件写入的原子性
                using (var fileStream = new FileStream(tempFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    fileData.Document.Save(fileStream);
                }

                // 如果原文件存在，先删除它
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }

                // 重命名临时文件
                File.Move(tempFilePath, filePath);
                
                _savedFileCount++;
                _lastSaveTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                _failedFileCount++;
                throw new IOException($"保存文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 关闭指定的XML文件
        /// </summary>
        /// <param name="filePath">文件路径</param>
        public void CloseFile(string filePath)
        {
            _fileHandles.TryRemove(filePath, out _);
        }
        
        /// <summary>
        /// 保存所有未保存的更改
        /// </summary>
        public void SaveChanges()
        {
            int successCount = 0;
            int failCount = 0;
            
            foreach (var kvp in _fileHandles)
            {
                try
                {
                    SaveFile(kvp.Key);
                    successCount++;
                }
                catch (Exception)
                {
                    failCount++;
                }
            }
        }
        
        /// <summary>
        /// 关闭所有打开的文件
        /// </summary>
        public void CloseAllFiles()
        {
            var fileHandlesToClose = new List<string>(_fileHandles.Keys);
            
            foreach (var filePath in fileHandlesToClose)
            {
                CloseFile(filePath);
            }
        }
    }
} 