using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Drx.Sdk.Network.Email;
using System.Text;

namespace NDV.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailVerificationController : ControllerBase
    {
        // 存储邮箱验证码的字典，实际项目中应使用数据库或分布式缓存
        private static readonly ConcurrentDictionary<string, (string Code, DateTime Expiry)> _verificationCodes = new();
        
        // 验证码过期时间（5分钟）
        private static readonly TimeSpan _codeExpiryTime = TimeSpan.FromMinutes(5);
        
        // 邮件客户端配置
        private readonly DRXEmail _emailClient;
        
        public EmailVerificationController()
        {
            // 初始化邮件客户端，实际项目中应从配置中读取
            _emailClient = new DRXEmail("drxhelp@qq.com", "kyzernkjwlsicifb", "NDV系统");
        }
        
        [HttpPost("send")]
        public IActionResult SendVerificationEmail([FromForm] string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                return BadRequest(new { success = false, message = "邮箱地址不能为空" });
            }
            
            try
            {
                // 生成验证码
                string verificationCode = GenerateVerificationCode();
                
                // 生成验证链接
                string verificationLink = GenerateVerificationLink(email, verificationCode);
                
                // 构建邮件内容
                string emailContent = BuildVerificationEmailContent(email, verificationLink);
                
                // 发送邮件
                bool emailSent = _emailClient.TrySendMarkdownEmail(
                    "NDV系统 - 邮箱验证",
                    emailContent,
                    email
                );
                
                if (!emailSent)
                {
                    // 邮件发送失败，可能是邮箱不存在或其他错误
                    return BadRequest(new { success = false, message = "邮件发送失败，请检查邮箱地址是否正确" });
                }
                
                // 存储验证码和过期时间
                _verificationCodes[email] = (verificationCode, DateTime.UtcNow.Add(_codeExpiryTime));
                
                // 返回成功响应
                return Ok(new { success = true, message = "验证邮件已发送，请查收" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"发送验证邮件时出错: {ex.Message}");
                return StatusCode(500, new { success = false, message = "服务器错误，请稍后再试" });
            }
        }
        
        [HttpGet("verify")]
        public IActionResult VerifyEmail(string email, string code)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(code))
            {
                return BadRequest(new { success = false, message = "验证参数不完整" });
            }
            
            // 检查验证码是否存在且未过期
            if (_verificationCodes.TryGetValue(email, out var verificationData))
            {
                if (verificationData.Code == code)
                {
                    if (DateTime.UtcNow <= verificationData.Expiry)
                    {
                        // 验证成功，移除验证码
                        _verificationCodes.TryRemove(email, out _);
                        
                        // 重定向到验证成功页面
                        return Redirect($"/EmailVerified?email={Uri.EscapeDataString(email)}");
                    }
                    else
                    {
                        // 验证码已过期
                        _verificationCodes.TryRemove(email, out _);
                        return BadRequest(new { success = false, message = "验证链接已过期，请重新发送验证邮件" });
                    }
                }
            }
            
            return BadRequest(new { success = false, message = "验证失败，无效的验证链接" });
        }
        
        [HttpGet("check")]
        public IActionResult CheckVerificationStatus(string email)
        {
            // 此接口用于前端轮询检查邮箱是否已验证
            // 实际项目中应该使用数据库或其他持久化存储来检查用户验证状态
            
            // 这里简单实现：如果验证码不存在，则认为已验证（因为验证成功后会删除验证码）
            bool verified = !_verificationCodes.ContainsKey(email);
            
            return Ok(new { success = true, verified });
        }
        
        #region 辅助方法
        
        private string GenerateVerificationCode()
        {
            // 生成6位随机验证码
            Random random = new Random();
            return random.Next(100000, 999999).ToString();
        }
        
        private string GenerateVerificationLink(string email, string code)
        {
            // 生成验证链接
            string baseUrl = $"{Request.Scheme}://{Request.Host}";
            return $"{baseUrl}/api/EmailVerification/verify?email={Uri.EscapeDataString(email)}&code={code}";
        }
        
        private string BuildVerificationEmailContent(string email, string verificationLink)
        {
            // 使用Markdown格式构建邮件内容
            StringBuilder sb = new StringBuilder();
            
            sb.AppendLine("# NDV 邮箱验证");
            sb.AppendLine();
            sb.AppendLine("感谢您注册 NDV 系统！");
            sb.AppendLine();
            sb.AppendLine("请点击下面的链接完成邮箱验证：");
            sb.AppendLine();
            sb.AppendLine($"[点击此处验证邮箱]({verificationLink})");
            sb.AppendLine();
            sb.AppendLine("> 此链接将在 **5分钟** 后失效，请尽快完成验证。");
            sb.AppendLine();
            sb.AppendLine("如果您没有注册 NDV 系统，请忽略此邮件。");
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine();
            sb.AppendLine("NDV 团队");
            
            return sb.ToString();
        }
        
        #endregion
    }
} 