using Drx.Sdk.Text.Serialization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Drx.Sdk.Network.Session;
using Web.KaxServer.Models;
using System.Xml.Linq;
using Web.KaxServer.Services;

namespace Web.KaxServer.Pages.Forums
{
    public class ForumIndexModel : PageModel
    {
        private readonly ILogger<ForumIndexModel> _logger;
        private readonly SessionManager _sessionManager;
        private readonly ForumDataHelper _forumDataHelper;

        public UserSession CurrentUserSession { get; private set; }
        public bool IsLoggedIn => CurrentUserSession != null;
        public string Username => CurrentUserSession?.Username ?? string.Empty;
        public UserPermissionType UserPermission => CurrentUserSession?.UserPermission ?? UserPermissionType.Normal;

        public class ForumStatsModel
        {
            public int TotalThreads { get; set; }
            public int TotalPosts { get; set; }
            public int MembersOnline { get; set; }
        }

        public List<ForumCategoryModel> Categories { get; set; } = new();
        public ForumStatsModel Stats { get; set; } = new();
        
        private readonly string _usersDataPath = Path.Combine(AppContext.BaseDirectory, "user", "users.xml");

        public ForumIndexModel(ILogger<ForumIndexModel> logger, SessionManager sessionManager, ForumDataHelper forumDataHelper)
        {
            _logger = logger;
            _sessionManager = sessionManager;
            _forumDataHelper = forumDataHelper;
        }

        public void OnGet()
        {
            CurrentUserSession = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            LoadForumData();
            
            Stats = new ForumStatsModel
            {
                TotalThreads = Categories.Sum(c => c.ThreadCount),
                TotalPosts = Categories.Sum(c => c.PostCount),
                MembersOnline = new Random().Next(50, 200) // Placeholder for online members
            };
        }
        
        private void LoadForumData()
        {
            _logger.LogInformation("Attempting to load forum data from distributed file system.");
            try
            {
                Categories = _forumDataHelper.GetAllCategories();
                
                if (!Categories.Any())
                {
                    _logger.LogWarning("No categories found. Generating default category.");
                    GenerateDefaultCategory();
                    Categories = _forumDataHelper.GetAllCategories();
                }

                if (Categories.Any())
                {
                    // Load LastThread for each category
                    foreach (var category in Categories)
                    {
                        if (!string.IsNullOrEmpty(category.LastThreadId))
                        {
                            category.LastThread = _forumDataHelper.GetThread(category.LastThreadId);
                        }
                    }

                    var lastThreadAuthors = Categories
                        .Where(c => c.LastThread != null)
                        .Select(c => c.LastThread.AuthorName)
                        .Distinct()
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);

                    var authorAvatars = GetAvatarUrlsByUsernames(lastThreadAuthors);

                    foreach (var category in Categories.Where(c => c.LastThread != null))
                    {
                        if (authorAvatars.TryGetValue(category.LastThread.AuthorName, out var avatarUrl))
                        {
                            category.LastThread.AuthorAvatarUrl = avatarUrl;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during forum data loading and processing.");
            }
        }
        
        private Dictionary<string, string> GetAvatarUrlsByUsernames(HashSet<string> usernames)
        {
            var avatars = new Dictionary<string, string>();
            if (!System.IO.File.Exists(_usersDataPath) || !usernames.Any())
            {
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

        private void GenerateDefaultCategory()
        {
            var admin = "Admin";
            var user1 = "User1";
            var postTime1 = DateTime.Now.AddHours(-2);
            var postTime2 = DateTime.Now.AddMinutes(-5);

            var category = new ForumCategoryModel
            {
                Id = _forumDataHelper.GenerateId("default-category"),
                Title = "默认板块",
                Description = "这是一个自动生成的默认板块。",
                IconClass = "fa-solid fa-folder-open",
            };

            var thread1 = new ForumThreadModel
            {
                Id = _forumDataHelper.GenerateId("default-thread-1"),
                CategoryId = category.Id,
                Title = "论坛使用指南",
                AuthorName = admin,
                Content = "这是论坛的 **使用指南**。请遵守社区规则，友好交流。\n\n这里支持 `Markdown` 语法！",
                PostTime = postTime1.AddDays(-1),
                Comments = new List<ForumThreadCommentModel>(),
                Views = 150,
                LastPostInfo = new LastPostInfoModel { AuthorName = admin, PostTime = postTime1 }
            };

            var thread2 = new ForumThreadModel
            {
                Id = _forumDataHelper.GenerateId("default-thread-2"),
                CategoryId = category.Id,
                Title = "欢迎来到我们的论坛！",
                AuthorName = admin,
                Content = "欢迎大家！这是一个测试帖子。\n\n你可以在这里发帖、回复、讨论任何事情。",
                PostTime = postTime1,
                Comments = new List<ForumThreadCommentModel>
                {
                    new()
                    {
                        Id = _forumDataHelper.GenerateId("default-comment-1"),
                        AuthorName = user1,
                        Content = "很高兴来到这里！",
                        PostTime = postTime2
                    }
                },
                Views = 280,
                LastPostInfo = new LastPostInfoModel { AuthorName = user1, PostTime = postTime2 }
            };
            
            _forumDataHelper.SaveThread(thread1);
            _forumDataHelper.SaveThread(thread2);

            category.ThreadIds.Add(thread1.Id);
            category.ThreadIds.Add(thread2.Id);
            category.ThreadCount = 2;
            category.PostCount = 1; // thread2 has one comment
            category.LastThreadId = thread2.Id;
            
            _forumDataHelper.SaveCategory(category);
        }
    }
} 