using Drx.Sdk.Text.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Web.KaxServer.Models;
using Markdig;
using Drx.Sdk.Network.Session;
using System.Xml.Linq;
using Web.KaxServer.Services;

namespace Web.KaxServer.Pages.Forums
{
    public class ViewThreadModel : PageModel
    {
        private readonly ILogger<ViewThreadModel> _logger;
        private readonly SessionManager _sessionManager;
        private readonly ForumDataHelper _forumDataHelper;
        private readonly string _forumDataPath = Path.Combine(AppContext.BaseDirectory, "data", "forum.xml");
        private readonly string _usersDataPath = Path.Combine(AppContext.BaseDirectory, "user", "users.xml");
        private static readonly MarkdownPipeline _markdownPipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();
        private static readonly object _fileLock = new object();

        public ThreadViewModel Thread { get; set; }
        public List<CommentViewModel> Comments { get; set; } = new();
        public ForumCategoryModel ParentCategory { get; set; }
        public UserSession CurrentUserSession { get; private set; }
        public bool IsLoggedIn => CurrentUserSession != null;
        public string Username => CurrentUserSession?.Username ?? string.Empty;
        public UserPermissionType UserPermission => CurrentUserSession?.UserPermission ?? UserPermissionType.Normal;

        public ViewThreadModel(ILogger<ViewThreadModel> logger, SessionManager sessionManager, ForumDataHelper forumDataHelper)
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
            _logger.LogInformation("Attempting to load thread with ID: {ThreadId}", id);

            try
            {
                var foundThread = _forumDataHelper.GetThread(id);

                if (foundThread == null)
                {
                    _logger.LogWarning("Thread with ID {ThreadId} not found. Redirecting.", id);
                    return RedirectToPage("/Forums/ForumIndex");
                }
                
                ParentCategory = _forumDataHelper.GetCategory(foundThread.CategoryId);
                if (ParentCategory == null)
                {
                     _logger.LogWarning("Parent category with ID {CategoryId} for thread {ThreadId} not found. Redirecting.", foundThread.CategoryId, id);
                    return RedirectToPage("/Forums/ForumIndex");
                }

                var authorNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { foundThread.AuthorName };
                foundThread.Comments.ForEach(c => authorNames.Add(c.AuthorName));
                var authorAvatars = GetAvatarUrlsByUsernames(authorNames);

                Thread = new ThreadViewModel(foundThread, authorAvatars.GetValueOrDefault(foundThread.AuthorName));
                Comments = foundThread.Comments
                    .OrderBy(c => c.PostTime)
                    .Select(c => new CommentViewModel(c, authorAvatars.GetValueOrDefault(c.AuthorName)))
                    .ToList();
                
                // TODO: Increment view count

                _logger.LogInformation("Successfully loaded thread '{ThreadTitle}'.", Thread.Title);
                return Page();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while loading thread ID {ThreadId}.", id);
                return RedirectToPage("/Forums/ForumIndex");
            }
        }

        public IActionResult OnPost(string id)
        {
            LoadUserSession();
            if (!IsLoggedIn)
            {
                return Forbid();
            }

            var replyContent = Request.Form["ReplyContent"];

            if (string.IsNullOrWhiteSpace(replyContent))
            {
                TempData["ErrorMessage"] = "回复内容不能为空。";
                return RedirectToPage(new { id });
            }

            try
            {
                var foundThread = _forumDataHelper.GetThread(id);
                if (foundThread != null)
                {
                    var parentCategory = _forumDataHelper.GetCategory(foundThread.CategoryId);
                    if (parentCategory != null)
                    {
                        var newComment = new ForumThreadCommentModel
                        {
                            Id = _forumDataHelper.GenerateId(replyContent + CurrentUserSession.Username),
                            AuthorName = CurrentUserSession.Username,
                            Content = replyContent,
                            PostTime = DateTime.Now
                        };

                        foundThread.Comments.Add(newComment);
                        foundThread.LastPostInfo = new LastPostInfoModel { AuthorName = newComment.AuthorName, PostTime = newComment.PostTime };
                        _forumDataHelper.SaveThread(foundThread);

                        parentCategory.PostCount++;
                        _forumDataHelper.SaveCategory(parentCategory);

                        _logger.LogInformation("User {Username} posted a reply to thread {ThreadId}", CurrentUserSession.Username, id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error posting reply to thread {ThreadId}", id);
                TempData["ErrorMessage"] = "发表回复时发生未知错误。";
            }

            return RedirectToPage(new { id });
        }
        
        private Dictionary<string, string> GetAvatarUrlsByUsernames(HashSet<string> usernames)
        {
            var avatars = new Dictionary<string, string>();
            if (!System.IO.File.Exists(_usersDataPath))
            {
                _logger.LogWarning("Users data file not found at {Path}", _usersDataPath);
                return avatars;
            }

            try
            {
                var doc = XDocument.Load(_usersDataPath);
                var userElements = doc.Root?.Elements("user") ?? Enumerable.Empty<XElement>();
                
                foreach (var userElement in userElements)
                {
                    var username = userElement.Attribute("username")?.Value;
                    if (!string.IsNullOrEmpty(username) && usernames.Contains(username))
                    {
                        var avatarUrl = userElement.Attribute("avatarUrl")?.Value;
                        if (!string.IsNullOrEmpty(avatarUrl))
                        {
                            avatars[username] = avatarUrl;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading user avatars from {Path}", _usersDataPath);
            }

            return avatars;
        }

        private static string GetTimeAgo(DateTime dateTime)
        {
            var timeSpan = DateTime.Now - dateTime;
            if (timeSpan.TotalMinutes < 1) return "刚刚";
            if (timeSpan.TotalMinutes < 60) return $"{(int)timeSpan.TotalMinutes} 分钟前";
            if (timeSpan.TotalHours < 24) return $"{(int)timeSpan.TotalHours} 小时前";
            if (timeSpan.TotalDays < 30) return $"{(int)timeSpan.TotalDays} 天前";
            return dateTime.ToString("yyyy-MM-dd");
        }

        public class ThreadViewModel
        {
            public string Id { get; }
            public string Title { get; }
            public string AuthorName { get; }
            public string AuthorAvatarUrl { get; }
            public string Content { get; }
            public string PostTimeAgo { get; }
            public int Views { get; }

            public ThreadViewModel(ForumThreadModel model, string authorAvatarUrl)
            {
                Id = model.Id;
                Title = model.Title;
                AuthorName = model.AuthorName;
                Content = Markdown.ToHtml(model.Content ?? "", _markdownPipeline);
                PostTimeAgo = GetTimeAgo(model.PostTime);
                Views = model.Views;
                AuthorAvatarUrl = authorAvatarUrl;
            }
        }

        public class CommentViewModel
        {
            public string Id { get; }
            public string AuthorName { get; }
            public string AuthorAvatarUrl { get; }
            public string Content { get; }
            public string PostTimeAgo { get; }

            public CommentViewModel(ForumThreadCommentModel model, string authorAvatarUrl)
            {
                Id = model.Id;
                AuthorName = model.AuthorName;
                Content = Markdown.ToHtml(model.Content ?? "", _markdownPipeline);
                PostTimeAgo = GetTimeAgo(model.PostTime);
                AuthorAvatarUrl = authorAvatarUrl;
            }
        }
    }
} 