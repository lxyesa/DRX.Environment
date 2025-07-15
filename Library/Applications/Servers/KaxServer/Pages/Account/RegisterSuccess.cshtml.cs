using KaxServer.Models;
using KaxServer.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaxServer.Pages.Account
{
    public class RegisterSuccessModel : PageModel
    {
        public UserData? CurrentUser { get; set; }

        public async Task OnGet()
        {
            CurrentUser = await UserManager.GetCurrentUserAsync(HttpContext);

            if (CurrentUser != null)
            {
                RedirectToPage("/Index");
            }
        }
    }
} 