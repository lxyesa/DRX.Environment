using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

/*
 * 文件职责：为 kax-decoupling-refactor 的 5.1 任务提供关键路径冒烟回归校验。
 * 关键依赖：KaxSocket 后端统一响应辅助（Api.cs）、前端关键脚本（developer/profile/shop/manage-users）、API 文档。
 */
namespace AssetManagementAcceptanceTests;

/// <summary>
/// 解耦改造关键路径冒烟回归测试。
/// 覆盖登录、developer、profile、shop、manage-users 以及统一响应契约的关键断言，
/// 以快速发现迁移后协议/入口回归。
/// </summary>
public class DecouplingSmokeRegressionTests
{
    private static readonly string RepoRoot = ResolveRepoRoot();

    /// <summary>
    /// 验证统一响应契约仍包含 code/message/data/traceId，并保留统一封装入口。
    /// </summary>
    [Fact(DisplayName = "冒烟：统一响应契约字段与入口存在")]
    public void EnvelopeContract_ShouldExposeCoreFieldsAndFactoryMethods()
    {
        var apiHelperContent = ReadRepoFile("Library/Applications/Servers/KaxSocket/Handlers/Helpers/Api.cs");

        Assert.Contains("public sealed class ApiEnvelope", apiHelperContent, StringComparison.Ordinal);
        Assert.Contains("public int Code", apiHelperContent, StringComparison.Ordinal);
        Assert.Contains("public string Message", apiHelperContent, StringComparison.Ordinal);
        Assert.Contains("public object? Data", apiHelperContent, StringComparison.Ordinal);
        Assert.Contains("public string TraceId", apiHelperContent, StringComparison.Ordinal);
        Assert.Contains("EnvelopeOk", apiHelperContent, StringComparison.Ordinal);
        Assert.Contains("EnvelopeFail", apiHelperContent, StringComparison.Ordinal);
        Assert.Contains("traceId = envelope.TraceId", apiHelperContent, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证关键页面脚本已接入统一请求层与语义权限适配，避免回退到分散实现。
    /// </summary>
    [Fact(DisplayName = "冒烟：关键页面接入统一网络层与语义权限")]
    public void CriticalFrontendPaths_ShouldUseUnifiedClientAndSemanticRoleChecks()
    {
        var developerApi = ReadRepoFile("Library/Applications/Servers/KaxSocket/Views/js/developer.api.js");
        var profileUser = ReadRepoFile("Library/Applications/Servers/KaxSocket/Views/js/profile.user.js");
        var shop = ReadRepoFile("Library/Applications/Servers/KaxSocket/Views/js/shop.js");
        var manageUsers = ReadRepoFile("Library/Applications/Servers/KaxSocket/Views/js/manage-users.js");

        Assert.Contains("ApiClient.requestJson", developerApi, StringComparison.Ordinal);
        Assert.Contains("/api/developer/assets", developerApi, StringComparison.Ordinal);

        Assert.Contains("ApiClient.request('/api/user/profile'", profileUser, StringComparison.Ordinal);
        Assert.Contains("ApiClient.request('/api/user/assets/active'", profileUser, StringComparison.Ordinal);

        Assert.Contains("ApiClient.requestJson('/api/asset/list", shop, StringComparison.Ordinal);
        Assert.Contains("ErrorPresenter.resolveError", shop, StringComparison.Ordinal);

        Assert.Contains("window.AuthState.permissionGroupToCssClass", manageUsers, StringComparison.Ordinal);
        Assert.Contains("/api/user/verify/account", manageUsers, StringComparison.Ordinal);
        Assert.Contains("/api/system/users", manageUsers, StringComparison.Ordinal);
    }

    /// <summary>
    /// 验证 API 文档覆盖关键路径端点，确保冒烟回归可追踪到统一文档。
    /// </summary>
    [Fact(DisplayName = "冒烟：API 文档覆盖关键路径端点")]
    public void ApiDocument_ShouldCoverCriticalEndpoints()
    {
        var apiDoc = ReadRepoFile("Library/Applications/Servers/KaxSocket/Handlers/api文档.md");

        var criticalEndpoints = new List<string>
        {
            "/api/user/login",
            "/api/developer/assets",
            "/api/user/profile",
            "/api/asset/list",
            "/api/system/users"
        };

        foreach (var endpoint in criticalEndpoints)
        {
            Assert.Contains(endpoint, apiDoc, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// 读取仓库内文件文本内容。
    /// </summary>
    /// <param name="relativePath">相对仓库根目录的路径。</param>
    /// <returns>文件文本。</returns>
    private static string ReadRepoFile(string relativePath)
    {
        var fullPath = Path.Combine(RepoRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"未找到回归测试目标文件: {relativePath}", fullPath);
        }

        return File.ReadAllText(fullPath);
    }

    /// <summary>
    /// 自测试运行目录向上定位仓库根目录（包含 DRX.Environment.sln）。
    /// </summary>
    /// <returns>仓库根目录绝对路径。</returns>
    private static string ResolveRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var solutionPath = Path.Combine(current.FullName, "DRX.Environment.sln");
            if (File.Exists(solutionPath))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("无法定位仓库根目录（未找到 DRX.Environment.sln）。");
    }
}
