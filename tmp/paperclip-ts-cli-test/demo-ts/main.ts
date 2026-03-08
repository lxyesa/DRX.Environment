// Paperclip TypeScript 入口脚本（运行时已自动预加载 Models 目录下模块）
const { log, fs } = Paperclip.use();

log.info('Hello, Paperclip TypeScript!');
fs.writeText('./hello.txt', 'Created by Paperclip SDK module.');
