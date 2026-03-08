namespace Drx.Sdk.Shared.JavaScript.Attributes
{
    /// <summary>
    /// 指定类、方法、属性或字段在 JavaScript 中使用的自定义导出名称。
    /// 优先级高于 <see cref="ScriptExportAttribute.Name"/>。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class ScriptNameAttribute : Attribute
    {
        /// <summary>
        /// 在 JavaScript 中使用的自定义名称。
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 初始化 <see cref="ScriptNameAttribute"/>，指定 JS 导出名称。
        /// </summary>
        /// <param name="name">JavaScript 侧使用的名称，不可为空或空白。</param>
        public ScriptNameAttribute(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("JS 导出名称不能为空。", nameof(name));
            Name = name;
        }
    }
}
