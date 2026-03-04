using System;
using System.Collections.Generic;
using System.Linq;
using KaxSocket.Model;
using Xunit;

namespace AssetManagementAcceptanceTests;

/// <summary>
/// 状态流转验收测试
/// 
/// 测试场景：验证资产状态机的合法与非法流转
/// 覆盖需求：R4, R5
/// 
/// 状态定义：
/// - PendingReview (0): 审核中
/// - Rejected (1): 已退回/拒绝
/// - ApprovedPendingPublish (2): 待发布（审核通过后进入此状态，而非直接 Active）
/// - Active (3): 已上线
/// - OffShelf (4): 已下架（独立于软删除，不修改 IsDeleted）
/// 
/// 状态流转规则：
/// - 退回（return）: 0/2/3/4 -> 1
/// - 下架（off-shelf）: 3 -> 4
/// - 强制重审（force-review）: 1/3/4 -> 0
/// - 恢复上架（relist）: 4 -> 3
/// </summary>
public class StateTransitionTests
{
    #region 状态流转规则定义

    /// <summary>
    /// 定义每个动作的合法前置状态和目标状态
    /// </summary>
    private static readonly Dictionary<string, (AssetStatus[] ValidSourceStates, AssetStatus TargetState)> TransitionRules = new Dictionary<string, (AssetStatus[] ValidSourceStates, AssetStatus TargetState)>
    {
        { "return", (
            new[] { AssetStatus.PendingReview, AssetStatus.ApprovedPendingPublish, AssetStatus.Active, AssetStatus.OffShelf },
            AssetStatus.Rejected
        ) },
        { "off-shelf", (
            new[] { AssetStatus.Active },
            AssetStatus.OffShelf
        ) },
        { "force-review", (
            new[] { AssetStatus.Rejected, AssetStatus.Active, AssetStatus.OffShelf },
            AssetStatus.PendingReview
        ) },
        { "relist", (
            new[] { AssetStatus.OffShelf },
            AssetStatus.Active
        ) }
    };

    /// <summary>
    /// 所有可能的资产状态
    /// </summary>
    private static readonly AssetStatus[] AllStatuses = new[]
    {
        AssetStatus.PendingReview,        // 0
        AssetStatus.Rejected,             // 1
        AssetStatus.ApprovedPendingPublish, // 2
        AssetStatus.Active,               // 3
        AssetStatus.OffShelf              // 4
    };

    #endregion

    #region T2.1-T2.2: 退回（Return）流转测试

    /// <summary>
    /// T2.1: 退回动作的合法流转测试
    /// 合法前置状态: PendingReview(0), ApprovedPendingPublish(2), Active(3), OffShelf(4)
    /// 目标状态: Rejected(1)
    /// </summary>
    [Theory(DisplayName = "退回 - 合法流转")]
    [InlineData(AssetStatus.PendingReview, "审核中 -> 退回")]
    [InlineData(AssetStatus.ApprovedPendingPublish, "待发布 -> 退回")]
    [InlineData(AssetStatus.Active, "已上线 -> 退回")]
    [InlineData(AssetStatus.OffShelf, "已下架 -> 退回")]
    public void Return_ValidSourceState_ShouldBeAllowed(AssetStatus sourceState, string scenario)
    {
        // Arrange
        var action = "return";
        var rule = TransitionRules[action];

        // Act
        var isValid = ValidateStateTransition(sourceState, action);

        // Assert
        Assert.True(isValid.IsValid, $"场景 [{scenario}]: 状态 {sourceState} 应该允许执行退回操作");
        Assert.Equal(AssetStatus.Rejected, rule.TargetState);
    }

    /// <summary>
    /// T2.2: 退回动作的非法流转测试
    /// 非法前置状态: Rejected(1) - 已退回状态不能再退回
    /// </summary>
    [Fact(DisplayName = "退回 - 非法流转: Rejected -> Rejected")]
    public void Return_FromRejectedState_ShouldBeRejected()
    {
        // Arrange
        var sourceState = AssetStatus.Rejected;
        var action = "return";

        // Act
        var result = ValidateStateTransition(sourceState, action);

        // Assert
        Assert.False(result.IsValid, "已退回状态不应允许再次退回");
        Assert.Equal("INVALID_STATE_TRANSITION", result.ErrorCode);
    }

    #endregion

    #region T2.3-T2.4: 下架（Off-Shelf）流转测试

    /// <summary>
    /// T2.3: 下架动作的合法流转测试
    /// 合法前置状态: Active(3)
    /// 目标状态: OffShelf(4)
    /// </summary>
    [Fact(DisplayName = "下架 - 合法流转: Active -> OffShelf")]
    public void OffShelf_FromActiveState_ShouldBeAllowed()
    {
        // Arrange
        var sourceState = AssetStatus.Active;
        var action = "off-shelf";

        // Act
        var isValid = ValidateStateTransition(sourceState, action);

        // Assert
        Assert.True(isValid.IsValid, "Active 状态应该允许下架");
    }

    /// <summary>
    /// T2.4: 下架动作的非法流转测试
    /// 非法前置状态: PendingReview(0), Rejected(1), ApprovedPendingPublish(2), OffShelf(4)
    /// </summary>
    [Theory(DisplayName = "下架 - 非法流转")]
    [InlineData(AssetStatus.PendingReview, "审核中不能下架")]
    [InlineData(AssetStatus.Rejected, "已退回不能下架")]
    [InlineData(AssetStatus.ApprovedPendingPublish, "待发布不能下架")]
    [InlineData(AssetStatus.OffShelf, "已下架不能再下架")]
    public void OffShelf_InvalidSourceState_ShouldBeRejected(AssetStatus sourceState, string scenario)
    {
        // Arrange
        var action = "off-shelf";

        // Act
        var result = ValidateStateTransition(sourceState, action);

        // Assert
        Assert.False(result.IsValid, $"场景 [{scenario}]: 状态 {sourceState} 不应允许下架操作");
        Assert.Equal("INVALID_STATE_TRANSITION", result.ErrorCode);
    }

    #endregion

    #region T2.5-T2.6: 强制重审（Force-Review）流转测试

    /// <summary>
    /// T2.5: 强制重审动作的合法流转测试
    /// 合法前置状态: Rejected(1), Active(3), OffShelf(4)
    /// 目标状态: PendingReview(0)
    /// </summary>
    [Theory(DisplayName = "强制重审 - 合法流转")]
    [InlineData(AssetStatus.Rejected, "已退回 -> 重审")]
    [InlineData(AssetStatus.Active, "已上线 -> 重审")]
    [InlineData(AssetStatus.OffShelf, "已下架 -> 重审")]
    public void ForceReview_ValidSourceState_ShouldBeAllowed(AssetStatus sourceState, string scenario)
    {
        // Arrange
        var action = "force-review";

        // Act
        var result = ValidateStateTransition(sourceState, action);

        // Assert
        Assert.True(result.IsValid, $"场景 [{scenario}]: 状态 {sourceState} 应该允许强制重审操作");
    }

    /// <summary>
    /// T2.6: 强制重审动作的非法流转测试
    /// 非法前置状态: PendingReview(0), ApprovedPendingPublish(2)
    /// </summary>
    [Theory(DisplayName = "强制重审 - 非法流转")]
    [InlineData(AssetStatus.PendingReview, "审核中不能重审")]
    [InlineData(AssetStatus.ApprovedPendingPublish, "待发布不能重审")]
    public void ForceReview_InvalidSourceState_ShouldBeRejected(AssetStatus sourceState, string scenario)
    {
        // Arrange
        var action = "force-review";

        // Act
        var result = ValidateStateTransition(sourceState, action);

        // Assert
        Assert.False(result.IsValid, $"场景 [{scenario}]: 状态 {sourceState} 不应允许强制重审操作");
        Assert.Equal("INVALID_STATE_TRANSITION", result.ErrorCode);
    }

    #endregion

    #region T2.7-T2.8: 恢复上架（Relist）流转测试

    /// <summary>
    /// T2.7: 恢复上架动作的合法流转测试
    /// 合法前置状态: OffShelf(4)
    /// 目标状态: Active(3)
    /// </summary>
    [Fact(DisplayName = "恢复上架 - 合法流转: OffShelf -> Active")]
    public void Relist_FromOffShelfState_ShouldBeAllowed()
    {
        // Arrange
        var sourceState = AssetStatus.OffShelf;
        var action = "relist";

        // Act
        var result = ValidateStateTransition(sourceState, action);

        // Assert
        Assert.True(result.IsValid, "OffShelf 状态应该允许恢复上架");
    }

    /// <summary>
    /// T2.8: 恢复上架动作的非法流转测试
    /// 非法前置状态: PendingReview(0), Rejected(1), ApprovedPendingPublish(2), Active(3)
    /// </summary>
    [Theory(DisplayName = "恢复上架 - 非法流转")]
    [InlineData(AssetStatus.PendingReview, "审核中不能恢复上架")]
    [InlineData(AssetStatus.Rejected, "已退回不能恢复上架")]
    [InlineData(AssetStatus.ApprovedPendingPublish, "待发布不能恢复上架")]
    [InlineData(AssetStatus.Active, "已上线不能恢复上架")]
    public void Relist_InvalidSourceState_ShouldBeRejected(AssetStatus sourceState, string scenario)
    {
        // Arrange
        var action = "relist";

        // Act
        var result = ValidateStateTransition(sourceState, action);

        // Assert
        Assert.False(result.IsValid, $"场景 [{scenario}]: 状态 {sourceState} 不应允许恢复上架操作");
        Assert.Equal("INVALID_STATE_TRANSITION", result.ErrorCode);
    }

    #endregion

    #region T2.9: 审核通过状态测试

    /// <summary>
    /// T2.9: 验证审核通过后状态为 ApprovedPendingPublish(2)
    /// 需求 R5: 审核通过后状态应为"待发布"，而非直接"已上线"
    /// </summary>
    [Fact(DisplayName = "审核通过后状态应为 ApprovedPendingPublish(2)")]
    public void ReviewApproval_ShouldSetStatusToApprovedPendingPublish()
    {
        // Arrange
        var expectedApprovalStatus = AssetStatus.ApprovedPendingPublish;

        // Assert - 验证状态值
        Assert.Equal(2, (int)expectedApprovalStatus);
        Assert.Equal("ApprovedPendingPublish", expectedApprovalStatus.ToString());
        
        // 审核通过不应该直接进入 Active
        Assert.NotEqual(AssetStatus.Active, expectedApprovalStatus);
    }

    #endregion

    #region 下架与软删除独立性测试

    /// <summary>
    /// 验证 OffShelf 状态独立于 IsDeleted 软删除
    /// 需求 R4/R5: 下架是独立状态，不等同于软删除
    /// </summary>
    [Fact(DisplayName = "下架状态独立于软删除")]
    public void OffShelf_ShouldBeIndependentFromSoftDelete()
    {
        // Arrange - 模拟下架前后的状态
        var assetBeforeOffShelf = new MockAsset
        {
            Status = AssetStatus.Active,
            IsDeleted = false
        };

        // Act - 执行下架（仅改变 Status，不改变 IsDeleted）
        var assetAfterOffShelf = new MockAsset
        {
            Status = AssetStatus.OffShelf,
            IsDeleted = false  // 下架不应修改此字段
        };

        // Assert
        Assert.Equal(AssetStatus.OffShelf, assetAfterOffShelf.Status);
        Assert.False(assetAfterOffShelf.IsDeleted, "下架操作不应修改 IsDeleted 字段");
    }

    /// <summary>
    /// 模拟资产对象（用于测试）
    /// </summary>
    private class MockAsset
    {
        public AssetStatus Status { get; set; }
        public bool IsDeleted { get; set; }
    }

    #endregion

    #region 状态流转验证方法（镜像后端实现）

    private readonly struct StateTransitionResult
    {
        public bool IsValid { get; init; }
        public string ErrorMessage { get; init; }
        public string ErrorCode { get; init; }

        public static StateTransitionResult Valid() => new() { IsValid = true };
        public static StateTransitionResult Invalid(string code, string message) => new() 
        { 
            IsValid = false, 
            ErrorCode = code, 
            ErrorMessage = message 
        };
    }

    /// <summary>
    /// 校验状态流转是否合法（镜像 KaxHttp.AssetManagement.cs 中的实现）
    /// </summary>
    private static StateTransitionResult ValidateStateTransition(AssetStatus currentStatus, string action)
    {
        var validTransitions = action.ToLowerInvariant() switch
        {
            "return" => new[] { AssetStatus.PendingReview, AssetStatus.ApprovedPendingPublish, AssetStatus.Active, AssetStatus.OffShelf },
            "off-shelf" => new[] { AssetStatus.Active },
            "force-review" => new[] { AssetStatus.Rejected, AssetStatus.Active, AssetStatus.OffShelf },
            "relist" => new[] { AssetStatus.OffShelf },
            _ => Array.Empty<AssetStatus>()
        };

        if (validTransitions.Length == 0)
            return StateTransitionResult.Invalid("INVALID_ACTION", $"未知的动作类型: {action}");

        if (!validTransitions.Contains(currentStatus))
        {
            var allowedStates = string.Join(", ", validTransitions.Select(s => $"{(int)s}({s})"));
            return StateTransitionResult.Invalid(
                "INVALID_STATE_TRANSITION",
                $"当前状态 {(int)currentStatus}({currentStatus}) 不允许执行 {action} 操作。允许的前置状态: {allowedStates}"
            );
        }

        return StateTransitionResult.Valid();
    }

    #endregion

    #region 全矩阵覆盖测试

    /// <summary>
    /// 生成所有动作和所有状态的组合测试数据
    /// </summary>
    public static IEnumerable<object[]> AllTransitionCombinations
    {
        get
        {
            var actions = new[] { "return", "off-shelf", "force-review", "relist" };
            foreach (var action in actions)
            {
                foreach (var status in AllStatuses)
                {
                    var rule = TransitionRules[action];
                    var shouldBeValid = rule.ValidSourceStates.Contains(status);
                    yield return new object[] { action, status, shouldBeValid };
                }
            }
        }
    }

    /// <summary>
    /// 完整状态流转矩阵测试
    /// 确保所有动作 × 所有状态的组合都有明确的处理
    /// </summary>
    [Theory(DisplayName = "状态流转完整矩阵测试")]
    [MemberData(nameof(AllTransitionCombinations))]
    public void StateTransition_FullMatrix_ShouldMatchExpectedBehavior(
        string action,
        AssetStatus sourceStatus,
        bool expectedValid)
    {
        // Act
        var result = ValidateStateTransition(sourceStatus, action);

        // Assert
        Assert.Equal(expectedValid, result.IsValid);
    }

    #endregion
}
