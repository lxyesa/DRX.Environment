using DLTools.Module;
using DLTools.Scripts;
using Drx.Sdk.Input;
using iNKORE.UI.WPF.Modern;
using System.ComponentModel;
using System.Windows;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace DLTools
{
    public partial class MainWindow : Window
    {
#pragma warning disable CS0649 // 从未对字段赋值，字段将一直保持其默认值
        private WpfHotkeyManager? _hotkeyManager;
        private bool openCheat = true;
#pragma warning restore CS0649 // 从未对字段赋值，字段将一直保持其默认值
        public MainWindow()
        {
            GlobalSettings.Instance.Load();
            InitializeComponent();

            // 初始化全局管理器
            GlobalManager.Instance.mainWindow = this;
            GlobalManager.Instance.GetPage<ConsolePage>("pConsoles");

            this.Closing += MainWindow_Closing;
            ThemeManager.SetRequestedTheme(this, GlobalSettings.Instance.AppTheme);
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            _hotkeyManager?.Dispose();
            GlobalSettings.Instance.Save();
            base.OnClosed(e);
        }


        private void NavigationView_SelectionChanged(iNKORE.UI.WPF.Modern.Controls.NavigationView sender,
            iNKORE.UI.WPF.Modern.Controls.NavigationViewSelectionChangedEventArgs args)
        {
            if (sender.SelectedItem is not iNKORE.UI.WPF.Modern.Controls.NavigationViewItem sItem) return;
            NavHeaderText.Text = sItem.Content.ToString();
            if (sItem.Tag == null) return;
            switch (sItem.Tag)
            {
                case "Home":
                    if (openCheat)
                    {
                        MessageBox.Show("不允许的操作", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                    MainFrame.Navigate(GlobalManager.Instance.GetPage<HomePage>("pHome"));
                    break;
                case "Settings":
                    MainFrame.Navigate(GlobalManager.Instance.GetPage<SettingsPage>("pSettings"));
                    break;
                case "Consoles":
                    MainFrame.Navigate(GlobalManager.Instance.GetPage<ConsolePage>("pConsoles"));
                    break;
            }
        }
    }
}