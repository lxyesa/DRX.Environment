using DRX.Framework;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NDVDemoWinUI.Models;
using System;
using System.Threading.Tasks;
using DRX.Framework.Media.UI;
using Microsoft.UI;
using Microsoft.UI.Xaml.Media;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace NDVDemoWinUI.Views.DialogContents
{
    public sealed partial class ConnectDialogContent : UserControl
    {
        private int _retryCount = 0;
        private const int MaxRetry = 5; // 修改为5次最大重试

        public ConnectDialogContent()
        {
            this.InitializeComponent();
            this.Loaded += ConnectDialogContent_Loaded;
        }

        private async void ConnectDialogContent_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await AttemptConnectionAsync();
            }
            catch (Exception exception)
            {
#if DEBUG
                Logger.Error(exception.Message);
#endif
            }
        }

        private async Task AttemptConnectionAsync()
        {
            while (_retryCount < MaxRetry)
            {
                try
                {
                    NetManager.Instance.Connect();
                }
                catch (Exception exception)
                {
                    Logger.Error(exception.Message);
                }

                // 等待2秒
                await Task.Delay(2000);

                // 检查连接状态
                var isConnected = NetManager.Instance.IsConnected();

                if (isConnected)
                {
#if DEBUG
                    Logger.Debug("成功连接到服务器。");
#endif
                    UpdateConnectionSuccessUi();
                    break;
                }

                _retryCount++;
#if DEBUG
                Logger.Debug($"连接尝试 {_retryCount} 失败。");
#endif
            }

            if (_retryCount >= MaxRetry)
            {
                // 更新XAML状态，例如显示错误信息
                UpdateConnectionFailedUi();
            }
        }

        private void UpdateConnectionSuccessUi()
        {
            // 假设 InfoBar 名为 "Msg"，ProgressBar 为 "PrB"，CheckBox 为 "Chk"
            DispatcherQueue.TryEnqueue(() =>
            {
                Msg.IsOpen = true;
                Msg.Title = "连接成功";
                Msg.Message = "已成功连接到DRX服务器。";
                Msg.Severity = InfoBarSeverity.Success;
                PrB.IsIndeterminate = false;
                PrB.Value = 100;
                PrB.Background = new SolidColorBrush(Colors.Green);
                Chk.IsEnabled = true;
            });
        }

        private void UpdateConnectionFailedUi()
        {
            // 假设 InfoBar 名为 "Msg"，ProgressBar 为 "PrB"，CheckBox 为 "Chk"
            DispatcherQueue.TryEnqueue(() =>
            {
                Msg.IsOpen = true;
                Msg.Title = "连接失败";
                Msg.Message = "无法连接到DRX服务器。请检查网络设置或稍后重试。";
                Msg.Severity = InfoBarSeverity.Error;

                PrB.IsIndeterminate = false;
                PrB.Value = 0;

                Chk.IsEnabled = false;
            });
        }
    }
}
