using DRX.Framework.Common.Base;

namespace DRX.Library.Kax.Configs
{
    public class DrxClientConfig : BaseConfig
    {
        public double WindowHeight = 900;
        public double WindowWidth = 1600;

        public bool AutoLogin = false; // 是否自动登录

        public override async Task LoadAsync()
        {
            var config = await LoadFromFileAsync<DrxClientConfig>();

            // 从配置文件加载配置
            WindowHeight = config.WindowHeight;
            WindowWidth = config.WindowWidth;
            AutoLogin = config.AutoLogin;
        }
    }
}
