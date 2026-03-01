import * as vscode from 'vscode';
import { ThinkingBlockProvider } from './thinking-block-provider';
import { ThinkingChatParticipant } from './chat-participant';

export function activate(context: vscode.ExtensionContext) {
	console.log('Thinking Block Display extension activated');

	// 创建思考块提供者
	const thinkingProvider = new ThinkingBlockProvider();

	// 注册 Chat Participant
	const chatParticipant = new ThinkingChatParticipant(thinkingProvider);
	chatParticipant.register(context);

	// 注册命令: 切换思考块视图
	let toggleCommand = vscode.commands.registerCommand(
		'thinking-display.toggleThinkingView',
		() => {
			vscode.window.showInformationMessage('Thinking blocks view toggled');
		}
	);

	context.subscriptions.push(toggleCommand);
}

export function deactivate() {
	console.log('Thinking Block Display extension deactivated');
}
