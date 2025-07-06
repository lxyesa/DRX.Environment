using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Web.KaxServer.Services;
using Drx.Sdk.Network.Session;
using Web.KaxServer.Models;
using System.IO;
using System.Xml;
using System.Linq;
using System;
using System.Globalization;
using System.Xml.Linq;
using Microsoft.AspNetCore.Hosting;

namespace Web.KaxServer.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SessionManager _sessionManager;
        private readonly MessageBoxService _messageBoxService;
        private readonly IUserService _userService;

        [BindProperty]
        public required string Username { get; set; }

        [BindProperty]
        public required string Password { get; set; }

        [BindProperty]
        public bool RememberMe { get; set; }

        public LoginModel(SessionManager sessionManager, MessageBoxService messageBoxService, IUserService userService)
        {
            _sessionManager = sessionManager;
            _messageBoxService = messageBoxService;
            _userService = userService;
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

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                _messageBoxService.Inject("登录失败", "请输入有效的用户名和密码。", "我知道了");
                return Page();
            }

            // 验证用户凭据
            var userSession = _userService.AuthenticateUser(Username, Password);

            if (userSession != null)
            {
                try
                {
                    // Find and remove any existing sessions for this user to enforce single-session login
                    var existingSessions = _sessionManager.GetAllSessions()
                        .OfType<UserSession>()
                        .Where(s => s.Username.Equals(Username, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    foreach (var sessionToKick in existingSessions)
                    {
                        _sessionManager.RemoveSession(sessionToKick.ID);
                    }
                    
                    // 设置会话，如果选择"记住我"，则设置Cookie
                    var newSession = _sessionManager.CreateSession(userSession, RememberMe);

                    // Persist session change to user data file by updating the session ID
                    if (_userService is UserService concreteUserService)
                    {
                        var userData = concreteUserService.GetUserDataById(userSession.UserId);
                        if (userData != null)
                        {
                            userData.SessionId = newSession;
                            userSession.Coins = userData.Coins;
                            userSession.Email = userData.Email;
                            userSession.Username = userData.Username;
                            userSession.AvatarUrl = userData.AvatarUrl;
                            userSession.UserPermission = userData.UserPermission;
                            userSession.UserId = userData.UserId;
                        }
                    }

                    // 检查是否有重定向URL
                    if (TempData["ReturnUrl"] is string returnUrl && !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    {
                        return Redirect(returnUrl);
                    }

                    // 登录成功，重定向到首页
                    return RedirectToPage("/Index");
                }
                catch (System.Exception)
                {
                     _messageBoxService.Inject("登录失败", "登录过程中发生未知错误。", "我知道了");
                    return Page();
                }
            }
            else
            {
                // 登录失败
                _messageBoxService.Inject("登录失败", "用户名或密码错误。", "我知道了");
                return Page();
            }
        }
    }
} 