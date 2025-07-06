using Drx.Sdk.Network.Session;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;
using Web.KaxServer.Models;
using Web.KaxServer.Pages.Shared;
using Web.KaxServer.Services;
using Microsoft.AspNetCore.Hosting;
using Drx.Sdk.Text.Serialization;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Threading.Tasks;
using System.Threading;
using System.Linq.Expressions;
using Web.KaxServer.Services.Queries;
using Drx.Sdk.Network.DataBase;
using DRX.Framework;

namespace Web.KaxServer.Pages.Account
{
    public class ManagementModel : PageModel
    {
        private readonly SessionManager _sessionManager;
        private readonly ICdkService _cdkService;
        private readonly StoreService _storeService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ManagementModel> _logger;
        private readonly ForumDataHelper _forumDataHelper;

        [BindProperty]
        public int CdkType { get; set; }

        [BindProperty]
        public int Quantity { get; set; } = 1;

        [BindProperty]
        public int? AssetId { get; set; }

        [BindProperty]
        public decimal? CoinAmount { get; set; }

        [BindProperty]
        public int? DurationValue { get; set; }

        [BindProperty]
        public DurationUnit DurationUnit { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SortBy { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }

        [BindProperty(SupportsGet = true)]
        public int CurrentPage { get; set; } = 1;
        
        public int PageSize { get; } = 10;
        public int TotalPages { get; private set; }
        public int TotalCount { get; private set; }
        public int StartPage { get; private set; }
        public int EndPage { get; private set; }

        public List<StoreItem> StoreItems { get; set; } = new List<StoreItem>();
        public List<Cdk> RecentCdks { get; set; } = new List<Cdk>();
        public List<UserSession> ActiveUserSessions { get; set; } = new List<UserSession>();
        public List<CdkBatch> CdkBatches { get; set; } = new List<CdkBatch>();
        public List<ForumCategoryModel> ForumCategories { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? SelectedSessionId { get; set; }

        [BindProperty]
        public UserSession? SelectedUserSession { get; set; }

        [TempData]
        public string? SuccessMessage { get; set; }

        [TempData]
        public string? ErrorMessage { get; set; }

        public bool IsLoggedIn { get; private set; }
        public string Username { get; private set; } = string.Empty;
        public UserPermissionType UserPermission { get; private set; }

        private readonly string _usersDataPath = Path.Combine(AppContext.BaseDirectory, "user", "users.xml");

        public ManagementModel(SessionManager sessionManager, ICdkService cdkService, StoreService storeService, IWebHostEnvironment env, ILogger<ManagementModel> logger, ForumDataHelper forumDataHelper)
        {
            _sessionManager = sessionManager;
            _cdkService = cdkService;
            _storeService = storeService;
            _env = env;
            _logger = logger;
            _forumDataHelper = forumDataHelper;
            InitializeUserSession();
        }

        private void InitializeUserSession()
        {
            var userSession = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (userSession != null)
            {
                IsLoggedIn = true;
                Username = userSession.Username;
                UserPermission = userSession.UserPermission;
            }
            else
            {
                IsLoggedIn = false;
            }
        }

        public IActionResult OnGet()
        {
            if (!IsLoggedIn || UserPermission != UserPermissionType.Developer)
            {
                return RedirectToPage("/Account/Profile");
            }

            // 获取商店商品列表
            StoreItems = _storeService.GetAllItems();
            LoadCdkData();
            LoadActiveUserSessions();
            LoadCdkBatches();
            LoadForumData();

            if (!string.IsNullOrEmpty(SelectedSessionId))
            {
                SelectedUserSession = ActiveUserSessions.FirstOrDefault(s => s.ID == SelectedSessionId);
            }

            return Page();
        }

        public PartialViewResult OnGetCdkList()
        {
            StoreItems = _storeService.GetAllItems();
            LoadCdkData();
            LoadCdkBatches();
            return Partial("Shared/_CdkList", this);
        }
        
        public PartialViewResult OnGetUserSessionList()
        {
            LoadActiveUserSessions();
            return Partial("Shared/_UserSessionList", this);
        }
        
        public PartialViewResult OnGetEditUserPartial(string sessionId)
        {
            if (string.IsNullOrEmpty(sessionId))
            {
                return Partial("_EditUserForm", null);
            }

            LoadActiveUserSessions();
            var userSession = ActiveUserSessions.FirstOrDefault(s => s.ID == sessionId);
            
            return Partial("_EditUserForm", userSession);
        }
        
        private void LoadCdkData()
        {
            var parameters = new CdkQueryParameters
            {
                Page = this.CurrentPage,
                PageSize = this.PageSize,
                SearchTerm = this.SearchTerm,
                SortBy = this.SortBy
            };

            var result = _cdkService.QueryCdks(parameters);

            TotalCount = result.TotalCount;
            TotalPages = (int)Math.Ceiling(TotalCount / (double)PageSize);
            CurrentPage = Math.Max(1, Math.Min(CurrentPage, TotalPages == 0 ? 1 : TotalPages)); // 确保当前页在有效范围内
            RecentCdks = result.Cdks;

            // 4. 计算智能分页范围
            var maxPagesToShow = 5;
            var pageNumberBuffer = 2;

            if (TotalPages <= maxPagesToShow)
            {
                StartPage = 1;
                EndPage = TotalPages;
            }
            else
            {
                if (CurrentPage <= pageNumberBuffer + 1)
                {
                    StartPage = 1;
                    EndPage = maxPagesToShow;
                }
                else if (CurrentPage >= TotalPages - pageNumberBuffer)
                {
                    StartPage = TotalPages - maxPagesToShow + 1;
                    EndPage = TotalPages;
                }
                else
                {
                    StartPage = CurrentPage - pageNumberBuffer;
                    EndPage = CurrentPage + pageNumberBuffer;
                }
            }
        }

        private void LoadActiveUserSessions()
        {
            ActiveUserSessions = _sessionManager.GetAllSessions()
                .OfType<UserSession>()
                .OrderByDescending(s => s.ExpireTime)
                .ToList();
        }

        public IActionResult OnPostCreateCdk()
        {
            if (!IsLoggedIn || UserPermission != UserPermissionType.Developer)
            {
                return Forbid();
            }

            // 创建CDK
            var cdkType = (Models.CdkType)CdkType;
            var newCdks = _cdkService.CreateCdks(
                Quantity,
                cdkType,
                cdkType == Models.CdkType.Asset ? AssetId : null,
                cdkType == Models.CdkType.Coins ? CoinAmount : null,
                cdkType == Models.CdkType.Asset ? DurationValue : null,
                cdkType == Models.CdkType.Asset ? DurationUnit : null
            );

            if (newCdks == null || !newCdks.Any())
            {
                return new JsonResult(new { success = false, message = "CDK创建失败。" });
            }

            return new JsonResult(new
            {
                success = true,
                message = $"成功创建 {newCdks.Count} 个CDK"
            });
        }

        public string GetAssetName(int assetId)
        {
            var item = _storeService.GetItemById(assetId);
            return item?.Title ?? "未知资产";
        }

        public string GetDurationString(int? durationValue, DurationUnit? durationUnit)
        {
            if (!durationValue.HasValue || !durationUnit.HasValue)
            {
                return "默认";
            }

            var unitStr = durationUnit.Value switch
            {
                DurationUnit.Minute => "分钟",
                DurationUnit.Hour => "小时",
                DurationUnit.Day => "天",
                DurationUnit.Week => "周",
                DurationUnit.Month => "月",
                DurationUnit.Year => "年",
                _ => ""
            };

            return $"{durationValue.Value} {unitStr}";
        }

        public IActionResult OnPostUpdateUser()
        {
            if (!IsLoggedIn || UserPermission != UserPermissionType.Developer)
            {
                return Forbid();
            }

            if (SelectedUserSession != null && 
                !string.IsNullOrEmpty(SelectedUserSession.ID) && 
                !string.IsNullOrEmpty(SelectedUserSession.Username))
            {
                // 更新会话信息
                var sessionInManager = _sessionManager.GetSession<UserSession>(SelectedUserSession.ID);
                if (sessionInManager != null)
                {
                    sessionInManager.Coins = SelectedUserSession.Coins;
                    sessionInManager.UserPermission = SelectedUserSession.UserPermission;
                    _sessionManager.UpdateSession(sessionInManager, updateCookie: false);
                }

                // 更新 user_data/index.xml
                var userDataIndexPath = Path.Combine(_env.ContentRootPath, "user_data");
                try
                {
                    var userDataRepository = new IndexedRepository<UserData>(userDataIndexPath, "user_");
                    var userDataToUpdate = userDataRepository.Get(SelectedUserSession.UserId.ToString());
                    if (userDataToUpdate != null)
                    {
                        userDataToUpdate.Coins = SelectedUserSession.Coins;
                        userDataToUpdate.UserPermission = SelectedUserSession.UserPermission;
                        userDataRepository.Save(userDataToUpdate);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "更新用户 {Username} 的数据文件时发生错误", SelectedUserSession.Username);
                    ErrorMessage = "更新用户信息时发生错误。";
                }
            }

            return RedirectToPage(new { SelectedSessionId = SelectedUserSession?.ID });
        }

        private void LoadCdkBatches()
        {
            CdkBatches = new List<CdkBatch>();
            var dataDirectory = Path.Combine(_env.ContentRootPath, "Data");
            if (!Directory.Exists(dataDirectory)) return;

            var batchFiles = Directory.EnumerateFiles(dataDirectory, "*.txt")
                                      .OrderByDescending(f => new FileInfo(f).CreationTime);

            foreach (var file in batchFiles)
            {
                try
                {
                    CdkBatches.Add(new CdkBatch
                    {
                        FileName = Path.GetFileName(file),
                        CreationTime = new FileInfo(file).CreationTime,
                        CdkCount = System.IO.File.ReadLines(file).Count()
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing CDK batch file: {File}", file);
                }
            }
        }

        public IActionResult OnGetDownloadCdkBatch(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || fileName.Contains("..") || fileName.Contains("/") || fileName.Contains("\\"))
            {
                return BadRequest("Invalid file name.");
            }

            var dataDirectory = Path.Combine(_env.ContentRootPath, "Data");
            var filePath = Path.Combine(dataDirectory, fileName);

            if (!System.IO.File.Exists(filePath) || !filePath.StartsWith(dataDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return NotFound();
            }
            
            return PhysicalFile(filePath, "text/plain", fileName);
        }

        public IActionResult OnPostDeleteCdkBatchAsync(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                ErrorMessage = "无效的文件名。";
                LoadCdkData(); 
                return Partial("Shared/_CdkList", this);
            }

            try
            {
                var dataDirectory = Path.Combine(_env.ContentRootPath, "Data");
                var filePath = Path.Combine(dataDirectory, fileName);

                if (!System.IO.File.Exists(filePath))
                {
                    ErrorMessage = "批次文件不存在。";
                    LoadCdkData();
                    return Partial("Shared/_CdkList", this);
                }

                var firstCdkCode = System.IO.File.ReadLines(filePath).FirstOrDefault();
                
                if (!string.IsNullOrEmpty(firstCdkCode))
                {
                    var batchId = _cdkService.GetBatchIdFromCdkCode(firstCdkCode);
                    if (!string.IsNullOrEmpty(batchId))
                    {
                        _cdkService.DeleteCdksByBatchId(batchId);
                    }
                }

                System.IO.File.Delete(filePath);
                
                SuccessMessage = "批次已成功删除。";
                LoadCdkData();
                LoadCdkBatches();
                return Partial("Shared/_CdkList", this);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting CDK batch for file {FileName}", fileName);
                ErrorMessage = "删除过程中发生错误。";
                LoadCdkData();
                LoadCdkBatches();
                return Partial("Shared/_CdkList", this);
            }
        }

        public class CdkBatch
        {
            public string FileName { get; set; }
            public DateTime CreationTime { get; set; }
            public int CdkCount { get; set; }
        }

        private void LoadForumData()
        {
            _logger.LogInformation("Attempting to load forum data from distributed file system.");
            try
            {
                ForumCategories = _forumDataHelper.GetAllCategories();

                if (!ForumCategories.Any())
                {
                    _logger.LogWarning("No categories found. Generating default category.");
                    GenerateDefaultCategory();
                    ForumCategories = _forumDataHelper.GetAllCategories();
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
                Id = _forumDataHelper.GenerateId("default-category-management"),
                Title = "默认板块",
                Description = "这是一个自动生成的默认板块。",
                IconClass = "fa-solid fa-folder-open",
            };

            var thread1 = new ForumThreadModel
            {
                Id = _forumDataHelper.GenerateId("default-thread-m-1"),
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
                Id = _forumDataHelper.GenerateId("default-thread-m-2"),
                CategoryId = category.Id,
                Title = "欢迎来到我们的论坛！",
                AuthorName = admin,
                Content = "欢迎大家！这是一个测试帖子。\n\n你可以在这里发帖、回复、讨论任何事情。",
                PostTime = postTime1,
                Comments = new List<ForumThreadCommentModel>
                {
                    new()
                    {
                        Id = _forumDataHelper.GenerateId("default-comment-m-1"),
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