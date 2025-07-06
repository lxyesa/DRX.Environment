using Drx.Sdk.Network.Session;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Collections.Generic;
using System.Linq;
using Web.KaxServer.Models;
using Web.KaxServer.Services;

namespace Web.KaxServer.Pages.Account
{
    public class ProfileModel : PageModel
    {
        private readonly SessionManager _sessionManager;

        public bool IsLoggedIn { get; private set; }
        public string Username { get; private set; } = string.Empty;
        public string Email { get; private set; } = string.Empty;
        public string AvatarUrl { get; private set; } = string.Empty;
        public UserPermissionType UserPermission { get; private set; }

        public ProfileModel(SessionManager sessionManager)
        {
            _sessionManager = sessionManager;
        }

        public IActionResult OnGet()
        {
            var userSession = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (userSession == null)
            {
                return RedirectToPage("/Account/Login", new { returnUrl = "/Account/Profile" });
            }

            IsLoggedIn = true;
            Username = userSession.Username;
            Email = userSession.Email;
            UserPermission = userSession.UserPermission;
            AvatarUrl = userSession.AvatarUrl;

            return Page();
        }

        public IActionResult OnPostLogout()
        {
            var userSession = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (userSession != null)
            {
                _sessionManager.RemoveSession(userSession.ID, true);
            }
            return RedirectToPage("/Index");
        }
    }
} 