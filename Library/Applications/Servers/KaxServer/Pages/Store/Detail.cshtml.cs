using KaxServer.Models;
using KaxServer.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace KaxServer.Pages.Store
{
    // 修复命名空间与类型缺失：提供与 Razor @model 对应的 PageModel
    public class DetailModel : PageModel
    {
        public int Id { get; set; }
        public UserData CurrentUser { get; set; } // 当前用户信息

        public void OnGet(int id)
        {
            Id = id;
            CurrentUser = UserManager.GetCurrentUserAsync(HttpContext).Result;
            // 前端脚本基于 QueryString /api/store/items/{id} 获取并渲染
        }
    }
}