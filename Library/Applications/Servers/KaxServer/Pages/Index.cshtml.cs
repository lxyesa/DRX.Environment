using System;
using DRX.Framework;
using KaxServer.Models;
using KaxServer.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaxServer.Pages;

public class IndexModel : PageModel
{
    public UserData CurrentUser { get; set; }
    public async Task OnGet()
    {
        CurrentUser = await UserManager.GetCurrentUserAsync(HttpContext);
    }
}
