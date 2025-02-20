using Drx.Sdk.Script;
using Drx.Sdk.Script.Functions;

class Program
{
    static void Main(string[] args)
    {
        // 创建脚本引擎实例
        var scriptEngine = new ScriptEngine();

        // 注册脚本函数
        scriptEngine.RegisterScriptFunctions(new ScriptFunctions());

        // 定义脚本文本
        string scriptText = @"
                var x = 10;
                var y = 20;
                var result = add(x, y);
                var str = int_tostring(result);
                if x == 10 then
                    print('x is 10');
                end;
                print(str);
            ";

        // 解析脚本
        var context = scriptEngine.Parse(scriptText);

        // 执行脚本
        scriptEngine.Execute(context);
    }
}
