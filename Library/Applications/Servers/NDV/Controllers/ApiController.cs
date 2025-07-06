using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Drx.Sdk.Network.Email;


namespace NDV.Controllers
{
    [ApiController]
    [Route("api")]
    public class ApiController : ControllerBase
    {
        [HttpGet("test")]
        public IActionResult Test()
        {
            // 返回一个简单的成功响应
            return Ok(new { 
                success = true, 
                message = "API请求成功", 
                timestamp = DateTime.Now
            });
        }
        
        [HttpPost("test")]
        public async Task<IActionResult> PostTest([FromForm] string name, [FromForm] string email, [FromForm] string password)
        {
            // 模拟处理延迟
            await Task.Delay(1000);
            
            // 验证数据
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "所有字段都是必填的"
                });
            }
            
            // 验证昵称必须为 6 个字符
            if (name.Length <= 6)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "昵称必须为6个字符",
                    field = "name"
                });
            }
            
            // 验证邮箱必须为xxx@xxx.xxx格式
            if (!Regex.IsMatch(email, @"^[^@]+@[^@]+\.[^@]+$", RegexOptions.IgnoreCase))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "邮箱必须为xxx@xxx.xxx格式",
                    field = "email"
                });
            }
            
            // 密码必须带有数字和字母
            bool hasLetter = Regex.IsMatch(password, @"[a-zA-Z]");
            bool hasDigit = Regex.IsMatch(password, @"\d");
            
            if (!hasLetter || !hasDigit)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "密码必须同时包含字母和数字",
                    field = "password"
                });
            }

            Console.WriteLine(name);
            Console.WriteLine(email);
            Console.WriteLine(password);
            
            // 发送注册成功邮件
            try
            {
                // 初始化邮件客户端 (使用QQ邮箱作为示例)
                var emailClient = new DRXEmail("drxhelp@qq.com", "kyzernkjwlsicifb", "NDV系统");
                
                // 构建Markdown格式的邮件内容
                string markdownContent = $@"
# 欢迎加入NDV系统，{name}！

您的账号已成功注册。以下是您的账号信息：

- **用户名**: {name}
- **邮箱**: {email}
- **注册时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

## 安全提示
请妥善保管您的密码，不要将密码告知他人。
如有任何问题，请随时联系我们的支持团队。

祝您使用愉快！

*NDV团队*
";

                // 发送Markdown邮件
                bool emailSent = emailClient.TrySendMarkdownEmail(
                    "NDV系统 - 注册成功通知",
                    markdownContent,
                    email
                );

                if (!emailSent)
                {
                    // 邮件发送失败，记录日志但不影响注册流程
                    Console.WriteLine($"无法向用户 {name}({email}) 发送注册成功邮件");
                }
                else
                {
                    Console.WriteLine($"已向 {email} 发送注册成功邮件");
                }
            }
            catch (Exception ex)
            {
                // 捕获邮件发送过程中的任何异常，记录日志但不影响注册流程
                Console.WriteLine($"发送邮件时发生错误: {ex.Message}");
            }
            
            // 返回成功响应
            return Ok(new
            {
                success = true,
                message = "注册成功",
                data = new
                {
                    name,
                    email,
                    registeredAt = DateTime.Now
                }
            });
        }
    }
} 