using Drx.Sdk.Text.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Web.KaxServer.Models;

namespace Web.KaxServer.Services
{
    public class ForumDataHelper
    {
        private readonly string _baseDataPath;
        private readonly string _categoriesPath;
        private readonly string _threadsPath;
        private readonly ILogger<ForumDataHelper> _logger;
        private static readonly object _fileLock = new object();

        public ForumDataHelper(IWebHostEnvironment env, ILogger<ForumDataHelper> logger)
        {
            _baseDataPath = Path.Combine(env.ContentRootPath, "data", "forum");
            _categoriesPath = Path.Combine(_baseDataPath, "categories");
            _threadsPath = Path.Combine(_baseDataPath, "threads");
            _logger = logger;
            EnsureDirectoriesExist();
        }

        private void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(_categoriesPath);
            Directory.CreateDirectory(_threadsPath);
        }

        public string GetCategoryPath(string categoryId) => Path.Combine(_categoriesPath, $"{categoryId}.xml");
        public string GetThreadPath(string threadId) => Path.Combine(_threadsPath, $"{threadId}.xml");

        public string GenerateId(string input)
        {
            using (var sha256 = SHA256.Create())
            {
                var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(input + Guid.NewGuid())); // Add salt to avoid collision
                var builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString(0, 64);
            }
        }

        public List<ForumCategoryModel> GetAllCategories()
        {
            var categories = new List<ForumCategoryModel>();
            if (!Directory.Exists(_categoriesPath)) 
            {
                _logger.LogWarning("Categories directory not found at {Path}", _categoriesPath);
                return categories;
            }

            var categoryFiles = Directory.GetFiles(_categoriesPath, "*.xml");
            _logger.LogInformation("Found {Count} category files in {Path}.", categoryFiles.Length, _categoriesPath);
            foreach (var file in categoryFiles)
            {
                try
                {
                    var category = Xml.DeserializeFromFile<ForumCategoryModel>(file);
                    categories.Add(category);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize category file: {File}", file);
                }
            }
            return categories.OrderBy(c => c.Title).ToList();
        }

        public ForumCategoryModel GetCategory(string categoryId)
        {
            if (string.IsNullOrEmpty(categoryId)) return null;
            var path = GetCategoryPath(categoryId);
            if (!File.Exists(path)) 
            {
                _logger.LogWarning("Category file not found: {Path}", path);
                return null;
            }
            try
            {
                return Xml.DeserializeFromFile<ForumCategoryModel>(path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize category file: {File}", path);
                return null;
            }
        }

        public ForumThreadModel GetThread(string threadId)
        {
            if (string.IsNullOrEmpty(threadId)) return null;
            var path = GetThreadPath(threadId);
            if (!File.Exists(path)) 
            {
                _logger.LogWarning("Thread file not found: {Path}", path);
                return null;
            }
            try
            {
                return Xml.DeserializeFromFile<ForumThreadModel>(path);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize thread file: {File}", path);
                return null;
            }
        }

        public void SaveCategory(ForumCategoryModel category)
        {
            lock (_fileLock)
            {
                var path = GetCategoryPath(category.Id);
                Xml.SerializeToFile(category, path);
            }
        }

        public void SaveThread(ForumThreadModel thread)
        {
            lock (_fileLock)
            {
                var path = GetThreadPath(thread.Id);
                Xml.SerializeToFile(thread, path);
            }
        }
    }
} 