using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.IO;
using Web.KaxServer.Services;
using Drx.Sdk.Network.Email;
using Drx.Sdk.Network.Session;
using Web.KaxServer.Models;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Collections.Generic;

namespace Web.KaxServer.Pages.Account
{
    public class RegisterModel : PageModel
    {
        [BindProperty]
        [Required(ErrorMessage = "请输入用户名")]
        [StringLength(16, MinimumLength = 4, ErrorMessage = "用户名长度必须在4-16个字符之间")]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "用户名只能包含字母、数字和下划线")]
        public required string Username { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "请输入电子邮箱")]
        [EmailAddress(ErrorMessage = "请输入有效的电子邮箱地址")]
        public required string Email { get; set; }
        
        [BindProperty]
        [Required(ErrorMessage = "请设置密码")]
        [StringLength(100, MinimumLength = 8, ErrorMessage = "密码长度至少为8个字符")]
        [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d)[A-Za-z\d]{8,}$", ErrorMessage = "密码必须包含至少一个字母和一个数字")]
        public required string Password { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "请确认密码")]
        [Compare("Password", ErrorMessage = "两次输入的密码不一致")]
        public required string ConfirmPassword { get; set; }

        [TempData]
        public string? UserEmail { get; set; }

        private readonly MessageBoxService _messageBoxService;
        private readonly SessionManager _sessionManager;
        private readonly IUserService _userService;

        public RegisterModel(MessageBoxService messageBoxService, SessionManager sessionManager, IUserService userService)
        {
            _messageBoxService = messageBoxService;
            _sessionManager = sessionManager;
            _userService = userService;
        }

        public void OnGet()
        {
        }
        
        public IActionResult OnPost()
        {
            if (!ModelState.IsValid){
                StringBuilder errorMsg = new StringBuilder();
                errorMsg.AppendLine("<p>请检查以下输入内容：</p>");
                errorMsg.AppendLine("<ul style='color:#b91c1c;margin-left:1.5em;'>");
                errorMsg.AppendLine("<li>用户名长度必须在4-16个字符之间</li>");
                errorMsg.AppendLine("<li>密码长度至少为8个字符</li>");
                errorMsg.AppendLine("<li>密码必须包含至少一个字母和一个数字</li>");
                errorMsg.AppendLine("<li>两次输入的密码不一致</li>");
                errorMsg.AppendLine("</ul>");

                _messageBoxService.Inject("注册失败", errorMsg.ToString(), "我知道了");
                return Page();
            }
            
            // Check if username or email already exists
            if (_userService is UserService concreteUserService)
            {
                var allUsers = concreteUserService.GetAllUsers();
                if (allUsers.Any(u => u.Username.Equals(Username, StringComparison.OrdinalIgnoreCase)))
                {
                    _messageBoxService.Inject("注册失败", "该用户名已被使用，请换一个。", "我知道了");
                    return Page();
                }
                if (allUsers.Any(u => u.Email.Equals(Email, StringComparison.OrdinalIgnoreCase)))
                {
                    _messageBoxService.Inject("注册失败", "该电子邮箱已被注册，请使用其他邮箱或直接登录。", "我知道了");
                    return Page();
                }
            }

            // 创建验证会话
            var session = new EmailVerificationSession(Username, Email, Password);
            _sessionManager.CreateSession(session, false);
            
            // 构建验证链接
            var verificationLink = Url.Page(
                "/Account/VerifyEmail",
                pageHandler: null,
                values: new { token = session.ID },
                protocol: Request.Scheme);

            // 发送邮件
            var emailSender = new DRXEmail("drxhelp@qq.com", "kyzernkjwlsicifb", "Kax辅助平台");
            string emailBody = $@"
尊敬的用户：

您好！感谢您注册KAX游戏辅助平台。

请点击下面的链接以完成注册：
<a href='{verificationLink}'>{verificationLink}</a>

该链接将在10分钟内有效。如果这不是您的操作，请忽略此邮件。

KAX游戏辅助平台团队
";
            bool sendResult = emailSender.TrySendHtmlEmail("KAX游戏辅助平台 - 邮箱验证", emailBody, Email);

            if (!sendResult)
            {
                _messageBoxService.Inject("邮件发送失败", "无法发送验证邮件，请稍后再试或联系管理员。", "我知道了");
                return Page();
            }

            UserEmail = Email;
            TempData["Username"] = Username;
            return RedirectToPage("/Account/CheckYourEmail");
        }
    }
} 