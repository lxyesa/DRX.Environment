using Drx.Sdk.Network.Session;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Xml.Linq;
using Web.KaxServer.Models;
using Web.KaxServer.Services;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
using System;

namespace Web.KaxServer.Pages.Shop
{
    public class StoreModel : PageModel
    {
        private readonly SessionManager _sessionManager;
        private readonly IWebHostEnvironment _env;
        private readonly StoreService _storeService;

        public bool IsLoggedIn { get; private set; }
        public string Username { get; private set; } = string.Empty;
        public List<StoreItem> StoreItems { get; private set; } = new List<StoreItem>();
        public bool IsDeveloper { get; private set; }

        [BindProperty(SupportsGet = true)]
        public string? SearchTerm { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? GameFilter { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? CategoryFilter { get; set; }

        public List<string> UniqueGames { get; private set; } = new();
        public List<string> UniqueCategories { get; private set; } = new();

        public StoreModel(SessionManager sessionManager, IWebHostEnvironment env, StoreService storeService)
        {
            _sessionManager = sessionManager;
            _env = env;
            _storeService = storeService;
        }

        public void OnGet()
        {
            var userSession = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (userSession != null)
            {
                IsLoggedIn = true;
                Username = userSession.Username;
                IsDeveloper = userSession.UserPermission == UserPermissionType.Developer;
            }
            else
            {
                IsLoggedIn = false;
                IsDeveloper = false;
            }

            var allItems = _storeService.GetAllItems();

            UniqueGames = allItems.Select(i => i.Game).Distinct().ToList();
            UniqueCategories = allItems.Select(i => i.Category).Distinct().ToList();

            IEnumerable<StoreItem> filteredItems = allItems;

            if (!string.IsNullOrEmpty(GameFilter))
            {
                filteredItems = filteredItems.Where(i => i.Game == GameFilter);
            }

            if (!string.IsNullOrEmpty(CategoryFilter))
            {
                filteredItems = filteredItems.Where(i => i.Category == CategoryFilter);
            }

            if (!string.IsNullOrEmpty(SearchTerm))
            {
                filteredItems = filteredItems.Where(item =>
                    (item.Title?.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (item.AuthorName?.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (item.Game?.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ?? false)
                );
            }

            StoreItems = filteredItems.ToList();
        }
    }
} 