using System;
using Drx.Sdk.Shared.JavaScript;

namespace Drx.Sdk.Shared.JavaScript
{
    /// <summary>
    /// 静态数学工具类示例
    /// </summary>
    [ScriptExport("MathUtils", ScriptExportType.StaticClass)]
    public static class MathUtils
    {
        [ScriptExport]
        public static int Add(int a, int b) => a + b;

        [ScriptExport]
        public static int Subtract(int a, int b) => a - b;

        [ScriptExport]
        public static int Multiply(int a, int b) => a * b;

        [ScriptExport]
        public static double Divide(int a, int b) => b == 0 ? double.NaN : (double)a / b;
    }

    /// <summary>
    /// 普通类示例
    /// </summary>
    [ScriptExport("Person", ScriptExportType.Class)]
    public class Person
    {
        [ScriptExport]
        public string Name { get; set; }

        [ScriptExport]
        public int Age { get; set; }

        public Person(string name, int age)
        {
            Name = name;
            Age = age;
        }

        [ScriptExport]
        public string Greet() => $"Hello, my name is {Name}, age {Age}.";
    }

    /// <summary>
    /// 字符串辅助方法导出示例
    /// </summary>
    [ScriptExport("StringHelper", ScriptExportType.StaticClass)]
    public static class StringHelper
    {
        [ScriptExport]
        public static string ToUpper(string s) => s?.ToUpper();

        [ScriptExport]
        public static string ToLower(string s) => s?.ToLower();

        [ScriptExport]
        public static bool IsNullOrEmpty(string s) => string.IsNullOrEmpty(s);

        [ScriptExport]
        public static string Reverse(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            char[] arr = s.ToCharArray();
            Array.Reverse(arr);
            return new string(arr);
        }
    }
}