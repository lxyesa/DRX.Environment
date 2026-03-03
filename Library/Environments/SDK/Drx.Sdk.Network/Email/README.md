# Drx.Sdk.Network.Email

轻量、模块化的邮件发送模块，支持 **PlainText / HTML / Markdown**，并提供 **同步兼容 API** 与 **现代异步 API**。

## 设计目标

- **现代化 API**：以 `IEmailSender` + `EmailMessage` 为核心。
- **高模块化**：配置、消息模型、发送器实现分层解耦。
- **高封装**：SMTP 细节统一封装在 `SmtpEmailSender`。
- **向后兼容**：保留 `DRXEmail`、`NetworkServerEmailExtensions` 旧调用方式。

## 文件结构

- `IEmailSender.cs`：发送器抽象
- `SmtpEmailSender.cs`：SMTP 实现（支持 Markdown 渲染）
- `EmailSenderOptions.cs`：SMTP 配置
- `EmailMessage.cs`：邮件消息模型（To/Cc/Bcc/Subject/Body）
- `EmailContentType.cs`：正文类型枚举
- `EmailConfig.cs`：兼容配置模型（可转新模型）
- `DRXEmail.cs`：兼容门面（旧 API + 新 async API）
- `NetworkServerEmailExtensions.cs`：`NetworkServer` 扩展方法

---

## 快速开始（推荐：新 API）

### 1) 创建发送器

```csharp
using Drx.Sdk.Network.Email;

var sender = new SmtpEmailSender(new EmailSenderOptions
{
    SenderAddress = "noreply@example.com",
    Password = "your_smtp_password_or_auth_code",
    SmtpHost = "smtp.example.com",
    SmtpPort = 587,
    EnableSsl = true,
    DisplayName = "DRX Notify"
});
```

### 2) 发送文本邮件

```csharp
var message = EmailMessage.Create(
    to: "user@example.com",
    subject: "欢迎使用 DRX",
    body: "这是一封测试邮件。",
    contentType: EmailContentType.PlainText
);

await sender.SendAsync(message);
```

### 3) 发送 HTML / Markdown

```csharp
await sender.SendAsync(EmailMessage.Create(
    "user@example.com",
    "HTML 邮件",
    "<h1>Hello</h1><p>欢迎</p>",
    EmailContentType.Html));

await sender.SendAsync(EmailMessage.Create(
    "user@example.com",
    "Markdown 邮件",
    "# 标题\n\n- 项目 A\n- 项目 B",
    EmailContentType.Markdown));
```

### 4) 安全发送（不抛异常）

```csharp
var ok = await sender.TrySendAsync(message);
if (!ok)
{
    // 可按需重试或记录业务日志
}
```

---

## 兼容模式（旧 API）

`DRXEmail` 仍可使用，内部已切换到新发送器：

```csharp
var email = new DRXEmail("xxx@qq.com", "auth_code", "DRX");
email.SendEmail("主题", "正文", "to@example.com");

// 也支持新异步入口
await email.SendAsync(EmailMessage.Create(
    "to@example.com", "主题", "正文", EmailContentType.PlainText));
```

---

## 与 NetworkServer 集成

```csharp
using Drx.Sdk.Network.Email;

// 同步兼容
server.SendEmail("to@example.com", "正文", "sender@example.com", "auth_code", "主题");

// 异步推荐
await server.SendEmailAsync(
    to: "to@example.com",
    body: "正文",
    senderAddress: "sender@example.com",
    authCode: "auth_code",
    subject: "主题");
```

也可使用完整配置：

```csharp
var cfg = new EmailConfig
{
    SenderAddress = "sender@example.com",
    Password = "auth_code",
    To = "to@example.com",
    Subject = "通知",
    Body = "<b>hello</b>",
    SmtpHost = "smtp.example.com",
    SmtpPort = 587,
    EnableSsl = true,
    DisplayName = "DRX",
    ContentType = EmailContentType.Html
};

await server.SendEmailAsync(cfg);
```

---

## 注意事项

1. `SenderAddress/Password/SmtpHost/SmtpPort` 必须有效，否则会抛参数异常。
2. `EmailMessage.To` 至少需要一个收件人。
3. `EmailContentType.Auto` 会根据正文是否含 HTML 片段自动判定。
4. 生产环境建议：
   - 使用应用专用授权码（不要使用账户登录密码）
   - 将敏感配置放在安全配置源（环境变量/密钥管理）
   - 在上层业务增加重试与告警

## 迁移建议

- 新代码优先使用：`IEmailSender + SmtpEmailSender + EmailMessage`
- 旧代码可继续使用：`DRXEmail` / `SendEmail(...)`
- 逐步把同步调用迁移到 `SendAsync/TrySendAsync`，提升吞吐与可维护性
