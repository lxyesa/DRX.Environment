using Microsoft.AspNetCore.Mvc;

namespace Web.KaxServer.Pages.Shared
{
    public class MessageBoxModel
    {
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ButtonText { get; set; } = "确定";
        public bool IsButtonHighlighted { get; set; } = true;
        public string CallbackUrl { get; set; } = string.Empty;
        public string ReturnUrl { get; set; } = string.Empty;
        public bool IsVisible { get; set; } = false;
        public string CallbackId { get; set; } = string.Empty;
        public CallbackType CallbackType { get; set; } = CallbackType.Url;
    }

    public enum CallbackType
    {
        Url,        // 使用URL回调
        Function,   // 使用JavaScript函数回调
        Task        // 使用Task等待
    }
} 