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
using DRX.Framework;

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
            public int Id { get; set; }        // 主键ID
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

            // 查找子表字段Phone = 13812345678 的子表
            var user1 = sql.Query("Name", "张三");
            user1[0].Profiles.Add(new UserProfile
            {
                Phone = "138123456781",
                Address = "北京市朝阳区工作地址1",
                Birthday = new DateTime(1990, 5, 15),
                Age = 331
            });

            Logger.Info(user1[0].Profiles[0].Phone);
        }
    }
}