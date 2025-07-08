using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Socket
{
    /// <summary>
    /// 扩展的 TcpClient 类，提供额外的映射存储功能
    /// </summary>
    public class DrxTcpClient : TcpClient
    {
        // 存储映射数据的字典，结构：mapId -> (mapKey -> mapValue)
        private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, object>> _maps = new ConcurrentDictionary<string, ConcurrentDictionary<string, object>>();

        /// <summary>
        /// 初始化 DrxTcpClient 的新实例
        /// </summary>
        public DrxTcpClient() : base()
        {
        }

        /// <summary>
        /// 使用指定的主机名和端口号初始化 DrxTcpClient 的新实例
        /// </summary>
        /// <param name="hostname">要连接到的主机名</param>
        /// <param name="port">要连接到的端口号</param>
        public DrxTcpClient(string hostname, int port) : base(hostname, port)
        {
        }

        /// <summary>
        /// 使用指定的地址族初始化 DrxTcpClient 的新实例
        /// </summary>
        /// <param name="addressFamily">要使用的地址族</param>
        public DrxTcpClient(AddressFamily addressFamily) : base(addressFamily)
        {
        }

        /// <summary>
        /// 将值存储到指定的映射中
        /// </summary>
        /// <typeparam name="T">值的类型</typeparam>
        /// <param name="mapId">映射ID，如果不存在则创建</param>
        /// <param name="mapKey">映射键</param>
        /// <param name="mapValue">要存储的值</param>
        /// <returns>如果成功存储则返回 true，否则返回 false</returns>
        public Task<bool> PushMap<T>(string mapId, string mapKey, T mapValue)
        {
            if (string.IsNullOrEmpty(mapId) || string.IsNullOrEmpty(mapKey))
                return Task.FromResult(false);

            var map = _maps.GetOrAdd(mapId, _ => new ConcurrentDictionary<string, object>());
            return Task.FromResult(map.AddOrUpdate(mapKey, mapValue, (_, _) => mapValue) != null);
        }

        /// <summary>
        /// 从指定的映射中获取值
        /// </summary>
        /// <typeparam name="T">值的类型</typeparam>
        /// <param name="mapId">映射ID</param>
        /// <param name="mapKey">映射键</param>
        /// <returns>如果找到则返回值，否则返回默认值</returns>
        public Task<T> GetMap<T>(string mapId, string mapKey)
        {
            if (string.IsNullOrEmpty(mapId) || string.IsNullOrEmpty(mapKey))
                return Task.FromResult<T>(default);

            if (_maps.TryGetValue(mapId, out var map) && map.TryGetValue(mapKey, out var value))
            {
                if (value is T typedValue)
                    return Task.FromResult(typedValue);
            }

            return Task.FromResult<T>(default);
        }

        /// <summary>
        /// 检查指定的映射是否存在
        /// </summary>
        /// <param name="mapId">映射ID</param>
        /// <returns>如果映射存在则返回 true，否则返回 false</returns>
        public Task<bool> HasMap(string mapId)
        {
            return Task.FromResult(!string.IsNullOrEmpty(mapId) && _maps.ContainsKey(mapId));
        }

        /// <summary>
        /// 检查指定的映射键是否存在
        /// </summary>
        /// <param name="mapId">映射ID</param>
        /// <param name="mapKey">映射键</param>
        /// <returns>如果映射键存在则返回 true，否则返回 false</returns>
        public Task<bool> HasMapKey(string mapId, string mapKey)
        {
            if (string.IsNullOrEmpty(mapId) || string.IsNullOrEmpty(mapKey))
                return Task.FromResult(false);

            return Task.FromResult(_maps.TryGetValue(mapId, out var map) && map.ContainsKey(mapKey));
        }

        /// <summary>
        /// 从指定的映射中移除键
        /// </summary>
        /// <param name="mapId">映射ID</param>
        /// <param name="mapKey">要移除的映射键</param>
        /// <returns>如果成功移除则返回 true，否则返回 false</returns>
        public Task<bool> RemoveMapKey(string mapId, string mapKey)
        {
            if (string.IsNullOrEmpty(mapId) || string.IsNullOrEmpty(mapKey))
                return Task.FromResult(false);

            if (_maps.TryGetValue(mapId, out var map))
                return Task.FromResult(map.TryRemove(mapKey, out _));

            return Task.FromResult(false);
        }

        /// <summary>
        /// 移除整个映射
        /// </summary>
        /// <param name="mapId">要移除的映射ID</param>
        /// <returns>如果成功移除则返回 true，否则返回 false</returns>
        public Task<bool> RemoveMap(string mapId)
        {
            if (string.IsNullOrEmpty(mapId))
                return Task.FromResult(false);

            return Task.FromResult(_maps.TryRemove(mapId, out _));
        }

        /// <summary>
        /// 获取指定映射中的所有键
        /// </summary>
        /// <param name="mapId">映射ID</param>
        /// <returns>映射中的所有键，如果映射不存在则返回空集合</returns>
        public Task<IEnumerable<string>> GetMapKeys(string mapId)
        {
            if (string.IsNullOrEmpty(mapId) || !_maps.TryGetValue(mapId, out var map))
                return Task.FromResult<IEnumerable<string>>(Array.Empty<string>());

            return Task.FromResult<IEnumerable<string>>(map.Keys);
        }

        /// <summary>
        /// 清除所有映射数据
        /// </summary>
        public Task ClearAllMaps()
        {
            _maps.Clear();
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// DrxTcpClient 的扩展方法
    /// </summary>
    public static class DrxTcpClientExtensions
    {
        /// <summary>
        /// 将 TcpClient 转换为 DrxTcpClient
        /// </summary>
        /// <param name="tcpClient">要转换的 TcpClient</param>
        /// <returns>转换后的 DrxTcpClient</returns>
        public static DrxTcpClient ToDrxTcpClient(this TcpClient tcpClient)
        {
            if (tcpClient == null)
                return null;

            if (tcpClient is DrxTcpClient drxClient)
                return drxClient;

            var client = new DrxTcpClient();
            client.Client = tcpClient.Client;
            return client;
        }
    }
} 