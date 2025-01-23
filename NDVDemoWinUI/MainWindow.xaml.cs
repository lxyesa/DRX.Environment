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

            /* ---- ������ ---- */
            nv.ItemInvoked += Nv_ItemInvoked;
            nv.Loaded += Nv_Loaded;
        }

        /* ----------------------- �¼����� ----------------------- */
        private void Nv_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
        {
            if (args.IsSettingsInvoked)
            {
                // ��������ҳ�浼��
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
                    ConnectDialog.Title = "���ӳɹ�";
                    ConnectDialog.IsPrimaryButtonEnabled = true;
                    ConnectStateIcon.Glyph = "\uE701";
                    ConnectStateIcon.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Colors.ForestGreen);
                    ConnectStateItem.Text = "������";
                });
            });
        }


        /* ----------------------- �������� ----------------------- */
        private async Task TryConnectServer()
        {
            /* ---- ���������� ---- */
            NetManager.Instance.GetClient()!.OnConnectedCallback += MainWindow_OnConnectedCallback;
            // ��ʾ�Ի��򲢻�ȡ���
            var result = await ConnectDialog.ShowAsync();

            // ����Ի�����
            switch (result)
            {
                case ContentDialogResult.Primary:
#if DEBUG
                    Logger.Debug("�����ѵ�� PrimaryBtn");
#endif
                    break;
                case ContentDialogResult.Secondary:
                    // �û������"ȡ��"
                    break;
                case ContentDialogResult.None:
                    // �û������"�ر�"������ Esc ��
                    break;
                default:
                    break;
            }
        }

        /* ----------------------- ���� ----------------------- */

        //TODO : �ͻ���������ʵ�֡�

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
