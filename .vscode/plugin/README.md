# Thinking Block Display Extension

一个VS Code扩展，用于在Copilot对话中显示和格式化思考块。

## 功能特性

✨ **核心功能:**
- 🤔 自动检测和解析 `<thinking>` 标签
- 📝 格式化显示思考过程和最终答案
- 💡 集成VS Code Chat Participant接口
- 🎨 美化的markdown输出

## 安装

```bash
cd plugin
npm install
npm run compile
```

## 使用

1. 在VS Code中打开该文件夹
2. 按 `F5` 启动调试
3. 在Copilot对话中使用 `@analyzer think` 或 `@analyzer analyze` 命令

## 命令

- `@analyzer think` - 显示详细的思考过程
- `@analyzer analyze` - 快速分析显示
- `@analyzer` - 通用响应

## 文件结构

```
src/
├── extension.ts              # 扩展入口点
├── thinking-block-provider.ts # 思考块解析和格式化
└── chat-participant.ts        # Chat Participant实现
```

## 核心类型

### ThinkingBlock
```typescript
interface ThinkingBlock {
  content: string;        // 思考内容
  startIndex: number;     // 在原文本中的开始位置
  endIndex: number;       // 在原文本中的结束位置
}
```

### ParsedResponse
```typescript
interface ParsedResponse {
  thinkingBlocks: ThinkingBlock[];  // 所有思考块
  mainContent: string;              // 主要回答内容
  hasThinking: boolean;             // 是否有思考块
}
```

## API

### ThinkingBlockProvider

#### parseThinkingBlocks(response: string)
解析响应中的所有思考块。

#### formatResponse(parsed: ParsedResponse)
格式化完整的响应（思考块+主要内容）。

#### getThinkingBlockCount(response: string)
获取响应中思考块的数量。

#### extractPureThinkingContent(response: string)
提取纯文本的思考块内容。

## 示例

### 输入
```
@analyzer think
为什么学习TypeScript很重要？
```

### 输出
将显示格式化的思考过程，例如：

```
## 🤔 推理过程

**💭 思考块 1:**
```
分析用户问题: "为什么学习TypeScript很重要?"
...
```

---

## 📝 回答

基于深入的分析，这是我的详细回答：
...
```

## 实现细节

1. **思考块识别**: 使用正则表达式匹配 `<thinking>...</thinking>` 标签
2. **内容分离**: 将思考块与主要内容分离显示
3. **格式化**: 使用markdown格式美化输出

## 开发

编译TypeScript:
```bash
npm run compile
```

监听文件变化:
```bash
npm run watch
```

## License

MIT
