using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Drx.Sdk.Network.Session;
using Web.KaxServer.Models;
using Web.KaxServer.Services;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using DRX.Framework;

namespace Web.KaxServer.Pages.Account
{
    public class EditAssetModel : PageModel
    {
        private readonly SessionManager _sessionManager;
        private readonly StoreService _storeService;
        private readonly IUserService _userService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<EditAssetModel> _logger;

        public UserSession User { get; private set; }
        public bool IsLoggedIn => User != null;
        public string Username => User?.Username ?? string.Empty;
        public UserPermissionType UserPermission => User?.UserPermission ?? UserPermissionType.Normal;
        public decimal Coins => User?.Coins ?? 0m;
        public StoreItem Asset { get; set; }
        public string SuccessMessage { get; set; }
        public string ErrorMessage { get; set; }
        
        public EditAssetModel(
            SessionManager sessionManager,
            StoreService storeService,
            IUserService userService,
            IWebHostEnvironment env,
            ILogger<EditAssetModel> logger)
        {
            _sessionManager = sessionManager;
            _storeService = storeService;
            _userService = userService;
            _env = env;
            _logger = logger;
        }
        
        public IActionResult OnGet(int id)
        {
            User = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (User == null || User.UserPermission < UserPermissionType.Developer)
            {
                return RedirectToPage("/Account/Login");
            }
            
            Asset = _storeService.GetItemById(id);
            
            // 验证用户是否有权限编辑该资产
            if (Asset == null || Asset.AuthorId != User.UserId)
            {
                Asset = null;
                return Page();
            }
            
            return Page();
        }
        
        public IActionResult OnPost(StoreItem updatedAsset, string Tags)
        {
            User = _sessionManager.GetSessionFromCookie<UserSession>("UserAuth");
            if (User == null || User.UserPermission < UserPermissionType.Developer)
            {
                return RedirectToPage("/Account/Login");
            }
            
            // 获取原始资产信息
            var originalAsset = _storeService.GetItemById(updatedAsset.Id);
            
            Logger.Info($"原始资产发布时间: {originalAsset.ReleaseDate}");

            
            // 验证用户是否有权限编辑该资产
            if (originalAsset == null || originalAsset.AuthorId != User.UserId)
            {
                Asset = null;
                ErrorMessage = "您无权编辑此资产或资产不存在。";
                return Page();
            }
            
            // 处理标签（将逗号分隔的字符串转换为列表）
            if (!string.IsNullOrEmpty(Tags))
            {
                updatedAsset.Tags = Tags.Split(',')
                    .Select(t => t.Trim())
                    .Where(t => !string.IsNullOrEmpty(t))
                    .ToList();
            }
            else
            {
                updatedAsset.Tags = new List<string>();
            }
            
            // 处理自定义卡片数据
            try
            {
                updatedAsset.CustomCards = new List<CustomCard>();
                
                // 从表单数据中提取自定义卡片信息
                var customCardKeys = Request.Form.Keys
                    .Where(k => k.StartsWith("customCards[") && k.EndsWith("].Id"))
                    .ToList();
                
                _logger.LogInformation($"找到 {customCardKeys.Count} 个自定义卡片ID");
                foreach (var key in customCardKeys)
                {
                    _logger.LogInformation($"卡片key: {key}, 值: {Request.Form[key]}");
                }
                
                foreach (var cardKey in customCardKeys)
                {
                    var cardIdValue = Request.Form[cardKey].ToString();
                    
                    // 从key中提取卡片索引，例如从"customCards[1].Id"或"customCards[custom-card-1].Id"中提取索引部分
                    var cardIndexMatch = System.Text.RegularExpressions.Regex.Match(cardKey, @"customCards\[([^\]]+)\]");
                    if (!cardIndexMatch.Success) 
                    {
                        _logger.LogWarning($"无法从key '{cardKey}'中提取卡片索引");
                        continue;
                    }
                    
                    var cardIndex = cardIndexMatch.Groups[1].Value;
                    var titleKey = $"customCards[{cardIndex}].Title";
                    
                    _logger.LogInformation($"处理卡片: ID={cardIdValue}, 索引={cardIndex}");
                    
                    var cardTitle = Request.Form.ContainsKey(titleKey)
                        ? Request.Form[titleKey].ToString()
                        : "未命名卡片";
                    
                    var card = new CustomCard 
                    { 
                        Id = cardIdValue,
                        Title = cardTitle,
                        Elements = new List<CustomElement>()
                    };
                    
                    // 获取当前卡片的所有元素
                    var elementKeys = Request.Form.Keys
                        .Where(k => k.StartsWith($"customCards[{cardIndex}].elements[") && 
                                  (k.EndsWith("].type") || k.EndsWith("].value") || k.EndsWith("].id") || k.EndsWith("].label")))
                        .ToList();
                    
                    _logger.LogInformation($"卡片'{cardTitle}'中找到 {elementKeys.Count} 个元素相关的键");
                    
                    // 先收集所有元素ID
                    var elementIds = new HashSet<string>();
                    foreach (var elementKey in elementKeys)
                    {
                        var elementIdMatch = System.Text.RegularExpressions.Regex.Match(elementKey, @"elements\[([^\]]+)\]");
                        if (elementIdMatch.Success)
                        {
                            elementIds.Add(elementIdMatch.Groups[1].Value);
                        }
                    }
                    
                    _logger.LogInformation($"卡片'{cardTitle}'中找到 {elementIds.Count} 个唯一元素ID");
                    
                    // 对每个元素ID处理其所有属性
                    foreach (var elementId in elementIds)
                    {
                        var typeKey = $"customCards[{cardIndex}].elements[{elementId}].type";
                        var valueKey = $"customCards[{cardIndex}].elements[{elementId}].value";
                        var labelKey = $"customCards[{cardIndex}].elements[{elementId}].label";
                        
                        if (!Request.Form.ContainsKey(typeKey))
                        {
                            _logger.LogWarning($"元素{elementId}缺少类型信息，跳过");
                            continue;
                        }
                        
                        var elementType = Request.Form[typeKey].ToString();
                        var elementValue = Request.Form.ContainsKey(valueKey) ? Request.Form[valueKey].ToString() : "";
                        
                        _logger.LogInformation($"处理元素: ID={elementId}, 类型={elementType}, 值={elementValue}");
                        
                        var element = new CustomElement
                        {
                            Id = elementId,
                            Type = elementType,
                            Value = elementValue
                        };
                        
                        // 对于滑块和链接类型，可能还有一个额外的label属性
                        if ((elementType == "slider" || elementType == "link") && Request.Form.ContainsKey(labelKey))
                        {
                            element.Label = Request.Form[labelKey].ToString();
                            _logger.LogInformation($"元素'{elementId}'包含标签: {element.Label}");
                        }
                        
                        card.Elements.Add(element);
                    }
                    
                    updatedAsset.CustomCards.Add(card);
                }
                
                _logger.LogInformation($"处理了 {updatedAsset.CustomCards.Count} 个自定义卡片，包含 {updatedAsset.CustomCards.Sum(c => c.Elements.Count)} 个元素");
                
                // 处理提示信息数据
                updatedAsset.Hints = new List<Hint>();
                
                // 从表单数据中提取提示信息
                var hintKeys = Request.Form.Keys
                    .Where(k => k.StartsWith("hints[") && k.EndsWith("].Id"))
                    .ToList();
                
                _logger.LogInformation($"找到 {hintKeys.Count} 个提示信息ID");
                
                foreach (var hintKey in hintKeys)
                {
                    var hintIdValue = Request.Form[hintKey].ToString();
                    
                    // 从key中提取提示索引
                    var hintIndexMatch = System.Text.RegularExpressions.Regex.Match(hintKey, @"hints\[([^\]]+)\]");
                    if (!hintIndexMatch.Success) 
                    {
                        _logger.LogWarning($"无法从key '{hintKey}'中提取提示索引");
                        continue;
                    }
                    
                    var hintIndex = hintIndexMatch.Groups[1].Value;
                    var typeKey = $"hints[{hintIndex}].Type";
                    var contentKey = $"hints[{hintIndex}].Content";
                    
                    _logger.LogInformation($"处理提示: ID={hintIdValue}, 索引={hintIndex}");
                    
                    var hintType = Request.Form.ContainsKey(typeKey)
                        ? Request.Form[typeKey].ToString()
                        : "Info";
                    
                    var hintContent = Request.Form.ContainsKey(contentKey)
                        ? Request.Form[contentKey].ToString()
                        : string.Empty;
                    
                    var hint = new Hint
                    {
                        Id = hintIdValue,
                        Type = hintType,
                        Content = hintContent
                    };
                    
                    updatedAsset.Hints.Add(hint);
                }
                
                _logger.LogInformation($"处理了 {updatedAsset.Hints.Count} 个提示信息");
            }
            catch (Exception ex)
            {
                _logger.LogError($"处理自定义卡片或提示信息时出错: {ex.Message}");
                _logger.LogError($"异常堆栈: {ex.StackTrace}");
                ErrorMessage = $"处理自定义卡片或提示信息时出错: {ex.Message}";
                Asset = originalAsset;
                return Page();
            }
            
            // 保留原始资产中不应更新的字段
            updatedAsset.AuthorId = originalAsset.AuthorId;
            updatedAsset.AuthorName = originalAsset.AuthorName;
            updatedAsset.PurchaseCount = originalAsset.PurchaseCount;
            updatedAsset.ReviewCount = originalAsset.ReviewCount;
            
            try
            {
                // 保存更新后的资产
                _logger.LogInformation($"开始保存资产: ID={updatedAsset.Id}, 自定义卡片数量={updatedAsset.CustomCards.Count}, 提示信息数量={updatedAsset.Hints.Count}");
                updatedAsset.SaveToXml(_env.ContentRootPath);
                _logger.LogInformation($"资产保存成功");
                
                // 重新加载资产以显示更新后的内容
                Asset = _storeService.GetItemById(updatedAsset.Id);
                _logger.LogInformation($"重新加载资产: ID={Asset.Id}, 自定义卡片数量={Asset.CustomCards.Count}, 提示信息数量={Asset.Hints.Count}");
                
                SuccessMessage = "资产更新成功！";
            }
            catch (Exception ex)
            {
                Asset = originalAsset;
                ErrorMessage = $"更新资产时发生错误: {ex.Message}";
                _logger.LogError($"更新资产时发生错误: {ex}");
                _logger.LogError($"异常堆栈: {ex.StackTrace}");
            }
            
            return Page();
        }
    }
} 