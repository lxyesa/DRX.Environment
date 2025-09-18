using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KS.Pages
{
    /// <summary>
    /// 注册页面 PageModel（骨架）。
    /// 仅包含绑定属性与空的提交处理方法。
    /// </summary>
    public class RegisterModel : PageModel
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

            // TODO: 在此添加创建用户逻辑（保存用户、发送验证邮件等）
            bool createSuccess = false; // TODO: replace with real creation

            if (createSuccess)
            {
                // 注册成功后重定向到登录页或作者首页
                return RedirectToPage("/Login");
            }

            ModelState.AddModelError(string.Empty, "注册失败：请检查输入后重试。");
            return Page();
        }

        public class InputModel
        {
            [Required]
            [Display(Name = "用户名")]
            public string? UserName { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "邮箱")]
            public string? Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "密码")]
            public string? Password { get; set; }

            [Required]
            [DataType(DataType.Password)]
            [Display(Name = "确认密码")]
            [Compare("Password", ErrorMessage = "两次输入的密码不匹配。")]
            public string? ConfirmPassword { get; set; }
        }
    }
}
