using DRX.Framework.Common.Models;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace DRX.Framework.Common.Pool;

public class DRXObjectPool<T> : IDisposable
{
    private readonly ConcurrentBag<T> _objects;
    private readonly Func<T> _objectGenerator;
    private readonly Action<T>? _objectReset;
    private readonly int _maxSize;
    private int _count;
    private T? _firstItem; // 快速路径
    private long _missCount;
    private long _hitCount;
    private bool _isDisposed;

    public DRXObjectPool(Func<T> objectGenerator, Action<T>? objectReset = null!, int maxSize = 1000)
    {
        _objects = new ConcurrentBag<T>();
        _objectGenerator = objectGenerator ?? throw new ArgumentNullException(nameof(objectGenerator));
        _objectReset = objectReset;
        _maxSize = maxSize;
        _count = 0;
    }

    public T Rent()
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(DRXObjectPool<T>));

        // 快速路径检查
        T item = _firstItem;
        if (item != null && Interlocked.CompareExchange(ref Unsafe.As<T, object>(ref _firstItem!)!, null, item) == (object)item)
        {
            Interlocked.Increment(ref _hitCount);
            return item;
        }

        // 常规路径
        if (_objects.TryTake(out item))
        {
            Interlocked.Increment(ref _hitCount);
            return item;
        }

        // 缓存未命中
        Interlocked.Increment(ref _missCount);
        if (Interlocked.Increment(ref _count) <= _maxSize)
        {
            try
            {
                return _objectGenerator();
            }
            catch (Exception ex)
            {
                Interlocked.Decrement(ref _count);
                throw new ObjectPoolException("创建对象失败", ex);
            }
        }

        Interlocked.Decrement(ref _count);
        throw new ObjectPoolException("对象池已达到最大容量限制");
    }

    public void Return(T item)
    {
        if (_isDisposed)
        {
            DisposeItem(item);
            return;
        }

        if (item == null) throw new ArgumentNullException(nameof(item));

        try
        {
            _objectReset?.Invoke(item);

            // 尝试快速路径返回
            if (_firstItem == null)
            {
                _firstItem = item;
                return;
            }

            _objects.Add(item);
        }
        catch
        {
            Interlocked.Decrement(ref _count);
            throw;
        }
    }

    public void Preload(int count)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(DRXObjectPool<T>));

        for (int i = 0; i < count && _count < _maxSize; i++)
        {
            _objects.Add(_objectGenerator());
            Interlocked.Increment(ref _count);
        }
    }

    public void Trim(int keepAlive)
    {
        if (_isDisposed) throw new ObjectDisposedException(nameof(DRXObjectPool<T>));

        while (_objects.Count > keepAlive && _objects.TryTake(out T item))
        {
            Interlocked.Decrement(ref _count);
            DisposeItem(item);
        }
    }

    private void DisposeItem(T item)
    {
        if (item is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }

    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        if (typeof(IDisposable).IsAssignableFrom(typeof(T)))
        {
            while (_objects.TryTake(out T item))
            {
                DisposeItem(item);
            }
        }

        _objects.Clear();
        DisposeItem(_firstItem);
        _firstItem = default;
    }

    // 诊断属性
    public int Count => _objects.Count;
    public int TotalCount => _count;
    public double HitRate => _hitCount == 0 ? 0 : (double)_hitCount / (_hitCount + _missCount);
    public long MissCount => _missCount;
    public long HitCount => _hitCount;

    public static implicit operator DRXObjectPool<T>(Microsoft.Extensions.ObjectPool.DefaultObjectPool<NetworkPacket> v)
    {
        throw new NotImplementedException();
    }
}

public class ObjectPoolException : Exception
{
    public ObjectPoolException(string message) : base(message) { }
    public ObjectPoolException(string message, Exception innerException)
        : base(message, innerException) { }
}