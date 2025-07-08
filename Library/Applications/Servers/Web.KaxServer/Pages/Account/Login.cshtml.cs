using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Web.KaxServer.Services;
using Drx.Sdk.Network.Session;
using Web.KaxServer.Models;
using System;
using System.Linq;
using Web.KaxServer.Services.Repositorys;
using DRX.Framework;
using System.Threading.Tasks;

namespace Web.KaxServer.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SessionManager _sessionManager;
        private readonly MessageBoxService _messageBoxService;

        [BindProperty]
        public required string Username { get; set; }

        [BindProperty]
        public required string Password { get; set; }

        [BindProperty]
        public bool RememberMe { get; set; }

        public LoginModel(SessionManager sessionManager, MessageBoxService messageBoxService)
        {
            _sessionManager = sessionManager;
            _messageBoxService = messageBoxService;
        }

        public void OnGet(string returnUrl = null)
        {
            // 检查用户是否已经登录
            var userSession = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (userSession != null)
            {
                // 用户已登录，重定向到首页
                Response.Redirect("/Index");
                return;
            }

            // 将 returnUrl 存储在 TempData 中以便在 Post 请求中使用
            if (!string.IsNullOrEmpty(returnUrl))
            {
                TempData["ReturnUrl"] = returnUrl;
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                _ = _messageBoxService.Inject("登录失败", "请输入有效的用户名和密码。", "我知道了");
                return Page();
            }

            // 等待 1 ~ 3 秒，模拟网络延迟，同时缓解服务器压力
            await Task.Delay(new Random().Next(1000, 3000));

            var userData = UserRepository.GetUser(Username);

            // 验证用户凭据 (注意: 密码应哈希存储和比较)
            if (userData == null || userData.Password != Password)
            {
                _ = _messageBoxService.Inject("登录失败", "用户名或密码错误。", "我知道了");
                return Page();
            }
            
            // 检测用户是否被封禁
            if (userData.IsBanned())
            {
                _ = _messageBoxService.Inject("登录失败", $"您已被封禁，封禁结束时间：{userData.BanEndTime:yyyy-MM-dd HH:mm:ss}", "我知道了");
                Logger.Info($"用户 {Username} 尝试登陆，但被封禁。\r\n封禁结束时间：{userData.BanEndTime:yyyy-MM-dd HH:mm:ss}");
                return Page();
            }

            try
            {
                // 强制单一会话登录：查找并移除该用户已存在的会话
                var existingSessions = _sessionManager.GetAllSessions()
                    .OfType<UserSession>()
                    .Where(s => s.Username.Equals(Username, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                foreach (var sessionToKick in existingSessions)
                {
                    _sessionManager.RemoveSession(sessionToKick.ID, removeCookie: true);
                }
                
                // 创建新的用户会话
                var userSession = new UserSession(userData);
                var newSessionId = _sessionManager.CreateSession(userSession, RememberMe);

                // 将新的会话ID持久化到用户数据中
                userData.SessionId = newSessionId;
                UserRepository.SaveUser(userData);
                
                // 检查是否有重定向URL
                if (TempData["ReturnUrl"] is string returnUrl && !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }

                // 登录成功，重定向到首页
                return RedirectToPage("/Index");
            }
            catch (System.Exception ex)
            {
                Logger.Error($"登录过程中发生未知错误: {ex}");
                _ = _messageBoxService.Inject("登录失败", "登录过程中发生未知错误。", "我知道了");
                return Page();
            }
        }
    }
}