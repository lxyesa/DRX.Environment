using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KaxServer.Models;
using KaxServer.Services;
using System.Threading.Tasks;
using DRX.Framework;
using Microsoft.AspNetCore.Http.HttpResults;

namespace KaxServer.Pages.Account
{
    /// <summary>
    /// 用户个人资料页面模型，负责处理用户信息的展示、编辑、保存及登出等操作。
    /// </summary>
    public class ProfileModel : PageModel
    {
        /// <summary>
        /// 当前用户数据，页面绑定属性。
        /// </summary>
        [BindProperty]
        public UserData? CurrentUser { get; set; }

        /// <summary>
        /// 处理 GET 请求，加载当前用户信息。
        /// </summary>
        /// <returns>返回页面或重定向到登录页</returns>
        public async Task<IActionResult> OnGetAsync()
        {
            // 获取当前用户数据（依赖 UserManager，通常从 Session 或 Cookie 中获取用户标识）
            CurrentUser = await UserManager.GetCurrentUserAsync(HttpContext);

            // 如果用户未登录，重定向到登录页面，防止未授权访问
            if (CurrentUser == null)
            {
                return RedirectToPage("/Account/Login");
            }

            // 用户已登录，正常返回页面
            return Page();
        }

        /// <summary>
        /// 处理用户资料保存请求（POST），包括用户名、邮箱、订阅设置等。
        /// </summary>
        /// <returns>保存成功或失败后返回页面</returns>
        public async Task<IActionResult> OnPostSaveUserDataAsync()
        {
            // 再次获取当前用户，防止伪造请求
            CurrentUser = await UserManager.GetCurrentUserAsync(HttpContext);
            if (CurrentUser == null)
            {
                // 未登录则重定向到登录页
                return RedirectToPage("/Account/Login");
            }

            // 获取表单中的用户名
            var usernameValue = Request.Form["username"].FirstOrDefault();
            if (!string.IsNullOrEmpty(usernameValue))
            {
                // 用户名发生变更时，更新用户名及相关时间戳
                if (usernameValue != CurrentUser.Username)
                {
                    CurrentUser.Username = usernameValue;
                    // 记录上次改名时间
                    CurrentUser.UserSettingData.LastChangeNameTime = DateTime.UtcNow;
                    // 设置下次可改名时间（30天后）
                    CurrentUser.UserSettingData.NextChangeNameTime = DateTime.UtcNow.AddDays(30);

                    // 同步更新 Session 中的用户名，确保后续请求一致性
                    HttpContext.Session.SetString("Username", CurrentUser.Username);
                }
            }
            // 更新邮箱，若未填写则设为空字符串
            CurrentUser.Email = Request.Form["email"].FirstOrDefault() ?? string.Empty;

            // 处理新闻订阅选项，若未勾选则设为 false
            if (Request.Form.ContainsKey("newsSubscription"))
                CurrentUser.UserSettingData.NewsSubscription = Request.Form["newsSubscription"] == "on";
            else
                CurrentUser.UserSettingData.NewsSubscription = false;

            // 处理市场营销订阅选项，若未勾选则设为 false
            if (Request.Form.ContainsKey("marketingSubscription"))
                CurrentUser.UserSettingData.MarketingSubscription = Request.Form["marketingSubscription"] == "on";
            else
                CurrentUser.UserSettingData.MarketingSubscription = false;

            // 调用 UserManager 更新用户数据，涉及数据库操作
            var result = await UserManager.UpdateUserAsync(CurrentUser);

            // 更新失败，添加模型错误并返回当前页面
            if (!result)
            {
                ModelState.AddModelError(string.Empty, "更新用户数据失败，请稍后重试");
                return Page();
            }

            // 保存成功后重新获取最新用户数据，确保页面内容实时同步
            CurrentUser = await UserManager.GetCurrentUserAsync(HttpContext);
            return Page();
        }

        /// <summary>
        /// 管理员编辑用户信息（如用户名、邮箱、权限、等级等），需防止越权操作。
        /// </summary>
        /// <returns>返回部分视图或错误信息</returns>
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostEditUserAsync()
        {
            // 获取表单中的用户ID，通常由管理员操作
            var userIdStr = Request.Form["userId"].FirstOrDefault();
            if (!int.TryParse(userIdStr, out int userId))
                return BadRequest("用户ID无效");

            // 根据用户ID获取用户对象，依赖 UserManager
            var user = await UserManager.GetUserByIdAsync(userId);
            if (user == null)
                return NotFound("用户不存在");

            // 基本信息
            user.Username = Request.Form["UserName"].FirstOrDefault() ?? user.Username;
            user.Email = Request.Form["Email"].FirstOrDefault() ?? user.Email;
            if (int.TryParse(Request.Form["Level"], out int level)) user.Level = level;
            if (int.TryParse(Request.Form["Coins"], out int coins)) user.Coins = coins;
            if (int.TryParse(Request.Form["Exp"], out int exp)) user.Exp = exp;

            // 用户设置数据
            var us = user.UserSettingData;
            if (us != null)
            {
                us.EmailNotifications = Request.Form["UserSettingData.EmailNotifications"] == "on";
                us.NewsSubscription = Request.Form["UserSettingData.NewsSubscription"] == "on";
                us.MarketingSubscription = Request.Form["UserSettingData.MarketingSubscription"] == "on";
                if (DateTime.TryParse(Request.Form["UserSettingData.LastChangeNameTime"], out var lastChange))
                    us.LastChangeNameTime = lastChange;
                if (DateTime.TryParse(Request.Form["UserSettingData.NextChangeNameTime"], out var nextChange))
                    us.NextChangeNameTime = nextChange;
            }

            // 用户状态数据
            var status = user.UserStatusData;
            if (status != null)
            {
                status.IsAdmin = Request.Form["IsAdmin"] == "true";
                status.IsBanned = Request.Form["UserStatusData.IsBanned"] == "on";
                status.IsAppLogin = Request.Form["UserStatusData.IsAppLogin"] == "on";
                status.IsWebLogin = Request.Form["UserStatusData.IsWebLogin"] == "on";
                status.AppToken = Request.Form["UserStatusData.AppToken"].FirstOrDefault() ?? status.AppToken;
            }

            // 更新用户数据，涉及数据库操作
            var result = await UserManager.UpdateUserAsync(user);
            if (!result)
            {
                ModelState.AddModelError(string.Empty, "用户信息保存失败");
                return Page();
            }
            // 保存成功后重定向到管理视图
            return Redirect("/Account/Profile?view=management");
        }

        /// <summary>
        /// 用户登出操作，清理 Session、Cookie 并调用 UserManager 注销方法。
        /// </summary>
        /// <returns>AJAX 返回 JSON，普通请求重定向到登录页</returns>
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostLogout()
        {
            // 获取当前用户，若已登录则执行注销逻辑
            var user = await UserManager.GetCurrentUserAsync(HttpContext);
            if (user != null)
            {
                // 调用 UserManager 注销方法，通常会更新数据库登录状态
                await UserManager.LogoutWebAsync(user.Id);
            }
            // 清空 Session，移除所有会话数据
            HttpContext.Session.Clear();
            // 删除关键 Cookie，防止伪造身份
            Response.Cookies.Delete("UserId");
            Response.Cookies.Delete("UserName");

            // 判断是否为 AJAX 请求，前端异步登出时返回 JSON
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return new JsonResult(new { success = true });
            }
            else
            {
                // 普通请求则重定向到登录页面
                return RedirectToPage("/Account/Login");
            }
        }

                /// <summary>
        /// 正在编辑的用户数据，页面绑定属性。
        /// </summary>
        [BindProperty]
        public UserData? EditingUser { get; set; }

        /// <summary>
        /// 选择用户进行编辑
        /// </summary>
        public async Task<IActionResult> OnPostSelectUserAsync()
        {
            // 获取表单中的用户ID
            var userIdStr = Request.Form["userId"].FirstOrDefault();
            if (!int.TryParse(userIdStr, out int userId))
                // 用户ID无效，直接返回 400 错误
                return BadRequest("用户ID无效");

            // 根据用户ID获取用户对象
            var user = await UserManager.GetUserByIdAsync(userId);
            if (user == null)
                // 用户不存在，返回 404
                return NotFound("用户不存在");

            // 将选中的用户数据赋值给 EditingUser
            EditingUser = user;

            // 返回部分视图，便于前端 AJAX 局部刷新，无需整页跳转
            return Partial("Shared/Managements/_ManagementUserEdit", EditingUser);
        }
    }
}