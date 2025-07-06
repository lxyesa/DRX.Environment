using Drx.Sdk.Network.Session;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Xml;
using Web.KaxServer.Models;
using Web.KaxServer.Services;
using System.Linq;
using System.Xml.Linq;
using Drx.Sdk.Network.DataBase;

namespace Web.KaxServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ShopController : ControllerBase
    {
        private readonly SessionManager _sessionManager;
        private readonly StoreService _storeService;
        private readonly MessageBoxService _messageBoxService;

        public ShopController(SessionManager sessionManager, StoreService storeService, MessageBoxService messageBoxService)
        {
            _sessionManager = sessionManager;
            _storeService = storeService;
            _messageBoxService = messageBoxService;
        }

        [HttpPost("buy/{itemId}/{days}")]
        [ValidateAntiForgeryToken]
        public IActionResult Buy(int itemId, int days)
        {
            var userSession = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (userSession == null)
            {
                return Unauthorized(new { message = "请先登录后再进行操作。" });
            }

            var item = _storeService.GetItemById(itemId);
            if (item == null)
            {
                return NotFound(new { message = "您要购买的商品不存在。" });
            }

            var validDurations = new[] { 1, 7, 15, 30, 60, 180, 360 };
            if (!validDurations.Contains(days))
            {
                return BadRequest(new { message = "无效的购买时长。" });
            }

            if (userSession.HasValidAsset(itemId))
            {
                return BadRequest(new { message = "您已拥有此商品，无需重复购买。" });
            }

            var cost = (item.MonthlyPrice / 30m) * days;

            if (userSession.Coins < cost)
            {
                return BadRequest(new { message = "您的金币余额不足。" });
            }

            userSession.Coins -= cost;
            var expiryDate = DateTime.Now.AddDays((double)days);
            userSession.OwnedAssets[itemId] = expiryDate;

            try
            {
                var userDataIndexPath = Path.Combine(Directory.GetCurrentDirectory(), "user_data");
                var userDataRepository = new IndexedRepository<UserData>(userDataIndexPath, "user_");
                var userDataToUpdate = userDataRepository.Get(userSession.UserId.ToString());

                if (userDataToUpdate == null)
                {
                    return StatusCode(500, new { message = "未找到您的用户记录。" });
                }

                userDataToUpdate.Coins = userSession.Coins;
                userDataToUpdate.OwnedAssets = userSession.OwnedAssets;
                userDataRepository.Save(userDataToUpdate);
            }
            catch (System.Exception)
            {
                return StatusCode(500, new { message = "更新用户数据时发生内部错误，请稍后重试。" });
            }

            // 更新商品购买数量
            try
            {
                string shopItemsXmlPath = Path.Combine(Directory.GetCurrentDirectory(), "shop", "shopitems.xml");
                if (System.IO.File.Exists(shopItemsXmlPath))
                {
                    var shopDoc = XDocument.Load(shopItemsXmlPath);
                    var itemElement = shopDoc.Descendants("Item")
                                             .FirstOrDefault(el => (int)el.Element("Id") == itemId);

                    if (itemElement != null)
                    {
                        var purchaseCountElement = itemElement.Element("PurchaseCount");
                        if (purchaseCountElement != null)
                        {
                            purchaseCountElement.Value = (int.Parse(purchaseCountElement.Value) + 1).ToString();
                        }
                        else
                        {
                            itemElement.Add(new XElement("PurchaseCount", "1"));
                        }
                        shopDoc.Save(shopItemsXmlPath);
                    }
                }
            }
            catch (System.Exception)
            {
                // 购买量更新失败不应影响购买流程，只记录错误
            }

            _messageBoxService.Inject("购买成功", $"商品已成功添加到您的账户，有效期{days}天。");

            return Ok();
        }

        [HttpGet("assets/{userToken}")]
        public IActionResult GetUserAssets(string userToken)
        {
            var userSession = _sessionManager.GetAllSessions()
                .OfType<UserSession>()
                .FirstOrDefault(s => s.UserToken == userToken && !s.IsExpired());

            if (userSession == null)
            {
                return Unauthorized(new { message = "用户Token无效或会话已过期。" });
            }

            return Ok(userSession.OwnedAssets);
        }

        [HttpGet("assets/valid/{userToken}")]
        public IActionResult GetValidUserAssets(string userToken, [FromQuery] int id)
        {
            var userSession = _sessionManager.GetAllSessions()
                .OfType<UserSession>()
                .FirstOrDefault(s => s.UserToken == userToken && !s.IsExpired());

            if (userSession == null)
            {
                return Unauthorized(new { message = "用户Token无效或会话已过期。" });
            }

            var isValid = userSession.HasValidAsset(id);

            return Ok(new { AssetId = id, IsValid = isValid });
        }

        [HttpPost("buy/custom")]
        public IActionResult BuyCustom([FromBody] CustomPurchaseRequest request)
        {
            var userSession = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (userSession == null)
            {
                return Unauthorized(new { message = "请先登录后再进行操作。" });
            }

            var item = _storeService.GetItemById(request.ItemId);
            if (item == null)
            {
                return NotFound(new { message = "您要购买的商品不存在。" });
            }

            if (userSession.HasValidAsset(request.ItemId))
            {
                return BadRequest(new { message = "您已拥有此商品，无需重复购买。" });
            }

            decimal days;
            switch (request.Unit.ToLower())
            {
                case "minutes":
                    days = request.Duration / (60m * 24m);
                    break;
                case "hours":
                    days = request.Duration / 24m;
                    break;
                case "days":
                    days = request.Duration;
                    break;
                case "weeks":
                    days = request.Duration * 7m;
                    break;
                case "months":
                    days = request.Duration * 30m; // Using 30 days as a standard month
                    break;
                default:
                    return BadRequest(new { message = "无效的时间单位。" });
            }

            if (days <= 0)
            {
                return BadRequest(new { message = "无效的购买时长。" });
            }

            var cost = (item.MonthlyPrice / 30m) * days;

            if (userSession.Coins < cost)
            {
                return BadRequest(new { message = "您的金币余额不足。" });
            }

            userSession.Coins -= cost;
            var expiryDate = DateTime.Now.AddDays((double)days);
            userSession.OwnedAssets[request.ItemId] = expiryDate;

            try
            {
                var userDataRepository = new IndexedRepository<UserData>(Path.Combine(Directory.GetCurrentDirectory(), "user_data"), "user_");
                var userDataToUpdate = userDataRepository.Get(userSession.UserId.ToString());

                if (userDataToUpdate == null)
                {
                    return StatusCode(500, new { message = $"未找到您的用户记录或用户数据文件丢失, 用户ID: {userSession.UserId}" });
                }

                userDataToUpdate.Coins = userSession.Coins;
                userDataToUpdate.OwnedAssets = userSession.OwnedAssets;
                userDataRepository.Save(userDataToUpdate);
            }
            catch (System.Exception ex)
            {
                return StatusCode(500, new { message = $"更新用户数据时发生内部错误，请稍后重试。{ex.Message}" });
            }

            // 更新商品购买数量
            try
            {
                string shopItemsXmlPath = Path.Combine(Directory.GetCurrentDirectory(), "shop", "shopitems.xml");
                if (System.IO.File.Exists(shopItemsXmlPath))
                {
                    var shopDoc = XDocument.Load(shopItemsXmlPath);
                    var itemElement = shopDoc.Descendants("Item")
                                             .FirstOrDefault(el => (int)el.Element("Id") == request.ItemId);

                    if (itemElement != null)
                    {
                        var purchaseCountElement = itemElement.Element("PurchaseCount");
                        if (purchaseCountElement != null)
                        {
                            purchaseCountElement.Value = (int.Parse(purchaseCountElement.Value) + 1).ToString();
                        }
                        else
                        {
                            itemElement.Add(new XElement("PurchaseCount", "1"));
                        }
                        shopDoc.Save(shopItemsXmlPath);
                    }
                }
            }
            catch (System.Exception)
            {
                // 购买量更新失败不应影响购买流程，只记录错误
            }

            _messageBoxService.Inject("购买成功", $"商品已成功添加到您的账户。");

            return Ok();
        }

        [HttpGet("assets/version/{assetId}")]
        public IActionResult GetAssetVersion(int assetId)
        {
            var item = _storeService.GetItemById(assetId);
            if (item == null)
            {
                return NotFound(new { message = "您要购买的商品不存在。" });
            }

            var version = item.Version;
            return Ok(new { version });
        }

        [HttpPost("assets/version/{assetId}/set/{version}")]
        public IActionResult SetAssetVersion(int assetId, string version)
        {
            // 1. 验证用户是否为管理员
            var userSession = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (userSession == null)
            {
                return Unauthorized(new { message = "请先登录后再进行操作。" });
            }

            // 仅允许管理员及更高权限的用户操作
            if (userSession.UserPermission < Models.UserPermissionType.Admin)
            {
                return Forbid(); // 403 Forbidden - 用户已认证但无权限
            }

            // 2. 调用服务更新资产版本
            var result = _storeService.UpdateItemVersion(assetId, version);

            // 3. 根据服务层返回的结果，向客户端响应不同的状态
            return result switch
            {
                // 更新成功
                StoreService.UpdateResult.Success => Ok(new { message = "资产版本更新成功。" }),
                // 资产不存在
                StoreService.UpdateResult.NotFound => NotFound(new { message = "资产不存在。" }),
                // 版本未变化
                StoreService.UpdateResult.NoChange => BadRequest(new { message = "新版本与当前版本相同，无需更新。" }),
                // 发生内部错误
                StoreService.UpdateResult.Error or _ => StatusCode(500, new { message = "更新资产版本时发生内部错误。" }),
            };
        }
    }

    public class CustomPurchaseRequest
    {
        public int ItemId { get; set; }
        public int Duration { get; set; }
        public string Unit { get; set; }
    }
} 