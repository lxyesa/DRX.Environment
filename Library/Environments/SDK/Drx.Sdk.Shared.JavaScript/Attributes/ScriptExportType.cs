namespace Drx.Sdk.Shared.JavaScript.Attributes
{
    /// <summary>
    /// 定义脚本导出的目标类型：普通类、静态方法函数或静态类。
    /// </summary>
    public enum ScriptExportType
    {
        /// <summary>普通类，支持实例化。</summary>
        Class,
        /// <summary>静态方法，以函数形式导出。</summary>
        Function,
        /// <summary>静态类，以对象形式导出所有成员。</summary>
        StaticClass
    }
}
