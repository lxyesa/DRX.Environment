using System;

namespace Drx.Sdk.Script.Attributes;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class ScriptMethodAttribute : Attribute
{
    public string? Name { get; set; }
    public bool IsGlobal { get; set; }
    // TODO: [ScriptMethod(Function, true)]
    

    public ScriptMethodAttribute(string? name = null, bool isGlobal = false)
    {
        Name = name;
        IsGlobal = isGlobal;
    }
}
