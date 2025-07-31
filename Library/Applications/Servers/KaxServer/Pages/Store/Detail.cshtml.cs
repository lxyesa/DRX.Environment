
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using KaxServer.Services;
using System.Threading.Tasks;
using System.Linq;
using KaxServer.Models;

public class DetailModel : PageModel
{
    [BindProperty(SupportsGet = true)]
    public int id { get; set; }

    public UserData CurrentUser { get; set; }

    public StoreItem StoreItem { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        CurrentUser = await UserManager.GetCurrentUserAsync(HttpContext);
        if (id <= 0)
        {
            return NotFound();
        }
        var item = await StoreManager.StoreItemSqlite.QueryByIdAsync(id);
        if (item == null)
        {
            return NotFound();
        }
        StoreItem = item;
        return Page();
    }
}
