using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NDV.Pages
{
    public class EmailVerificationWaitingModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string Email { get; set; }
        
        public IActionResult OnGet()
        {
            if (string.IsNullOrEmpty(Email))
            {
                return RedirectToPage("/Index");
            }
            
            return Page();
        }
    }
} 