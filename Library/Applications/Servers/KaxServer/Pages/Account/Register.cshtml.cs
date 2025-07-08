using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KaxServer.Services;
using KaxServer.Models;

namespace KaxServer.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly EmailService _emailService;
        private readonly EmailVerificationCode _verificationCode;

        public RegisterModel(
            EmailService emailService,
            EmailVerificationCode verificationCode)
        {
            _emailService = emailService;
            _verificationCode = verificationCode;
        }

        public UserData CurrentUser { get; set; }

        public async Task OnGet()
        {
            CurrentUser = await UserManager.GetCurrentUserAsync(HttpContext);

            if (CurrentUser != null)
            {
                Response.Redirect("/");
                return;
            }
        }

        public async Task<IActionResult> OnPostRegister(string userName, string password, string email, string verificationCode)
        {
            var result = await UserManager.Register(userName, password, email, verificationCode, _verificationCode);
            if (result.Success)
            {
                // 在Session中存储用户信息
                HttpContext.Session.SetInt32("UserId", result.User.Id);
                HttpContext.Session.SetString("UserName", result.User.UserName);
                
                return RedirectToPage("RegisterSuccess");
            }
            else
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return Page();
            }
        }

        public async Task<IActionResult> OnPostSendVerificationCodeAsync([FromBody] SendVerificationCodeRequest request)
        {
            if (string.IsNullOrEmpty(request.Email))
            {
                return new JsonResult(new { success = false, message = "邮箱地址不能为空" });
            }

            if (!_verificationCode.CanSendCode(request.Email))
            {
                return new JsonResult(new { success = false, message = "请等待60秒后再次发送验证码" });
            }

            if (_emailService.SendVerificationEmail(request.Email))
            {
                return new JsonResult(new { success = true, message = "验证码已发送，请查收邮件" });
            }

            return new JsonResult(new { success = false, message = "验证码发送失败，请稍后重试" });
        }
    }

    public class SendVerificationCodeRequest
    {
        public string Email { get; set; }
    }
}
