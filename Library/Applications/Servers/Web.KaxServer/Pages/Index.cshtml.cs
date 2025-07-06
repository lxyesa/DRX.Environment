using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Drx.Sdk.Network.Session;
using Web.KaxServer.Models;

namespace Web.KaxServer.Pages
{
    public class IndexModel : PageModel
    {
        private readonly SessionManager _sessionManager;

        public bool IsLoggedIn { get; private set; }
        public string Username { get; private set; }
        public string Email { get; private set; }
        public UserPermissionType UserPermission { get; private set; }
        public decimal Coins { get; private set; }

        public IndexModel(SessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public void OnGet()
        {
            // 检查用户是否已登录
            var userSession = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (userSession != null)
            {
                IsLoggedIn = true;
                Username = userSession.Username;
                Email = userSession.Email;
                UserPermission = userSession.UserPermission;
                Coins = userSession.Coins;
            }
            else
            {
                IsLoggedIn = false;
            }
        }

        // 处理登录按钮点击
        public IActionResult OnPostLogin()
        {
            return RedirectToPage("Account/Login");
        }

        // 处理注册按钮点击
        public IActionResult OnPostRegister()
        {
            return RedirectToPage("Account/Register");
        }

        // 处理退出登录按钮点击
        public IActionResult OnPostLogout()
        {
            var userSession = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (userSession != null)
            {
                _sessionManager.RemoveSession(userSession.ID, true);
            }
            return RedirectToPage("/Index");
        }
    }
}
