using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
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
using Path = System.IO.Path;

namespace Drx.Sdk.Ui.Wpf.Console.Controls
{
    /// <summary>
    /// Console.xaml 的交互逻辑
    /// </summary>
    public partial class Console : UserControl
    {
        /// <summary>
        /// HeaderHeight依赖属性，用于设置控制台顶部标题栏的高度
        /// </summary>
        public static readonly DependencyProperty HeaderHeightProperty =
            DependencyProperty.Register(
                "HeaderHeight",
                typeof(GridLength),
                typeof(Console),
                new PropertyMetadata(new GridLength(60))); // 默认高度为40

        /// <summary>
        /// 获取或设置控制台顶部标题栏的高度
        /// </summary>
        public GridLength HeaderHeight
        {
            get { return (GridLength)GetValue(HeaderHeightProperty); }
            set { SetValue(HeaderHeightProperty, value); }
        }

        public Console()
        {
            InitializeComponent();

            // 将UserControl设置为DataContext，使绑定生效
            this.DataContext = this;

            // 初始化控制台
            ConsoleRedirector.RedirectToRichTextBox(ConsoleTextWriteContent);
        }

        /// <summary>
        /// 执行命令行命令
        /// </summary>
        /// <param name="command">要执行的命令</param>
        /// <param name="arguments">命令参数</param>
        /// <param name="workingDirectory">工作目录，可选</param>
        /// <param name="waitForExit">是否等待命令执行完成</param>
        /// <returns>如果waitForExit为true，返回命令执行的退出代码；否则返回0</returns>
        public int ExecuteCommand(string command, string arguments = "", string workingDirectory = "", bool waitForExit = true)
        {
            try
            {
                ProcessStartInfo processStartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                if (!string.IsNullOrEmpty(workingDirectory))
                {
                    processStartInfo.WorkingDirectory = workingDirectory;
                }

                using (Process process = new Process())
                {
                    process.StartInfo = processStartInfo;

                    // 处理输出和错误
                    process.OutputDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            System.Console.WriteLine(e.Data);
                        }
                    };

                    process.ErrorDataReceived += (sender, e) =>
                    {
                        if (!string.IsNullOrEmpty(e.Data))
                        {
                            System.Console.WriteLine($"[错误] {e.Data}");
                        }
                    };

                    process.Start();

                    // 开始异步读取
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (waitForExit)
                    {
                        process.WaitForExit();
                        System.Console.WriteLine($"[命令] 执行完成，退出代码: {process.ExitCode}");
                        return process.ExitCode;
                    }

                    return 0;
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[错误] 执行命令时发生异常: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// 执行BAT文件
        /// </summary>
        /// <param name="batFilePath">BAT文件的完整路径</param>
        /// <param name="arguments">BAT文件的参数</param>
        /// <param name="workingDirectory">工作目录，可选</param>
        /// <param name="waitForExit">是否等待执行完成</param>
        /// <returns>如果waitForExit为true，返回BAT文件执行的退出代码；否则返回0</returns>
        public int ExecuteBatFile(string batFilePath, string arguments = "", string workingDirectory = "", bool waitForExit = true)
        {
            if (!File.Exists(batFilePath))
            {
                System.Console.WriteLine($"[错误] BAT文件不存在: {batFilePath}");
                return -1;
            }

            // 默认使用BAT文件所在目录作为工作目录
            if (string.IsNullOrEmpty(workingDirectory))
            {
                workingDirectory = Path.GetDirectoryName(batFilePath) ?? "";
            }

            return ExecuteCommand("cmd.exe", $"/c \"{batFilePath}\" {arguments}", workingDirectory, waitForExit);
        }

        /// <summary>
        /// 执行命令行命令（异步版本）
        /// </summary>
        /// <param name="command">要执行的命令</param>
        /// <param name="arguments">命令参数</param>
        /// <param name="workingDirectory">工作目录，可选</param>
        /// <returns>任务对象，完成时返回退出代码</returns>
        public async Task<int> ExecuteCommandAsync(string command, string arguments = "", string workingDirectory = "")
        {
            return await Task.Run(() => ExecuteCommand(command, arguments, workingDirectory, true));
        }

        /// <summary>
        /// 执行BAT文件（异步版本）
        /// </summary>
        /// <param name="batFilePath">BAT文件的完整路径</param>
        /// <param name="arguments">BAT文件的参数</param>
        /// <param name="workingDirectory">工作目录，可选</param>
        /// <returns>任务对象，完成时返回退出代码</returns>
        public async Task<int> ExecuteBatFileAsync(string batFilePath, string arguments = "", string workingDirectory = "")
        {
            return await Task.Run(() => ExecuteBatFile(batFilePath, arguments, workingDirectory, true));
        }
    }
}
