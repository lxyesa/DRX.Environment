using System;
using System.Collections.Generic;
using System.Linq;

namespace Drx.Sdk.Network.Sqlite.Examples
{
    /// <summary>
    /// 示例代码：展示如何使用Repair方法
    /// </summary>
    public class RepairExample
    {
        /// <summary>
        /// 演示如何使用Sqlite类的Repair方法
        /// </summary>
        public void DemoSqliteRepair()
        {
            string dbPath = "example.db";
            var userDb = new Sqlite<ExampleUser>(dbPath);
            
            // 创建一个新用户
            var user = new ExampleUser
            {
                Username = "testuser",
                Email = "test@example.com"
            };
            userDb.Save(user);
            
            // 稍后，需要更新用户信息
            var updatedUser = new ExampleUser
            {
                Username = "testuser",  // 相同的用户名用于识别
                Email = "updated@example.com"  // 更新的邮箱
            };
            
            // 使用Repair方法，根据用户名查找并更新用户
            bool found = userDb.Repair(updatedUser, "Username", "testuser");
            
            Console.WriteLine($"用户是否找到并更新: {found}");
            
            // 也可以使用多个条件进行查找
            var anotherUser = new ExampleUser
            {
                Username = "another",
                Email = "another@example.com"
            };
            
            var conditions = new Dictionary<string, object>
            {
                { "Username", "another" },
                { "Email", "old@example.com" }
            };
            
            // 如果找不到匹配的条目，会创建一个新的
            bool foundAnother = userDb.Repair(anotherUser, conditions);
            
            Console.WriteLine($"另一个用户是否找到并更新: {foundAnother}");
        }
        
        /// <summary>
        /// 演示如何使用SqliteRelationship类的Repair方法
        /// </summary>
        public void DemoRelationshipRepair()
        {
            string dbPath = "example.db";
            var userDb = new Sqlite<ExampleUser>(dbPath);
            var relationDb = new SqliteRelationship(dbPath);
            
            // 获取用户
            var user = userDb.ReadSingle("Username", "testuser");
            if (user == null)
            {
                // 创建用户
                user = new ExampleUser
                {
                    Username = "testuser",
                    Email = "test@example.com"
                };
                userDb.Save(user);
            }
            
            // 添加或更新用户资产
            var asset = new UserAsset
            {
                UserId = user.Id,
                AssetId = 101,  // 用AssetId作为唯一标识符
                PurchaseDate = DateTime.Now,
                IsActive = true
            };
            
            // 使用RepairRelationshipItem方法，根据AssetId查找并更新资产
            relationDb.RepairRelationshipItem(user.Id, asset, "UserId", "AssetId");
            
            // 批量修复多个资产
            var assets = new List<UserAsset>
            {
                new UserAsset
                {
                    AssetId = 101,  // 已存在的资产，将被更新
                    PurchaseDate = DateTime.Now.AddDays(1),
                    IsActive = false
                },
                new UserAsset
                {
                    AssetId = 102,  // 可能是新资产
                    PurchaseDate = DateTime.Now,
                    IsActive = true
                }
            };
            
            // 使用RepairRelationship方法，根据AssetId查找并更新资产
            relationDb.RepairRelationship<ExampleUser, UserAsset>(
                user.Id, assets, "UserAssets", "UserId", "AssetId");
                
            // 查询所有用户资产
            var userAssets = relationDb.LoadRelationship<UserAsset>(user.Id, "UserAssets", "UserId");
            
            Console.WriteLine($"用户资产数量: {userAssets.Count}");
            foreach (var a in userAssets)
            {
                Console.WriteLine($"资产ID: {a.AssetId}, 激活状态: {a.IsActive}, 购买日期: {a.PurchaseDate}");
            }
        }
        
        /// <summary>
        /// 演示在实际应用中如何使用Repair方法
        /// </summary>
        public void RealWorldExample()
        {
            // 假设这是一个用户登录并更新机械码的场景
            string dbPath = "production.db";
            var userDb = new Sqlite<ExampleUser>(dbPath);
            var relationDb = new SqliteRelationship(dbPath);
            
            // 1. 根据用户名查找用户
            var user = userDb.ReadSingle("Username", "customer123");
            if (user == null)
            {
                Console.WriteLine("用户不存在");
                return;
            }
            
            // 2. 更新用户的机械码
            // 假设我们收到了新的机械码，需要更新或添加
            int assetId = 201;
            string newMachineCode = "ABC-DEF-GHI-JKL";
            
            // 创建机械码对象
            var mcaCode = new UserCode
            {
                UserId = user.Id,
                CodeType = assetId,  // 使用资产ID作为CodeType
                CodeValue = newMachineCode
            };
            
            // 使用RepairRelationshipItem方法，根据CodeType查找并更新机械码
            relationDb.RepairRelationshipItem(user.Id, mcaCode, "UserId", "CodeType");
            
            Console.WriteLine($"用户 {user.Username} 的资产 {assetId} 机械码已更新为 {newMachineCode}");
            
            // 3. 查询用户的所有机械码
            var userCodes = relationDb.LoadRelationship<UserCode>(user.Id, "UserCodes", "UserId");
            
            Console.WriteLine($"用户 {user.Username} 的机械码数量: {userCodes.Count}");
            foreach (var code in userCodes)
            {
                Console.WriteLine($"资产ID: {code.CodeType}, 机械码: {code.CodeValue}");
            }
        }
    }
} 