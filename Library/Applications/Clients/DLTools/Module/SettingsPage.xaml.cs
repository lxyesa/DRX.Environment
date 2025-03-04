using DLTools.Scripts;
using Drx.Sdk.Memory;
using Drx.Sdk.Script.Functions;
using Drx.Sdk.Ui.Wpf.Controls;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using iNKORE.UI.WPF.Modern.Controls.Helpers;
using KopiLua;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = System.Windows.Controls.Page;

namespace DLTools.Module
{
    /// <summary>
    /// Page1.xaml 的交互逻辑
    /// </summary>
    public partial class SettingsPage : Page
    {
        private Window window = GlobalManager.Instance.mainWindow;
        public SettingsPage()
        {
            InitializeComponent();
            Init();
        }

        private void Init()
        {
            // 设置主题的下拉框
            switch (GlobalSettings.Instance.AppTheme)
            {
                case ElementTheme.Light:
                    ThemeComboBox.SelectedIndex = 1;
                    break;
                case ElementTheme.Dark:
                    ThemeComboBox.SelectedIndex = 0;
                    break;
                case ElementTheme.Default:
                    ThemeComboBox.SelectedIndex = 2;
                    break;
                default:
                    break;
            }

            // 设置开关状态
            DirverHotKeyToggleSwitch.IsOn = GlobalSettings.Instance.AppDirverHotKey;
            HotKeyListenerToggleSwitch.IsOn = GlobalSettings.Instance.AppHotKeyListener;
            ListenerProcessToggleSwitch.IsOn = GlobalSettings.Instance.AppListenerProcess;

            // 设置游戏路径
            if (!string.IsNullOrEmpty(GlobalSettings.Instance.GamePath))
            {
                GamePathSettingCard.Description = new StackPanel()
                {
                    Children =
                    {
                        new TextBlock()
                        {
                            // Text = "游戏路径："+GlobalSettings.Instance.GamePath,
                            Text = $"游戏路径：{GlobalSettings.Instance.GamePath}",
                        },
                    }
                };
            }




            // 如果没有启用热键监听器，禁用驱动级热键设置
            if (!GlobalSettings.Instance.AppHotKeyListener)
                DirverHotKeyCard.IsEnabled = false;
        }

        private void ComboBox_Selected(object sender, RoutedEventArgs e)
        {
            if (sender != null)
            {
                if (sender is ComboBox cb)
                {
                    if (cb.SelectedItem != null)
                    {
                        if (cb.SelectedItem is ComboBoxItem cbi)
                        {
                            if (cbi.Tag != null)
                            {
                                switch (cbi.Tag)
                                {
                                    case "Light":
                                        ThemeManager.SetRequestedTheme(window, ElementTheme.Light);
                                        GlobalSettings.Instance.AppTheme = ElementTheme.Light;
                                        break;
                                    case "Dark":
                                        ThemeManager.SetRequestedTheme(window, ElementTheme.Dark);
                                        GlobalSettings.Instance.AppTheme = ElementTheme.Dark;
                                        break;
                                    case "System":
                                        ThemeManager.SetRequestedTheme(window, ElementTheme.Default);
                                        GlobalSettings.Instance.AppTheme = ElementTheme.Default;
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

        private void GamePathSel_Click(object sender, RoutedEventArgs e)
        {
            // 创建打开文件对话框
            Microsoft.Win32.OpenFileDialog fileDialog = new Microsoft.Win32.OpenFileDialog();

            // 设置文件筛选器，只显示exe文件
            fileDialog.Filter = "游戏可执行文件 (DyingLightGame.exe)|DyingLightGame.exe";
            fileDialog.Title = "选择Dying Light游戏可执行文件";
            fileDialog.CheckFileExists = true;

            // 显示对话框
            bool? result = fileDialog.ShowDialog();

            // 处理用户选择的文件
            if (result == true)
            {
                string filePath = fileDialog.FileName;

                // 获取选择的文件路径后，可以将其保存到全局设置中
                // 假设GlobalSettings中有一个属性来存储游戏路径
                GlobalSettings.Instance.GamePath = filePath;
                GlobalSettings.Instance.Save();

                GamePathSettingCard.Description = new StackPanel()
                {
                    Children =
                    {
                        new TextBlock()
                        {
                            Text = "游戏路径："+filePath,
                        },
                    }
                };

                // 可以选择显示成功消息
                MessageBox.Show($"游戏路径已成功设置为：\n{filePath}", "设置成功");
            }
        }

        private void SettingToggleSwitch_Toggled(object sender, RoutedEventArgs e)
        {
            if (sender != null)
            {
                if (sender is ToggleSwitch ts)
                {
                    if (ts.Tag != null)
                    {
                        switch (ts.Tag)
                        {
                            case "DirverHotKey":
                                GlobalSettings.Instance.AppDirverHotKey = ts.IsOn;
                                break;
                            case "HotKeyListener":
                                GlobalSettings.Instance.AppHotKeyListener = ts.IsOn;
                                if (!ts.IsOn)
                                    DirverHotKeyCard.IsEnabled = false;
                                else
                                    DirverHotKeyCard.IsEnabled = true;
                                break;
                            case "ListenerProcess":
                                GlobalSettings.Instance.AppListenerProcess = ts.IsOn;
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }

        // 用户点击卡片组中的卡片项
        private void CardGroupItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("当前应用程序版本不支持该操作。(0x2A)", "提示");
        }
    }
}
