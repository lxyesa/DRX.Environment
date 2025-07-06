using Drx.Sdk.Text.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Web.KaxServer.Models;
using Drx.Sdk.Network.Session;
using Web.KaxServer.Services;

namespace Web.KaxServer.Pages.Forums
{
    public class ViewCategoryModel : PageModel
    {
        private readonly ILogger<ViewCategoryModel> _logger;
        private readonly SessionManager _sessionManager;
        private readonly ForumDataHelper _forumDataHelper;
        public ForumCategoryModel CurrentCategory { get; set; }
        public List<ThreadViewModel> Threads { get; set; } = new();
        public UserSession CurrentUserSession { get; private set; }
        public bool IsLoggedIn => CurrentUserSession != null;
        public string Username => CurrentUserSession?.Username ?? string.Empty;
        public UserPermissionType UserPermission => CurrentUserSession?.UserPermission ?? UserPermissionType.Normal;

        public class ThreadViewModel
        {
            public string Id { get; set; }
            public string Title { get; set; }
            public string AuthorName { get; set; }
            public int ReplyCount { get; set; }
            public int ViewCount { get; set; }
            public string LastPostAuthorName { get; set; }
            public string LastPostTimeAgo { get; set; }
        }

        // 应为程序执行目录
        private string ForumDataPath = Path.Combine(AppContext.BaseDirectory, "data", "forum.xml");

        public ViewCategoryModel(ILogger<ViewCategoryModel> logger, SessionManager sessionManager, ForumDataHelper forumDataHelper)
        {
            _logger = logger;
            _sessionManager = sessionManager;
            _forumDataHelper = forumDataHelper;
        }

        private void LoadUserSession()
        {
            if (CurrentUserSession == null)
            {
                CurrentUserSession = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            }
        }

        public IActionResult OnGet(string id)
        {
            LoadUserSession();
            _logger.LogInformation("Attempting to load category with ID: {CategoryId}", id);
            
            try
            {
                CurrentCategory = _forumDataHelper.GetCategory(id);

                if (CurrentCategory == null)
                {
                    _logger.LogWarning("Category with ID {CategoryId} not found. Redirecting to forum index.", id);
                    return RedirectToPage("/Forums/ForumIndex");
                }
                
                foreach (var threadId in CurrentCategory.ThreadIds)
                {
                    var thread = _forumDataHelper.GetThread(threadId);
                    if (thread != null)
                    {
                        Threads.Add(new ThreadViewModel
                        {
                            Id = thread.Id,
                            Title = thread.Title,
                            AuthorName = thread.AuthorName,
                            ReplyCount = thread.Comments.Count,
                            ViewCount = thread.Views,
                            LastPostAuthorName = thread.LastPostInfo?.AuthorName,
                            LastPostTimeAgo = thread.LastPostInfo != null ? GetTimeAgo(thread.LastPostInfo.PostTime) : "N/A"
                        });
                    }
                }
                
                Threads = Threads.OrderByDescending(t => t.LastPostTimeAgo).ToList(); // Assuming GetTimeAgo can be sorted meaningfully or sort by PostTime before converting
                
                _logger.LogInformation("Successfully loaded category '{CategoryTitle}'.", CurrentCategory.Title);
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while loading category ID {CategoryId}.", id);
                return RedirectToPage("/Forums/ForumIndex");
            }
        }
        
        private string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;
            if (timeSpan.TotalMinutes < 1) return "刚刚";
            if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes} 分钟前";
            if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours} 小时前";
            if (timeSpan.TotalDays < 30) return $"{(int)timeSpan.TotalDays} 天前";
            return dateTime.ToString("yyyy-MM-dd");
        }
    }
} 