namespace Drx.Sdk.Shared.JavaScript.Attributes
{
    /// <summary>
    /// 标记方法、属性或字段不导出到 JavaScript 环境中。
    /// 当父类已标记 <see cref="ScriptExportAttribute"/> 时，可用此特性排除特定成员。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class ScriptIgnoreAttribute : Attribute
    {
    }
}
