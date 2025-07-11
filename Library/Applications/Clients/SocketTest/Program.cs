using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Drx.Sdk.Network.DataBase;
using Microsoft.Data.Sqlite;
using System.IO;
using Drx.Sdk.Network.Sqlite;

namespace SocketTest
{
    class Program
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

            // 关联表属性 - 继承自 IDataTable（一对多关系）
            public List<UserProfile> Profiles { get; set; } = new List<UserProfile>();
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

        static void Main(string[] args)
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
                Profiles = new List<UserProfile>
                {
                    new UserProfile
                    {
                        Phone = "13812345678",
                        Address = "北京市朝阳区工作地址",
                        Birthday = new DateTime(1990, 5, 15),
                        Age = 33
                    },
                    new UserProfile
                    {
                        Phone = "15987654321",
                        Address = "北京市海淀区家庭地址",
                        Birthday = new DateTime(1990, 5, 15),
                        Age = 33
                    }
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
                Console.WriteLine($"档案数量: {foundUser.Profiles.Count}");
                
                foreach (var profile in foundUser.Profiles)
                {
                    Console.WriteLine($"  电话: {profile.Phone}, 地址: {profile.Address}");
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
                
                // 修改第一个档案
                if (foundUser.Profiles.Count > 0)
                {
                    foundUser.Profiles[0].Phone = "13987654321";
                    foundUser.Profiles[0].Address = "北京市朝阳区新工作地址";
                }
                
                // 添加新的档案
                foundUser.Profiles.Add(new UserProfile
                {
                    Phone = "18912345678",
                    Address = "上海市浦东新区出差地址",
                    Birthday = new DateTime(1990, 5, 15),
                    Age = 33
                });

                sql.Update(foundUser);
                Console.WriteLine("用户信息已更新");
            }

            // 再次查询验证更新
            var updatedUser = sql.QueryById(user.Id);
            if (updatedUser != null)
            {
                Console.WriteLine($"更新后的邮箱: {updatedUser.Email}");
                Console.WriteLine($"更新后的状态: {(updatedUser.IsActive ? "活跃" : "非活跃")}");
                Console.WriteLine($"更新后的档案数量: {updatedUser.Profiles.Count}");
                
                foreach (var profile in updatedUser.Profiles)
                {
                    Console.WriteLine($"  电话: {profile.Phone}, 地址: {profile.Address}");
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
}