// SqliteUnifiedExample.cs 示例代码，演示如何使用 SqliteUnified<T> 进行增删改查、关联表操作和异步操作。
// 包含：
// 1. User/UserProfile 一对一关系示例
// 2. Product/Details/Inventory 多关联表示例
// 3. Group/GroupMember 一对多关系示例
// 4. 异步操作示例
//
// 主要接口：IDataBase（主表）、IDataTable（子表/关联表）
// 主要方法：Push/Query/Update/Delete/GetAll 及其 Async 版本
//
// 适用于演示如何在实际项目中集成和使用 SqliteUnified 统一 ORM 封装。
//
// 详细用法见每个 RunExample/RunComplexExample/RunAsyncExample/RunOneToManyExample 静态方法。

using System;
using Drx.Sdk.Network.Sqlite;

namespace Drx.Sdk.Network.Sqlite.Examples
{
    /// <summary>
    /// 用户实体示例 - 继承自 IDataBase
    /// </summary>
    public class User : IDataBase
    {
        public int Id { get; set; }
        public string TableName => null; // 使用类名作为表名

        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public DateTime CreateTime { get; set; }
        public bool IsActive { get; set; }

        // 关联表属性 - 继承自 IDataTable
        public UserProfile? Profile { get; set; }
    }

    /// <summary>
    /// 用户档案实体 - 继承自 IDataTable，作为关联表
    /// </summary>
    public class UserProfile : IDataTable
    {
        public int ParentId { get; set; }  // 父表ID（User的ID）
        public string TableName => "UserProfile"; // 子表名

        public string Phone { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public DateTime Birthday { get; set; }
        public int Age { get; set; }
    }

    /// <summary>
    /// 使用示例
    /// </summary>
    public class SqliteUnifiedExample
    {
        public static void RunExample()
        {
            // 初始化数据库操作类
            var sql = new SqliteUnified<User>("Data/users.db");

            // 创建用户实例
            var user = new User
            {
                Name = "张三",
                Email = "zhangsan@example.com",
                CreateTime = DateTime.Now,
                IsActive = true,
                Profile = new UserProfile
                {
                    Phone = "13812345678",
                    Address = "北京市朝阳区",
                    Birthday = new DateTime(1990, 5, 15),
                    Age = 33
                }
            };

            // 保存数据（Push操作）
            sql.Push(user);
            Console.WriteLine($"用户已保存，ID: {user.Id}");

            // 根据ID查询
            var foundUser = sql.QueryById(user.Id);
            if (foundUser != null)
            {
                Console.WriteLine($"找到用户: {foundUser.Name}, 电子邮箱: {foundUser.Email}");
                if (foundUser.Profile != null)
                {
                    Console.WriteLine($"用户档案: 电话 {foundUser.Profile.Phone}, 地址 {foundUser.Profile.Address}");
                }
            }

            // 根据属性查询
            var usersByName = sql.Query("Name", "张三");
            Console.WriteLine($"名称为'张三'的用户数量: {usersByName.Count}");

            // 获取所有用户
            var allUsers = sql.GetAll();
            Console.WriteLine($"数据库中总用户数: {allUsers.Count}");

            // 更新用户信息
            if (foundUser != null)
            {
                foundUser.Email = "zhangsan_new@example.com";
                foundUser.IsActive = false;
                if (foundUser.Profile != null)
                {
                    foundUser.Profile.Phone = "13987654321";
                }

                sql.Update(foundUser);
                Console.WriteLine("用户信息已更新");
            }

            // 再次查询验证更新
            var updatedUser = sql.QueryById(user.Id);
            if (updatedUser != null)
            {
                Console.WriteLine($"更新后的邮箱: {updatedUser.Email}");
                Console.WriteLine($"更新后的状态: {(updatedUser.IsActive ? "活跃" : "非活跃")}");
                if (updatedUser.Profile != null)
                {
                    Console.WriteLine($"更新后的电话: {updatedUser.Profile.Phone}");
                }
            }

            // 删除用户
            var deleteResult = sql.Delete(user.Id);
            Console.WriteLine($"删除结果: {(deleteResult ? "成功" : "失败")}");

            // 验证删除
            var deletedUser = sql.QueryById(user.Id);
            Console.WriteLine($"删除后查询结果: {(deletedUser == null ? "用户已删除" : "用户仍存在")}");
        }
    }

    /// <summary>
    /// 产品实体示例 - 展示更复杂的关联表关系
    /// </summary>
    public class Product : IDataBase
    {
        public int Id { get; set; }
        public string TableName => null;

        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime CreateTime { get; set; }

        // 多个关联表
        public ProductDetails? Details { get; set; }
        public ProductInventory? Inventory { get; set; }
    }

    public class ProductDetails : IDataTable
    {
        public int ParentId { get; set; }
        public string TableName => "ProductDetails";

        public string Manufacturer { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public double Weight { get; set; }
        public string Color { get; set; } = string.Empty;
    }

    public class ProductInventory : IDataTable
    {
        public int ParentId { get; set; }
        public string TableName => "ProductInventory";

        public int Stock { get; set; }
        public int MinStock { get; set; }
        public string Location { get; set; } = string.Empty;
        public DateTime LastUpdated { get; set; }
    }

    /// <summary>
    /// 复杂示例演示
    /// </summary>
    public class ComplexExample
    {
        public static void RunComplexExample()
        {
            var sql = new SqliteUnified<Product>("Data/products.db");

            var product = new Product
            {
                Name = "iPhone 15 Pro",
                Price = 8999.00m,
                Description = "最新款iPhone",
                CreateTime = DateTime.Now,
                Details = new ProductDetails
                {
                    Manufacturer = "Apple",
                    Category = "智能手机",
                    Weight = 187.0,
                    Color = "深空黑色"
                },
                Inventory = new ProductInventory
                {
                    Stock = 100,
                    MinStock = 10,
                    Location = "仓库A-01",
                    LastUpdated = DateTime.Now
                }
            };

            // 保存产品
            sql.Push(product);
            Console.WriteLine($"产品已保存，ID: {product.Id}");

            // 查询产品
            var foundProduct = sql.QueryById(product.Id);
            if (foundProduct != null)
            {
                Console.WriteLine($"产品: {foundProduct.Name}, 价格: ¥{foundProduct.Price}");
                if (foundProduct.Details != null)
                {
                    Console.WriteLine($"制造商: {foundProduct.Details.Manufacturer}, 重量: {foundProduct.Details.Weight}g");
                }
                if (foundProduct.Inventory != null)
                {
                    Console.WriteLine($"库存: {foundProduct.Inventory.Stock}, 位置: {foundProduct.Inventory.Location}");
                }
            }

            // 根据价格范围查询（需要自定义SQL查询方法，这里展示基本用法）
            var expensiveProducts = sql.Query("Price", 8999.00m);
            Console.WriteLine($"价格为8999的产品数量: {expensiveProducts.Count}");
        }
    }

    /// <summary>
    /// 一对多关系实体示例
    /// </summary>
    public class Group : IDataBase
    {
        public int Id { get; set; }
        public string TableName => null;
        public string Name { get; set; } = string.Empty;
        public List<GroupMember> Members { get; set; } = new List<GroupMember>();
    }
    public class GroupMember : IDataTable
    {
        public int ParentId { get; set; }
        public string TableName => "GroupMember";
        public string UserName { get; set; } = string.Empty;
        public int Age { get; set; }
    }

    public class AsyncAndOneToManyExample
    {
        public static async System.Threading.Tasks.Task RunAsyncExample()
        {
            var sql = new SqliteUnified<User>("Data/users_async.db");
            var user = new User
            {
                Name = "李雷",
                Email = "lilei@example.com",
                CreateTime = DateTime.Now,
                IsActive = true,
                Profile = new UserProfile
                {
                    Phone = "13700001111",
                    Address = "上海市浦东新区",
                    Birthday = new DateTime(1992, 8, 8),
                    Age = 32
                }
            };
            await sql.PushAsync(user);
            Console.WriteLine($"[Async] 用户已保存，ID: {user.Id}");
            var found = await sql.QueryByIdAsync(user.Id);
            Console.WriteLine(found != null ? $"[Async] 查询到: {found.Name}" : "[Async] 未找到");
            if (found != null)
            {
                found.Email = "lilei_new@example.com";
                await sql.UpdateAsync(found);
            }
            var all = await sql.GetAllAsync();
            Console.WriteLine($"[Async] 用户总数: {all.Count}");
            await sql.DeleteAsync(user.Id);
            var afterDelete = await sql.QueryByIdAsync(user.Id);
            Console.WriteLine($"[Async] 删除后: {(afterDelete == null ? "已删除" : "仍存在")}");
        }

        public static async System.Threading.Tasks.Task RunOneToManyExample()
        {
            var sql = new SqliteUnified<Group>("Data/groups.db");
            var group = new Group
            {
                Name = "开发团队",
                Members = new System.Collections.Generic.List<GroupMember>
                {
                    new GroupMember { UserName = "Alice", Age = 28 },
                    new GroupMember { UserName = "Bob", Age = 30 },
                    new GroupMember { UserName = "Charlie", Age = 25 }
                }
            };
            await sql.PushAsync(group);
            Console.WriteLine($"[一对多] 群组已保存，ID: {group.Id}");
            var found = await sql.QueryByIdAsync(group.Id);
            if (found != null)
            {
                Console.WriteLine($"[一对多] 群组: {found.Name}, 成员数: {found.Members?.Count}");
                if (found.Members != null)
                {
                    foreach (var m in found.Members)
                    {
                        Console.WriteLine($"成员: {m.UserName}, 年龄: {m.Age}");
                    }
                }
            }
            // 更新成员
            if (found != null && found.Members != null && found.Members.Count > 0)
            {
                found.Members[0].Age = 29;
                await sql.UpdateAsync(found);
                Console.WriteLine("[一对多] 成员信息已更新");
            }
            // 删除群组
            await sql.DeleteAsync(group.Id);
            var afterDelete = await sql.QueryByIdAsync(group.Id);
            Console.WriteLine($"[一对多] 删除后: {(afterDelete == null ? "已删除" : "仍存在")}");
        }
    }
}
