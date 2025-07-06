using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Web.KaxServer.Services;

namespace Web.KaxServer.Pages
{
    public class MessageBoxCallbackModel : PageModel
    {
        [BindProperty]
        public string ReturnUrl { get; set; } = "/";

        public void OnPost()
        {
            // 这里可以添加任何需要在回调时执行的逻辑
            // 例如记录用户点击了确认按钮等
        }

        public IActionResult OnPostTriggerCallback(string callbackId)
        {
            // 触发回调函数或完成Task
            MessageBoxService.TriggerCallback(callbackId);
            return new JsonResult(new { success = true });
        }
    }
} 