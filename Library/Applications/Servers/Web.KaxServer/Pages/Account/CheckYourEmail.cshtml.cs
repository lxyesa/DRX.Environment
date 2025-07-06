using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Web.KaxServer.Pages.Account
{
    public class CheckYourEmailModel : PageModel
    {
        [TempData]
        public string UserEmail { get; set; }

        public bool IsValidRequest => !string.IsNullOrEmpty(UserEmail);

        public void OnGet()
        {
        }
        
        public IActionResult OnPostGoToRegister()
        {
            return RedirectToPage("/Account/Register");
        }
    }
} 