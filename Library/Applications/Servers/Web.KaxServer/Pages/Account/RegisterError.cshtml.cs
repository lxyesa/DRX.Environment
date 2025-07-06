using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Web.KaxServer.Pages.Account
{
    public class RegisterErrorModel : PageModel
    {
        [TempData]
        public string ErrorMessage { get; set; }

        public void OnGet()
        {
            if (string.IsNullOrEmpty(ErrorMessage))
            {
                ErrorMessage = "发生了未知错误。";
            }
        }
    }
} 