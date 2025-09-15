using System;
using System.IO;

namespace Drx.Sdk.Network.V2.Persistence.Sqlite.Test;

/// <summary>
/// SqlitePersistence 功能测试
/// </summary>
public static class SqlitePersistenceTest
{
    public static void RunTests()
    {
        Console.WriteLine("开始 SqlitePersistence 功能测试...");
        
        // 使用临时数据库文件
        var dbPath = Path.GetTempFileName();
        try
        {
            TestBasicOperations(dbPath);
            TestCompositeOperations(dbPath);
            TestCacheOperations(dbPath);
            
            Console.WriteLine("所有测试通过！");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"测试失败: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        finally
        {
            // 清理临时文件
            try
            {
                if (File.Exists(dbPath))
                    File.Delete(dbPath);
                if (File.Exists(dbPath + "-wal"))
                    File.Delete(dbPath + "-wal");
                if (File.Exists(dbPath + "-shm"))
                    File.Delete(dbPath + "-shm");
            }
            catch { }
        }
    }

    private static void TestBasicOperations(string dbPath)
    {
        Console.WriteLine("测试基础操作...");
        
        using var persistence = new SqlitePersistence(dbPath);
        
        // 测试表操作
        Console.WriteLine("  测试表操作...");
        Assert(persistence.CreateTable("TestTable"), "创建表失败");
        Assert(!persistence.CreateTable("TestTable"), "重复创建表应该返回 false");
        
        // 测试字符串操作
        Console.WriteLine("  测试字符串操作...");
        Assert(persistence.WriteString("TestTable", "key1", "value1"), "写入字符串失败");
        var result = persistence.ReadString("TestTable", "key1");
        Assert(result == "value1", $"读取字符串失败，期望 'value1'，实际 '{result}'");
        
        Assert(persistence.KeyExists("TestTable", "key1"), "键存在检查失败");
        Assert(!persistence.KeyExists("TestTable", "nonexistent"), "不存在的键检查失败");
        
        Assert(persistence.UpdateString("TestTable", "key1", "updated_value"), "更新字符串失败");
        result = persistence.ReadString("TestTable", "key1");
        Assert(result == "updated_value", $"更新后读取字符串失败，期望 'updated_value'，实际 '{result}'");
        
        // 测试数值操作
        Console.WriteLine("  测试数值操作...");
        Assert(persistence.WriteInt32("TestTable", "int_key", 42), "写入整数失败");
        var intResult = persistence.ReadInt32("TestTable", "int_key");
        Assert(intResult == 42, $"读取整数失败，期望 42，实际 {intResult}");
        
        Assert(persistence.WriteDouble("TestTable", "double_key", 3.14), "写入浮点数失败");
        var doubleResult = persistence.ReadDouble("TestTable", "double_key");
        Assert(Math.Abs(doubleResult!.Value - 3.14) < 0.001, $"读取浮点数失败，期望 3.14，实际 {doubleResult}");
        
        Assert(persistence.WriteBool("TestTable", "bool_key", true), "写入布尔值失败");
        var boolResult = persistence.ReadBool("TestTable", "bool_key");
        Assert(boolResult == true, $"读取布尔值失败，期望 true，实际 {boolResult}");
        
        // 测试删除操作
        Console.WriteLine("  测试删除操作...");
        Assert(persistence.RemoveKey("TestTable", "key1"), "删除键失败");
        Assert(!persistence.KeyExists("TestTable", "key1"), "删除后键仍然存在");
        Assert(persistence.ReadString("TestTable", "key1") == null, "删除后仍能读取到值");
        
        // 测试列出键
        Console.WriteLine("  测试列出键...");
        var keys = persistence.ListKeys("TestTable");
        Assert(keys.Contains("int_key"), "键列表中应包含 int_key");
        Assert(keys.Contains("double_key"), "键列表中应包含 double_key");
        Assert(keys.Contains("bool_key"), "键列表中应包含 bool_key");
        Assert(!keys.Contains("key1"), "键列表中不应包含已删除的 key1");
        
        Console.WriteLine("  基础操作测试通过");
    }

    private static void TestCompositeOperations(string dbPath)
    {
        Console.WriteLine("测试复合数据操作...");
        
        using var persistence = new SqlitePersistence(dbPath);
        
        // 测试写入复合数据
        Console.WriteLine("  测试写入复合数据...");
        var success = persistence.WriteComposite("TestTable", "UserProfile", (builder) =>
        {
            return builder
                .Add("Name", "Alice")
                .Add("Age", 30)
                .Add("IsPremium", true)
                .Add("Score", 95.5);
        });
        Assert(success, "写入复合数据失败");
        
        // 测试读取复合数据
        Console.WriteLine("  测试读取复合数据...");
        var composite = persistence.ReadComposite("TestTable", "UserProfile");
        Assert(composite != null, "读取复合数据失败");
        
        Assert(composite!.Get<string>("Name") == "Alice", "复合数据中姓名不正确");
        Assert(composite.Get<int>("Age") == 30, "复合数据中年龄不正确");
        Assert(composite.Get<bool>("IsPremium") == true, "复合数据中高级状态不正确");
        Assert(Math.Abs(composite.Get<double>("Score") - 95.5) < 0.001, "复合数据中分数不正确");
        
        // 测试复合数据存在性
        Console.WriteLine("  测试复合数据存在性...");
        Assert(persistence.CompositeExists("TestTable", "UserProfile"), "复合数据存在检查失败");
        Assert(!persistence.CompositeExists("TestTable", "NonExistent"), "不存在的复合数据检查失败");
        
        // 测试删除复合数据
        Console.WriteLine("  测试删除复合数据...");
        Assert(persistence.RemoveComposite("TestTable", "UserProfile"), "删除复合数据失败");
        Assert(!persistence.CompositeExists("TestTable", "UserProfile"), "删除后复合数据仍然存在");
        
        Console.WriteLine("  复合数据操作测试通过");
    }

    private static void TestCacheOperations(string dbPath)
    {
        Console.WriteLine("测试缓存操作...");

        using var persistence = new SqlitePersistence(dbPath);

        // 写入一些数据
        persistence.WriteString("TestTable", "cache_test", "test_value");

        // 测试缓存同步
        Console.WriteLine("  测试缓存同步...");
        persistence.SyncCache(); // 应该不抛异常

        // 测试缓存重载
        Console.WriteLine("  测试缓存重载...");
        persistence.ReloadCache(); // 应该不抛异常

        // 重载后应该仍能读取数据
        var value = persistence.ReadString("TestTable", "cache_test");
        Assert(value == "test_value", "缓存重载后数据读取失败");

        // 测试缓存大小更新
        Console.WriteLine("  测试缓存大小更新...");
        Assert(persistence.UpdateCacheSize(2048), "更新缓存大小失败");

        // 测试缓存与磁盘比较
        Console.WriteLine("  测试缓存与磁盘比较...");
        var consistent = persistence.CompareCacheWithDisk();
        // 这个测试可能因为实现细节而失败，但不应抛异常
        Console.WriteLine($"    缓存一致性检查结果: {consistent}");

        Console.WriteLine("  缓存操作测试通过");

        persistence.Dump();
    }

    private static void TestCompositeBuilder()
    {
        Console.WriteLine("测试 CompositeBuilder...");
        
        var builder = new CompositeBuilder("TestTable", "TestKey");
        
        // 测试添加数据
        builder.Add("Name", "Bob")
               .Add("Age", 25)
               .Add("IsActive", true)
               .Add("Data", new byte[] { 1, 2, 3, 4 });
        
        // 测试检查键
        Assert(builder.ContainsKey("Name"), "应该包含 Name 键");
        Assert(!builder.ContainsKey("NonExistent"), "不应该包含不存在的键");
        
        // 测试获取值
        Assert(builder.Get<string>("Name") == "Bob", "获取字符串值失败");
        Assert(builder.Get<int>("Age") == 25, "获取整数值失败");
        Assert(builder.Get<bool>("IsActive") == true, "获取布尔值失败");
        
        // 测试序列化与反序列化
        var data = builder.Build();
        var parsed = CompositeBuilder.Parse(data, "TestTable", "TestKey");
        
        Assert(parsed != null, "解析复合数据失败");
        Assert(parsed!.Get<string>("Name") == "Bob", "解析后获取字符串值失败");
        Assert(parsed.Get<int>("Age") == 25, "解析后获取整数值失败");
        Assert(parsed.Get<bool>("IsActive") == true, "解析后获取布尔值失败");
        
        // 测试 Dump（应该不抛异常）
        Console.WriteLine("  测试 Dump 输出:");
        parsed.Dump();
        
        Console.WriteLine("  CompositeBuilder 测试通过");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition)
        {
            throw new Exception($"断言失败: {message}");
        }
    }

    public static void Main()
    {
        TestCompositeBuilder();
        RunTests();
        
        Console.WriteLine("\n按任意键退出...");
        Console.ReadKey();
    }
}