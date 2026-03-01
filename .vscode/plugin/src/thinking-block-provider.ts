/**
 * 思考块解析器 - 从文本中提取和处理思考块
 */
export interface ThinkingBlock {
	content: string;
	startIndex: number;
	endIndex: number;
}

export interface ParsedResponse {
	thinkingBlocks: ThinkingBlock[];
	mainContent: string;
	hasThinking: boolean;
}

export class ThinkingBlockProvider {
	/**
	 * 从响应文本中解析思考块
	 */
	parseThinkingBlocks(response: string): ParsedResponse {
		const thinkingBlocks: ThinkingBlock[] = [];
		const thinkingRegex = /<thinking>([\s\S]*?)<\/thinking>/g;
		
		let match;
		let mainContent = response;

		while ((match = thinkingRegex.exec(response)) !== null) {
			thinkingBlocks.push({
				content: match[1].trim(),
				startIndex: match.index,
				endIndex: match.index + match[0].length
			});
		}

		// 移除思考块，获取主要内容
		mainContent = response.replace(/<thinking>[\s\S]*?<\/thinking>/g, '').trim();

		return {
			thinkingBlocks,
			mainContent,
			hasThinking: thinkingBlocks.length > 0
		};
	}

	/**
	 * 格式化思考块为可显示的形式
	 */
	formatThinkingBlock(block: ThinkingBlock, index: number): string {
		return `**💭 思考块 ${index + 1}:**\n\`\`\`\n${block.content}\n\`\`\`\n`;
	}

	/**
	 * 生成完整的格式化响应（包含思考块和主要内容）
	 */
	formatResponse(parsed: ParsedResponse): string {
		let formatted = '';

		// 添加所有思考块
		if (parsed.thinkingBlocks.length > 0) {
			formatted += '## 🤔 推理过程\n\n';
			parsed.thinkingBlocks.forEach((block, index) => {
				formatted += this.formatThinkingBlock(block, index);
				if (index < parsed.thinkingBlocks.length - 1) {
					formatted += '\n---\n\n';
				}
			});
			formatted += '\n\n## 📝 回答\n\n';
		}

		// 添加主要内容
		formatted += parsed.mainContent;

		return formatted;
	}

	/**
	 * 检测响应中的思考块数量
	 */
	getThinkingBlockCount(response: string): number {
		const matches = response.match(/<thinking>[\s\S]*?<\/thinking>/g);
		return matches ? matches.length : 0;
	}

	/**
	 * 提取纯文本思考块内容（无HTML标签）
	 */
	extractPureThinkingContent(response: string): string[] {
		const thinkingRegex = /<thinking>([\s\S]*?)<\/thinking>/g;
		const contents: string[] = [];
		let match;

		while ((match = thinkingRegex.exec(response)) !== null) {
			contents.push(match[1].trim());
		}

		return contents;
	}
}
