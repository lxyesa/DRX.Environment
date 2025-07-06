using Drx.Sdk.Network.DataBase.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

            _config = new IndexSystemConfig
            {
                RootPath = _repositoryPath,
                UseHashForFilenames = false, // Use predictable filenames based on keys
                AutoCreateDirectories = true
            };
        }

        /// <summary>
        /// Retrieves a single object from the repository by its ID.
        /// </summary>
        /// <param name="id">The unique ID of the object.</param>
        /// <returns>The deserialized object, or null if not found.</returns>
        public T Get(string id)
        {
            return _database.LoadSingleFromIndexSystem<T>(_indexFilePath, _keyPrefix + id);
        }

        /// <summary>
        /// Retrieves all objects from the repository.
        /// </summary>
        /// <returns>A list of all deserialized objects.</returns>
        public List<T> GetAll()
        {
            return _database.LoadFromIndexSystem<T>(_indexFilePath) ?? new List<T>();
        }

        /// <summary>
        /// Saves or updates a single object in the repository.
        /// </summary>
        /// <param name="item">The object to save.</param>
        public void Save(T item)
        {
            _database.UpdateInIndexSystem(item, _config, i => _keyPrefix + i.Id);
            _database.SaveChanges();
        }

        /// <summary>
        /// Saves a collection of objects to the repository, overwriting any existing index.
        /// This is typically used for initial data seeding.
        /// </summary>
        /// <param name="items">The collection of objects to save.</param>
        public void SaveAll(IEnumerable<T> items)
        {
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
        }

        /// <summary>
        /// Closes all file handles held by the underlying database instance.
        /// </summary>
        public void Close()
        {
            _database.CloseAll();
        }
    }
} 