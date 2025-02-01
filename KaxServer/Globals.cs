using DRX.Library.Kax.Configs;
using DRX.Library.Kax;

namespace KaxServer
{
    public static class Globals
    {
        public static string _key = "ffffffffffffffff";
        public static readonly DrxServer Server = new DrxServer(new DrxServerConfig()
        {
            ServerEndIp = "0.0.0.0",
            ServerEndPort = "8463",
        });
        
        public static void StartServer()
        {
            // TODO:判断服务器是否启动，然后启动服务器
            if (!Server.IsRunning)
            {
                Server.Start();
            }
        }
    }
}
