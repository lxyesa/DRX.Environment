# EmailBridge
> 邮件发送脚本桥接层

## Classes
| 类名 | 简介 |
|------|------|
| `EmailBridge` | 静态类，包装 SmtpEmailSender 提供邮件发送能力 |

## Methods
| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `createSender(addr, pwd, host?, port?, ssl?, name?)` | `senderAddress:string, password:string, smtpHost:string, smtpPort:int, enableSsl:bool, displayName:string` | `SmtpEmailSender` | 创建 SMTP 发送器 |
| `send(sender, to, subject, body)` | `sender:SmtpEmailSender, to:string, subject:string, body:string` | `Task` | 发送纯文本邮件 |
| `sendHtml(sender, to, subject, htmlBody)` | `sender:SmtpEmailSender, to:string, subject:string, htmlBody:string` | `Task` | 发送 HTML 邮件 |
| `sendMarkdown(sender, to, subject, mdBody)` | `sender:SmtpEmailSender, to:string, subject:string, mdBody:string` | `Task` | 发送 Markdown 邮件 |
| `trySend(sender, to, subject, body)` | `sender:SmtpEmailSender, to:string, subject:string, body:string` | `Task<bool>` | 安全发送（不抛异常） |

## Usage
```typescript
const sender = Email.createSender("me@qq.com", "auth_code", "smtp.qq.com");
await Email.send(sender, "you@example.com", "Hello", "World");
```
