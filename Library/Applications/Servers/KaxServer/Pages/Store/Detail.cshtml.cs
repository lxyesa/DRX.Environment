
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

    // 每个价格项是否已拥有
    public List<bool> OwnStatus { get; set; } = new List<bool>();

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
        OwnStatus = item.StoreItemPrices.Select(_ =>
            CurrentUser?.BuyedStoreItems.Any(b =>
                b.StoreItemId == item.Id && !b.IsExpired
            ) ?? false
        ).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostBuyAsync(int itemId, int priceIndex)
    {
        CurrentUser = await UserManager.GetCurrentUserAsync(HttpContext);
        var storeItem = await StoreManager.StoreItemSqlite.QueryByIdAsync(itemId);
        if (storeItem == null)
        {
            return NotFound();
        }
        StoreItem = storeItem;
        if (CurrentUser == null)
        {
            return Unauthorized();
        }

        // 禁止已拥有再次购买
        bool alreadyOwned = CurrentUser.BuyedStoreItems.Any(b => b.StoreItemId == itemId && !b.IsExpired);
        OwnStatus = storeItem.StoreItemPrices.Select(_ => alreadyOwned).ToList();
        if (alreadyOwned)
        {
            ModelState.AddModelError("", "已拥有该商品，无需重复购买");
            return Page();
        }

        var result = await StoreManager.BuyItemAsync(CurrentUser, itemId, priceIndex);
        if (result.Success)
        {
            return RedirectToPage(new { id = itemId });
        }
        else
        {
            ModelState.AddModelError("", result.Message ?? "未知错误");
            return Page();
        }
    }
}
