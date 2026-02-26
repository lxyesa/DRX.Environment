using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Legacy.Socket.Services
{
    /// <summary>
    /// 提供Socket服务端的数据存储系统，支持类型分组
    /// </summary>
    public class SocketMapService : SocketServiceBase
    {
        private readonly ConcurrentDictionary<string, MapType> _typeMap = new ConcurrentDictionary<string, MapType>();

        /// <summary>
        /// 创建或获取指定类型的数据映射
        /// </summary>
        /// <param name="typeName">类型名称</param>
        /// <returns>映射类型对象</returns>
        public MapType CreateMapType(string typeName)
        {
            if (string.IsNullOrEmpty(typeName))
                throw new ArgumentNullException(nameof(typeName), "类型名称不能为空");

            return _typeMap.GetOrAdd(typeName, name => new MapType(name));
        }

        public MapType CreateOrGetMapType(string typeName)
        {
            return CreateMapType(typeName);
        }

        /// <summary>
        /// 获取所有已创建的类型名称
        /// </summary>
        /// <returns>类型名称集合</returns>
        public IEnumerable<string> GetTypeNames()
        {
            return _typeMap.Keys;
        }

        /// <summary>
        /// 删除指定类型的数据映射
        /// </summary>
        /// <param name="typeName">类型名称</param>
        /// <returns>是否成功删除</returns>
        public bool RemoveType(string typeName)
        {
            return _typeMap.TryRemove(typeName, out _);
        }

        /// <summary>
        /// 清空所有类型的数据映射
        /// </summary>
        public void ClearAll()
        {
            _typeMap.Clear();
        }

        /// <summary>
        /// 为客户端设置映射数据
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="client">客户端</param>
        /// <param name="mapId">映射ID</param>
        /// <param name="key">键</param>
        /// <param name="value">值</param>
        /// <returns>操作是否成功</returns>
        public async Task<bool> SetClientMapAsync<T>(DrxTcpClient client, string mapId, string key, T value)
        {
            if (client == null)
                return false;
                
            return await client.PushMap(mapId, key, value);
        }

        /// <summary>
        /// 从客户端获取映射数据
        /// </summary>
        /// <typeparam name="T">值类型</typeparam>
        /// <param name="client">客户端</param>
        /// <param name="mapId">映射ID</param>
        /// <param name="key">键</param>
        /// <returns>获取的值</returns>
        public async Task<T> GetClientMapAsync<T>(DrxTcpClient client, string mapId, string key)
        {
            if (client == null)
                return default;
                
            return await client.GetMap<T>(mapId, key);
        }

        /// <summary>
        /// 检查客户端是否包含指定映射
        /// </summary>
        /// <param name="client">客户端</param>
        /// <param name="mapId">映射ID</param>
        /// <returns>是否包含</returns>
        public async Task<bool> ClientHasMapAsync(DrxTcpClient client, string mapId)
        {
            if (client == null)
                return false;
                
            return await client.HasMap(mapId);
        }

        /// <summary>
        /// 检查客户端映射是否包含指定键
        /// </summary>
        /// <param name="client">客户端</param>
        /// <param name="mapId">映射ID</param>
        /// <param name="key">键</param>
        /// <returns>是否包含</returns>
        public async Task<bool> ClientHasMapKeyAsync(DrxTcpClient client, string mapId, string key)
        {
            if (client == null)
                return false;
                
            return await client.HasMapKey(mapId, key);
        }

        /// <summary>
        /// 数据映射类型，用于存储同一类型的键值对数据
        /// </summary>
        public class MapType
        {
            private readonly string _typeName;
            private readonly ConcurrentDictionary<string, object> _dataMap = new ConcurrentDictionary<string, object>();

            internal MapType(string typeName)
            {
                _typeName = typeName;
            }

            /// <summary>
            /// 获取类型名称
            /// </summary>
            public string TypeName => _typeName;

            /// <summary>
            /// 存储指定类型的值
            /// </summary>
            /// <typeparam name="T">值类型</typeparam>
            /// <param name="key">键</param>
            /// <param name="value">值</param>
            public void Push<T>(string key, T value)
            {
                if (string.IsNullOrEmpty(key))
                    throw new ArgumentNullException(nameof(key), "键不能为空");
                
                if (value == null)
                    throw new ArgumentNullException(nameof(value), "值不能为空");

                _dataMap[key] = value;
            }

            /// <summary>
            /// 异步存储指定类型的值
            /// </summary>
            /// <typeparam name="T">值类型</typeparam>
            /// <param name="key">键</param>
            /// <param name="value">值</param>
            /// <returns>完成任务</returns>
            public Task PushAsync<T>(string key, T value)
            {
                Push(key, value);
                return Task.CompletedTask;
            }

            /// <summary>
            /// 获取指定类型的值
            /// </summary>
            /// <typeparam name="T">值类型</typeparam>
            /// <param name="key">键</param>
            /// <param name="defaultValue">默认值</param>
            /// <returns>值或默认值</returns>
            public T Get<T>(string key, T? defaultValue = default)
            {
                if (string.IsNullOrEmpty(key))
                    return defaultValue ?? default;

                if (_dataMap.TryGetValue(key, out var value) && value is T typedValue)
                    return typedValue;

                return defaultValue ?? default;
            }

            /// <summary>
            /// 异步获取指定类型的值
            /// </summary>
            /// <typeparam name="T">值类型</typeparam>
            /// <param name="key">键</param>
            /// <param name="defaultValue">默认值</param>
            /// <returns>值或默认值的任务</returns>
            public Task<T> GetAsync<T>(string key, T? defaultValue = default)
            {
                return Task.FromResult(Get(key, defaultValue));
            }

            /// <summary>
            /// 移除指定键的值
            /// </summary>
            /// <param name="key">键</param>
            /// <returns>是否成功移除</returns>
            public bool Remove(string key)
            {
                if (string.IsNullOrEmpty(key))
                    return false;

                return _dataMap.TryRemove(key, out _);
            }

            /// <summary>
            /// 异步移除指定键的值
            /// </summary>
            /// <param name="key">键</param>
            /// <returns>是否成功移除的任务</returns>
            public Task<bool> RemoveAsync(string key)
            {
                return Task.FromResult(Remove(key));
            }

            /// <summary>
            /// 检查是否包含指定键
            /// </summary>
            /// <param name="key">键</param>
            /// <returns>是否包含</returns>
            public bool Contains(string key)
            {
                if (string.IsNullOrEmpty(key))
                    return false;

                return _dataMap.ContainsKey(key);
            }

            /// <summary>
            /// 异步检查是否包含指定键
            /// </summary>
            /// <param name="key">键</param>
            /// <returns>是否包含的任务</returns>
            public Task<bool> ContainsAsync(string key)
            {
                return Task.FromResult(Contains(key));
            }

            /// <summary>
            /// 获取所有键
            /// </summary>
            /// <returns>键集合</returns>
            public IEnumerable<string> GetKeys()
            {
                return _dataMap.Keys;
            }

            /// <summary>
            /// 异步获取所有键
            /// </summary>
            /// <returns>键集合的任务</returns>
            public Task<IEnumerable<string>> GetKeysAsync()
            {
                return Task.FromResult(GetKeys());
            }

            /// <summary>
            /// 获取所有值
            /// </summary>
            /// <returns>值集合</returns>
            public IEnumerable<object> GetValues()
            {
                return _dataMap.Values;
            }

            /// <summary>
            /// 异步获取所有值
            /// </summary>
            /// <returns>值集合的任务</returns>
            public Task<IEnumerable<object>> GetValuesAsync()
            {
                return Task.FromResult(GetValues());
            }

            /// <summary>
            /// 获取键值对数量
            /// </summary>
            public int Count => _dataMap.Count;

            /// <summary>
            /// 清空所有键值对
            /// </summary>
            public void Clear()
            {
                _dataMap.Clear();
            }

            /// <summary>
            /// 异步清空所有键值对
            /// </summary>
            /// <returns>完成任务</returns>
            public Task ClearAsync()
            {
                Clear();
                return Task.CompletedTask;
            }

            /// <summary>
            /// 获取所有键值对
            /// </summary>
            /// <returns>键值对集合</returns>
            public IEnumerable<KeyValuePair<string, object>> GetAll()
            {
                return _dataMap;
            }

            /// <summary>
            /// 异步获取所有键值对
            /// </summary>
            /// <returns>键值对集合的任务</returns>
            public Task<IEnumerable<KeyValuePair<string, object>>> GetAllAsync()
            {
                return Task.FromResult(GetAll());
            }

            /// <summary>
            /// 尝试获取指定键的值
            /// </summary>
            /// <param name="key">键</param>
            /// <param name="value">值</param>
            /// <returns>是否成功获取</returns>
            public bool TryGetValue(string key, out object? value)
            {
                return _dataMap.TryGetValue(key, out value);
            }
        }
    }
}
