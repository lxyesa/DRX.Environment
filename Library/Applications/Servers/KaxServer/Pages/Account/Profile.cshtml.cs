using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KaxServer.Models;
using KaxServer.Services;
using System.Threading.Tasks;
using DRX.Framework;

namespace KaxServer.Pages.Account
{
    public class ProfileModel : PageModel
    {
        [BindProperty]
        public UserData CurrentUser { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            // 获取当前用户数据
            CurrentUser = await UserManager.GetCurrentUserAsync(HttpContext);

            // 如果用户未登录，重定向到登录页面
            if (CurrentUser == null)
            {
                return RedirectToPage("/Account/Login");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostSaveUserDataAsync()
        {
            CurrentUser = await UserManager.GetCurrentUserAsync(HttpContext);
            if (CurrentUser == null)
            {
                return RedirectToPage("/Account/Login");
            }
            
            var usernameValue = Request.Form["username"].FirstOrDefault();
            if (!string.IsNullOrEmpty(usernameValue))
            {
                // 对比用户名
                if (usernameValue != CurrentUser.UserName)
                {
                    CurrentUser.UserName = usernameValue;
                    CurrentUser.UserSettingData.LastChangeNameTime = DateTime.UtcNow;
                    CurrentUser.UserSettingData.NextChangeNameTime = DateTime.UtcNow.AddDays(30);

                    // 更新Session中的用户名
                    HttpContext.Session.SetString("UserName", CurrentUser.UserName);
                }
            }
            CurrentUser.Email = Request.Form["email"].FirstOrDefault() ?? string.Empty;

            // 订阅与咨询相关字段
            if (Request.Form.ContainsKey("newsSubscription"))
                CurrentUser.UserSettingData.NewsSubscription = Request.Form["newsSubscription"] == "on";
            else
                CurrentUser.UserSettingData.NewsSubscription = false;

            if (Request.Form.ContainsKey("marketingSubscription"))
                CurrentUser.UserSettingData.MarketingSubscription = Request.Form["marketingSubscription"] == "on";
            else
                CurrentUser.UserSettingData.MarketingSubscription = false;

            // 保存用户数据
            var result = await UserManager.UpdateUserAsync(CurrentUser);

            if (!result)
            {
                ModelState.AddModelError(string.Empty, "更新用户数据失败，请稍后重试");
                return Page();
            }

            return RedirectToPage("/Account/Profile");
        }
    }
}