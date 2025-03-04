using System;
using System.Collections.Generic;
using System.Linq;

namespace Drx.Sdk.Script.Functions.Arrays
{
    public class JSArray<T>
    {
        // 内部存储
        protected List<T> _items = new List<T>();

        public virtual int Length => _items.Count;

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _items.Count)
                    throw new IndexOutOfRangeException($"索引 {index} 超出范围");
                return _items[index];
            }
            set
            {
                if (index < 0 || index >= _items.Count)
                    throw new IndexOutOfRangeException($"索引 {index} 超出范围");
                _items[index] = value;
            }
        }

        /// <summary>
        /// 默认构造函数，创建一个空数组
        /// </summary>
        public JSArray()
        {
        }

        /// <summary>
        /// 使用指定长度创建数组
        /// </summary>
        /// <param name="size">数组初始大小</param>
        public JSArray(int size)
        {
            for (int i = 0; i < size; i++)
            {
                _items.Add(default);
            }
        }

        /// <summary>
        /// 使用指定元素创建数组
        /// </summary>
        /// <param name="items">初始化数组的元素</param>
        public JSArray(params T[] items)
        {
            if (items != null)
            {
                _items.AddRange(items);
            }
        }

        /// <summary>
        /// 获取指定索引的值
        /// </summary>
        /// <param name="index">索引位置</param>
        /// <returns>索引位置的值，如果索引无效则返回默认值</returns>
        public virtual T GetValue(int index)
        {
            if (index >= 0 && index < _items.Count)
                return _items[index];
            return default;
        }

        /// <summary>
        /// 设置指定索引的值
        /// </summary>
        /// <param name="index">索引位置</param>
        /// <param name="value">要设置的值</param>
        public virtual void SetValue(int index, T value)
        {
            // 如果索引超出当前范围，则扩展数组
            while (_items.Count <= index)
            {
                _items.Add(default);
            }
            _items[index] = value;
        }

        /// <summary>
        /// 将一个值添加到数组末尾
        /// </summary>
        /// <param name="value">要添加的值</param>
        /// <returns>添加后的数组长度</returns>
        public virtual int Push(T value)
        {
            _items.Add(value);
            return _items.Count;
        }

        /// <summary>
        /// 移除指定索引的元素
        /// </summary>
        /// <param name="index">要移除的元素索引</param>
        public virtual void Remove(int index)
        {
            if (index >= 0 && index < _items.Count)
            {
                _items.RemoveAt(index);
            }
        }

        /// <summary>
        /// 清空数组
        /// </summary>
        public virtual void Clear()
        {
            _items.Clear();
        }

        /// <summary>
        /// 将JSArray转换为普通数组
        /// </summary>
        /// <returns>包含所有元素的数组</returns>
        public virtual T[] ToArray()
        {
            return _items.ToArray();
        }

        public virtual void Fill(T value)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                _items[i] = value;
            }
        }
    }

    // 为了保持向后兼容性的非泛型基类
    public abstract class JSArray : JSArray<object>
    {
    }
}
