using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace AssetManagementAcceptanceTests;

/// <summary>
/// 端到端集成测试
/// 
/// 这些测试需要运行中的 KaxSocket 服务器
/// 测试实际 HTTP 请求和响应
/// 
/// 注意：这些测试标记为 [Trait("Category", "Integration")]
/// 可通过 `dotnet test --filter Category=Integration` 单独运行
/// </summary>
[Trait("Category", "Integration")]
public class EndToEndIntegrationTests : IDisposable
{
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    
    // 测试服务器地址（根据实际部署调整）
    private const string BaseUrl = "http://localhost:5000";

    public EndToEndIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
        _client = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromSeconds(30)
        };
    }

    public void Dispose()
    {
        _client.Dispose();
    }

    #region 权限矩阵集成测试

    /// <summary>
    /// E2E: 验证权限组0可访问 system API
    /// 需要有效的 System 用户 JWT Token
    /// </summary>
    [Fact(DisplayName = "E2E: System 用户可访问 system API", Skip = "需要运行中的服务器和有效Token")]
    public async Task SystemUser_ShouldAccessSystemApi()
    {
        // Arrange - 需要配置有效的 System 用户 Token
        var systemToken = GetTestToken(permissionGroup: 0);
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", systemToken);

        // Act
        var response = await _client.GetAsync("/api/asset/system/list");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        _output.WriteLine($"Response: {response.StatusCode} - {content}");
        Assert.True(response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.OK);
    }

    /// <summary>
    /// E2E: 验证权限组2被拒绝访问 system API
    /// </summary>
    [Fact(DisplayName = "E2E: Console 用户被拒绝访问 system API", Skip = "需要运行中的服务器和有效Token")]
    public async Task ConsoleUser_ShouldBeRejectedFromSystemApi()
    {
        // Arrange
        var consoleToken = GetTestToken(permissionGroup: 2);
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", consoleToken);

        // Act
        var response = await _client.GetAsync("/api/asset/system/list");
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        _output.WriteLine($"Response: {response.StatusCode} - {content}");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }

    #endregion

    #region 状态流转集成测试

    /// <summary>
    /// E2E: 验证退回操作状态流转
    /// </summary>
    [Fact(DisplayName = "E2E: 退回操作应正确执行状态流转", Skip = "需要运行中的服务器和测试数据")]
    public async Task ReturnAction_ShouldExecuteStateTransition()
    {
        // Arrange
        var systemToken = GetTestToken(permissionGroup: 0);
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", systemToken);

        var requestBody = new
        {
            assetId = 1,  // 需要替换为实际测试资产ID
            reason = "测试退回原因"
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/asset/system/return", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        // Assert
        _output.WriteLine($"Response: {response.StatusCode} - {responseContent}");
        // 根据资产当前状态，可能成功或失败
        Assert.True(
            response.IsSuccessStatusCode || 
            response.StatusCode == System.Net.HttpStatusCode.BadRequest,
            "应该成功或返回状态流转错误");
    }

    #endregion

    #region 字段更新集成测试

    /// <summary>
    /// E2E: 验证禁改字段被正确拒绝
    /// </summary>
    [Fact(DisplayName = "E2E: 禁改字段应被拒绝", Skip = "需要运行中的服务器和测试数据")]
    public async Task ForbiddenField_ShouldBeRejected()
    {
        // Arrange
        var systemToken = GetTestToken(permissionGroup: 0);
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", systemToken);

        var requestBody = new
        {
            id = 1,
            field = "authorId",  // 禁止修改的字段
            value = 999
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/asset/system/update-field", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        // Assert
        _output.WriteLine($"Response: {response.StatusCode} - {responseContent}");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("FIELD_FORBIDDEN", responseContent);
    }

    /// <summary>
    /// E2E: 验证允许的字段可以修改
    /// </summary>
    [Fact(DisplayName = "E2E: 允许的字段应可修改", Skip = "需要运行中的服务器和测试数据")]
    public async Task AllowedField_ShouldBeUpdatable()
    {
        // Arrange
        var systemToken = GetTestToken(permissionGroup: 0);
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", systemToken);

        var requestBody = new
        {
            id = 1,
            field = "description",  // 允许修改的字段
            value = "更新后的描述 - 测试时间: " + DateTime.Now
        };

        var content = new StringContent(
            JsonSerializer.Serialize(requestBody),
            Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/asset/system/update-field", content);
        var responseContent = await response.Content.ReadAsStringAsync();

        // Assert
        _output.WriteLine($"Response: {response.StatusCode} - {responseContent}");
        Assert.True(response.IsSuccessStatusCode);
    }

    #endregion

    #region 审计查询集成测试

    /// <summary>
    /// E2E: 验证审计记录可查询
    /// </summary>
    [Fact(DisplayName = "E2E: 审计记录应可查询", Skip = "需要运行中的服务器和测试数据")]
    public async Task AuditLog_ShouldBeQueryable()
    {
        // Arrange
        var systemToken = GetTestToken(permissionGroup: 0);
        _client.DefaultRequestHeaders.Authorization = 
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", systemToken);

        // Act
        var response = await _client.GetAsync("/api/asset/system/audit/1");  // 资产ID=1
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        _output.WriteLine($"Response: {response.StatusCode} - {content}");
        Assert.True(response.IsSuccessStatusCode);
        
        // 验证返回结构
        var result = JsonSerializer.Deserialize<JsonElement>(content);
        Assert.True(result.TryGetProperty("data", out _) || result.TryGetProperty("Data", out _));
    }

    #endregion

    #region 辅助方法

    /// <summary>
    /// 获取测试用 JWT Token
    /// 实际使用时需要替换为有效的测试 Token
    /// </summary>
    private static string GetTestToken(int permissionGroup)
    {
        // TODO: 实际测试时需要替换为有效的 JWT Token
        // 可以通过以下方式获取：
        // 1. 调用登录 API 获取
        // 2. 使用预配置的测试用户 Token
        // 3. 在测试环境中生成有效 Token
        
        return permissionGroup switch
        {
            0 => "SYSTEM_USER_TOKEN_PLACEHOLDER",
            2 => "CONSOLE_USER_TOKEN_PLACEHOLDER",
            3 => "ADMIN_USER_TOKEN_PLACEHOLDER",
            _ => "USER_TOKEN_PLACEHOLDER"
        };
    }

    #endregion
}

/// <summary>
/// 测试执行摘要
/// 
/// 运行所有单元测试：
///   dotnet test
/// 
/// 仅运行单元测试（排除集成测试）：
///   dotnet test --filter "Category!=Integration"
/// 
/// 运行集成测试（需要服务器）：
///   dotnet test --filter "Category=Integration"
/// 
/// 运行特定测试类：
///   dotnet test --filter "FullyQualifiedName~PermissionMatrixTests"
///   dotnet test --filter "FullyQualifiedName~StateTransitionTests"
///   dotnet test --filter "FullyQualifiedName~FieldBlacklistAndAuditTests"
///   dotnet test --filter "FullyQualifiedName~DeveloperCenterRegressionTests"
/// </summary>
public class TestRunnerGuide
{
    [Fact(DisplayName = "测试运行指南")]
    public void PrintTestRunnerGuide()
    {
        // 此测试仅用于显示测试运行指南
        Assert.True(true);
    }
}
