using System;

namespace Web.KaxServer.Models
{
    /// <summary>
    /// 用户权限类型枚举
    /// </summary>
    public enum UserPermissionType
    {
        /// <summary>
        /// 普通用户
        /// </summary>
        Normal = 0,
        
        /// <summary>
        /// VIP用户
        /// </summary>
        VIP = 1,
        
        /// <summary>
        /// 超级VIP用户
        /// </summary>
        SVIP = 2,

        /// <summary>
        /// 管理员
        /// </summary>
        Admin = 3,

        /// <summary>
        /// 开发者（开发人员）比管理员更高权限
        /// </summary>
        Developer = 4,
    }
    
    /// <summary>
    /// 用户权限类型扩展方法
    /// </summary>
    public static class UserPermissionTypeExtensions
    {
        /// <summary>
        /// 获取权限类型的显示名称
        /// </summary>
        /// <param name="permissionType">权限类型</param>
        /// <returns>显示名称</returns>
        public static string GetDisplayName(this UserPermissionType permissionType)
        {
            return permissionType switch
            {
                UserPermissionType.Normal => "普通",
                UserPermissionType.VIP => "VIP",
                UserPermissionType.SVIP => "SVIP",
                UserPermissionType.Admin => "管理员",
                UserPermissionType.Developer => "开发者",
                _ => "未知"
            };
        }
        
        /// <summary>
        /// 从字符串解析权限类型
        /// </summary>
        /// <param name="permissionName">权限名称</param>
        /// <returns>权限类型</returns>
        public static UserPermissionType ParseFromString(string permissionName)
        {
            return permissionName.ToLower() switch
            {
                "vip" => UserPermissionType.VIP,
                "svip" => UserPermissionType.SVIP,
                "admin" => UserPermissionType.Admin,
                "developer" => UserPermissionType.Developer,
                "dev" => UserPermissionType.Developer,
                _ => UserPermissionType.Normal
            };
        }
    }
} 