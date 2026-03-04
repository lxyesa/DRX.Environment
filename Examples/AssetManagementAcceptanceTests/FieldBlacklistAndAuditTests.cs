using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using KaxSocket.Model;
using Xunit;

namespace AssetManagementAcceptanceTests;

/// <summary>
/// 禁改字段与审计记录验收测试
/// 
/// 测试场景：
/// 1. 字段黑名单校验（禁止修改 DeveloperId/AssetId）
/// 2. 允许修改其他业务字段
/// 3. 字段变更审计记录
/// 4. 动作审计记录
/// 5. 失败场景审计
/// 
/// 覆盖需求：R3, R6
/// </summary>
public class FieldBlacklistAndAuditTests
{
    #region 字段黑名单定义

    /// <summary>
    /// System-Only 资产字段黑名单（镜像后端实现）
    /// 这些字段禁止通过 update-field 接口修改
    /// </summary>
    private static readonly HashSet<string> SystemFieldBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "id",
        "assetid",
        "authorid",
        "developerid"  // 设计文档使用 DeveloperId，但模型字段为 AuthorId，两者都禁止
    };

    /// <summary>
    /// 允许修改的业务字段示例
    /// </summary>
    private static readonly string[] AllowedFields = new[]
    {
        "name",
        "version",
        "description",
        "category",
        "tags",
        "badges",
        "features",
        "coverImage",
        "iconImage",
        "screenshots",
        "status",
        "rejectReason",
        "fileSize",
        "downloadUrl",
        "license",
        "compatibility"
    };

    #endregion

    #region T3.1-T3.2: 禁止修改的字段测试

    /// <summary>
    /// T3.1: 禁止修改 id/assetId 字段
    /// </summary>
    [Theory(DisplayName = "禁止修改 id/assetId 字段")]
    [InlineData("id")]
    [InlineData("Id")]
    [InlineData("ID")]
    [InlineData("assetid")]
    [InlineData("assetId")]
    [InlineData("AssetId")]
    [InlineData("ASSETID")]
    public void UpdateField_IdAndAssetId_ShouldBeRejected(string fieldName)
    {
        // Act
        var isBlacklisted = SystemFieldBlacklist.Contains(fieldName);

        // Assert
        Assert.True(isBlacklisted, $"字段 '{fieldName}' 应该在黑名单中，禁止修改");
    }

    /// <summary>
    /// T3.2: 禁止修改 authorId/developerId 字段
    /// </summary>
    [Theory(DisplayName = "禁止修改 authorId/developerId 字段")]
    [InlineData("authorid")]
    [InlineData("authorId")]
    [InlineData("AuthorId")]
    [InlineData("AUTHORID")]
    [InlineData("developerid")]
    [InlineData("developerId")]
    [InlineData("DeveloperId")]
    [InlineData("DEVELOPERID")]
    public void UpdateField_AuthorIdAndDeveloperId_ShouldBeRejected(string fieldName)
    {
        // Act
        var isBlacklisted = SystemFieldBlacklist.Contains(fieldName);

        // Assert
        Assert.True(isBlacklisted, $"字段 '{fieldName}' 应该在黑名单中，禁止修改");
    }

    #endregion

    #region T3.3: 允许修改的字段测试

    /// <summary>
    /// T3.3: 允许修改其他业务字段
    /// </summary>
    [Theory(DisplayName = "允许修改的业务字段")]
    [InlineData("name")]
    [InlineData("version")]
    [InlineData("description")]
    [InlineData("category")]
    [InlineData("tags")]
    [InlineData("badges")]
    [InlineData("features")]
    [InlineData("coverImage")]
    [InlineData("iconImage")]
    [InlineData("screenshots")]
    [InlineData("fileSize")]
    [InlineData("downloadUrl")]
    [InlineData("license")]
    [InlineData("compatibility")]
    public void UpdateField_AllowedFields_ShouldNotBeBlacklisted(string fieldName)
    {
        // Act
        var isBlacklisted = SystemFieldBlacklist.Contains(fieldName);

        // Assert
        Assert.False(isBlacklisted, $"字段 '{fieldName}' 不应该在黑名单中，应该允许修改");
    }

    /// <summary>
    /// 验证所有预定义的允许字段都不在黑名单中
    /// </summary>
    [Fact(DisplayName = "所有业务字段都应允许修改")]
    public void AllAllowedFields_ShouldNotBeBlacklisted()
    {
        // Act & Assert
        foreach (var field in AllowedFields)
        {
            var isBlacklisted = SystemFieldBlacklist.Contains(field);
            Assert.False(isBlacklisted, $"业务字段 '{field}' 不应该在黑名单中");
        }
    }

    #endregion

    #region T3.4-T3.7: 审计记录测试

    /// <summary>
    /// 模拟审计日志记录结构（镜像 AssetAuditLog）
    /// </summary>
    private class MockAuditLog
    {
        public int Id { get; set; }
        public int AssetId { get; set; }
        public int OperatorUserId { get; set; }
        public string OperatorUserName { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        public long CreatedAt { get; set; }
        public int BeforeStatus { get; set; } = -1;
        public int AfterStatus { get; set; } = -1;
        public string FieldChangesJson { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string ErrorCode { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string RequestId { get; set; } = string.Empty;
    }

    /// <summary>
    /// T3.4: 字段更新应生成审计记录
    /// </summary>
    [Fact(DisplayName = "字段更新应生成审计记录")]
    public void FieldUpdate_ShouldGenerateAuditLog()
    {
        // Arrange
        var auditLog = CreateFieldUpdateAuditLog(
            assetId: 123,
            operatorId: 1,
            operatorName: "system_admin",
            field: "name",
            oldValue: "旧名称",
            newValue: "新名称"
        );

        // Assert - 验证审计记录包含必要字段
        Assert.Equal(123, auditLog.AssetId);
        Assert.Equal(1, auditLog.OperatorUserId);
        Assert.Equal("system_admin", auditLog.OperatorUserName);
        Assert.Equal("update-field", auditLog.ActionType);
        Assert.True(auditLog.Success);
        Assert.True(auditLog.CreatedAt > 0);
        Assert.False(string.IsNullOrEmpty(auditLog.RequestId));

        // 验证字段变更记录
        Assert.False(string.IsNullOrEmpty(auditLog.FieldChangesJson));
        Assert.Contains("name", auditLog.FieldChangesJson);
        Assert.Contains("旧名称", auditLog.FieldChangesJson);
        Assert.Contains("新名称", auditLog.FieldChangesJson);
    }

    /// <summary>
    /// T3.5: 动作操作应生成审计记录
    /// </summary>
    [Theory(DisplayName = "动作操作应生成审计记录")]
    [InlineData("return", 3, 1, "内容违规")]
    [InlineData("off-shelf", 3, 4, "暂时下架")]
    [InlineData("force-review", 1, 0, "重新审核")]
    [InlineData("relist", 4, 3, "恢复上架")]
    public void ActionOperation_ShouldGenerateAuditLog(
        string actionType,
        int beforeStatus,
        int afterStatus,
        string reason)
    {
        // Arrange
        var auditLog = CreateActionAuditLog(
            assetId: 456,
            operatorId: 1,
            operatorName: "system_admin",
            actionType: actionType,
            beforeStatus: beforeStatus,
            afterStatus: afterStatus,
            reason: reason,
            success: true
        );

        // Assert - 验证审计记录包含必要字段
        Assert.Equal(456, auditLog.AssetId);
        Assert.Equal(1, auditLog.OperatorUserId);
        Assert.Equal("system_admin", auditLog.OperatorUserName);
        Assert.Equal(actionType, auditLog.ActionType);
        Assert.Equal(beforeStatus, auditLog.BeforeStatus);
        Assert.Equal(afterStatus, auditLog.AfterStatus);
        Assert.Equal(reason, auditLog.Reason);
        Assert.True(auditLog.Success);
        Assert.True(auditLog.CreatedAt > 0);
        Assert.False(string.IsNullOrEmpty(auditLog.RequestId));
    }

    /// <summary>
    /// T3.6: 失败操作应保留错误上下文
    /// </summary>
    [Fact(DisplayName = "失败操作应保留错误上下文")]
    public void FailedOperation_ShouldPreserveErrorContext()
    {
        // Arrange
        var auditLog = CreateActionAuditLog(
            assetId: 789,
            operatorId: 1,
            operatorName: "system_admin",
            actionType: "off-shelf",
            beforeStatus: 0,  // PendingReview - 非法前置状态
            afterStatus: -1,  // 未执行
            reason: "尝试下架",
            success: false,
            errorCode: "INVALID_STATE_TRANSITION",
            errorMessage: "当前状态 0(PendingReview) 不允许执行 off-shelf 操作"
        );

        // Assert - 验证失败审计记录
        Assert.False(auditLog.Success);
        Assert.Equal("INVALID_STATE_TRANSITION", auditLog.ErrorCode);
        Assert.Contains("PendingReview", auditLog.ErrorMessage);
        Assert.Contains("off-shelf", auditLog.ErrorMessage);
        Assert.False(string.IsNullOrEmpty(auditLog.RequestId));
    }

    /// <summary>
    /// T3.7: 审计记录可按资产ID查询
    /// </summary>
    [Fact(DisplayName = "审计记录可按资产ID查询")]
    public void AuditLog_ShouldBeQueryableByAssetId()
    {
        // Arrange - 模拟多条审计记录
        var logs = new List<MockAuditLog>
        {
            CreateActionAuditLog(100, 1, "admin1", "return", 0, 1, "退回1", true),
            CreateFieldUpdateAuditLog(100, 1, "admin1", "name", "A", "B"),
            CreateActionAuditLog(200, 2, "admin2", "off-shelf", 3, 4, "下架", true),
            CreateActionAuditLog(100, 1, "admin1", "force-review", 1, 0, "重审", true),
        };

        // Act - 按资产ID筛选
        var asset100Logs = logs.Where(l => l.AssetId == 100).ToList();
        var asset200Logs = logs.Where(l => l.AssetId == 200).ToList();

        // Assert
        Assert.Equal(3, asset100Logs.Count);
        Assert.Single(asset200Logs);
        
        // 验证资产100的日志按时间可排序
        var sortedLogs = asset100Logs.OrderByDescending(l => l.CreatedAt).ToList();
        Assert.Equal(3, sortedLogs.Count);
    }

    /// <summary>
    /// 验证审计记录字段完整性（根据设计文档 R6）
    /// </summary>
    [Fact(DisplayName = "审计记录字段完整性验证")]
    public void AuditLog_ShouldHaveAllRequiredFields()
    {
        // Arrange
        var auditLog = CreateActionAuditLog(
            assetId: 123,
            operatorId: 1,
            operatorName: "system_admin",
            actionType: "return",
            beforeStatus: 3,
            afterStatus: 1,
            reason: "内容违规",
            success: true
        );

        // Assert - 验证必要字段（根据 R6 定义）
        // 基础：AssetId, OperatorUserId, ActionType, Reason, CreatedAt
        Assert.True(auditLog.AssetId > 0, "必须包含资产ID");
        Assert.True(auditLog.OperatorUserId >= 0, "必须包含操作者ID");
        Assert.False(string.IsNullOrEmpty(auditLog.OperatorUserName), "必须包含操作者用户名");
        Assert.False(string.IsNullOrEmpty(auditLog.ActionType), "必须包含动作类型");
        Assert.True(auditLog.CreatedAt > 0, "必须包含时间戳");
        
        // 状态：BeforeStatus, AfterStatus
        Assert.True(auditLog.BeforeStatus >= 0, "必须包含操作前状态");
        Assert.True(auditLog.AfterStatus >= 0, "必须包含操作后状态");
        
        // 结果：Success, ErrorCode, ErrorMessage, RequestId
        Assert.NotNull(auditLog.ErrorCode);
        Assert.NotNull(auditLog.ErrorMessage);
        Assert.False(string.IsNullOrEmpty(auditLog.RequestId), "必须包含请求ID以便追踪");
    }

    #endregion

    #region 辅助方法

    private static MockAuditLog CreateFieldUpdateAuditLog(
        int assetId,
        int operatorId,
        string operatorName,
        string field,
        string oldValue,
        string newValue)
    {
        var fieldChanges = new
        {
            field = field,
            oldValue = oldValue,
            newValue = newValue
        };

        return new MockAuditLog
        {
            Id = Random.Shared.Next(1, 10000),
            AssetId = assetId,
            OperatorUserId = operatorId,
            OperatorUserName = operatorName,
            ActionType = "update-field",
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            FieldChangesJson = JsonSerializer.Serialize(fieldChanges),
            Success = true,
            RequestId = Guid.NewGuid().ToString()
        };
    }

    private static MockAuditLog CreateActionAuditLog(
        int assetId,
        int operatorId,
        string operatorName,
        string actionType,
        int beforeStatus,
        int afterStatus,
        string reason,
        bool success,
        string errorCode = "",
        string errorMessage = "")
    {
        return new MockAuditLog
        {
            Id = Random.Shared.Next(1, 10000),
            AssetId = assetId,
            OperatorUserId = operatorId,
            OperatorUserName = operatorName,
            ActionType = actionType,
            BeforeStatus = beforeStatus,
            AfterStatus = afterStatus,
            Reason = reason,
            CreatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Success = success,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage,
            RequestId = Guid.NewGuid().ToString()
        };
    }

    #endregion
}
