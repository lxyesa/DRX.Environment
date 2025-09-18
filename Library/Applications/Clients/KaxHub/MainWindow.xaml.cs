using Drx.Sdk.Shared;
using KaxHub.Network;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace KaxHub
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            _ = Socket.Initialize();
        }

        // --------------------------------------------------------
        // 事件
        // --------------------------------------------------------

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            // Logger.Debug("Close button clicked, shutting down application.");
            if (Socket.IsConnected()) { Socket.drxTcpClient.Close(); }
            Close();
            Application.Current.Shutdown();
        }

        private void TitleBar_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                DragMove();
            }
        }
    }
}