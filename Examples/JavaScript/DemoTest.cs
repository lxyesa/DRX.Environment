using System;
using Drx.Sdk.Shared.JavaScript;

namespace Examples.JavaScript
{
    /// <summary>
    /// 单元测试：验证导出类型、错误处理、日志、异步与泛型返回
    /// </summary>
    public class DemoTest
    {
        public static void RunAll()
        {
            var logger = new ScriptLogger { Level = ScriptLogger.LogLevel.Debug };
            logger.Log("=== MathUtils 静态方法测试 ===");
            logger.Log($"Add(2,3): {MathUtils.Add(2, 3)}");
            logger.Log($"Divide(4,2): {MathUtils.Divide(4, 2)}");
            logger.Log($"Divide(1,0): {MathUtils.Divide(1, 0)}");

            logger.Log("=== Person 类测试 ===");
            var p = new Person("Bob", 25);
            logger.Log($"Name: {p.Name}, Age: {p.Age}, Greet: {p.Greet()}");

            logger.Log("=== StringHelper 静态方法测试 ===");
            logger.Log($"ToUpper('abc'): {StringHelper.ToUpper("abc")}");
            logger.Log($"Reverse('hello'): {StringHelper.Reverse("hello")}");

            logger.Log("=== 错误处理与日志 ===");
            try
            {
                MathUtils.Divide(1, 0);
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
            }

            logger.Log("=== 泛型与异步返回测试 ===");
            // 假设有异步接口可用
            // var result = await JavaScript.ExecuteAsync<int>("return MathUtils.Add(5,6);");
            // logger.Log($"Async Add(5,6): {result}");
        }
    }
}