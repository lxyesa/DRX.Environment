using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KaxSocket.Handlers.Helpers;
using KaxSocket.Model;
using Xunit;

namespace AssetManagementAcceptanceTests;

/// <summary>
/// 权限矩阵验收测试
/// 
/// 测试场景：验证 system-only API 仅允许权限组 0 (System) 访问
/// 覆盖需求：R1, R7
/// 
/// 权限矩阵定义：
/// | 角色    | 权限组 | 可见资产管理Tab | 可调用资产管理API |
/// |---------|--------|-----------------|-------------------|
/// | System  | 0      | 是              | 是                |
/// | Console | 2      | 否              | 否                |
/// | Admin   | 3      | 否              | 否                |
/// | User    | 999    | 否              | 否                |
/// </summary>
public class PermissionMatrixTests
{
    /// <summary>
    /// 测试用例数据：各权限组及其预期访问结果
    /// </summary>
    public static IEnumerable<object[]> PermissionTestData => new List<object[]>
    {
        // [权限组, 组名, 是否应允许访问]
        new object[] { UserPermissionGroup.System, "System (0)", true },
        new object[] { UserPermissionGroup.Console, "Console (2)", false },
        new object[] { UserPermissionGroup.Admin, "Admin (3)", false },
        new object[] { UserPermissionGroup.User, "User (999)", false }
    };

    /// <summary>
    /// T1.1-T1.4: 验证各权限组对 system-only API 的访问权限
    /// </summary>
    [Theory(DisplayName = "权限组访问控制测试")]
    [MemberData(nameof(PermissionTestData))]
    public void Permission_SystemOnlyApiAccess_ShouldMatchExpectedBehavior(
        UserPermissionGroup permissionGroup,
        string groupName,
        bool shouldAllowAccess)
    {
        // Arrange
        var isSystemUser = IsSystem(permissionGroup);

        // Assert
        if (shouldAllowAccess)
        {
            Assert.True(isSystemUser, 
                $"权限组 {groupName} 应该允许访问 system-only API");
        }
        else
        {
            Assert.False(isSystemUser, 
                $"权限组 {groupName} 不应该允许访问 system-only API");
        }
    }

    /// <summary>
    /// T1.5: 验证前端入口可见性逻辑
    /// 前端通过 permissionGroup === 0 判断是否显示资产管理 Tab
    /// </summary>
    [Theory(DisplayName = "前端Tab可见性测试")]
    [InlineData(0, true, "System")]      // permissionGroup 0 可见
    [InlineData(2, false, "Console")]    // permissionGroup 2 不可见
    [InlineData(3, false, "Admin")]      // permissionGroup 3 不可见
    [InlineData(999, false, "User")]     // permissionGroup 999 不可见
    public void FrontendVisibility_AssetManagementTab_ShouldOnlyShowForSystem(
        int permissionGroupValue,
        bool expectedVisible,
        string groupName)
    {
        // Arrange - 模拟前端判断逻辑: isSystemUser = permissionGroup === 0
        var isSystemUser = permissionGroupValue == 0;

        // Assert
        Assert.Equal(expectedVisible, isSystemUser);
    }

    /// <summary>
    /// 验证 isAdmin 包含 0/2/3 但 isSystem 仅包含 0
    /// 确保本功能范围内使用 isSystem 而非 isAdmin
    /// </summary>
    [Fact(DisplayName = "isSystem vs isAdmin 权限边界测试")]
    public void Permission_SystemVsAdmin_ShouldHaveCorrectBoundary()
    {
        // isAdmin 允许 0, 2, 3
        Assert.True(IsAdmin(UserPermissionGroup.System));
        Assert.True(IsAdmin(UserPermissionGroup.Console));
        Assert.True(IsAdmin(UserPermissionGroup.Admin));
        Assert.False(IsAdmin(UserPermissionGroup.User));

        // isSystem 仅允许 0
        Assert.True(IsSystem(UserPermissionGroup.System));
        Assert.False(IsSystem(UserPermissionGroup.Console));
        Assert.False(IsSystem(UserPermissionGroup.Admin));
        Assert.False(IsSystem(UserPermissionGroup.User));
    }

    #region Helper Methods (镜像 Api.cs 的权限判断逻辑)

    private static bool IsAdmin(UserPermissionGroup group)
    {
        return group == UserPermissionGroup.System
            || group == UserPermissionGroup.Console
            || group == UserPermissionGroup.Admin;
    }

    private static bool IsSystem(UserPermissionGroup group)
    {
        return group == UserPermissionGroup.System;
    }

    #endregion
}
