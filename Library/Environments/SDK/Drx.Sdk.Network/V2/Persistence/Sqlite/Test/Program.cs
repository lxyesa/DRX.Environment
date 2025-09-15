using System;
using Drx.Sdk.Network.V2.Persistence.Sqlite;
using Drx.Sdk.Network.V2.Persistence.Sqlite.Test;

namespace TestSqlitePersistence;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("开始测试 SqlitePersistence 实现...");
        Console.WriteLine("=" + new string('=', 50));
        
        try
        {
            SqlitePersistenceTest.Main();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"测试过程中发生错误: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }
}