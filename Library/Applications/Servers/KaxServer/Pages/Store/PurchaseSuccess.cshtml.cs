using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KaxServer.Services;
using KaxServer.Models;

namespace KaxServer.Pages.Store
{
    public class PurchaseSuccessModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string? orderId { get; set; } // 路由绑定：/Store/PurchaseSuccess/{orderId}

        public UserData? CurrentUser { get; private set; }

        // 优先尝试真实订单类型/服务；若无，则使用内部 ViewModel
        public OrderViewModel? Order { get; private set; }

        // 可能的跳转链接（存在商品时回到详情页）
        public string? ProductDetailUrl { get; private set; }
        public string? MyOrdersUrl { get; private set; } = "/"; // 若无“我的订单”路由，降级为首页

        public async Task<IActionResult> OnGetAsync()
        {
            try
            {
                // 当前用户
                CurrentUser = await UserManager.GetCurrentUserAsync(HttpContext);

                if (string.IsNullOrWhiteSpace(orderId))
                {
                    // 没有订单ID也能渲染成功状态页，但信息显示为“-”
                    Order = BuildFallback("-",
                        productName: null,
                        amount: null,
                        currency: null,
                        description: null,
                        status: "已支付/完成",
                        deliveryUrl: null,
                        purchaseTime: DateTime.Now);
                    return Page();
                }

                // TODO：接入实际 StoreManager 查询逻辑
                // 没有找到订单实体/服务（仅有 StoreItem 与购买流程），这里做最小可用展示。
                // 若未来有 Order/Payment 表与 GetOrderById 等方法，请在此处替换并映射到 OrderViewModel。
                // 同时建议使用 orderId 与用户购买记录进行校验/回查。

                // 简易策略：如果 orderId 可被解析为商品ID，则从 Store 中回填商品信息
                OrderViewModel? mapped = null;
                if (int.TryParse(orderId, out var itemId))
                {
                    var item = await StoreManager.GetStoreItemByIdAsync(itemId);
                    if (item != null)
                    {
                        ProductDetailUrl = Url.Page("/Store/Detail", new { id = item.Id });
                        mapped = new OrderViewModel
                        {
                            OrderId = orderId,
                            ProductName = item.Title,
                            ProductDescription = item.Description,
                            Amount = null, // 实付金额无法从现有API直接获得，留空降级为“-”
                            Currency = null,
                            PurchaseTime = DateTime.Now,
                            Status = "已支付/完成",
                            DeliveryUrl = null // 如未来 StoreItem/订单增加交付地址，可回填
                        };
                    }
                }

                Order ??= mapped ?? BuildFallback(orderId,
                    productName: null,
                    amount: null,
                    currency: null,
                    description: null,
                    status: "已支付/完成",
                    deliveryUrl: null,
                    purchaseTime: DateTime.Now);

                return Page();
            }
            catch
            {
                // 任何异常都保证页面可渲染
                Order ??= BuildFallback(orderId ?? "-", null, null, null, null, "已支付/完成", null, DateTime.Now);
                return Page();
            }
        }

        // 统一的优雅降级
        private static OrderViewModel BuildFallback(
            string orderId,
            string? productName,
            decimal? amount,
            string? currency,
            string? description,
            string? status,
            string? deliveryUrl,
            DateTime? purchaseTime)
        {
            return new OrderViewModel
            {
                OrderId = string.IsNullOrWhiteSpace(orderId) ? "-" : orderId,
                ProductName = productName,
                Amount = amount,
                Currency = currency,
                ProductDescription = description,
                PurchaseTime = purchaseTime,
                Status = status,
                DeliveryUrl = deliveryUrl
            };
        }

        // 页面帮助：将 null 或空白转换为 “-”
        public string DisplayOrDash(string? text)
        {
            return string.IsNullOrWhiteSpace(text) ? "-" : text!;
        }

        public class OrderViewModel
        {
            public string? OrderId { get; set; }
            public string? ProductName { get; set; }
            public decimal? Amount { get; set; }
            public string? Currency { get; set; }
            public string? ProductDescription { get; set; }
            public DateTime? PurchaseTime { get; set; }
            public string? Status { get; set; }
            public string? DeliveryUrl { get; set; }
        }
    }
}