using DLTools.Module;
using DLTools.Scripts;
using Drx.Sdk.Input;
using Drx.Sdk.Memory;
using Drx.Sdk.Script.Functions;
using Drx.Sdk.Ui.Wpf;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls.Primitives;
using iNKORE.UI.WPF.Modern.Helpers.Styles;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;

namespace DLTools
{
    public partial class MainWindow : Window
    {
        private WpfHotkeyManager? _hotkeyManager;
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

        private void NavigationView_SelectionChanged(iNKORE.UI.WPF.Modern.Controls.NavigationView sender, iNKORE.UI.WPF.Modern.Controls.NavigationViewSelectionChangedEventArgs args)
        {
            if (sender is iNKORE.UI.WPF.Modern.Controls.NavigationView nav)
            {
                if (nav.SelectedItem is iNKORE.UI.WPF.Modern.Controls.NavigationViewItem sItem)
                {
                    NavHeaderText.Text = sItem.Content.ToString();
                    if (sItem.Tag != null)
                    {
                        switch (sItem.Tag)
                        {
                            case "Home":
                                MainFrame.Navigate(GlobalManager.Instance.GetPage<HomePage>("pHome"));
                                break;
                            case "Settings":
                                MainFrame.Navigate(GlobalManager.Instance.GetPage<SettingsPage>("pSettings"));
                                break;
                            case "Consoles":
                                MainFrame.Navigate(GlobalManager.Instance.GetPage<ConsolePage>("pConsoles"));
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }
    }
}