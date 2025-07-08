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
        public class ABC : IDataBase
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }
        static async Task Main(string[] args)
        {
            Console.WriteLine("SQLite泛型封装类使用示例");
            Console.WriteLine("------------------------");

            // 创建SQLite数据库操作对象
            var sql = new Sqlite<ABC>("abc_database.db");

            // 保存单个对象
            var abc1 = new ABC { Id = 1, Name = "测试数据1" };
            sql.Save(abc1);
            Console.WriteLine($"已保存: Id={abc1.Id}, Name={abc1.Name}");

            List<ABC> items = new List<ABC>();
            for (int i = 0; i < 100000; i++)
            {
                items.Add(new ABC { Id = i, Name = $"测试数据{i}" });
            }
            sql.SaveAll(items);
            Console.WriteLine($"已批量保存 {items.Count} 条数据");

            var loaded = sql.ReadSingle("Id", 1);
            Console.WriteLine($"按ID查询: Id={loaded.Id}, Name={loaded.Name}");

            Console.WriteLine("\n按任意键退出...");
            Console.ReadKey();
        }
    }
}