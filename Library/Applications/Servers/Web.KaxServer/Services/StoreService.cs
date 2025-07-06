using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Web.KaxServer.Models;
using System;
using System.Globalization;
using Microsoft.Extensions.Logging;
using Drx.Sdk.Network.DataBase;

namespace Web.KaxServer.Services
{
    public class StoreService
    {
        public enum UpdateResult
        {
            Success,
            NotFound,
            NoChange,
            Error
        }

        private readonly string _itemsPath;
        private readonly string _detailsPath;
        private List<StoreItem> _items;
        private Dictionary<int, ItemDetail> _details;
        private readonly IUserService _userService;
        private readonly string _contentRootPath;
        private readonly ILogger<StoreService> _logger;
        private readonly object _lock = new object();
        private readonly IndexedRepository<StoreItem> _itemsRepository;

        // A simple class to hold both description and reviews, matching the new XML structure
        private class ItemDetail
        {
            public string Description { get; set; }
            public List<Review> Reviews { get; set; } = new List<Review>();
        }

        public StoreService(string contentRootPath, IUserService userService, ILogger<StoreService> logger)
        {
            _contentRootPath = contentRootPath;
            _itemsPath = Path.Combine(contentRootPath, "shop", "shopitems.xml");
            _detailsPath = Path.Combine(contentRootPath, "shop", "item-details.xml");
            _userService = userService;
            _logger = logger;
        }

        private void LoadDetails()
        {
            if (_details == null)
            {
                var xdoc = XDocument.Load(_detailsPath);
                _details = xdoc.Descendants("ItemDetail").ToDictionary(
                    detail => (int)detail.Attribute("ItemId"),
                    detail => new ItemDetail
                    {
                        Description = detail.Element("Description")?.Value ?? string.Empty,
                        Reviews = detail.Descendants("Review").Select(r => new Review
                        {
                            // ItemId is now the key of the dictionary, no need to store it in the Review object itself
                            ReviewerName = (string)r.Element("ReviewerName"),
                            ReviewerPermission = UserPermissionTypeExtensions.ParseFromString((string)r.Element("ReviewerPermission")),
                            Rating = (int)r.Element("Rating"),
                            Comment = (string)r.Element("Comment"),
                            Date = DateTime.Parse(r.Element("Date")?.Value ?? DateTime.MinValue.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal)
                        }).ToList()
                    }
                );
            }
        }

        public List<StoreItem> GetAllItems()
        {
            lock (_lock)
            {
                if (!File.Exists(_itemsPath))
                {
                    _logger.LogWarning("Shop items XML file not found at path: {path}", _itemsPath);
                    return new List<StoreItem>();
                }

                var xdoc = XDocument.Load(_itemsPath);
                
                LoadDetails();
                
                var result = xdoc.Descendants("Item").Select(item =>
                {
                    var id = (int)item.Element("Id");
                    var authorId = (int?)item.Element("AuthorId") ?? 0;
                    var authorName = _userService.GetUsernameById(authorId);
                    
                    ItemDetail detail = null;
                    _details.TryGetValue(id, out detail);
                    
                    var tags = item.Element("Tags")?.Elements("Tag")?.Select(tag => tag.Value).ToList() ?? new List<string>();
                    
                    return new StoreItem
                    {
                        Id = id,
                        Title = (string)item.Element("Title"),
                        ShortDescription = (string)item.Element("ShortDescription"),
                        MonthlyPrice = (decimal)item.Element("MonthlyPrice"),
                        Rating = (int)item.Element("Rating"),
                        Category = (string)item.Element("Category"),
                        Game = (string)item.Element("Game"),
                        ImageUrl = (string)item.Element("ImageUrl"),
                        DownloadUrl = (string)item.Element("DownloadUrl") ?? string.Empty,
                        Description = detail?.Description ?? string.Empty,
                        AuthorId = authorId,
                        AuthorName = authorName,
                        Version = (string)item.Element("Version") ?? string.Empty,
                        ReleaseDate = (string)item.Element("ReleaseDate") ?? string.Empty,
                        LastUpdated = (string)item.Element("LastUpdated") ?? string.Empty,
                        RepositoryUrl = (string)item.Element("RepositoryUrl") ?? string.Empty,
                        SupportUrl = (string)item.Element("SupportUrl") ?? string.Empty,
                        Tags = tags
                    };
                }).ToList();
                
                return result;
            }
        }

        public StoreItem GetItemById(int id)
        {
            // 使用LoadWithDetails方法加载完整的商品数据，包括自定义卡片
            var item = StoreItem.LoadWithDetails(_contentRootPath, id);
            
            // 如果找到了商品，设置作者名称
            if (item != null)
            {
                item.AuthorName = _userService.GetUsernameById(item.AuthorId);
            }
            
            _logger.LogInformation($"GetItemById({id}): 找到商品={item != null}, 自定义卡片数量={item?.CustomCards?.Count ?? 0}");
            
            return item;
        }
        
        public List<Review> GetReviewsByItemId(int itemId)
        {
            LoadDetails();
            return _details.TryGetValue(itemId, out var detail) ? detail.Reviews : new List<Review>();
        }

        public void AddReview(Review review)
        {
            LoadDetails();
            var xdoc = XDocument.Load(_detailsPath);

            var itemDetailElement = xdoc.Descendants("ItemDetail")
                                        .FirstOrDefault(d => (int)d.Attribute("ItemId") == review.ItemId);

            var newReviewElement = new XElement("Review",
                new XElement("ReviewerName", review.ReviewerName),
                new XElement("ReviewerPermission", review.ReviewerPermission.ToString()),
                new XElement("Rating", review.Rating),
                new XElement("Comment", review.Comment),
                new XElement("Date", review.Date.ToString("o")) // ISO 8601 format
            );

            if (itemDetailElement != null)
            {
                var reviewsElement = itemDetailElement.Element("Reviews");
                if (reviewsElement == null)
                {
                    reviewsElement = new XElement("Reviews");
                    itemDetailElement.Add(reviewsElement);
                }
                reviewsElement.Add(newReviewElement);
            }
            else
            {
                // If item has no details entry yet, create one
                itemDetailElement = new XElement("ItemDetail",
                    new XAttribute("ItemId", review.ItemId),
                    new XElement("Description", ""), // No description to add here
                    new XElement("Reviews", newReviewElement)
                );
                xdoc.Root.Add(itemDetailElement);
            }

            xdoc.Save(_detailsPath);

            // Invalidate the cache
            _details = null;
        }

        public void UpdateItem(StoreItem itemToUpdate)
        {
            itemToUpdate.LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-dd");
            itemToUpdate.SaveToXml(_contentRootPath);
            // Invalidate caches to force a reload on the next request
            _items = null;
            _details = null;
        }

        public StoreItem CreateItem(StoreItem newItem)
        {
            var allItems = GetAllItems();
            var newId = allItems.Any() ? allItems.Max(i => i.Id) + 1 : 1;
            newItem.Id = newId;

            // 设置默认值
            newItem.Rating = 0;
            newItem.PurchaseCount = 0;
            newItem.ReviewCount = 0;
            newItem.ReleaseDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            newItem.LastUpdated = DateTime.UtcNow.ToString("yyyy-MM-dd");
            
            newItem.SaveToXml(_contentRootPath);
            
            // 使缓存失效
            _items = null;
            _details = null;

            return newItem;
        }

        public UpdateResult UpdateItemVersion(int itemId, string newVersion)
        {
            try
            {
                if (!File.Exists(_itemsPath))
                {
                    return UpdateResult.NotFound;
                }

                var xdoc = XDocument.Load(_itemsPath);
                var itemElement = xdoc.Descendants("Item")
                                      .FirstOrDefault(el => (int)el.Element("Id") == itemId);

                if (itemElement == null)
                {
                    return UpdateResult.NotFound;
                }

                var versionElement = itemElement.Element("Version");

                // If version exists and is the same, no change needed.
                if (versionElement != null && versionElement.Value == newVersion)
                {
                    return UpdateResult.NoChange;
                }

                if (versionElement != null)
                {
                    versionElement.Value = newVersion;
                }
                else
                {
                    // If version element doesn't exist, add it after "Tags" for consistency.
                    var tagsElement = itemElement.Element("Tags");
                    if (tagsElement != null)
                    {
                        tagsElement.AddAfterSelf(new XElement("Version", newVersion));
                    }
                    else // Fallback if Tags element is missing
                    {
                        itemElement.Add(new XElement("Version", newVersion));
                    }
                }

                xdoc.Save(_itemsPath);

                // Invalidate cache so it will be reloaded on next request
                _items = null;

                return UpdateResult.Success;
            }
            catch (Exception) // In a real application, you should log the exception
            {
                return UpdateResult.Error;
            }
        }
    }
} 