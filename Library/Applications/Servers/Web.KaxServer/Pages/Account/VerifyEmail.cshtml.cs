using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Drx.Sdk.Network.Session;
using Web.KaxServer.Models;
using System;
using Web.KaxServer.Services;
using System.Linq;
using Web.KaxServer.Services.Repositorys;

namespace Web.KaxServer.Pages.Account
{
    public class VerifyEmailModel : PageModel
    {
        private readonly SessionManager _sessionManager;

        [TempData]
        public string ErrorMessage { get; set; }
        
        [TempData]
        public string UsernameForSuccessPage { get; set; }

        public VerifyEmailModel(SessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public IActionResult OnGet(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                ErrorMessage = "无效的验证链接。";
                return RedirectToPage("/Account/RegisterError");
            }
            
            var session = _sessionManager.GetSession<EmailVerificationSession>(token);
            
            if (session == null || session.IsExpired())
            {
                 ErrorMessage = "验证链接已失效或已过期，请重新注册。";
                 return RedirectToPage("/Account/RegisterError");
            }

            // Redundant check for safety, the main check is now in Register.cshtml.cs
            if (UserRepository.GetAllUsers().Any(u => u.Username.Equals(session.Username, StringComparison.OrdinalIgnoreCase) || u.Email.Equals(session.Email, StringComparison.OrdinalIgnoreCase)))
            {
                ErrorMessage = "用户名或邮箱已被注册，请使用其他信息重新注册。";
                _sessionManager.RemoveSession(token);
                return RedirectToPage("/Account/RegisterError");
            }
            
            var newUser = new UserData
            {
                Username = session.Username,
                Email = session.Email,
                Password = session.Password, // Note: In a real app, hash this password!
                UserPermission = UserPermissionType.Normal,
                Coins = 1.0m, // Starting coins
                CreatedAt = DateTime.UtcNow,
                AvatarUrl = "/img/avatars/default.png" // Default avatar
            };

            UserRepository.SaveUser(newUser);
            
            _sessionManager.RemoveSession(token);
            
            UsernameForSuccessPage = newUser.Username;
            
            return RedirectToPage("/Account/RegisterSuccess");
        }
    }
} 