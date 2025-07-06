using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Tasks;
using Web.KaxServer.Pages.Shared;

namespace Web.KaxServer.Services
{
    public class MessageBoxService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ITempDataDictionaryFactory _tempDataDictionaryFactory;
        private static readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _pendingTasks = new();
        private static readonly ConcurrentDictionary<string, Action> _callbacks = new();

        public MessageBoxService(IHttpContextAccessor httpContextAccessor, ITempDataDictionaryFactory tempDataDictionaryFactory)
        {
            _httpContextAccessor = httpContextAccessor;
            _tempDataDictionaryFactory = tempDataDictionaryFactory;
        }

        /// <summary>
        /// 注入一个消息框到当前页面（简化版本，只需要标题、消息和按钮文本）
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="message">消息内容（支持HTML）</param>
        /// <param name="buttonText">按钮文本（默认为"确定"）</param>
        /// <returns>Task，可以等待用户点击按钮</returns>
        public Task<bool> Inject(string title, string message, string? buttonText = null)
        {
            var callbackId = Guid.NewGuid().ToString();
            var tcs = new TaskCompletionSource<bool>();
            _pendingTasks[callbackId] = tcs;

            InjectInternal(title, message, "/MessageBoxCallback", buttonText, true, null, callbackId, CallbackType.Task);
            
            return tcs.Task;
        }

        /// <summary>
        /// 注入一个消息框到当前页面（带回调函数）
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="message">消息内容（支持HTML）</param>
        /// <param name="buttonText">按钮文本（默认为"确定"）</param>
        /// <param name="callback">按钮点击后的回调函数</param>
        public void Inject(string title, string message, string? buttonText, Action callback)
        {
            var callbackId = Guid.NewGuid().ToString();
            _callbacks[callbackId] = callback;

            InjectInternal(title, message, "/MessageBoxCallback", buttonText, true, null, callbackId, CallbackType.Function);
        }

        /// <summary>
        /// 注入一个消息框到当前页面（原始版本，完整参数）
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="message">消息内容（支持HTML）</param>
        /// <param name="callbackUrl">按钮点击后的回调URL</param>
        /// <param name="buttonText">按钮文本（默认为"确定"）</param>
        /// <param name="isButtonHighlighted">按钮是否高亮显示</param>
        /// <param name="returnUrl">回调完成后的返回URL</param>
        public void Inject(string title, string message, string callbackUrl, string? buttonText = null, bool isButtonHighlighted = true, string? returnUrl = null)
        {
            InjectInternal(title, message, callbackUrl, buttonText, isButtonHighlighted, returnUrl, string.Empty, CallbackType.Url);
        }

        private void InjectInternal(string title, string message, string callbackUrl, string? buttonText, bool isButtonHighlighted, string? returnUrl, string callbackId, CallbackType callbackType)
        {
            if (_httpContextAccessor.HttpContext == null)
            {
                return;
            }

            var tempData = _tempDataDictionaryFactory.GetTempData(_httpContextAccessor.HttpContext);

            var messageBox = new MessageBoxModel
            {
                Title = title,
                Message = message,
                ButtonText = buttonText ?? "确定",
                IsButtonHighlighted = isButtonHighlighted,
                CallbackUrl = callbackUrl,
                ReturnUrl = returnUrl ?? _httpContextAccessor.HttpContext.Request.Path,
                IsVisible = true,
                CallbackId = callbackId,
                CallbackType = callbackType
            };

            tempData["MessageBox"] = JsonSerializer.Serialize(messageBox);
        }

        /// <summary>
        /// 触发回调函数或完成Task
        /// </summary>
        /// <param name="callbackId">回调ID</param>
        public static void TriggerCallback(string callbackId)
        {
            if (_pendingTasks.TryRemove(callbackId, out var tcs))
            {
                tcs.TrySetResult(true);
            }

            if (_callbacks.TryRemove(callbackId, out var callback))
            {
                callback?.Invoke();
            }
        }
    }
} 