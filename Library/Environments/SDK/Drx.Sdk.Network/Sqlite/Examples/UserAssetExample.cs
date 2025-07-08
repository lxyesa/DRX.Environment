using System;
using System.Collections.Generic;
using System.Linq;

namespace Drx.Sdk.Network.Sqlite.Examples
{
    /// <summary>
    /// 用户资产实体类，用于替代Dictionary<int, DateTime> OwnedAssets
    /// </summary>
    public class UserAsset : IDataBase
    {
        public int Id { get; set; }
        public int UserId { get; set; }  // 外键关联到User
        public int AssetId { get; set; }
        public DateTime PurchaseDate { get; set; }
        public bool IsActive { get; set; } = true;
    }

    /// <summary>
    /// 用户代码实体类，用于替代Dictionary<int, string> McaCodes
    /// </summary>
    public class UserCode : IDataBase
    {
        public int Id { get; set; }
        public int UserId { get; set; }  // 外键关联到User
        public int CodeType { get; set; }
        public string CodeValue { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 示例用户类
    /// </summary>
    public class ExampleUser : IDataBase
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        
        // 使用SqliteRelation特性标记关联表
        [SqliteRelation("UserAssets", "UserId")]
        public List<UserAsset> Assets { get; set; } = new List<UserAsset>();
        
        [SqliteRelation("UserCodes", "UserId")]
        public List<UserCode> Codes { get; set; } = new List<UserCode>();
    }

    /// <summary>
    /// 示例代码：展示如何使用关联表
    /// </summary>
    public class UserAssetExample
    {
        public void DemoUsage()
        {
            // 初始化数据库
            string dbPath = "example.db";
            var userDb = new Sqlite<ExampleUser>(dbPath);
            var assetDb = new Sqlite<UserAsset>(dbPath);
            var codeDb = new Sqlite<UserCode>(dbPath);
            var relationDb = new SqliteRelationship(dbPath);
            
            // 创建用户
            var user = new ExampleUser
            {
                Username = "testuser",
                Email = "test@example.com"
            };
            userDb.Save(user);
            
            // 添加用户资产
            var asset1 = new UserAsset
            {
                UserId = user.Id,
                AssetId = 101,
                PurchaseDate = DateTime.Now
            };
            var asset2 = new UserAsset
            {
                UserId = user.Id,
                AssetId = 102,
                PurchaseDate = DateTime.Now.AddDays(-1)
            };
            
            // 方式1：单独添加资产
            relationDb.AddRelationshipItem(user.Id, asset1, "UserId");
            relationDb.AddRelationshipItem(user.Id, asset2, "UserId");
            
            // 方式2：批量添加资产
            var assets = new List<UserAsset> { asset1, asset2 };
            relationDb.SaveRelationship<ExampleUser, UserAsset>(user.Id, assets, "UserAssets", "UserId");
            
            // 查询所有用户资产
            var userAssets = relationDb.LoadRelationship<UserAsset>(user.Id, "UserAssets", "UserId");
            
            // 查询特定条件的资产
            var conditions = new Dictionary<string, object>
            {
                { "AssetId", 101 }
            };
            var specificAssets = relationDb.QueryRelationship<UserAsset>(user.Id, conditions, "UserId");
            
            // 更新单个资产
            var assetToUpdate = specificAssets.FirstOrDefault();
            if (assetToUpdate != null)
            {
                assetToUpdate.IsActive = false;
                relationDb.UpdateRelationshipItem(assetToUpdate);
            }
            
            // 删除单个资产
            relationDb.DeleteRelationshipItem(assetToUpdate);
            
            // 添加用户代码
            var code = new UserCode
            {
                UserId = user.Id,
                CodeType = 1,
                CodeValue = "ABC123"
            };
            relationDb.AddRelationshipItem(user.Id, code, "UserId");
        }
        
        /// <summary>
        /// 演示如何将旧的Dictionary数据迁移到关联表
        /// </summary>
        public void MigrateFromDictionary(int userId, Dictionary<int, DateTime> ownedAssets)
        {
            string dbPath = "example.db";
            var relationDb = new SqliteRelationship(dbPath);
            
            // 将Dictionary转换为关联实体
            var assetEntities = ownedAssets.Select(kvp => new UserAsset
            {
                UserId = userId,
                AssetId = kvp.Key,
                PurchaseDate = kvp.Value
            }).ToList();
            
            // 保存到关联表
            relationDb.SaveRelationship<ExampleUser, UserAsset>(userId, assetEntities, "UserAssets", "UserId");
        }
    }
} 