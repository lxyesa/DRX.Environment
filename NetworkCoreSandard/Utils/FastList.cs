using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace NetworkCoreStandard.Utils;

[Serializable]
public class FastList<TKey, TValue> : IEnumerable<KeyValuePair<TKey, TValue>>, ISerializable 
    where TKey : IComparable<TKey>
{
    private class Node
    {
        public TKey Key { get; set; }
        public TValue Value { get; set; }
        public Node? Next { get; set; }
        public Node? Previous { get; set; }

        public Node(TKey key, TValue value)
        {
            Key = key;
            Value = value;
        }
    }

    private Node? head;
    private Node? tail;
    private readonly Dictionary<TKey, Node> lookupTable;
    private int count;

    public FastList()
    {
        lookupTable = new Dictionary<TKey, Node>();
    }

    public void Add(TKey key, TValue value)
    {
        if (lookupTable.ContainsKey(key))
            throw new ArgumentException("Key already exists");

        var newNode = new Node(key, value);
        if (head == null)
        {
            head = tail = newNode;
        }
        else
        {
            if (tail != null)
            {
                tail.Next = newNode;
            }
            newNode.Previous = tail;
            tail = newNode;
        }

        lookupTable.Add(key, newNode);
        count++;
    }

    public bool TryGetValue(TKey key, out TValue value)
    {
        value = default!;
        if (lookupTable.TryGetValue(key, out Node? node))
        {
            value = node.Value;
            return true;
        }
        return false;
    }

    public bool Remove(TKey key)
    {
        if (!lookupTable.TryGetValue(key, out Node? node))
            return false;

        if (node.Previous != null)
            node.Previous.Next = node.Next;
        else
            head = node.Next;

        if (node.Next != null)
            node.Next.Previous = node.Previous;
        else
            tail = node.Previous;

        lookupTable.Remove(key);
        count--;
        return true;
    }

    public int Count => count;

    public void Clear()
    {
        head = null;
        tail = null;
        lookupTable.Clear();
        count = 0;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        Node? current = head;
        while (current != null)
        {
            yield return new KeyValuePair<TKey, TValue>(current.Key, current.Value);
            current = current.Next;
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    // 序列化构造函数
    protected FastList(SerializationInfo info, StreamingContext context)
    {
        lookupTable = new Dictionary<TKey, Node>();
        count = info.GetInt32("Count");
        
        for (int i = 0; i < count; i++)
        {
            TKey key = (TKey)info.GetValue($"Key_{i}", typeof(TKey));
            TValue value = (TValue)info.GetValue($"Value_{i}", typeof(TValue));
            Add(key, value);
        }
    }

    // 序列化方法
    public virtual void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue("Count", count);
        int i = 0;
        Node? current = head;
        
        while (current != null)
        {
            info.AddValue($"Key_{i}", current.Key);
            info.AddValue($"Value_{i}", current.Value);
            current = current.Next;
            i++;
        }
    }
}
