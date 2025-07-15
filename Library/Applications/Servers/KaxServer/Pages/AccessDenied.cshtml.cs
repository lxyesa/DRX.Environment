using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaxServer.Pages;

using KaxServer.Models;
using KaxServer.Services;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

public class AccessDeniedModel : PageModel
{
    public UserData? CurrentUser { get; set; }

    public async Task OnGetAsync()
    {
        CurrentUser = await UserManager.GetCurrentUserAsync(HttpContext);
    }
}