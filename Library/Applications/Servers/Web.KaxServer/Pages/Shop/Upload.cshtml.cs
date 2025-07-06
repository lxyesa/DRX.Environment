using Drx.Sdk.Network.Session;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Web.KaxServer.Models;
using Web.KaxServer.Services;

namespace Web.KaxServer.Pages.Shop
{
    public class UploadModel : PageModel
    {
        private readonly SessionManager _sessionManager;
        private readonly StoreService _storeService;
        private readonly IWebHostEnvironment _env;

        [BindProperty]
        public StoreItem Item { get; set; } = new();

        public UploadModel(SessionManager sessionManager, StoreService storeService, IWebHostEnvironment env)
        {
            _sessionManager = sessionManager;
            _storeService = storeService;
            _env = env;
        }

        public IActionResult OnGet()
        {
            var userSession = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (userSession == null || userSession.UserPermission != UserPermissionType.Developer)
            {
                // 如果用户未登录或不是开发者，则重定向到商店主页
                return RedirectToPage("/Shop/Store");
            }

            return Page();
        }

        public IActionResult OnPost()
        {
            var userSession = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (userSession == null || userSession.UserPermission != UserPermissionType.Developer)
            {
                return Forbid();
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // 设置作者ID
            Item.AuthorId = userSession.UserId;

            // 创建商品
            var newItem = _storeService.CreateItem(Item);
            
            // 重定向到新商品的详情页
            return RedirectToPage("/Shop/Item", new { id = newItem.Id });
        }
    }
} 