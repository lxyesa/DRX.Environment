using Drx.Sdk.Network.Session;
using Drx.Sdk.Text.Serialization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Web.KaxServer.Models;
using Web.KaxServer.Services;

namespace Web.KaxServer.Pages.Forums
{
    public class CreateThreadModel : PageModel
    {
        private readonly ILogger<CreateThreadModel> _logger;
        private readonly SessionManager _sessionManager;
        private readonly ForumDataHelper _forumDataHelper;
        
        [BindProperty]
        public InputModel Input { get; set; }
        
        public ForumCategoryModel ParentCategory { get; set; }
        public UserSession CurrentUserSession { get; private set; }
        public bool IsLoggedIn => CurrentUserSession != null;
        public string Username => CurrentUserSession?.Username ?? string.Empty;
        public UserPermissionType UserPermission => CurrentUserSession?.UserPermission ?? UserPermissionType.Normal;

        public class InputModel
        {
            public string Title { get; set; }
            public string Content { get; set; }
            public string CategoryId { get; set; }
        }

        public CreateThreadModel(ILogger<CreateThreadModel> logger, SessionManager sessionManager, ForumDataHelper forumDataHelper)
        {
            _logger = logger;
            _sessionManager = sessionManager;
            _forumDataHelper = forumDataHelper;
        }

        public IActionResult OnGet(string categoryId)
        {
            var user = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (user == null)
            {
                return RedirectToPage("/Account/Login", new { returnUrl = Url.Page(null, new { categoryId }) });
            }

            ParentCategory = _forumDataHelper.GetCategory(categoryId);

            if (ParentCategory == null)
            {
                _logger.LogWarning("CreateThread: Category with ID {CategoryId} not found.", categoryId);
                return RedirectToPage("/Forums/ForumIndex");
            }

            Input = new InputModel { CategoryId = categoryId };
            return Page();
        }

        public IActionResult OnPost()
        {
            var user = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (user == null)
            {
                return Forbid();
            }
            
            CurrentUserSession = user;

            if (!ModelState.IsValid)
            {
                // Must reload parent category if model state is invalid
                ParentCategory = _forumDataHelper.GetCategory(Input.CategoryId);
                return Page();
            }

            try
            {
                var parentCategory = _forumDataHelper.GetCategory(Input.CategoryId);

                if (parentCategory != null)
                {
                    var now = DateTime.Now;
                    var newThread = new ForumThreadModel
                    {
                        Id = _forumDataHelper.GenerateId(Input.Title + user.Username + now),
                        CategoryId = parentCategory.Id,
                        Title = Input.Title,
                        Content = Input.Content,
                        AuthorName = user.Username,
                        PostTime = now,
                        Views = 0,
                        LastPostInfo = new LastPostInfoModel { AuthorName = user.Username, PostTime = now },
                        Comments = new List<ForumThreadCommentModel>()
                    };

                    _forumDataHelper.SaveThread(newThread);

                    parentCategory.ThreadIds.Insert(0, newThread.Id);
                    parentCategory.ThreadCount++;
                    parentCategory.PostCount++; // New thread counts as one post
                    parentCategory.LastThreadId = newThread.Id;

                    _forumDataHelper.SaveCategory(parentCategory);

                    _logger.LogInformation("User {Username} created new thread '{ThreadTitle}' in category {CategoryId}", user.Username, newThread.Title, parentCategory.Id);
                    
                    return RedirectToPage("/Forums/ViewThread", new { id = newThread.Id });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating new thread in category {CategoryId}", Input.CategoryId);
                ModelState.AddModelError(string.Empty, "创建主题时发生未知错误。");
            }
            
            // Must reload parent category if we fall through
            ParentCategory = _forumDataHelper.GetCategory(Input.CategoryId);
            return Page();
        }
    }
} 