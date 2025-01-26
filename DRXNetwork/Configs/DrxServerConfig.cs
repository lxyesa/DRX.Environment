using DRX.Framework.Common.Base;

namespace DRX.Library.Kax.Configs
{
    public class DrxServerConfig : BaseConfig
    {
        /// <summary>
        /// 服务器绑定的IP地址和。
        /// </summary>
        public string? ServerEndIp { get; set; }

        /// <summary>
        /// 服务器绑定的端口。
        /// </summary>
        public string? ServerEndPort { get; set; }

        /// <summary>
        /// 服务器最大连接数。
        /// </summary>
        public int MaxConnections { get; set; }

        public override async Task LoadAsync()
        {
            var config = await LoadFromFileAsync<DrxServerConfig>();

            // 从配置文件加载配置
            ServerEndIp = config.ServerEndIp;
            ServerEndPort = config.ServerEndPort;
            MaxConnections = config.MaxConnections;

        }
    }
}
