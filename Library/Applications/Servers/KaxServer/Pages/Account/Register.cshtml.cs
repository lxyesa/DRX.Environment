using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KaxServer.Services;
using KaxServer.Models;
using DRX.Framework;

namespace KaxServer.Pages.Account
{
    /// <summary>
    /// 用户注册页面模型
    /// </summary>
    public class RegisterModel : PageModel
    {
        private readonly EmailService _emailService;
        private readonly EmailVerificationCode _verificationCode;

        // UserManager 为静态类，无法依赖注入，只能静态调用

        public RegisterModel(
            EmailService emailService,
            EmailVerificationCode verificationCode)
        {
            _emailService = emailService;
            _verificationCode = verificationCode;
        }

        /// <summary>
        /// 当前用户信息
        /// </summary>
        public UserData? CurrentUser { get; private set; }

        /// <summary>
        /// GET: 注册页，已登录用户自动跳转首页
        /// </summary>
        public async Task<IActionResult> OnGetAsync()
        {
            CurrentUser = await UserManager.GetCurrentUserAsync(HttpContext);

            if (CurrentUser != null)
            {
                // 避免直接 Response.Redirect，统一用 IActionResult
                return RedirectToPage("/Index");
            }
            return Page();
        }

        /// <summary>
        /// POST: 用户注册
        /// </summary>
        public async Task<IActionResult> OnPostRegisterAsync(
            [FromForm][Required][StringLength(32, MinimumLength = 2)] string userName,
            [FromForm][Required][StringLength(64, MinimumLength = 6)] string password,
            [FromForm][Required][EmailAddress] string email,
            [FromForm][Required][StringLength(8, MinimumLength = 8)] string verificationCode)
        {
            Logger.Info($"表单已提交，准备注册用户：{userName} ({email})");
            // 基础参数校验
            if (!ModelState.IsValid)
            {
                ModelState.AddModelError(string.Empty, "输入信息格式有误，请检查后重试。");
                return Page();
            }

            // 基础格式校验已移至前端，后端仅保留最终验证
            // 前端已验证：用户名格式、密码强度、邮箱格式

            var result = await UserManager.Register(userName, password, email, verificationCode, _verificationCode);
            if (result.Success)
            {
                Logger.Info($"用户注册成功，自动登录：{userName} ({email})");
                SetUserSession(result.User);
                return RedirectToPage("RegisterSuccess");
            }
            else
            {
                ModelState.AddModelError(string.Empty, result.Message);
                return Page();
            }
        }

        /// <summary>
        /// POST: 发送邮箱验证码（异步）
        /// </summary>
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OnPostSendVerificationCodeAsync([FromBody] SendVerificationCodeRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Email))
            {
                return new JsonResult(new { success = false, message = "邮箱地址不能为空" });
            }
            if (!IsValidEmail(request.Email))
            {
                return new JsonResult(new { success = false, message = "邮箱格式不正确" });
            }
            if (!_verificationCode.CanSendCode(request.Email))
            {
                return new JsonResult(new { success = false, message = "请等待60秒后再次发送验证码" });
            }

            // 邮件发送建议异步（底层如为同步可用 Task.Run 包裹）
            var sendResult = await Task.Run(() => _emailService.SendVerificationEmail(request.Email));
            if (sendResult)
            {
                return new JsonResult(new { success = true, message = "验证码已发送，请查收邮件" });
            }
            return new JsonResult(new { success = false, message = "验证码发送失败，请稍后重试" });
        }

        #region 私有方法与校验

        /// <summary>
        /// 设置用户Session
        /// </summary>
        private void SetUserSession(UserData user)
        {
            HttpContext.Session.SetInt32("UserId", user.Id);
            HttpContext.Session.SetString("Username", user.Username);
        }

        /// <summary>
        /// 用户名校验：仅允许中英文、数字、下划线
        /// </summary>
        private bool IsValidUserName(string userName)
        {
            return Regex.IsMatch(userName, @"^[\u4e00-\u9fa5_a-zA-Z0-9]{2,32}$");
        }

        /// <summary>
        /// 密码强度校验：6-64位，包含大小写字母和数字
        /// </summary>
        private bool IsStrongPassword(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 6 || password.Length > 64)
                return false;
            bool hasUpper = password.Any(char.IsUpper);
            bool hasLower = password.Any(char.IsLower);
            bool hasDigit = password.Any(char.IsDigit);
            return hasUpper && hasLower && hasDigit;
        }

        /// <summary>
        /// 邮箱格式校验
        /// </summary>
        private bool IsValidEmail(string email)
        {
            return new EmailAddressAttribute().IsValid(email);
        }

        #endregion
    }

    /// <summary>
    /// 邮箱验证码请求体
    /// </summary>
    public class SendVerificationCodeRequest
    {
        [Required]
        [EmailAddress]
        public string? Email { get; set; }
    }
}
