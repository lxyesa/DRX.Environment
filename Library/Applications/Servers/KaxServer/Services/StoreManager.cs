using Drx.Sdk.Network.DataBase.Sqlite;
using DRX.Framework;

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
}