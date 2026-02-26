using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

/// <summary>
/// VSCode HTTP 请求拦截器
/// 通过劫持 hosts 文件将 VSCode 相关域名重定向到本机
/// 在本地监听 80/443 端口，对所有请求返回 400 Bad Request
/// 不依赖任何代理机制
/// </summary>
var interceptor = new VscodeRequestInterceptor();
await interceptor.RunAsync();

#region 拦截器核心实现

/// <summary>
/// VSCode 请求拦截器主类
/// 负责管理 hosts 劫持、HTTP/HTTPS 监听和请求拒绝
/// </summary>
public class VscodeRequestInterceptor
{
    private const string HostsFilePath = @"C:\Windows\System32\drivers\etc\hosts";
    private const string HostsMarkerBegin = "# === DRX VSCode Interceptor BEGIN ===";
    private const string HostsMarkerEnd = "# === DRX VSCode Interceptor END ===";

    /// VSCode 及相关服务所使用的域名列表
    private static readonly string[] VscodeDomains =
    [
        "marketplace.visualstudio.com",
        "update.code.visualstudio.com",
        "az764295.vo.msecnd.net",
        "download.visualstudio.microsoft.com",
        "vscode.blob.core.windows.net",
        "dc.services.visualstudio.com",
        "visualstudio-devdiv-c2s.msedge.net",
        "default.exp-tas.com",
        "api.github.com",
        "vscode.dev",
        "copilot-proxy.githubusercontent.com",
        "api.githubcopilot.com",
        "origin-tracker.githubusercontent.com",
        "github.com/login",
        "login.microsoftonline.com",
        "api.individual.githubcopilot.com"
    ];

    public async Task RunAsync()
    {
        // 检查管理员权限
        if (!IsRunningAsAdministrator())
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[错误] 拦截器需要管理员权限才能修改 hosts 文件和绑定 80/443 端口");
            Console.WriteLine("       请以管理员身份重新运行此程序");
            Console.ResetColor();
            return;
        }

        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine("╔══════════════════════════════════════════╗");
        Console.WriteLine("║   DRX VSCode 请求拦截器 v1.0             ║");
        Console.WriteLine("║   不依赖代理，直接返回 400 Bad Request   ║");
        Console.WriteLine("╚══════════════════════════════════════════╝");
        Console.ResetColor();

        // 注册退出时清理 hosts
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            Console.WriteLine("\n[信息] 正在清理 hosts 文件条目...");
            RemoveHostsEntries();
            Console.WriteLine("[信息] 清理完成，拦截器已停止");
            Environment.Exit(0);
        };

        AppDomain.CurrentDomain.ProcessExit += (_, _) => RemoveHostsEntries();

        // 注入 hosts 条目
        InjectHostsEntries();

        // 构建同时监听 HTTP 和 HTTPS 的 Web 应用
        var builder = WebApplication.CreateBuilder();

        builder.WebHost.ConfigureKestrel(options =>
        {
            // HTTP 80 端口
            options.Listen(IPAddress.Any, 80);

            // HTTPS 443 端口，使用自签名证书
            options.Listen(IPAddress.Any, 443, listenOptions =>
            {
                listenOptions.UseHttps(GenerateSelfSignedCertificate());
            });
        });

        // 禁用默认日志噪音
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Logging.AddFilter("Microsoft.Hosting.Lifetime", LogLevel.Information);

        var app = builder.Build();

        // 拦截所有请求，无论路径、方法、域名
        app.Use(async (HttpContext context, RequestDelegate next) =>
        {
            var host = context.Request.Host.Host;
            var method = context.Request.Method;
            var path = context.Request.Path;
            var scheme = context.Request.Scheme.ToUpper();

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[拦截] [{scheme}] {method} {host}{path}");
            Console.ResetColor();

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";

            var responseBody = """
                {
                  "error": "Bad Request",
                  "message": "此请求已被 DRX 拦截器拒绝",
                  "interceptedBy": "DRX.ApiReouter",
                  "timestamp": "{{TIMESTAMP}}"
                }
                """.Replace("{{TIMESTAMP}}", DateTimeOffset.UtcNow.ToString("o"));

            await context.Response.WriteAsync(responseBody, Encoding.UTF8);
        });

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n[启动] 正在监听 HTTP:80 和 HTTPS:443");
        Console.WriteLine($"[启动] 已劫持 {VscodeDomains.Length} 个 VSCode 域名");
        Console.WriteLine("[启动] 按 Ctrl+C 停止并清理 hosts\n");
        Console.ResetColor();

        await app.RunAsync();
    }

    #region Hosts 文件管理

    /// <summary>
    /// 向 hosts 文件注入域名劫持条目，将所有 VSCode 域名指向 127.0.0.1
    /// </summary>
    private static void InjectHostsEntries()
    {
        try
        {
            // 先清理可能存在的旧条目
            RemoveHostsEntries();

            var hostsContent = File.ReadAllText(HostsFilePath);
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine(HostsMarkerBegin);

            foreach (var domain in VscodeDomains)
            {
                sb.AppendLine($"127.0.0.1 {domain}");
                sb.AppendLine($"::1 {domain}");
            }

            sb.AppendLine(HostsMarkerEnd);

            File.AppendAllText(HostsFilePath, sb.ToString());

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[Hosts] 已注入 {VscodeDomains.Length} 条劫持规则");
            Console.ResetColor();

            // 刷新 DNS 缓存
            FlushDnsCache();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[错误] 写入 hosts 文件失败: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// 清理由本程序注入的 hosts 条目，恢复原始状态
    /// </summary>
    private static void RemoveHostsEntries()
    {
        try
        {
            var hostsContent = File.ReadAllText(HostsFilePath);
            var beginIndex = hostsContent.IndexOf(HostsMarkerBegin, StringComparison.Ordinal);
            var endIndex = hostsContent.IndexOf(HostsMarkerEnd, StringComparison.Ordinal);

            if (beginIndex < 0 || endIndex < 0) return;

            // 去掉 marker 之间的所有内容（包括前后换行）
            var cleanContent = hostsContent[..(beginIndex - 1)] +
                               hostsContent[(endIndex + HostsMarkerEnd.Length)..];

            File.WriteAllText(HostsFilePath, cleanContent.TrimEnd() + Environment.NewLine);
            FlushDnsCache();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[警告] 清理 hosts 文件失败: {ex.Message}");
            Console.ResetColor();
        }
    }

    /// <summary>
    /// 调用 ipconfig /flushdns 刷新系统 DNS 缓存，使 hosts 修改立即生效
    /// </summary>
    private static void FlushDnsCache()
    {
        try
        {
            var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = "/flushdns",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
            process?.WaitForExit(3000);
            Console.WriteLine("[Hosts] DNS 缓存已刷新");
        }
        catch
        {
            // DNS 刷新失败不影响主功能
        }
    }

    #endregion

    #region 证书与权限

    /// <summary>
    /// 生成用于 HTTPS 监听的自签名 X.509 证书
    /// 证书的 SubjectAlternativeName 涵盖所有被劫持的 VSCode 域名
    /// </summary>
    private static X509Certificate2 GenerateSelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=DRX-VSCode-Interceptor",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        // 添加所有被劫持域名作为 SAN
        var sanBuilder = new SubjectAlternativeNameBuilder();
        sanBuilder.AddIpAddress(IPAddress.Loopback);
        sanBuilder.AddIpAddress(IPAddress.IPv6Loopback);
        sanBuilder.AddDnsName("localhost");

        foreach (var domain in VscodeDomains)
        {
            // 跳过包含路径的条目，只取域名部分
            var domainName = domain.Contains('/') ? domain[..domain.IndexOf('/')] : domain;
            sanBuilder.AddDnsName(domainName);
        }

        request.CertificateExtensions.Add(sanBuilder.Build());
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature, false));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(
                [new Oid("1.3.6.1.5.5.7.3.1")], false)); // serverAuth

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddYears(5));

        // Windows 需要导出后重新导入以附带私钥
        return X509CertificateLoader.LoadPkcs12(
            cert.Export(X509ContentType.Pfx),
            password: (string?)null,
            keyStorageFlags: X509KeyStorageFlags.MachineKeySet);
    }

    /// <summary>
    /// 检查当前进程是否以 Windows 管理员权限运行
    /// </summary>
    private static bool IsRunningAsAdministrator()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return true;

        using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }

    #endregion
}

#endregion
