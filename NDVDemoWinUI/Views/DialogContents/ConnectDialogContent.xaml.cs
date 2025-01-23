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
        private const int MaxRetry = 5; // �޸�Ϊ5���������

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

                // �ȴ�2��
                await Task.Delay(2000);

                // �������״̬
                var isConnected = NetManager.Instance.IsConnected();

                if (isConnected)
                {
#if DEBUG
                    Logger.Debug("�ɹ����ӵ���������");
#endif
                    UpdateConnectionSuccessUi();
                    break;
                }

                _retryCount++;
#if DEBUG
                Logger.Debug($"���ӳ��� {_retryCount} ʧ�ܡ�");
#endif
            }

            if (_retryCount >= MaxRetry)
            {
                // ����XAML״̬��������ʾ������Ϣ
                UpdateConnectionFailedUi();
            }
        }

        private void UpdateConnectionSuccessUi()
        {
            // ���� InfoBar ��Ϊ "Msg"��ProgressBar Ϊ "PrB"��CheckBox Ϊ "Chk"
            DispatcherQueue.TryEnqueue(() =>
            {
                Msg.IsOpen = true;
                Msg.Title = "���ӳɹ�";
                Msg.Message = "�ѳɹ����ӵ�DRX��������";
                Msg.Severity = InfoBarSeverity.Success;
                PrB.IsIndeterminate = false;
                PrB.Value = 100;
                PrB.Background = new SolidColorBrush(Colors.Green);
                Chk.IsEnabled = true;
            });
        }

        private void UpdateConnectionFailedUi()
        {
            // ���� InfoBar ��Ϊ "Msg"��ProgressBar Ϊ "PrB"��CheckBox Ϊ "Chk"
            DispatcherQueue.TryEnqueue(() =>
            {
                Msg.IsOpen = true;
                Msg.Title = "����ʧ��";
                Msg.Message = "�޷����ӵ�DRX�������������������û��Ժ����ԡ�";
                Msg.Severity = InfoBarSeverity.Error;

                PrB.IsIndeterminate = false;
                PrB.Value = 0;

                Chk.IsEnabled = false;
            });
        }
    }
}
