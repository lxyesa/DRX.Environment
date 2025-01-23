using DRX.Framework.Media.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using NDVDemoWinUI.Views;
using System;
using System.Threading.Tasks;
using DRX.Framework;
using Microsoft.UI;
using NDVDemoWinUI.Models;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace NDVDemoWinUI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            this.InitializeComponent();
            this.ExtendIntoTitleBar(TitleBar);
            this.SetBackdrop(WindowHelper.WindowBackdropType.Mica);

            /* ---- 导航栏 ---- */
            nv.ItemInvoked += Nv_ItemInvoked;
            nv.Loaded += Nv_Loaded;
        }

        /* ----------------------- 事件处理 ----------------------- */
        private void Nv_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                // 处理设置页面导航
                // content.Navigate(typeof(SettingsPage));
            }
            else
            {
                var tag = args.InvokedItemContainer?.Tag?.ToString();
                NavigateToPage(tag);
            }
        }

        private async void Nv_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await TryConnectServer();
            }
            catch (Exception expException)
            {
#if DEBUG
                Logger.Error(expException.Message);
#endif
            }
        }

        private void MainWindow_OnConnectedCallback(object? sender, DRX.Framework.Common.Args.NetworkEventArgs e)
        {
            _ = Task.Run(() =>
            {
                DispatcherQueue.TryEnqueue(async void () =>
                {
                    await Task.Delay(2000);
                    ConnectDialog.Title = "连接成功";
                    ConnectDialog.IsPrimaryButtonEnabled = true;
                    ConnectStateIcon.Glyph = "\uE701";
                    ConnectStateIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.ForestGreen);
                    ConnectStateItem.Text = "已连接";
                });
            });
        }


        /* ----------------------- 辅助方法 ----------------------- */
        private async Task TryConnectServer()
        {
            /* ---- 服务器连接 ---- */
            NetManager.Instance.GetClient()!.OnConnectedCallback += MainWindow_OnConnectedCallback;
            // 显示对话框并获取结果
            var result = await ConnectDialog.ShowAsync();

            // 处理对话框结果
            switch (result)
            {
                case ContentDialogResult.Primary:
#if DEBUG
                    Logger.Debug("程序已点击 PrimaryBtn");
#endif
                    break;
                case ContentDialogResult.Secondary:
                    // 用户点击了"取消"
                    break;
                case ContentDialogResult.None:
                    // 用户点击了"关闭"或按下了 Esc 键
                    break;
                default:
                    break;
            }
        }

        /* ----------------------- 心跳 ----------------------- */

        //TODO : 客户端心跳包实现。

        private async void NavigateToPage(string tag)
        {
            var pageType = tag switch
            {
                "User" => typeof(UserPage),
                _ => null
            };

            if (pageType != null)
            {
                content.Navigate(pageType);
            }
        }
    }
}
