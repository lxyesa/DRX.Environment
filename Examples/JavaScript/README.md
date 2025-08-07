# JavaScript增强执行器示例与API说明

## 目录结构
- ScriptDemoExports.cs：导出示例类（MathUtils、Person、StringHelper）
- DemoTest.js：JavaScript端功能验证脚本
- DemoTest.cs：C#单元测试示例
- 相关核心类：ScriptException、ScriptLogger、ScriptExecutionContext

## 主要功能
- 类型自动扫描与注册，支持静态类、普通类、方法导出
- JavaScript调用.NET方法，支持同步/异步、泛型返回
- 完善的错误处理与重试机制，异常信息包含堆栈、类型、位置、上下文
- 分级日志记录，支持性能监控、输出到控制台/文件/自定义
- 示例类和脚本覆盖全部核心功能

## 示例用法

### 1. MathUtils 静态类
```js
console.log(MathUtils.Add(1, 2)); // 3
console.log(MathUtils.Divide(4, 2)); // 2
```

### 2. Person 普通类
```js
var p = new Person("Alice", 30);
console.log(p.Name); // "Alice"
console.log(p.Greet()); // "Hello, my name is Alice, age 30."
```

### 3. StringHelper 静态类
```js
console.log(StringHelper.ToUpper("abc")); // "ABC"
console.log(StringHelper.Reverse("hello")); // "olleh"
```

### 4. 错误处理与重试
```js
try {
    MathUtils.Divide(1, 0);
} catch(e) {
    console.log("捕获异常:", e.message);
}
```

### 5. 异步与泛型返回
```js
(async function() {
    let result = await MathUtils.Add(10, 20);
    console.log(result); // 30
})();
```

## API说明

### ScriptLogger
- 支持Debug/Info/Warning/Error分级日志
- Log(string, LogLevel)、LogException(Exception, Context)、LogPerformance(Context)
- 可自定义输出、日志文件路径

### ScriptException
- 封装JS运行时异常，包含详细堆栈、类型、位置、上下文

### ScriptExecutionContext
- 记录脚本内容、文件、起止时间、重试次数、调用方等

## 单元测试
- 参考DemoTest.cs，覆盖类型导出、方法调用、异常与日志、异步泛型等

## 运行说明
- 通过JavaScript.Execute/ExecuteAsync/ExecuteFile等接口运行脚本
- 日志与异常可通过ScriptLogger和ScriptException获取详细信息