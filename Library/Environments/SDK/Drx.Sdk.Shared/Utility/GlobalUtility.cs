using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Shared.Utility
{
    /// <summary>
    /// 全局通用工具类
    /// 功能包括：检测管理员权限、以管理员重启当前进程、运行外部命令（含 netsh）并返回结果、注册 URL ACL、开放防火墙端口等工具方法。
    /// 备注：这些方法主要针对 Windows（HttpListener / netsh / 防火墙 相关）。
    /// </summary>
    public static class GlobalUtility
    {
        /// <summary>
        /// 进程执行结果封装
        /// </summary>
        public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

        /// <summary>
        /// 判断当前进程是否以管理员（Elevated）权限运行（Windows 专用）。
        /// 非 Windows 平台将返回 false。
        /// </summary>
        public static bool IsAdministrator()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return false;

            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 以管理员权限重启当前可执行文件（触发 UAC 提示）。
        /// 返回 true 表示成功启动了提升后的进程（不会等待其退出）。
        /// 若当前已为管理员则直接返回 true（不做重启）。
        /// </summary>
        public static async Task<bool> RestartAsAdministratorAsync(string[]? args = null)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("RestartAsAdministratorAsync 仅在 Windows 上支持。");

            if (IsAdministrator())
                return true;

            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe))
            {
                // 最后手段：使用当前进程主模块（可能在某些环境下不可用）
                try
                {
                    exe = Process.GetCurrentProcess().MainModule?.FileName;
                }
                catch
                {
                    exe = null;
                }
            }

            if (string.IsNullOrEmpty(exe))
                throw new InvalidOperationException("无法确定当前可执行文件路径，无法以管理员方式重启。");

            string arguments = args is null || args.Length == 0 ? string.Empty : string.Join(" ", args.Select(EscapeArgument));
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = arguments,
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Environment.CurrentDirectory
            };

            try
            {
                Process.Start(psi);
                // 给启动过程一点时间（不阻塞主线程）
                await Task.Delay(200);
                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // 用户取消 UAC 或无法启动
                return false;
            }
        }

        /// <summary>
        /// 运行命令行程序并返回输出。若指定 runElevated 为 true，则尝试以管理员权限启动（Windows）。
        /// 注意：以管理员启动时无法同时重定向标准输入/输出（UseShellExecute = true），因此返回的标准输出/错误可能为空。
        /// </summary>
        public static async Task<ProcessResult> RunProcessAsync(string fileName, string arguments, bool runElevated = false, int timeoutMs = 60000)
        {
            if (runElevated && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("以管理员方式运行外部进程仅在 Windows 上支持。");

            if (runElevated && !IsAdministrator())
            {
                // 使用 Shell 提权启动（无法获取输出）
                var psiElev = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments,
                    UseShellExecute = true,
                    Verb = "runas",
                    WorkingDirectory = Environment.CurrentDirectory
                };

                try
                {
                    Process.Start(psiElev);
                    return new ProcessResult(0, string.Empty, string.Empty);
                }
                catch (System.ComponentModel.Win32Exception ex)
                {
                    return new ProcessResult(-1, string.Empty, ex.Message);
                }
            }

            // 非提权或已为管理员，可以重定向输出以获取结果
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Environment.CurrentDirectory
            };

            try
            {
                using var proc = Process.Start(psi) ?? throw new InvalidOperationException("无法启动进程");
                var stdOutTask = proc.StandardOutput.ReadToEndAsync();
                var stdErrTask = proc.StandardError.ReadToEndAsync();

                var completed = await Task.WhenAny(Task.WhenAll(stdOutTask, stdErrTask), Task.Delay(timeoutMs));
                if (completed is Task delayTask && delayTask.IsCompleted)
                {
                    try { proc.Kill(); } catch { }
                    return new ProcessResult(-2, await stdOutTask, await stdErrTask);
                }

                await proc.WaitForExitAsync();
                return new ProcessResult(proc.ExitCode, await stdOutTask, await stdErrTask);
            }
            catch (Exception ex)
            {
                return new ProcessResult(-1, string.Empty, ex.Message);
            }
        }

        /// <summary>
        /// 确保指定的 URL ACL 已注册（Windows Http.sys）。如果不存在，则尝试添加（需要管理员权限）。
        /// url 示例: http://+:8462/
        /// user 示例: Everyone 或 "DOMAIN\User"
        /// 返回 ProcessResult，其中 ExitCode==0 表示操作成功或已存在。
        /// </summary>
        public static async Task<ProcessResult> EnsureUrlAclAsync(string url, string user = "Everyone")
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("EnsureUrlAclAsync 仅在 Windows 平台支持。");

            // 查询现有 ACL
            var showResult = await RunProcessAsync("netsh", $"http show urlacl", runElevated: IsAdministrator());
            if (showResult.ExitCode == 0 && !string.IsNullOrEmpty(showResult.StandardOutput))
            {
                // 简单判断是否包含该 url（注意本地化输出可能不同，采用包含匹配）
                if (showResult.StandardOutput.Contains(url, StringComparison.OrdinalIgnoreCase))
                {
                    return new ProcessResult(0, "已存在相应的 URL ACL", string.Empty);
                }
            }

            // 当非管理员时，尝试以管理员方式执行添加命令（此时输出不可用）
            var addArgs = $"http add urlacl url={EscapeArgumentValue(url)} user={EscapeArgumentValue(user)}";
            var addResult = await RunProcessAsync("netsh", addArgs, runElevated: true);
            if (addResult.ExitCode == 0)
                return new ProcessResult(0, "URL ACL 已添加（或已存在）", string.Empty);

            // 若 RunProcessAsync 返回 -1 且包含错误信息，则转发
            return new ProcessResult(addResult.ExitCode, addResult.StandardOutput, addResult.StandardError);
        }

        /// <summary>
        /// 打开入站防火墙端口（添加一条规则）。需要管理员权限。
        /// 返回操作结果（ExitCode==0 表示执行成功或已存在）。
        /// </summary>
        public static async Task<ProcessResult> OpenFirewallPortAsync(int port, string ruleName = "Drx Firewall Rule")
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                throw new PlatformNotSupportedException("OpenFirewallPortAsync 仅在 Windows 平台支持。");

            var args = $"advfirewall firewall add rule name={EscapeArgumentValue(ruleName)} dir=in action=allow protocol=TCP localport={port}";
            var res = await RunProcessAsync("netsh", args, runElevated: true);
            return res;
        }

        #region 辅助方法

        private static string EscapeArgument(string arg)
        {
            if (string.IsNullOrEmpty(arg))
                return "\"\"";
            if (arg.Contains(' ') || arg.Contains('"'))
            {
                // 简单转义
                return "\"" + arg.Replace("\"", "\\\"") + "\"";
            }
            return arg;
        }

        private static string EscapeArgumentValue(string value)
        {
            // netsh 中若包含特殊字符，最好用引号包裹
            if (string.IsNullOrEmpty(value))
                return "\"\"";
            return $"\"{value.Replace("\"", "\\\"")}\"";
        }

        #endregion
    }
}
