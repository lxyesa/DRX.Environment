using Drx.Sdk.Network.Session;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Web.KaxServer.Models;
using Web.KaxServer.Services;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using System.Linq;
using System.Threading.Tasks;

namespace Web.KaxServer.Pages.Account
{
    public class EditBoardModel : PageModel
    {
        private readonly SessionManager _sessionManager;
        private readonly ForumDataHelper _forumDataHelper;
        private readonly ILogger<EditBoardModel> _logger;

        [BindProperty]
        public ForumCategoryModel Board { get; set; }

        public bool IsLoggedIn { get; private set; }
        public UserPermissionType UserPermission { get; private set; }

        public EditBoardModel(SessionManager sessionManager, ForumDataHelper forumDataHelper, ILogger<EditBoardModel> logger)
        {
            _sessionManager = sessionManager;
            _forumDataHelper = forumDataHelper;
            _logger = logger;
        }

        private void InitializeUserSession()
        {
            var userSession = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (userSession != null)
            {
                IsLoggedIn = true;
                UserPermission = userSession.UserPermission;
            }
            else
            {
                IsLoggedIn = false;
            }
        }

        public IActionResult OnGet(string id)
        {
            InitializeUserSession();
            if (!IsLoggedIn || UserPermission != UserPermissionType.Developer)
            {
                return Forbid();
            }

            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            Board = _forumDataHelper.GetCategory(id);

            if (Board == null)
            {
                return NotFound();
            }

            return Page();
        }

        public async Task<IActionResult> OnPost()
        {
            InitializeUserSession();
            if (!IsLoggedIn || UserPermission != UserPermissionType.Developer)
            {
                return Forbid();
            }

            var boardToUpdate = _forumDataHelper.GetCategory(Board.Id);
            if (boardToUpdate == null)
            {
                return NotFound();
            }

            // Bind form values to the fetched model, only updating specific fields.
            if (await TryUpdateModelAsync<ForumCategoryModel>(
                boardToUpdate,
                "Board", // The prefix for form fields e.g., "Board.Title"
                b => b.Title, b => b.Description, b => b.IconClass))
            {
                try
                {
                    _forumDataHelper.SaveCategory(boardToUpdate);
                    _logger.LogInformation("Successfully updated board {BoardId}", boardToUpdate.Id);
                    return RedirectToPage("/Account/Management");
                }
                catch (System.Exception ex)
                {
                    _logger.LogError(ex, "Error updating board {BoardId}", boardToUpdate.Id);
                    ModelState.AddModelError(string.Empty, "保存板块信息时发生错误。");
                }
            }

            // If TryUpdateModelAsync fails, the ModelState will be invalid.
            // Re-populate the Board property for the page to display the current (failed) state.
            Board = boardToUpdate;
            return Page();
        }
    }
} 