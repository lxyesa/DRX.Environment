namespace Drx.Sdk.Shared.JavaScript.Attributes
{
    /// <summary>
    /// 指定某个构造函数作为 JavaScript 侧使用 <c>new</c> 运算符创建实例时调用的目标构造函数。
    /// 当类有多个构造函数时，可通过此特性明确指定首选构造函数。
    /// </summary>
    [AttributeUsage(AttributeTargets.Constructor, AllowMultiple = false, Inherited = false)]
    public sealed class ScriptConstructorAttribute : Attribute
    {
    }
}
