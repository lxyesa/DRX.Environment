using Drx.Sdk.Network.Session;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Linq;
using Web.KaxServer.Models;
using Web.KaxServer.Services;
using Markdig;
using Microsoft.AspNetCore.Hosting;

namespace Web.KaxServer.Pages.Shop
{
    public class ItemModel : PageModel
    {
        private readonly SessionManager _sessionManager;
        private readonly StoreService _storeService;
        private readonly IWebHostEnvironment _env;

        public bool IsLoggedIn { get; private set; }
        public string Username { get; private set; } = string.Empty;
        
        public StoreItem? Item { get; private set; }
        public List<Review> Reviews { get; private set; } = new List<Review>();

        public bool OwnsItem { get; private set; }
        public decimal UserCoins { get; private set; }
        public string DescriptionAsHtml { get; private set; }

        public ItemModel(SessionManager sessionManager, StoreService storeService, IWebHostEnvironment env)
        {
            _sessionManager = sessionManager;
            _storeService = storeService;
            _env = env;
        }

        public IActionResult OnGet(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var userSession = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (userSession != null)
            {
                IsLoggedIn = true;
                Username = userSession.Username;
                UserCoins = userSession.Coins;
            }
            else
            {
                IsLoggedIn = false;
            }

            // 使用StoreService获取商品，以便正确获取作者名称
            Item = _storeService.GetItemById(id.Value);

            if (Item == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(Item.Description))
            {
                var dedentedMarkdown = Dedent(Item.Description);
                
                // 移除多余的空行：将连续的两个或更多换行符替换为单个换行符
                var processedText = System.Text.RegularExpressions.Regex.Replace(
                    dedentedMarkdown, 
                    @"(\r?\n){2,}", 
                    "\n\n"
                );
                
                var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
                DescriptionAsHtml = Markdown.ToHtml(processedText, pipeline);
            }
            else
            {
                DescriptionAsHtml = string.Empty;
            }

            if (userSession != null)
            {
                OwnsItem = userSession.HasValidAsset(Item.Id);
            }

            Reviews = _storeService.GetReviewsByItemId(id.Value);

            return Page();
        }

        public IActionResult OnPostAddReview(int itemId, int rating, string comment)
        {
            var userSession = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (userSession == null)
            {
                return Unauthorized(); // Or redirect to login
            }
            
            // Check if user owns the item
            if (!userSession.HasValidAsset(itemId))
            {
                return new ForbidResult(); // Returns a 403 Forbidden status
            }

            if (string.IsNullOrWhiteSpace(comment))
            {
                // Optionally, add a model error and redisplay the page
                return new BadRequestObjectResult(new { message = "评论内容不能为空。" });
            }

                 var review = new Review
                {
                    ItemId = itemId,
                    ReviewerName = userSession.Username,
                    ReviewerPermission = userSession.UserPermission,
                    Rating = rating,
                    Comment = comment,
                    Date = DateTime.Now
                };

                _storeService.AddReview(review);

            return new JsonResult(new
            {
                reviewerName = review.ReviewerName,
                reviewerPermission = review.ReviewerPermission.ToString(),
                rating = review.Rating,
                comment = review.Comment,
                date = review.Date.ToString("yyyy-MM-dd")
            });
        }

        private string Dedent(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;

            var lines = text.Replace("\r\n", "\n").Split('\n').ToList();

            if (lines.Count > 0 && string.IsNullOrWhiteSpace(lines.First()))
            {
                lines.RemoveAt(0);
            }
            if (lines.Count > 0 && string.IsNullOrWhiteSpace(lines.Last()))
            {
                lines.RemoveAt(lines.Count - 1);
            }
            if (!lines.Any()) return string.Empty;
            
            int? minIndent = lines
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => (int?)line.TakeWhile(char.IsWhiteSpace).Count())
                .Min();

            if (minIndent.HasValue && minIndent > 0)
            {
                return string.Join("\n", lines.Select(line => string.IsNullOrWhiteSpace(line) ? string.Empty : line.Substring(minIndent.Value)));
            }

            return string.Join("\n", lines);
        }

        // 提供给Razor页面使用的公共方法
        public string DedentText(string text)
        {
            return Dedent(text);
        }
    }
}