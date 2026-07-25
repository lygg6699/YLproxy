using System.Collections;
using System.Threading;

namespace YLproxy.Core.Concurrency;

/// <summary>
/// 线程安全的集合包装器，使用 ReaderWriterLockSlim 保护共享资源。
/// 适用于读多写少的场景（如代理列表、进程跟踪）。
/// </summary>
public class ThreadSafeCollection<T> : IReadOnlyCollection<T>
{
    private readonly List<T> _items = new();
    private readonly ReaderWriterLockSlim _lock = new();

    public void Add(T item)
    {
        _lock.EnterWriteLock();
        try
        {
            _items.Add(item);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public bool Remove(T item)
    {
        _lock.EnterWriteLock();
        try
        {
            return _items.Remove(item);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Clear()
    {
        _lock.EnterWriteLock();
        try
        {
            _items.Clear();
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public List<T> GetAll()
    {
        _lock.EnterReadLock();
        try
        {
            return new List<T>(_items);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public int Count
    {
        get
        {
            _lock.EnterReadLock();
            try
            {
                return _items.Count;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
    }

    public IEnumerator<T> GetEnumerator()
    {
        return GetAll().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public void Dispose()
    {
        _lock.Dispose();
    }
}
