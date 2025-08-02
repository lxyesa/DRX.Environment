namespace Drx.Sdk.Shared.ConsoleCommand
{
    /// <summary>
    /// 分支命令特性，用于标记只能作为特定父命令下级的命令
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class BranchCommandAttribute : Attribute
    {
        /// <summary>
        /// 分支命令名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 父命令名称
        /// </summary>
        public string ParentName { get; }

        /// <summary>
        /// 分支命令描述
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 参数说明
        /// </summary>
        public string[] ArgDescriptions { get; }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="name">分支命令名称</param>
        /// <param name="parentName">父命令名称</param>
        /// <param name="description">分支命令描述</param>
        /// <param name="argDescriptions">参数说明</param>
        public BranchCommandAttribute(string name, string parentName, string description, params string[] argDescriptions)
        {
            Name = name;
            ParentName = parentName;
            Description = description;
            ArgDescriptions = argDescriptions ?? Array.Empty<string>();
        }
    }
}