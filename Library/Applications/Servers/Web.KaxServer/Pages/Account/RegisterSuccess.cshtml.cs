using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Web.KaxServer.Pages.Account
{
    public class RegisterSuccessModel : PageModel
    {
        [TempData]
        public string UsernameForSuccessPage { get; set; }

        public string Username { get; private set; }

        public void OnGet()
        {
            Username = UsernameForSuccessPage;
        }

        public IActionResult OnPostGoToHome()
        {
            return RedirectToPage("/Index");
        }

        public IActionResult OnPostGoToRegister()
        {
            return RedirectToPage("/Register");
        }
    }
} 