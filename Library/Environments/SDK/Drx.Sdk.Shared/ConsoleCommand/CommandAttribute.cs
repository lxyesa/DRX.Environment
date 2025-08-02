namespace Drx.Sdk.Shared.ConsoleCommand
{
    public class CommandAttribute : Attribute
    {
        /// <summary>
        /// 命令名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 命令描述
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// 参数说明
        /// </summary>
        public string[] ArgDescriptions { get; }

        public CommandAttribute(string name, string description, params string[] argDescriptions)
        {
            Name = name;
            Description = description;
            ArgDescriptions = argDescriptions ?? Array.Empty<string>();
        }
    }
}