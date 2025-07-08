using Drx.Sdk.Network.DataBase;
using Drx.Sdk.Network.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Web.KaxServer.Data
{
    /// <summary>
    /// SqliteUnified的包装类，提供通用的数据访问方法
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">主键类型</typeparam>
    public class SqliteWrapper<TEntity, TKey> where TEntity : class, IDataBase, new()
    {
        private readonly SqliteUnified<TEntity> _sqlite;

        public SqliteWrapper(string dbPath)
        {
            _sqlite = new SqliteUnified<TEntity>(dbPath);
        }

        public TEntity GetById(TKey id)
        {
            return _sqlite.FindById(Convert.ToInt32(id));
        }

        public IEnumerable<TEntity> GetAll()
        {
            return _sqlite.Read();
        }

        public IEnumerable<TEntity> Find(Dictionary<string, object> conditions)
        {
            return _sqlite.Read(conditions);
        }

        public TEntity FindSingle(string propertyName, object value)
        {
            return _sqlite.ReadSingle(propertyName, value);
        }

        public void Add(TEntity entity)
        {
            _sqlite.Save(entity);
        }

        public void Update(TEntity entity)
        {
            _sqlite.Save(entity);
        }

        public void Delete(TEntity entity)
        {
            _sqlite.Delete(entity);
        }

        public void SaveAll(IEnumerable<TEntity> entities)
        {
            _sqlite.SaveAll(entities);
        }
    }
} 