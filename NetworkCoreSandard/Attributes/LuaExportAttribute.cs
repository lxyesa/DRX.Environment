using System;

namespace NetworkCoreStandard.Attributes;

/// <summary>
/// 标记需要导出到Lua环境的类型或成员
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property, AllowMultiple = false)]
public class LuaExportAttribute : Attribute
{
    /// <summary>
    /// 在Lua中使用的名称,为空则使用原名称
    /// </summary>
    public string? Name { get; }

    /// <summary>
    /// 是否导出所有公共成员
    /// </summary>
    public bool ExportMembers { get; set; }

    /// <summary>
    /// 创建一个新的LuaExport特性
    /// </summary>
    public LuaExportAttribute()
    {
    }

    /// <summary>
    /// 创建一个新的LuaExport特性并指定Lua中使用的名称
    /// </summary>
    /// <param name="name">在Lua中使用的名称</param>
    public LuaExportAttribute(string name)
    {
        Name = name;
    }
}