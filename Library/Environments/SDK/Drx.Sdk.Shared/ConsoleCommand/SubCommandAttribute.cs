namespace Drx.Sdk.Shared.ConsoleCommand
{
    public class SubCommandAttribute : Attribute
    {
        /// <summary>
        /// 子命令名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 子命令描述
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 参数说明
        /// </summary>
        public string[] ArgDescriptions { get; }

        /// <summary>
        /// 子命令索引级别，1为一级，2为二级，依此类推
        /// </summary>
        public int Level { get; }

        public SubCommandAttribute(string name, string description, params string[] argDescriptions)
        {
            Name = name;
            Description = description;
            ArgDescriptions = argDescriptions ?? Array.Empty<string>();
            Level = 1;
        }

        public SubCommandAttribute(string name, string description, int level, params string[] argDescriptions)
        {
            Name = name;
            Description = description;
            ArgDescriptions = argDescriptions ?? Array.Empty<string>();
            Level = level;
        }
    }
}