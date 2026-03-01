# Commands 目录 - 命令框架

## 概述
Commands 目录实现了内置命令系统，允许通过 HTTP 请求执行预定义的命令。

## 文件说明

### CommandAttribute.cs
**命令标记属性**
- 标记方法为可执行的命令
- 特点：
  - 命令名称定义
  - 权限级别控制
  - 帮助文本

**使用示例：**
```csharp
[Command("clear-cache", "清空缓存", PermissionLevel.Admin)]
public static void ClearCache(string[] args)
{
    // 清空缓存逻辑
}
```

### CommandManager.cs
**命令管理器**
- 集中管理所有注册的命令
- 特点：
  - 命令注册和执行
  - 权限验证
  - 输入验证

**主要方法：**
- `RegisterCommand()` - 注册命令
- `ExecuteCommand()` - 执行命令
- `GetCommand()` - 获取命令
- `ListCommands()` - 列出所有命令

### CommandParser.cs
**命令解析器**
- 解析命令行字符串
- 提取命令名和参数
- 特点：
  - 灵活的参数解析
  - 支持标志参数
  - 引号处理

**解析过程：**
```
输入字符串
    ↓
词法分析
    ↓
参数提取
    ↓
返回 CommandParseResult
```

### InteractiveCommandConsole.cs
**交互式命令控制台**
- 提供命令执行的交互式界面
- 特点：
  - REPL 模式
  - 命令历史
  - 自动补全（可选）
  - 帮助系统

## 命令系统架构

```
用户输入
    ↓
CommandParser 解析
    ↓
CommandManager 按名称查找
    ↓
权限验证
    ↓
命令执行
    ↓
返回结果
```

## 使用场景

1. **系统管理** - 清空缓存、重启服务等
2. **数据维护** - 数据导入导出、修复等
3. **性能分析** - 性能统计、诊断等
4. **开发调试** - 测试接口、查询数据等
5. **自动化脚本** - 通过 HTTP 调用命令

## 命令权限级别

通常支持的权限级别：
- `Public` - 公开命令
- `User` - 用户级
- `Admin` - 管理员级
- `System` - 系统级

## 内置命令示例

常见的内置命令：
- `help` - 获取帮助
- `status` - 获取服务器状态
- `version` - 获取版本信息
- `clear-cache` - 清空缓存
- `restart` - 重启服务器
- `stats` - 获取统计信息

## 与其他模块的关系

- **与 Configs 的关系** - CommandAttribute 定义命令
- **与 Server 的关系** - 服务器支持命令执行
- **与 Entry 的关系** - CommandQueueEntry 队列管理

## 使用示例

### 注册自定义命令
```csharp
[Command("user-count", "获取用户总数", PermissionLevel.Admin)]
public static string GetUserCount()
{
    return $"Total users: {Database.Users.Count}";
}
```

### 执行命令
```csharp
// 通过 HTTP 请求执行
POST /api/command
{
    "command": "user-count"
}
```

## 最佳实践

1. **命名** - 使用清晰的命名约定
2. **文档** - 提供详细的命令说明
3. **权限** - 严格控制敏感命令的权限
4. **验证** - 验证命令参数的有效性
5. **安全** - 防止命令注入攻击
6. **错误处理** - 提供有用的错误信息

## 相关文档
- 参见 [../Configs/CommandParseResult.cs](../Configs/) 了解解析结果
- 参见 [../Server/README.md](../Server/README.md) 了解服务器集成
