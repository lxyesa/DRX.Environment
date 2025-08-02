using Drx.Sdk.Network.DataBase.Sqlite;
using DRX.Framework;
using KaxServer.Models;
using KaxServer.Services;

public static class StoreManager
{
    public static readonly SqliteUnified<StoreItem> StoreItemSqlite;

    static StoreManager()
    {
        var dbDirectory = Path.Combine(AppContext.BaseDirectory, "data");
        if (!Directory.Exists(dbDirectory))
        {
            Directory.CreateDirectory(dbDirectory);
        }

        StoreItemSqlite =
            new SqliteUnified<StoreItem>(Path.Combine(dbDirectory, "store.db"));
    }

    public static async Task<bool> CreateStoreItemAsync(string title, string description, int ownerId, int itemID)
    {
        var storeItem = new StoreItem
        {
            Id = itemID,
            Title = title,
            OwnerId = ownerId,
            Description = description,
            StoreItemPrices = new List<StoreItemPrice>
            {
                new StoreItemPrice
                {
                    Title = $"{title}-月租包",
                    Price = 100,
                    Rebate = 1,
                    Duration = 1,
                    DurationUnit = StoreItemDurationUnit.Month,
                }
            },
            StoreItemDetails = new StoreItemDetail
            {
                Content = "商品详情，请商品所有者自行编辑"
            }
        };

        try
        {
            await StoreItemSqlite.PushAsync(storeItem);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"创建商品时发生错误: {ex.ToString()}");
            return false;
        }
    }

    public static async Task<List<StoreItem>> GetAllStoreItemsAsync()
    {
        var items = await StoreItemSqlite.QueryAllAsync();
        return items.ToList();
    }

    public static async Task<StoreItem> GetStoreItemByIdAsync(int id)
    {
        var item = await StoreItemSqlite.QueryByIdAsync(id);
        return item;
    }

    public static async Task<bool> UpdateStoreItemAsync(StoreItem item)
    {
        try
        {
            await StoreItemSqlite.UpdateAsync(item);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"更新商品时发生错误: {ex.ToString()}");
            return false;
        }
    }

    public static async Task<bool> DeleteStoreItemAsync(int id)
    {
        try
        {
            await StoreItemSqlite.DeleteAsync(id);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"删除商品时发生错误: {ex.ToString()}");
            return false;
        }
    }

    public static async Task<bool> ClearStoreItemsAsync()
    {
        try
        {
            await StoreItemSqlite.ClearAsync();
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"清空商品时发生错误: {ex.ToString()}");
            return false;
        }
    }

    public static async Task<BuyResult> BuyItemAsync(UserData user, int itemId, int priceIndex = 0)
    {
        var item = await StoreItemSqlite.QueryByIdAsync(itemId);
        var price = item?.StoreItemPrices[priceIndex].Price * item?.StoreItemPrices[priceIndex].Rebate ?? 0;    // 计算实际价格
        if (item == null)
        {
            return new BuyResult
            {
                Success = false,
                Message = "商品不存在",
                ItemId = itemId,
                UserId = user.Id,
                Price = 0
            };
        }

        if (priceIndex < 0 || priceIndex >= item.StoreItemPrices.Count)
        {
            return new BuyResult
            {
                Success = false,
                Message = "无效的价格索引",
                ItemId = itemId,
                UserId = user.Id,
                Price = 0
            };
        }

        if (user.Coins < price)
        {
            return new BuyResult
            {
                Success = false,
                Message = "余额不足",
                ItemId = itemId,
                UserId = user.Id,
            };
        }

        user.Coins -= (int)price;  // 扣除用户余额
        var existed = user.BuyedStoreItems.FirstOrDefault(x => x.StoreItemId == itemId);
        if (existed != null)
        {
            existed.BuyTime = DateTime.Now;
            existed.ExpireTime = item?.StoreItemPrices[priceIndex].DurationUnit switch
            {
                StoreItemDurationUnit.Day => DateTime.Now.AddDays(item.StoreItemPrices[priceIndex].Duration),
                StoreItemDurationUnit.Month => DateTime.Now.AddMonths((int)item.StoreItemPrices[priceIndex].Duration),
                StoreItemDurationUnit.Year => DateTime.Now.AddYears((int)item.StoreItemPrices[priceIndex].Duration),
                _ => DateTime.MaxValue, // 永不过期
            };
        }
        else
        {
            user.BuyedStoreItems.Add(new UserBuyedStoreItem
            {
                StoreItemId = itemId,
                BuyTime = DateTime.Now,
                ExpireTime = item?.StoreItemPrices[priceIndex].DurationUnit switch
                {
                    StoreItemDurationUnit.Day => DateTime.Now.AddDays(item.StoreItemPrices[priceIndex].Duration),
                    StoreItemDurationUnit.Month => DateTime.Now.AddMonths((int)item.StoreItemPrices[priceIndex].Duration),
                    StoreItemDurationUnit.Year => DateTime.Now.AddYears((int)item.StoreItemPrices[priceIndex].Duration),
                    _ => DateTime.MaxValue, // 永不过期
                }
            });
        }
        await UserManager.UpdateUserAsync(user);

        Logger.Info($"用户 {user.Id} 购买商品 {itemId} 成功，扣除金额: {price}");
        Logger.Info($"现在用户 {user.Id} 拥有的商品数量: {user.BuyedStoreItems.Count}");

        return new BuyResult
        {
            Success = true,
            Message = "购买成功",
            ItemId = itemId,
            UserId = user.Id
        };
    }
}