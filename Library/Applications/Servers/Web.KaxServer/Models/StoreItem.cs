using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using Drx.Sdk.Network.DataBase;

namespace Web.KaxServer.Models
{
    public class StoreItem : IXmlSerializable, IIndexable
    {
        public int Id { get; set; }

        [Required]
        public string Title { get; set; } = string.Empty;

        public string ShortDescription { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [DataType(DataType.Currency)]
        public decimal MonthlyPrice { get; set; }

        public int AuthorId { get; set; }
        public string AuthorName { get; set; } = "匿名";

        [Range(0, 5)]
        public double Rating { get; set; }

        public string ImageUrl { get; set; } = string.Empty;

        public string DownloadUrl { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;
        
        public string Game { get; set; } = string.Empty;

        public int PurchaseCount { get; set; }

        public int ReviewCount { get; set; }

        public decimal Price { get; set; }
        public int Stock { get; set; } // -1 for unlimited
        public bool IsFeatured { get; set; }
        public int DurationDays { get; set; }
        public List<string> Tags { get; set; } = new List<string>();

        // 详细信息字段
        public string Version { get; set; } = string.Empty;
        public string ReleaseDate { get; set; } = string.Empty;
        public string LastUpdated { get; set; } = string.Empty;
        public string RepositoryUrl { get; set; } = string.Empty;
        public string SupportUrl { get; set; } = string.Empty;
        
        // 自定义卡片
        public List<CustomCard> CustomCards { get; set; } = new List<CustomCard>();
        
        // 提示信息
        public List<Hint> Hints { get; set; } = new List<Hint>();

        string IIndexable.Id => throw new NotImplementedException();

        // 从XML文件读取所有商品基本信息

        public static List<StoreItem> LoadFromXml(string basePath)
        {
            var filePath = Path.Combine(basePath, "shop", "shopitems.xml");
            if (!File.Exists(filePath))
                return new List<StoreItem>();

            var doc = XDocument.Load(filePath);
            var items = new List<StoreItem>();

            foreach (var itemElement in doc.Root.Elements("Item"))
            {
                var item = new StoreItem
                {
                    Id = int.Parse(itemElement.Element("Id")?.Value ?? "0"),
                    Title = itemElement.Element("Title")?.Value ?? string.Empty,
                    ShortDescription = itemElement.Element("ShortDescription")?.Value ?? string.Empty,
                    MonthlyPrice = decimal.Parse(itemElement.Element("MonthlyPrice")?.Value ?? "0"),
                    AuthorId = int.Parse(itemElement.Element("AuthorId")?.Value ?? "0"),
                    Rating = double.Parse(itemElement.Element("Rating")?.Value ?? "0"),
                    Category = itemElement.Element("Category")?.Value ?? string.Empty,
                    Game = itemElement.Element("Game")?.Value ?? string.Empty,
                    ImageUrl = itemElement.Element("ImageUrl")?.Value ?? string.Empty,
                    DownloadUrl = itemElement.Element("DownloadUrl")?.Value ?? string.Empty,
                    Tags = itemElement.Element("Tags")?.Elements("Tag").Select(t => t.Value).ToList() ?? new List<string>(),
                    DurationDays = int.Parse(itemElement.Element("DurationDays")?.Value ?? "0"),
                    // 加载详细信息
                    Version = itemElement.Element("Version")?.Value ?? string.Empty,
                    ReleaseDate = itemElement.Element("ReleaseDate")?.Value ?? string.Empty,
                    LastUpdated = itemElement.Element("LastUpdated")?.Value ?? string.Empty,
                    RepositoryUrl = itemElement.Element("RepositoryUrl")?.Value ?? string.Empty,
                    SupportUrl = itemElement.Element("SupportUrl")?.Value ?? string.Empty,
                    PurchaseCount = int.Parse(itemElement.Element("PurchaseCount")?.Value ?? "0")
                };

                items.Add(item);
            }

            return items;
        }

        // 加载指定商品的详细信息
        public static StoreItem LoadWithDetails(string basePath, int itemId)
        {
            var items = LoadFromXml(basePath);
            var item = items.FirstOrDefault(i => i.Id == itemId);
            
            if (item == null)
                return null;

            var detailsPath = Path.Combine(basePath, "shop", "item-details.xml");
            if (File.Exists(detailsPath))
            {
                try
                {
                    var detailsDoc = XDocument.Load(detailsPath);
                    var itemDetail = detailsDoc.Root?.Elements("ItemDetail")
                        .FirstOrDefault(e => e.Attribute("ItemId")?.Value == itemId.ToString());

                    if (itemDetail != null)
                    {
                        item.Description = itemDetail.Element("Description")?.Value?.Trim() ?? string.Empty;
                        
                        // 加载自定义卡片
                        item.CustomCards = new List<CustomCard>(); // 确保列表为新实例
                        var customCardsElement = itemDetail.Element("CustomCards");
                        if (customCardsElement != null)
                        {
                            foreach (var cardElement in customCardsElement.Elements("CustomCard"))
                            {
                                var card = CustomCard.FromXml(cardElement);
                                if (card != null)
                                {
                                    item.CustomCards.Add(card);
                                }
                            }
                        }
                        
                        // 加载提示信息
                        item.Hints = new List<Hint>();
                        var hintsElement = itemDetail.Element("Hints");
                        if (hintsElement != null)
                        {
                            foreach (var hintElement in hintsElement.Elements("Hint"))
                            {
                                var hint = Hint.FromXml(hintElement);
                                if (hint != null)
                                {
                                    item.Hints.Add(hint);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 加载失败时记录错误并返回基本商品信息
                    System.Diagnostics.Debug.WriteLine($"加载商品详情时出错: {ex.Message}");
                    return item;
                }
            }

            return item;
        }

        // 保存当前商品到XML文件
        public void SaveToXml(string basePath)
        {
            var items = LoadFromXml(basePath);
            
            // 查找并更新已存在的商品，或添加新商品
            var existingItem = items.FirstOrDefault(i => i.Id == this.Id);
            if (existingItem != null)
            {
                items.Remove(existingItem);
            }
            
            items.Add(this);
            
            SaveItemsToXml(basePath, items);
            SaveDetailsToXml(basePath);
        }

        // 保存所有商品到XML文件
        public static void SaveItemsToXml(string basePath, List<StoreItem> items)
        {
            var shopDir = Path.Combine(basePath, "shop");
            if (!Directory.Exists(shopDir))
                Directory.CreateDirectory(shopDir);

            var filePath = Path.Combine(shopDir, "shopitems.xml");
            
            var doc = new XDocument(
                new XDeclaration("1.0", "utf-8", null),
                new XElement("ShopItems",
                    items.Select(item => new XElement("Item",
                        new XElement("Id", item.Id),
                        new XElement("Title", item.Title),
                        new XElement("ShortDescription", item.ShortDescription),
                        new XElement("MonthlyPrice", item.MonthlyPrice),
                        new XElement("AuthorId", item.AuthorId),
                        new XElement("Rating", item.Rating),
                        new XElement("Category", item.Category),
                        new XElement("Game", item.Game),
                        new XElement("ImageUrl", item.ImageUrl),
                        new XElement("DownloadUrl", item.DownloadUrl),
                        new XElement("Tags", item.Tags.Select(tag => new XElement("Tag", tag))),
                        new XElement("DurationDays", item.DurationDays),
                        // 保存详细信息
                        new XElement("Version", item.Version),
                        new XElement("ReleaseDate", item.ReleaseDate),
                        new XElement("LastUpdated", item.LastUpdated),
                        new XElement("RepositoryUrl", item.RepositoryUrl),
                        new XElement("SupportUrl", item.SupportUrl)
                    ))
                )
            );

            doc.Save(filePath);
        }

        // 保存当前商品详情到XML文件
        private void SaveDetailsToXml(string basePath)
        {
            var detailsPath = Path.Combine(basePath, "shop", "item-details.xml");
            XDocument doc;

            try
            {
                // 确保目录存在
                var directory = Path.GetDirectoryName(detailsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 尝试加载现有的XML文件
                if (File.Exists(detailsPath))
                {
                    try
                    {
                        doc = XDocument.Load(detailsPath);
                        
                        // 查找并移除已存在的详情
                        var existingDetail = doc.Root?.Elements("ItemDetail")
                            .FirstOrDefault(e => e.Attribute("ItemId")?.Value == Id.ToString());
                        
                        if (existingDetail != null)
                        {
                            existingDetail.Remove();
                        }
                    }
                    catch (Exception ex)
                    {
                        // 如果XML文件损坏，创建新的文档
                        System.Diagnostics.Debug.WriteLine($"加载详情XML文件时出错: {ex.Message}，将创建新文件");
                        doc = new XDocument(
                            new XDeclaration("1.0", "utf-8", null),
                            new XElement("ItemDetails")
                        );
                    }
                }
                else
                {
                    doc = new XDocument(
                        new XDeclaration("1.0", "utf-8", null),
                        new XElement("ItemDetails")
                    );
                }

                // 创建XML结构
                var customCardsElement = new XElement("CustomCards");
                
                // 添加自定义卡片数据（如果有）
                if (CustomCards != null)
                {
                    System.Diagnostics.Debug.WriteLine($"保存商品{Id}的{CustomCards.Count}个自定义卡片");
                    foreach (var card in CustomCards)
                    {
                        if (card != null && !string.IsNullOrEmpty(card.Id))
                        {
                            customCardsElement.Add(card.ToXml());
                            System.Diagnostics.Debug.WriteLine($"添加卡片 ID={card.Id}, 标题={card.Title}, 元素数量={card.Elements?.Count ?? 0}");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"跳过无效卡片: ID={card?.Id ?? "null"}, 标题={card?.Title ?? "null"}");
                        }
                    }
                }
                
                // 创建提示信息元素
                var hintsElement = new XElement("Hints");
                
                // 添加提示信息数据（如果有）
                if (Hints != null)
                {
                    System.Diagnostics.Debug.WriteLine($"保存商品{Id}的{Hints.Count}个提示信息");
                    foreach (var hint in Hints)
                    {
                        if (hint != null && !string.IsNullOrEmpty(hint.Id))
                        {
                            hintsElement.Add(hint.ToXml());
                            System.Diagnostics.Debug.WriteLine($"添加提示 ID={hint.Id}, 类型={hint.Type}, 内容={hint.Content?.Substring(0, Math.Min(20, hint.Content?.Length ?? 0))}...");
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"跳过无效提示: ID={hint?.Id ?? "null"}");
                        }
                    }
                }
                
                var detailElement = new XElement("ItemDetail",
                    new XAttribute("ItemId", Id),
                    new XElement("Description", Description ?? string.Empty),
                    customCardsElement,
                    hintsElement,
                    new XElement("Reviews", string.Empty)
                );

                // 如果有评论，应该保留之前的评论
                if (File.Exists(detailsPath))
                {
                    try
                    {
                        var oldDoc = XDocument.Load(detailsPath);
                        var oldDetail = oldDoc.Root?.Elements("ItemDetail")
                            .FirstOrDefault(e => e.Attribute("ItemId")?.Value == Id.ToString());
                        
                        if (oldDetail != null)
                        {
                            var oldReviews = oldDetail.Element("Reviews");
                            if (oldReviews != null && oldReviews.HasElements)
                            {
                                detailElement.Element("Reviews").ReplaceWith(oldReviews);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"加载旧评论时出错: {ex.Message}");
                    }
                }

                // 添加到文档并保存
                if (doc.Root == null)
                {
                    doc.Add(new XElement("ItemDetails"));
                }
                
                doc.Root.Add(detailElement);
                
                try
                {
                    // 确保目录存在
                    Directory.CreateDirectory(Path.GetDirectoryName(detailsPath));
                    doc.Save(detailsPath);
                    System.Diagnostics.Debug.WriteLine($"成功保存商品{Id}的详情到{detailsPath}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"保存详情XML文件时出错: {ex.Message}");
                    throw;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存商品详情时发生严重错误: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"错误堆栈: {ex.StackTrace}");
                throw new Exception($"保存商品详情时发生错误: {ex.Message}", ex);
            }
        }

        public void WriteToXml(IXmlNode node)
        {
            node.PushDecimal("data", "Id", Id);
            node.PushString("data", "Title", Title);
            node.PushString("data", "ShortDescription", ShortDescription);
            node.PushString("data", "Description", Description);
            node.PushDecimal("data", "MonthlyPrice", MonthlyPrice);
            node.PushDecimal("data", "AuthorId", AuthorId);
            node.PushFloat("data", "Rating", (float)Rating);
            node.PushString("data", "Category", Category);
            node.PushString("data", "Game", Game);
            node.PushString("data", "ImageUrl", ImageUrl);
            node.PushString("data", "DownloadUrl", DownloadUrl);
            node.PushString("data", "Tags", string.Join(",", Tags));
            node.PushInt("data", "DurationDays", DurationDays);
            node.PushString("data", "Version", Version);
            node.PushString("data", "ReleaseDate", ReleaseDate);
            node.PushString("data", "LastUpdated", LastUpdated);
            node.PushString("data", "RepositoryUrl", RepositoryUrl);
            node.PushString("data", "SupportUrl", SupportUrl);
            node.PushString("data", "PurchaseCount", PurchaseCount.ToString());
            node.PushString("data", "ReviewCount", ReviewCount.ToString());
            node.PushDecimal("data", "Price", Price);
            node.PushInt("data", "Stock", Stock);
            node.PushBool("data", "IsFeatured", IsFeatured);
            node.PushString("data", "CustomCards", string.Join(",", CustomCards.Select(c => c.ToXml())));
        }

        public void ReadFromXml(IXmlNode node)
        {
            throw new NotImplementedException();
        }

    }
} 