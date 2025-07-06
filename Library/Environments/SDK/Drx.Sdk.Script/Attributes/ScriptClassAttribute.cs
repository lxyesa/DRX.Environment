using System;

namespace Drx.Sdk.Script.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public class ScriptClassAttribute : Attribute
{
    public string? Name { get; set; }
    
    // 控制是否以原始 Host 类型导出
    public bool HostType { get; set; } = true;

    public ScriptClassAttribute(string? name = null)
    {
        Name = name;
    }
}