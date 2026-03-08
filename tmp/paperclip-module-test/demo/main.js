// Paperclip 入口脚本（运行时已自动预加载 paperclip.module.js）
const { log, fs } = Paperclip;

log.info('Hello, Paperclip!');
fs.writeText('./hello.txt', 'Created by Paperclip SDK module.');
