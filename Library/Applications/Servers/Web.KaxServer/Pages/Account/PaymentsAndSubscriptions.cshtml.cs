using Drx.Sdk.Network.Session;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Web.KaxServer.Models;
using Web.KaxServer.Services;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.IO;

namespace Web.KaxServer.Pages.Account
{
    public class PaymentsAndSubscriptionsModel : PageModel
    {
        private readonly ILogger<PaymentsAndSubscriptionsModel> _logger;
        private readonly SessionManager _sessionManager;
        private readonly StoreService _storeService;
        private readonly ICdkService _cdkService;
        private readonly IUserService _userService;

        public UserSession User { get; private set; }
        public bool IsLoggedIn => User != null;
        public string Username => User?.Username ?? string.Empty;
        public decimal Coins => User?.Coins ?? 0m;
        public UserPermissionType UserPermission => User?.UserPermission ?? UserPermissionType.Normal;

        public List<OwnedItem> OwnedItems { get; set; } = new List<OwnedItem>();
        public List<StoreItem> ManageableItems { get; set; } = new List<StoreItem>();

        public PaymentsAndSubscriptionsModel(
            ILogger<PaymentsAndSubscriptionsModel> logger,
            SessionManager sessionManager,
            StoreService storeService,
            ICdkService cdkService,
            IUserService userService)
        {
            _logger = logger;
            _sessionManager = sessionManager;
            _storeService = storeService;
            _cdkService = cdkService;
            _userService = userService;
        }

        private void LoadUserSession()
        {
            if (User == null)
            {
                User = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            }
        }

        public IActionResult OnGet()
        {
            LoadUserSession();
            if (User == null)
            {
                _logger.LogWarning("Unauthorized access attempt to Payments page without a valid session.");
                return RedirectToPage("/Account/Login");
            }
            
            // Populate user's published assets from the main store items list
            var allItems = _storeService.GetAllItems();
            User.PublishedAssetIds = allItems.Where(i => i.AuthorId == User.UserId).Select(i => i.Id).ToList();

            foreach (var asset in User.OwnedAssets.OrderByDescending(a => a.Value))
            {
                var assetDetails = allItems.FirstOrDefault(i => i.Id == asset.Key);
                if (assetDetails != null)
                {
                    var expiry = asset.Value;
                    var timeRemaining = GetTimeRemaining(expiry);

                    // 使用 assetDetails 中的 DurationDays（如果存在且有效）
                    var initialDurationDays = assetDetails.DurationDays;
                    var progress = (initialDurationDays > 0) 
                        ? CalculateProgress(initialDurationDays, expiry) 
                        : 100.0; // 如果没有设置时长，默认为100%

                    var ownedItem = new OwnedItem
                    {
                        AssetId = asset.Key,
                        Title = assetDetails.Title,
                        ExpiryDate = expiry,
                        TimeRemaining = timeRemaining,
                        ExpiryProgress = progress,
                        ExpiryStatus = GetStatusClass(timeRemaining),
                        McaCode = User.McaCodes.TryGetValue(asset.Key, out var mca) ? mca : "未初始化"
                    };

                    OwnedItems.Add(ownedItem);
                }
            }

            if (User.UserPermission >= UserPermissionType.Developer)
            {
                ManageableItems = allItems.Where(i => i.AuthorId == User.UserId).OrderBy(i => i.Id).ToList();
            }

            return Page();
        }

        public IActionResult OnPostUnsubscribe(int assetId)
        {
            LoadUserSession();
            if (User == null)
            {
                return Forbid();
            }

            if (User.OwnedAssets != null && User.OwnedAssets.ContainsKey(assetId))
            {
                // Remove the asset from user's owned assets
                User.OwnedAssets.Remove(assetId);

                // Update session via SessionManager
                _sessionManager.UpdateSession(User, true);

                return new JsonResult(new { success = true, message = "资产已成功退订" });
            }

            return new JsonResult(new { success = false, message = "退订失败，未找到该资产" });
        }

        public IActionResult OnPostRedeemCdk(string cdk)
        {
            if (string.IsNullOrWhiteSpace(cdk))
            {
                TempData["CdkMessage"] = "兑换码不能为空。";
                TempData["CdkMessageClass"] = "error";
                return RedirectToPage();
            }

            LoadUserSession();
            if (User == null)
            {
                return RedirectToPage("/Account/Login");
            }

            var activatedCdk = _cdkService.ActivateCdk(cdk, User);

            if (activatedCdk != null)
            {
                string message;
                switch (activatedCdk.Type)
                {
                    case CdkType.Asset:
                        var storeItem = _storeService.GetItemById(activatedCdk.AssetId.Value);
                        message = $"兑换成功！您已获得资产：{(storeItem?.Title ?? "未知资产")}";
                        break;
                    case CdkType.Coins:
                        message = $"兑换成功！您已获得 {activatedCdk.CoinAmount.Value:F0} 金币。";
                        break;
                    default:
                        message = "兑换成功！";
                        break;
                }
                TempData["CdkMessage"] = message;
                TempData["CdkMessageClass"] = "success";

                // Save updated user data using the new service
                if (_userService is UserService concreteUserService)
                {
                    var userData = concreteUserService.GetUserDataById(User.UserId);
                    if (userData != null)
                    {
                        // Sync changes from session back to the persistent user data object
                        userData.Coins = User.Coins;
                        userData.OwnedAssets = User.OwnedAssets;
                        concreteUserService.SaveUser(userData);
                    }
                }

                // Update session via SessionManager
                _sessionManager.UpdateSession(User, true);
            }
            else
            {
                TempData["CdkMessage"] = "无效的兑换码或已被使用，请重试。";
                TempData["CdkMessageClass"] = "error";
            }

            return RedirectToPage();
        }

        private string GetTimeRemaining(DateTime expiry)
        {
            var remaining = expiry - DateTime.Now;
            if (remaining.TotalDays > 1)
                return $"剩余 {(int)remaining.TotalDays} 天";
            if (remaining.TotalHours > 1)
                return $"剩余 {(int)remaining.TotalHours} 小时";
            if (remaining.TotalMinutes > 1)
                return $"剩余 {(int)remaining.TotalMinutes} 分钟";
            return "即将过期";
        }

        private double CalculateProgress(int initialDurationDays, DateTime expiry)
        {
            // 如果 initialDurationDays 小于或等于0，则无法计算有意义的进度
            if (initialDurationDays <= 0) return 100.0;

            DateTime startDate = expiry.AddDays(-initialDurationDays);
            var totalDuration = (expiry - startDate).TotalSeconds;
            if (totalDuration <= 0) return 100;

            var elapsed = (DateTime.Now - startDate).TotalSeconds;
            
            // 计算剩余进度的百分比
            var progress = ((totalDuration - elapsed) / totalDuration) * 100;

            return Math.Max(0, Math.Min(100, progress));
        }

        private string GetStatusClass(string timeRemaining)
        {
            if (timeRemaining == "即将过期")
                return "expiring-soon";
            if (timeRemaining == "剩余 1 天")
                return "notice";
            return "healthy";
        }

        public class OwnedItem
        {
            public int AssetId { get; set; }
            public string Title { get; set; }
            public string McaCode { get; set; }
            public DateTime ExpiryDate { get; set; }
            public string TimeRemaining { get; set; }
            public double ExpiryProgress { get; set; }
            public string ExpiryStatus { get; set; }
        }
    }
} 