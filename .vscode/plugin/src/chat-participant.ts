import * as vscode from 'vscode';
import { ThinkingBlockProvider } from './thinking-block-provider';

/**
 * VS Code Chat Participant - 显示思考块的AI助手
 */
export class ThinkingChatParticipant {
	private thinkingProvider: ThinkingBlockProvider;

	constructor(provider: ThinkingBlockProvider) {
		this.thinkingProvider = provider;
	}

	register(context: vscode.ExtensionContext) {
		const participant = vscode.chat.createChatParticipant(
			'thinking-display.analyzer',
			this.handleChatRequest.bind(this)
		);

		// 设置参与者图标（可选）
		participant.iconPath = new vscode.ThemeIcon('lightbulb');

		context.subscriptions.push(participant);
	}

	private async handleChatRequest(
		request: vscode.ChatRequest,
		context: vscode.ChatContext,
		stream: vscode.ChatResponseStream,
		token: vscode.CancellationToken
	): Promise<vscode.ChatResult> {
		try {
			// 根据命令处理不同的逻辑
			const command = request.command;

			if (command === 'think') {
				await this.handleThinkCommand(request, stream, token);
			} else if (command === 'analyze') {
				await this.handleAnalyzeCommand(request, stream, token);
			} else {
				await this.handleDefaultCommand(request, stream, token);
			}

			return { metadata: { responseComplete: true } };
		} catch (error) {
			stream.markdown(`❌ 错误: ${error instanceof Error ? error.message : '未知错误'}`);
			return { metadata: { responseComplete: false, error: true } };
		}
	}

	/**
	 * 处理 "think" 命令 - 详细显示思考过程
	 */
	private async handleThinkCommand(
		request: vscode.ChatRequest,
		stream: vscode.ChatResponseStream,
		token: vscode.CancellationToken
	): Promise<void> {
		const userInput = request.prompt;

		// 模拟AI思考和响应
		const simulatedResponse = `<thinking>
分析用户问题: "${userInput}"

第一步：理解需求
- 用户想要了解关于这个主题
- 需要详细的解释和示例

第二步：检索信息
- 收集相关信息
- 组织逻辑结构

第三步：组织答案
- 按照易理解的方式排列
- 包含具体示例
</thinking>

基于深入的分析，这是我的详细回答：

## 核心要点
1. **关键概念 1**: 这是第一个重要的方面
2. **关键概念 2**: 这是第二个重要的方面
3. **关键概念 3**: 这是第三个重要的方面

## 详细说明
这里提供了详细的说明和具体的使用示例。

## 实践建议
- 建议 1
- 建议 2
- 建议 3`;

		// 解析响应
		const parsed = this.thinkingProvider.parseThinkingBlocks(simulatedResponse);

		// 流式输出格式化的响应
		const formatted = this.thinkingProvider.formatResponse(parsed);
		stream.markdown(formatted);
	}

	/**
	 * 处理 "analyze" 命令 - 快速分析
	 */
	private async handleAnalyzeCommand(
		request: vscode.ChatRequest,
		stream: vscode.ChatResponseStream,
		token: vscode.CancellationToken
	): Promise<void> {
		const userInput = request.prompt;

		const simulatedResponse = `<thinking>
快速分析: "${userInput}"
- 识别关键点
- 评估重要性
- 形成结论
</thinking>

## 分析结果

| 方面 | 说明 |
|------|------|
| 重要性 | 高 |
| 复杂度 | 中等 |
| 优先级 | 立即处理 |

### 结论
这是一个重要的事项，建议立即采取行动。`;

		const parsed = this.thinkingProvider.parseThinkingBlocks(simulatedResponse);
		const formatted = this.thinkingProvider.formatResponse(parsed);
		stream.markdown(formatted);
	}

	/**
	 * 处理默认命令 - 通用响应
	 */
	private async handleDefaultCommand(
		request: vscode.ChatRequest,
		stream: vscode.ChatResponseStream,
		token: vscode.CancellationToken
	): Promise<void> {
		const userInput = request.prompt;

		// 显示加载指示器
		stream.markdown('🤔 正在思考...\n\n');

		const simulatedResponse = `<thinking>
用户询问: "${userInput}"

这由以下几个步骤组成:
1. 理解问题的本质
2. 检索相关知识
3. 形成答案
</thinking>

我已经思考了你的问题。

**答案:**
根据分析，我的建议是:

\`\`\`
这是一个代码示例
const result = await process(input);
\`\`\`

希望这个回答对你有帮助！`;

		const parsed = this.thinkingProvider.parseThinkingBlocks(simulatedResponse);
		const formatted = this.thinkingProvider.formatResponse(parsed);
		
		// 替换加载提示
		stream.markdown(formatted);
	}
}
