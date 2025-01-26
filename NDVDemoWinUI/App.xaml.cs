using DRX.Framework;
using Microsoft.UI.Xaml;
using System;
using DRX.Library.Kax.Configs;
using WinUIEx;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace NDVDemoWinUI
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
            this.UnhandledException += App_UnhandledException;
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            m_window = new MainWindow();

            OnInit();
            m_window.Activate();
        }


        private void M_window_SizeChanged(object sender, WindowSizeChangedEventArgs args)
        {
            Config.WindowHeight = args.Size.Height;
            Config.WindowWidth = args.Size.Width;
        }

        private async void Window_Closed(object sender, WindowEventArgs args)
        {
            try
            {
                // 更新配置文件
                await Config.SaveConfigAsync();
#if DEBUG
                Logger.Debug("配置文件已更新");
#endif
            }
            catch (Exception ex)
            {
                Logger.Error($"保存配置文件时发生错误: {ex.Message}");
            }
        }

        private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            try
            {
                // 在未处理异常发生时尝试保存配置
                _ = Config.SaveConfigAsync();
            }
            catch
            {
                // 忽略保存失败的异常，避免递归异常
            }
        }

        private void OnInit()
        {
#if DEBUG
            var msg = $"配置文件已创建";
            Logger.Debug(msg);
#endif
            _ = Config.CreateFileAsync();
            _ = Config.LoadAsync();

            m_window.SetWindowSize(Config.WindowWidth, Config.WindowHeight);

            m_window.Closed += Window_Closed;
            m_window.SizeChanged += M_window_SizeChanged;
        }

        public DrxClientConfig Config { get; } = new();

        private Window? m_window;
    }
}
