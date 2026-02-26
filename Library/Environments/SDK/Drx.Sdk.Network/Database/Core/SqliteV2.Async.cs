using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Drx.Sdk.Network.DataBase;

/// <summary>
/// 高性能 Sqlite ORM - V2 版本 (Partial: 异步操作)
/// 处理所有异步的增删改查操作，支持大数据集流式查询和取消令牌
/// </summary>
public partial class SqliteV2<T> where T : class, IDataBase, new()
{
    #region 异步操作

    /// <summary>
    /// 异步批量插入
    /// </summary>
    public async Task InsertBatchAsync(IEnumerable<T> entities, int batchSize = 1000, CancellationToken cancellationToken = default)
    {
        if (entities == null) throw new ArgumentNullException(nameof(entities));

        using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            var batch = new List<T>(batchSize);
            foreach (var entity in entities)
            {
                batch.Add(entity);

                if (batch.Count >= batchSize)
                {
                    await InsertBatchInternalAsync(connection, transaction, batch, cancellationToken);
                    batch.Clear();
                }
            }

            if (batch.Count > 0)
            {
                await InsertBatchInternalAsync(connection, transaction, batch, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// 异步查询所有
    /// </summary>
    public async Task<List<T>> SelectAllAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<T>();
        using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        using var cmd = new SqliteCommand(_sqlCache["SELECT_ALL"], connection);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        InitializeOrdinals(reader);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapFromReader(reader));
        }

        return result;
    }

    /// <summary>
    /// 异步根据 ID 查询单个实体
    /// </summary>
    public async Task<T?> SelectByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        using var cmd = new SqliteCommand(_sqlCache["SELECT_BY_ID"], connection);
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;

        InitializeOrdinals(reader);
        return MapFromReader(reader);
    }

    /// <summary>
    /// 异步条件查询
    /// </summary>
    public async Task<List<T>> SelectWhereAsync(string propertyName, object value, CancellationToken cancellationToken = default)
    {
        // 允许查询 Id 主键或 _simpleProperties 中的属性
        if (propertyName != "Id" && !_columnMapping.PropertyNames.Contains(propertyName))
            throw new ArgumentException($"属性 {propertyName} 不存在");

        var sql = $"SELECT * FROM [{_tableName}] WHERE [{propertyName}] = @value";
        var result = new List<T>();

        using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@value", value ?? DBNull.Value);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        InitializeOrdinals(reader);

        while (await reader.ReadAsync(cancellationToken))
        {
            result.Add(MapFromReader(reader));
        }

        return result;
    }

    /// <summary>
    /// 异步更新单个实体
    /// </summary>
    public async Task UpdateAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));
        if (entity.Id <= 0) throw new ArgumentException("实体 ID 必须大于 0");

        using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            // 1. 更新主表
            var sql = _sqlCache["UPDATE"];
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Transaction = transaction;

            foreach (var prop in _simpleProperties)
            {
                var value = GetPropertyValue(entity, prop);
                cmd.Parameters.AddWithValue($"@{prop.Name}", value ?? DBNull.Value);
            }

            cmd.Parameters.AddWithValue("@id", entity.Id);
            await cmd.ExecuteNonQueryAsync(cancellationToken);

            // 2. 同步所有 TableList<T> 子表的变更
            foreach (var childListProp in _dataTableListProperties)
            {
                if (IsTableList(childListProp.PropertyType))
                {
                    var childList = childListProp.GetValue(entity);
                    if (childList != null)
                    {
                        // 调用 TableList<T> 的 SyncChanges 方法
                        var syncMethod = childList.GetType().GetMethod("SyncChanges", 
                            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                            null,
                            new[] { typeof(SqliteConnection), typeof(SqliteTransaction) },
                            null);

                        if (syncMethod != null)
                        {
                            syncMethod.Invoke(childList, new object[] { connection, transaction });
                        }
                    }
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    /// <summary>
    /// 异步批量更新
    /// </summary>
    public async Task UpdateBatchAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        if (entities == null) throw new ArgumentNullException(nameof(entities));

        using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            var entitiesList = entities.ToList();
            
            var enumerator = entitiesList.GetEnumerator();
            if (!enumerator.MoveNext())
                return;

            var sql = _sqlCache["UPDATE"];
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Transaction = transaction;

            // 预绑定参数
            foreach (var prop in _simpleProperties)
            {
                cmd.Parameters.AddWithValue($"@{prop.Name}", DBNull.Value);
            }
            cmd.Parameters.AddWithValue("@id", 0);

            do
            {
                var entity = enumerator.Current;
                if (entity.Id <= 0) continue;

                // 更新参数值
                foreach (var prop in _simpleProperties)
                {
                    var value = GetPropertyValue(entity, prop);
                    cmd.Parameters[$"@{prop.Name}"].Value = value ?? DBNull.Value;
                }
                cmd.Parameters["@id"].Value = entity.Id;

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            } while (enumerator.MoveNext());

            // 同步所有 TableList<T> 子表的变更
            foreach (var childListProp in _dataTableListProperties)
            {
                if (IsTableList(childListProp.PropertyType))
                {
                    foreach (var entity in entitiesList)
                    {
                        var childList = childListProp.GetValue(entity);
                        if (childList != null)
                        {
                            // 调用 TableList<T> 的 SyncChanges 方法
                            var syncMethod = childList.GetType().GetMethod("SyncChanges", 
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                                null,
                                new[] { typeof(SqliteConnection), typeof(SqliteTransaction) },
                                null);

                            if (syncMethod != null)
                            {
                                syncMethod.Invoke(childList, new object[] { connection, transaction });
                            }
                        }
                    }
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// 异步批量删除
    /// </summary>
    public async Task DeleteBatchAsync(IEnumerable<T> entities, CancellationToken cancellationToken = default)
    {
        if (entities == null) throw new ArgumentNullException(nameof(entities));

        var ids = entities.Where(e => e.Id > 0).Select(e => e.Id).ToList();
        if (ids.Count == 0) return;

        await DeleteBatchByIdsAsync(ids, cancellationToken);
    }

    /// <summary>
    /// 异步按 ID 集合批量删除
    /// </summary>
    public async Task DeleteBatchByIdsAsync(ICollection<int> ids, CancellationToken cancellationToken = default)
    {
        if (ids == null || ids.Count == 0) return;

        using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        const int batchLimit = 999;
        var idList = ids.ToList();
        var batches = new List<List<int>>();

        for (int i = 0; i < idList.Count; i += batchLimit)
        {
            batches.Add(idList.Skip(i).Take(batchLimit).ToList());
        }

        if (batches.Count == 1)
        {
            await ExecuteDeleteBatchAsync(connection, null, batches[0], cancellationToken);
        }
        else
        {
            using var transaction = connection.BeginTransaction();
            try
            {
                foreach (var batch in batches)
                {
                    await ExecuteDeleteBatchAsync(connection, transaction, batch, cancellationToken);
                }
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
    }

    private async Task ExecuteDeleteBatchAsync(SqliteConnection connection, SqliteTransaction? transaction, IEnumerable<int> ids, CancellationToken cancellationToken)
    {
        var idList = ids.ToList();
        var placeholders = string.Join(",", Enumerable.Range(0, idList.Count).Select(i => $"@id{i}"));
        var sql = $"DELETE FROM [{_tableName}] WHERE Id IN ({placeholders})";

        using var cmd = new SqliteCommand(sql, connection);
        if (transaction != null) cmd.Transaction = transaction;

        for (int i = 0; i < idList.Count; i++)
        {
            cmd.Parameters.AddWithValue($"@id{i}", idList[i]);
        }

        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 异步流式查询 - 适合大数据集
    /// </summary>
    public async IAsyncEnumerable<T> SelectAllStreamAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        using var cmd = new SqliteCommand(_sqlCache["SELECT_ALL"], connection);
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);

        InitializeOrdinals(reader);

        while (await reader.ReadAsync(cancellationToken))
        {
            yield return MapFromReader(reader);
        }
    }

    /// <summary>
    /// 异步插入单个实体
    /// </summary>
    public async Task InsertAsync(T entity, CancellationToken cancellationToken = default)
    {
        if (entity == null) throw new ArgumentNullException(nameof(entity));

        using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        using var transaction = connection.BeginTransaction();
        try
        {
            var sql = _sqlCache["INSERT"];
            using var cmd = new SqliteCommand(sql, connection, transaction);

            foreach (var prop in _simpleProperties)
            {
                var value = GetPropertyValue(entity, prop);
                cmd.Parameters.AddWithValue($"@{prop.Name}", value ?? DBNull.Value);
            }

            await cmd.ExecuteNonQueryAsync(cancellationToken);

            using var idCmd = new SqliteCommand("SELECT last_insert_rowid()", connection, transaction);
            var newId = (long)(await idCmd.ExecuteScalarAsync(cancellationToken))!;
            entity.Id = (int)newId;

            InsertChildTablesSync(connection, transaction, entity);
            SyncTableListChanges(connection, transaction, entity);

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// 异步根据 ID 删除实体
    /// </summary>
    public async Task DeleteByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        using var cmd = new SqliteCommand(_sqlCache["DELETE_BY_ID"], connection);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 异步清空表中所有数据
    /// </summary>
    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        using var connection = GetConnection();
        await connection.OpenAsync(cancellationToken);

        using var cmd = new SqliteCommand($"DELETE FROM [{_tableName}]", connection);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task InsertBatchInternalAsync(SqliteConnection connection, SqliteTransaction transaction, List<T> batch, CancellationToken cancellationToken)
    {
        var sql = _sqlCache["INSERT"];

        foreach (var entity in batch)
        {
            using var cmd = new SqliteCommand(sql, connection);
            cmd.Transaction = transaction;

            foreach (var prop in _simpleProperties)
            {
                var value = GetPropertyValue(entity, prop);
                cmd.Parameters.AddWithValue($"@{prop.Name}", value ?? DBNull.Value);
            }

            await cmd.ExecuteNonQueryAsync(cancellationToken);
            
            // 获取新插入的 ID
            using var idCmd = new SqliteCommand("SELECT last_insert_rowid()", connection);
            idCmd.Transaction = transaction;
            var result = await idCmd.ExecuteScalarAsync(cancellationToken);
            if (result != null && result is long)
            {
                entity.Id = (int)(long)result;
            }
            
            // 插入 List<IDataTable> 子表数据
            InsertChildTablesSync(connection, transaction, entity);
        }

        // 同步所有 TableList 子表数据
        foreach (var entity in batch)
        {
            SyncTableListChanges(connection, transaction, entity);
        }
    }

    #endregion
}
