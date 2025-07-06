using Drx.Sdk.Network.Session;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading.Tasks;
using Web.KaxServer.Models;
using Web.KaxServer.Services;

namespace Web.KaxServer.Pages.Account
{
    public class PersonalInfoModel : PageModel
    {
        private readonly SessionManager _sessionManager;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly ILogger<PersonalInfoModel> _logger;
        private readonly IUserService _userService;

        public UserSession CurrentUser { get; private set; }
        public bool IsLoggedIn => CurrentUser != null;
        public UserPermissionType UserPermission => CurrentUser?.UserPermission ?? UserPermissionType.Normal;
        public string AvatarUrl => CurrentUser?.AvatarUrl ?? string.Empty;
        public string Username => CurrentUser?.Username ?? string.Empty;
        
        [TempData]
        public string SuccessMessage { get; set; }
        
        [TempData]
        public string ErrorMessage { get; set; }

        public PersonalInfoModel(SessionManager sessionManager, IWebHostEnvironment hostingEnvironment, ILogger<PersonalInfoModel> logger, IUserService userService)
        {
            _sessionManager = sessionManager;
            _hostingEnvironment = hostingEnvironment;
            _logger = logger;
            _userService = userService;
        }

        private void LoadUserSession()
        {
            if (CurrentUser == null)
            {
                CurrentUser = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            }
        }

        public IActionResult OnGet()
        {
            LoadUserSession();
            if (!IsLoggedIn)
            {
                return RedirectToPage("/Account/Login", new { returnUrl = "/Account/PersonalInfo" });
            }
            return Page();
        }

        public async Task<IActionResult> OnPostUploadAvatarAsync(IFormFile avatarFile)
        {
            LoadUserSession();
            if (!IsLoggedIn)
            {
                return Forbid();
            }

            if (avatarFile == null || avatarFile.Length == 0)
            {
                ErrorMessage = "请选择要上传的文件。";
                return RedirectToPage();
            }

            if (avatarFile.Length > 2 * 1024 * 1024) // 2MB limit
            {
                ErrorMessage = "文件大小不能超过 2MB。";
                return RedirectToPage();
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
            var extension = Path.GetExtension(avatarFile.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !allowedExtensions.Contains(extension))
            {
                ErrorMessage = "只允许上传 JPG, PNG, GIF 格式的图片。";
                return RedirectToPage();
            }

            try
            {
                var uploadsFolder = Path.Combine(AppContext.BaseDirectory, "wwwroot", "uploads", "avatars");
                Directory.CreateDirectory(uploadsFolder);
                
                var uniqueFileName = $"{CurrentUser.UserId}{extension}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await avatarFile.CopyToAsync(fileStream);
                }

                CurrentUser.AvatarUrl = $"/uploads/avatars/{uniqueFileName}?v={DateTime.UtcNow.Ticks}";
                
                // Persist the change using the new UserService
                if (_userService is UserService concreteUserService)
                {
                    var userData = concreteUserService.GetUserDataById(CurrentUser.UserId);
                    if (userData != null)
                    {
                        userData.AvatarUrl = CurrentUser.AvatarUrl;
                        concreteUserService.SaveUser(userData);
                    }
                }

                _sessionManager.UpdateSession(CurrentUser, true);

                SuccessMessage = "头像更新成功！";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Avatar upload failed for user {Username}", CurrentUser.Username);
                ErrorMessage = "上传失败，请稍后重试。";
            }

            return RedirectToPage();
        }
    }
} 