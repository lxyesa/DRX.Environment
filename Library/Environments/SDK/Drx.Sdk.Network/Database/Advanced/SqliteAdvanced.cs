using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace Drx.Sdk.Network.DataBase;

/// <summary>
/// 工作单元模式（Unit of Work）实现 - 支持事务管理和批处理
/// 用于复杂业务场景中的多个操作协调
/// </summary>
public sealed class SqliteUnitOfWork<T> : IDisposable where T : class, IDataBase, new()
{
    private readonly SqliteV2<T> _db;
    private readonly SqliteConnection _connection;
    private SqliteTransaction? _transaction;
    private bool _disposed;

    // 追踪变化的对象
    private readonly List<T> _addedItems = new();
    private readonly List<T> _modifiedItems = new();
    private readonly List<T> _deletedItems = new();

    public SqliteUnitOfWork(SqliteV2<T> db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _connection = new SqliteConnection(_db.ConnectionString);
    }

    /// <summary>
    /// 開始事務
    /// </summary>
    public async Task BeginTransactionAsync()
    {
        await _connection.OpenAsync();
        _transaction = _connection.BeginTransaction();
    }

    /// <summary>
    /// 提交所有变更
    /// </summary>
    public async Task CommitAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction == null)
            throw new InvalidOperationException("事务未开始");

        try
        {
            // 先删除
            foreach (var item in _deletedItems)
            {
                _db.DeleteById(item.Id);
            }

            // 再添加
            if (_addedItems.Count > 0)
            {
                await _db.InsertBatchAsync(_addedItems, cancellationToken: cancellationToken);
            }

            // 最后更新
            foreach (var item in _modifiedItems)
            {
                _db.Update(item);
            }

            await _transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await _transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            _transaction.Dispose();
            _transaction = null;
        }
    }

    /// <summary>
    /// 回滚事務
    /// </summary>
    public async Task RollbackAsync()
    {
        if (_transaction == null)
            throw new InvalidOperationException("事务未开始");

        await _transaction.RollbackAsync();
        _transaction.Dispose();
        _transaction = null;

        _addedItems.Clear();
        _modifiedItems.Clear();
        _deletedItems.Clear();
    }

    /// <summary>
    /// 标记对象为新增
    /// </summary>
    public void Add(T item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (!_addedItems.Contains(item))
            _addedItems.Add(item);
    }

    /// <summary>
    /// 标记对象为修改
    /// </summary>
    public void Update(T item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (!_modifiedItems.Contains(item))
            _modifiedItems.Add(item);
    }

    /// <summary>
    /// 标记对象为删除
    /// </summary>
    public void Delete(T item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        if (!_deletedItems.Contains(item))
            _deletedItems.Add(item);
    }

    /// <summary>
    /// 获取待提交的更改数
    /// </summary>
    public int GetPendingChangesCount() => _addedItems.Count + _modifiedItems.Count + _deletedItems.Count;

    /// <summary>
    /// 清除追踪信息
    /// </summary>
    public void Clear()
    {
        _addedItems.Clear();
        _modifiedItems.Clear();
        _deletedItems.Clear();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _transaction?.Dispose();
        _connection?.Dispose();
        _disposed = true;
    }
}

/// <summary>
/// 仓储模式（Repository Pattern）实现 - 为 ORM 操作提供统一接口
/// </summary>
public sealed class SqliteRepository<T> where T : class, IDataBase, new()
{
    private readonly SqliteV2<T> _db;
    private readonly object _lockObj = new();

    public SqliteRepository(string databasePath, string? basePath = null)
    {
        _db = new SqliteV2<T>(databasePath, basePath);
    }

    #region 查询操作

    /// <summary>
    /// 获取所有项
    /// </summary>
    public List<T> GetAll() => _db.SelectAll();

    /// <summary>
    /// 根据 ID 获取项
    /// </summary>
    public T? GetById(int id) => _db.SelectById(id);

    /// <summary>
    /// 条件查询
    /// </summary>
    public List<T> Find(string propertyName, object value) => _db.SelectWhere(propertyName, value);

    /// <summary>
    /// Lambda 表达式查询
    /// </summary>
    public List<T> FindWhere(Func<T, bool> predicate) => _db.SelectWhere(predicate);

    /// <summary>
    /// 获取数量
    /// </summary>
    public int Count() => _db.SelectAll().Count;

    /// <summary>
    /// 检查是否存在
    /// </summary>
    public bool Exists(int id) => _db.SelectById(id) != null;

    #endregion

    #region 修改操作

    /// <summary>
    /// 添加项
    /// </summary>
    public void Add(T item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        _db.Insert(item);
    }

    /// <summary>
    /// 批量添加
    /// </summary>
    public void AddRange(IEnumerable<T> items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));
        _db.InsertBatch(items);
    }

    /// <summary>
    /// 更新项
    /// </summary>
    public void Update(T item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        _db.Update(item);
    }

    /// <summary>
    /// 删除项
    /// </summary>
    public void Delete(T item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));
        _db.Delete(item);
    }

    /// <summary>
    /// 根据 ID 删除
    /// </summary>
    public void DeleteById(int id) => _db.DeleteById(id);

    #endregion

    #region 异步操作

    /// <summary>
    /// 异步获取所有
    /// </summary>
    public Task<List<T>> GetAllAsync(CancellationToken cancellationToken = default) =>
        _db.SelectAllAsync(cancellationToken);

    /// <summary>
    /// 异步批量添加
    /// </summary>
    public Task AddRangeAsync(IEnumerable<T> items, int batchSize = 1000, CancellationToken cancellationToken = default) =>
        _db.InsertBatchAsync(items, batchSize, cancellationToken);

    /// <summary>
    /// 异步流式查询
    /// </summary>
    public IAsyncEnumerable<T> GetAllStreamAsync(CancellationToken cancellationToken = default) =>
        _db.SelectAllStreamAsync(cancellationToken);

    #endregion
}

/// <summary>
/// 批处理器（Batch Processor）- 自动分批处理大量数据
/// </summary>
public sealed class SqliteBatchProcessor<T> where T : class, IDataBase, new()
{
    private readonly SqliteV2<T> _db;
    private readonly int _batchSize;
    private readonly List<T> _buffer;
    private readonly object _lockObj = new();

    public SqliteBatchProcessor(SqliteV2<T> db, int batchSize = 1000)
    {
        if (batchSize <= 0) throw new ArgumentException("批大小必须大于 0");
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _batchSize = batchSize;
        _buffer = new List<T>(batchSize);
    }

    /// <summary>
    /// 添加项到缓冲区
    /// </summary>
    public void Add(T item)
    {
        if (item == null) throw new ArgumentNullException(nameof(item));

        lock (_lockObj)
        {
            _buffer.Add(item);
            if (_buffer.Count >= _batchSize)
            {
                FlushInternal();
            }
        }
    }

    /// <summary>
    /// 添加多个项
    /// </summary>
    public void AddRange(IEnumerable<T> items)
    {
        if (items == null) throw new ArgumentNullException(nameof(items));

        foreach (var item in items)
        {
            Add(item);
        }
    }

    /// <summary>
    /// 手动刷新（提交缓冲区中的所有项）
    /// </summary>
    public void Flush()
    {
        lock (_lockObj)
        {
            FlushInternal();
        }
    }

    private void FlushInternal()
    {
        if (_buffer.Count > 0)
        {
            _db.InsertBatch(_buffer);
            _buffer.Clear();
        }
    }

    /// <summary>
    /// 异步刷新
    /// </summary>
    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        lock (_lockObj)
        {
            if (_buffer.Count == 0) return;
        }

        var items = new List<T>();
        lock (_lockObj)
        {
            items.AddRange(_buffer);
            _buffer.Clear();
        }

        await _db.InsertBatchAsync(items, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// 获取缓冲区中的项数
    /// </summary>
    public int GetBufferedCount()
    {
        lock (_lockObj)
        {
            return _buffer.Count;
        }
    }

    /// <summary>
    /// 清空缓冲区（不保存）
    /// </summary>
    public void Clear()
    {
        lock (_lockObj)
        {
            _buffer.Clear();
        }
    }
}
