using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KaxServer.Services;
using KaxServer.Models;
using Microsoft.AspNetCore.Http;

namespace KaxServer.Pages.Account
{
    public class LoginModel : PageModel
    {
        [BindProperty]
        public InputModel Input { get; set; }

        public string? ReturnUrl { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public UserData? CurrentUser { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "请输入用户名或电子邮箱。")]
            public string? Username { get; set; }

            [Required(ErrorMessage = "请输入密码。")]
            [DataType(DataType.Password)]
            public string? Password { get; set; }

            [Display(Name = "记住我?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGet(string? returnUrl = null)
        {
            CurrentUser = await UserManager.GetCurrentUserAsync(HttpContext);

            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            if (CurrentUser != null)
            {
                Response.Redirect("/");
                return;
            }

            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                if (Input == null || Input.Username == null || Input.Password == null)
                {
                    ModelState.AddModelError(string.Empty, "用户名和密码不能为空。");
                    return Page();
                }
                var result = await UserManager.LoginWebAsync(Input.Username, Input.Password);

                if (result.Success)
                {
                    // 检查账号是否被封禁
                    if (result.User?.UserStatusData?.IsBanned == true)
                    {
                        ModelState.AddModelError(string.Empty, "账号已被封禁");
                        return Page();
                    }
                
                    // 在Session中存储用户信息
                    var options = new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Lax
                    };
                
                    if (Input.RememberMe)
                    {
                        options.MaxAge = TimeSpan.FromDays(7); // 设置Cookie过期时间为7天
                        HttpContext.Session.SetString("RememberMe", "true");
                    }
                
                    // 设置持久化Cookie
                    HttpContext.Response.Cookies.Append("UserId", result.User.Id.ToString(), options);
                    HttpContext.Response.Cookies.Append("Username", result.User.Username, options);
                
                    // 同时也存储在Session中
                    HttpContext.Session.SetInt32("UserId", result.User.Id);
                    HttpContext.Session.SetString("Username", result.User.Username);
                
                    // 将returnUrl保存到TempData中，以便在LoginSuccess页面使用
                    TempData["ReturnUrl"] = returnUrl;
                    
                    // 重定向到登录成功页面
                    return RedirectToPage("./LoginSuccess");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, result.Message);
                    return Page();
                }
            }

            // 如果模型状态无效，则返回页面以显示验证错误
            return Page();
        }
    }
} 