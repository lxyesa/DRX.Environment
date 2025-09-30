using Drx.Sdk.Shared.DMixin;
using static Drx.Sdk.Shared.DMixin.DMixin;

namespace ConsoleApp1
{
    // 简单目标类，包含将被 mixin 注入的方法
    public class TargetClass
    {
        public virtual string SayHello(string name)
        {
            Console.WriteLine($"TargetClass.SayHello 正在执行：{name}");
            return $"Hello {name}";
        }
    }

    // Mixin 类，使用 DMixinAttribute 指向目标类型，并在方法头尾注入
    [DMixin(typeof(TargetClass))]
    public class MyMixin
    {
        // 注入 TargetClass.SayHello 的 Head，读取参数并可以取消原方法
        [DMixinInject("SayHello", InjectAt.Head, useCallbackCi: true)]
        public static void OnSayHelloHead(string name, CallbackInfo ci)
        {
            Console.WriteLine($"[Mixin 头] SayHello 之前：{name}");
            // 示例：不取消原方法，只读
        }

        // 注入 Tail，将修改返回值
        [DMixinInject("SayHello", InjectAt.Tail, useCallbackCi: true)]
        public static void OnSayHelloTail(string name, CallbackInfo ci)
        {
            Console.WriteLine($"[Mixin 尾] SayHello 之后：{name}，原始返回 = {ci.Result}");
            // 修改返回值
            if (ci.Result is string s) ci.Result = s + " (from mixin)";
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("开始 DMixin 测试...");

            // 初始化 mixin 引擎并执行测试
            DMixin.Initialize();

            var t = new TargetClass();
            var res = t.SayHello("Copilot");
            Console.WriteLine($"最终结果：{res}");
        }
    }
}
