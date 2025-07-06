using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace NDV.Pages
{
    public class EmailVerifiedModel : PageModel
    {
        [BindProperty(SupportsGet = true)]
        public string Email { get; set; }
        
        public void OnGet()
        {
            // 页面加载时的逻辑
        }
    }
} 