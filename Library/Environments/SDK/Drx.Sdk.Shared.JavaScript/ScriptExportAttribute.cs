namespace Drx.Sdk.Shared.JavaScript
{
    /// <summary>
    /// 指定类、方法、属性等成员可导出到JavaScript，并可自定义导出名称与类型。
    /// 支持Class（普通类）、Function（静态方法）、StaticClass（静态类）等多种导出类型。
    /// 可用于类、方法、属性、字段等目标，便于灵活标记导出。
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public sealed class ScriptExportAttribute : Attribute
    {
        /// <summary>
        /// 在JavaScript中使用的名称。若未指定，则默认采用目标成员的名称。
        /// </summary>
        public string? Name { get; }

        /// <summary>
        /// 导出类型，指定为Class、Function、StaticClass等。默认Function。
        /// </summary>
        public ScriptExportType ExportType { get; }

        /// <summary>
        /// 初始化ScriptExportAttribute，默认导出类型为Function，名称为目标成员名。
        /// </summary>
        public ScriptExportAttribute()
            : this((string?)null, ScriptExportType.Function)
        {
        }

        /// <summary>
        /// 初始化ScriptExportAttribute，指定导出名称，导出类型为Function。
        /// </summary>
        /// <param name="name">自定义导出名称</param>
        public ScriptExportAttribute(string name)
            : this(name, ScriptExportType.Function)
        {
        }

        /// <summary>
        /// 初始化ScriptExportAttribute，指定导出类型，名称为目标成员名。
        /// </summary>
        /// <param name="exportType">导出类型</param>
        public ScriptExportAttribute(ScriptExportType exportType)
            : this((string?)null, exportType)
        {
        }

        /// <summary>
        /// 初始化ScriptExportAttribute，指定导出名称和导出类型。
        /// </summary>
        /// <param name="name">自定义导出名称</param>
        /// <param name="exportType">导出类型</param>
        public ScriptExportAttribute(string? name, ScriptExportType exportType)
        {
            Name = name;
            ExportType = exportType;
        }
    }
}