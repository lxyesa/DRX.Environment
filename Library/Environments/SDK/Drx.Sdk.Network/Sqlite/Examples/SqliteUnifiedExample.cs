using System;
using System.Collections.Generic;
using Drx.Sdk.Network.Sqlite;

namespace Drx.Sdk.Network.Sqlite.Examples
{
    /// <summary>
    /// 用户类示例 - 主表
    /// </summary>
    public class User : IDataBase
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // 关联表属性 - 用户的订单
        [SqliteRelation("Order", "UserId")]
        public List<Order> Orders { get; set; } = new List<Order>();

        // 关联表属性 - 用户的地址
        [SqliteRelation("Address", "UserId")]
        public Address[] Addresses { get; set; } = Array.Empty<Address>();

        // 忽略的属性 - 不会保存到数据库
        [SqliteIgnore]
        public string TemporaryData { get; set; } = string.Empty;
    }

    /// <summary>
    /// 订单类示例 - 关联表
    /// </summary>
    public class Order : IDataBase
    {
        public int Id { get; set; }
        public int UserId { get; set; }  // 外键
        public string ProductName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.Now;
    }

    /// <summary>
    /// 地址类示例 - 关联表
    /// </summary>
    public class Address : IDataBase
    {
        public int Id { get; set; }
        public int UserId { get; set; }  // 外键
        public string Street { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string Country { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }

    /// <summary>
    /// SqliteUnified 使用示例
    /// </summary>
    public class SqliteUnifiedExample
    {
        private readonly SqliteUnified<User> _userDb;

        public SqliteUnifiedExample(string databasePath)
        {
            _userDb = new SqliteUnified<User>(databasePath);
        }

        /// <summary>
        /// 基础操作示例
        /// </summary>
        public void BasicOperationsExample()
        {
            // 创建用户
            var user = new User
            {
                Id = 1,
                Username = "john_doe",
                Email = "john@example.com"
            };

            // 添加订单
            user.Orders.Add(new Order
            {
                Id = 1,
                UserId = 1,
                ProductName = "Laptop",
                Amount = 999.99m
            });

            user.Orders.Add(new Order
            {
                Id = 2,
                UserId = 1,
                ProductName = "Mouse",
                Amount = 29.99m
            });

            // 添加地址
            user.Addresses = new[]
            {
                new Address
                {
                    Id = 1,
                    UserId = 1,
                    Street = "123 Main St",
                    City = "New York",
                    Country = "USA",
                    IsDefault = true
                }
            };

            // 保存用户（会自动保存关联的订单和地址）
            _userDb.Save(user);

            // 查询用户（会自动加载关联的订单和地址）
            var loadedUser = _userDb.FindById(1);
            Console.WriteLine($"用户: {loadedUser?.Username}, 订单数: {loadedUser?.Orders.Count}, 地址数: {loadedUser?.Addresses.Length}");

            // 条件查询
            var usersByEmail = _userDb.Read(new Dictionary<string, object>
            {
                { "Email", "john@example.com" }
            });

            // 更新用户
            if (loadedUser != null)
            {
                loadedUser.Email = "newemail@example.com";
                _userDb.Save(loadedUser);
            }

            // 删除用户
            if (loadedUser != null)
            {
                _userDb.Delete(loadedUser);
            }
        }

        /// <summary>
        /// 修复操作示例
        /// </summary>
        public void RepairOperationsExample()
        {
            var user = new User
            {
                Username = "jane_doe",
                Email = "jane@example.com"
            };

            // 根据用户名修复用户记录
            bool wasUpdated = _userDb.Repair(user, "Username", "jane_doe");
            Console.WriteLine($"用户记录 {(wasUpdated ? "已更新" : "已创建")}");

            // 根据多个条件修复
            var updatedUser = new User
            {
                Username = "jane_doe",
                Email = "jane.doe@newdomain.com"
            };

            bool wasFound = _userDb.Repair(updatedUser, new Dictionary<string, object>
            {
                { "Username", "jane_doe" },
                { "Email", "jane@example.com" }
            });
            Console.WriteLine($"用户记录 {(wasFound ? "已更新" : "已创建")}");
        }

        /// <summary>
        /// 关联表操作示例
        /// </summary>
        public void RelationshipOperationsExample()
        {
            var userId = 1;

            // 修复特定订单（根据ProductName识别）
            var newOrder = new Order
            {
                UserId = userId,
                ProductName = "Laptop",
                Amount = 899.99m  // 更新价格
            };

            _userDb.RepairRelationshipItem(userId, newOrder, "UserId", "ProductName", typeof(Order));

            // 查询特定条件的订单
            var expensiveOrders = _userDb.QueryRelationship(userId, 
                new Dictionary<string, object> { { "Amount", 500m } }, 
                "UserId", 
                typeof(Order));

            Console.WriteLine($"找到 {expensiveOrders.Count} 个昂贵订单");

            // 添加新地址
            var newAddress = new Address
            {
                UserId = userId,
                Street = "456 Oak Ave",
                City = "Boston",
                Country = "USA"
            };

            _userDb.AddRelationshipItem(userId, newAddress, "UserId", typeof(Address));

            // 更新地址
            newAddress.IsDefault = true;
            _userDb.UpdateRelationshipItem(newAddress, typeof(Address));

            // 删除地址
            _userDb.DeleteRelationshipItem(newAddress, typeof(Address));
        }

        /// <summary>
        /// 批量操作示例
        /// </summary>
        public void BatchOperationsExample()
        {
            var users = new List<User>
            {
                new User { Id = 10, Username = "user1", Email = "user1@example.com" },
                new User { Id = 11, Username = "user2", Email = "user2@example.com" },
                new User { Id = 12, Username = "user3", Email = "user3@example.com" }
            };

            // 批量保存
            _userDb.SaveAll(users);

            // 批量查询
            var allUsers = _userDb.Read();
            Console.WriteLine($"总用户数: {allUsers.Count}");

            // 条件删除
            int deletedCount = _userDb.DeleteWhere(new Dictionary<string, object>
            {
                { "Username", "user1" }
            });
            Console.WriteLine($"删除了 {deletedCount} 个用户");
        }
    }
}
