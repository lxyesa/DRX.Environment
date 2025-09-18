using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KS.Pages
{
    /// <summary>
    /// 登录页面的 PageModel（骨架）。
    /// 这里只提供基本的属性与空的提交处理器，实际验证/认证逻辑请在后端实现。
    /// </summary>
    public class LoginModel : PageModel
    {
        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public void OnGet()
        {
        }

        public IActionResult OnPost()
        {
            if (!ModelState.IsValid)
            {
                return Page();
            }

            // TODO: 在此添加登录验证逻辑（验证用户名/密码、签发 cookie 等）
            // 示例：如果认证成功，重定向到首页或返回到指定的 ReturnUrl

            // 临时代码：验证通过（请替换为真实实现）
            bool loginSuccess = false; // TODO: replace with real auth

            if (loginSuccess)
            {
                return RedirectToPage("/Main");
            }

            ModelState.AddModelError(string.Empty, "登录失败：用户名或密码不正确。");
            return Page();
        }

        public class InputModel
        {
            [Required]
            [Display(Name = "邮箱或用户名")]
            public string? Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "密码")]
            public string? Password { get; set; }

            [Display(Name = "记住我")]
            public bool RememberMe { get; set; }
        }
    }
}
