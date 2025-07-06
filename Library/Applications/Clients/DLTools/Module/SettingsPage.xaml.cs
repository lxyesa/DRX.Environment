using System.Diagnostics;
using System.IO;
using DLTools.Scripts;
using iNKORE.UI.WPF.Modern;
using iNKORE.UI.WPF.Modern.Controls;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Drx.Sdk.Resource.Utility;
using MessageBox = iNKORE.UI.WPF.Modern.Controls.MessageBox;
using Page = System.Windows.Controls.Page;

namespace DLTools.Module
{
    /// <summary>
    /// Page1.xaml 的交互逻辑
    /// </summary>
    public partial class SettingsPage : Page
    {
        private Window? window = GlobalManager.Instance.mainWindow;

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
        }

        private void ComboBox_Selected(object? sender, RoutedEventArgs e)
        {
            if (sender is not ComboBox cb) return;
            if (cb.SelectedItem is not ComboBoxItem cbi) return;
            if (cbi.Tag == null) return;
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

        private void GamePathSel_Click(object sender, RoutedEventArgs e)
        {
            // 创建打开文件对话框
            var fileDialog = new Microsoft.Win32.OpenFileDialog
            {
                // 设置文件筛选器，只显示exe文件
                Filter = "游戏可执行文件 (DyingLightGame.exe)|DyingLightGame.exe",
                Title = "选择Dying Light游戏可执行文件",
                CheckFileExists = true
            };

            // 显示对话框
            var result = fileDialog.ShowDialog();

            // 处理用户选择的文件
            if (result != true) return;
            var filePath = fileDialog.FileName;

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
                        Text = "游戏路径：" + filePath,
                    },
                }
            };

            // 获取除去 exe 的路径
            var gameDir = Path.GetDirectoryName(filePath);
            var success = ResourceManager.Unzip(
                targetPath: gameDir,
                resourceName: "Libs.zip"
            );

            // 可以选择显示成功消息
            MessageBox.Show($"游戏路径已成功设置为：\n{filePath}", "设置成功");
        }

        private void SettingToggleSwitch_Toggled(object? sender, RoutedEventArgs e)
        {
            if (sender is not ToggleSwitch ts) return;
            if (ts.Tag == null) return;
            switch (ts.Tag)
            {
                case "DirverHotKey":
                    GlobalSettings.Instance.AppDirverHotKey = ts.IsOn;
                    break;
                case "ListenerProcess":
                    GlobalSettings.Instance.AppListenerProcess = ts.IsOn;
                    break;
                default:
                    break;
            }
        }

        // 用户点击卡片组中的卡片项
        private void CardGroupItem_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("当前应用程序版本不支持该操作。(0x2A)", "提示");
        }

        private void AssetsPathSel_OnClick(object sender, RoutedEventArgs e)
        {
            if (GlobalSettings.Instance.GamePath == string.Empty)
            {
                MessageBox.Show("请先设置游戏路径", "提示");
                GamePathSel_Click(sender, e);
                return; // 添加返回，避免在游戏路径为空时继续执行
            }

            try
            {
                // 1. 找到或创建"dllsettings.ini"文件
                var gameDir = Path.GetDirectoryName(GlobalSettings.Instance.GamePath);
                var iniFilePath = Path.Combine(gameDir, "dllsettings.ini");

                // 2. 读取INI文件内容（如果存在）
                var iniContent = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
                string? texturesPath = null;

                if (File.Exists(iniFilePath))
                {
                    foreach (var line in File.ReadLines(iniFilePath))
                    {
                        var trimmedLine = line.Trim();
                        if (string.IsNullOrEmpty(trimmedLine) || !trimmedLine.Contains('='))
                            continue;

                        var parts = trimmedLine.Split('=', 2);
                        if (parts.Length != 2)
                            continue;

                        var key = parts[0].Trim();
                        var value = parts[1].Trim();
                        iniContent[key] = value;

                        if (key.Equals("modded_textures_folder", StringComparison.OrdinalIgnoreCase))
                            texturesPath = value;
                    }
                }

                // 3. 若纹理路径不存在或为空，则生成纹理包路径
                if (string.IsNullOrEmpty(texturesPath))
                {
                    if (string.IsNullOrEmpty(GlobalSettings.Instance.GamePath))
                    {
                        MessageBox.Show("游戏路径未设置，请先设置游戏路径。", "错误");
                        return;
                    }

                    // 3.1 生成纹理包路径
                    texturesPath = Path.Combine(gameDir, "Azurmardraco_DLCPack");

                    // 确保目录存在
                    if (!Directory.Exists(texturesPath))
                        Directory.CreateDirectory(texturesPath);
                }

                // 4. 设置所有必需的INI值
                // 固定值
                iniContent["version"] = "1.7.0";
                iniContent["mod_creator_mode_enabled"] = "false";
                iniContent["dll_log_enabled"] = "false";
                iniContent["dll_log_file"] = "\"C:\\User\"";
                iniContent["save_textures"] = "false";
                iniContent["original_textures_folder"] = "";

                // 变量值
                iniContent["modded_textures_folder"] = texturesPath;
                iniContent["application_to_hook"] = $"{gameDir}|BIT64";

                // 5. 构建并写入INI文件内容
                var iniBuilder = new StringBuilder();
                foreach (var (key, value) in iniContent)
                    iniBuilder.AppendLine($"{key}={value}");

                File.WriteAllText(iniFilePath, iniBuilder.ToString());

                // 6. 打开纹理包目录
                if (Directory.Exists(texturesPath))
                    Process.Start("explorer.exe", texturesPath);
                else
                    MessageBox.Show($"纹理包目录不存在：{texturesPath}", "错误");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开纹理包路径时发生错误：{ex.Message}", "错误");
            }
        }
    }
}