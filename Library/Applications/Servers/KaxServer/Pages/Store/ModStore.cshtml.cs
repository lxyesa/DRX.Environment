using KaxServer.Models;
using KaxServer.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaxServer.Pages.Store
{
    public class ModStoreModel : PageModel
    {
        public UserData CurrentUser { get; set; }

        public async Task OnGet()
        {
            CurrentUser = await UserManager.GetCurrentUserAsync(HttpContext);
        }
    }
}
