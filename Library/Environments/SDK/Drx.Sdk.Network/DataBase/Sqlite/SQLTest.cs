using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.DataBase.Sqlite;

#region 测试实体模型

/// <summary>
/// 主表测试实体 - 用户模型
/// </summary>
public class User : IDataBase
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    
    public string TableName => null;
}

/// <summary>
/// 子表测试实体 - 订单模型
/// </summary>
public class Order : IDataTable
{
    public int Id { get; set; }
    public int ParentId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = string.Empty;

    public string TableName => null;
}

/// <summary>
/// 带有子表集合的用户扩展模型
/// </summary>
public class UserWithOrders : IDataBase
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int Age { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsActive { get; set; }
    public List<Order> Orders { get; set; } = new();

    public string TableName => null;
}

#endregion

/// <summary>
/// SqliteUnified 性能单元测试类
/// 测试所有主要方法的性能指标，包括执行时间和吞吐量
/// </summary>
public class SQLTest
{
    private const string TestDbPath = "performance_test.db";
    private const string TestDbDirectory = "./test_databases";

    #region 工具方法

    /// <summary>
    /// 创建测试目录
    /// </summary>
    private static void EnsureTestDirectory()
    {
        if (!Directory.Exists(TestDbDirectory))
            Directory.CreateDirectory(TestDbDirectory);
    }

    /// <summary>
    /// 清理测试数据库文件
    /// </summary>
    private static void CleanupTestDatabase()
    {
        try
        {
            string fullPath = Path.Combine(TestDbDirectory, TestDbPath);
            if (File.Exists(fullPath))
                File.Delete(fullPath);
        }
        catch { /* 忽略清理失败 */ }
    }

    /// <summary>
    /// 创建测试用户
    /// </summary>
    private static User CreateTestUser(int id, string nameSuffix = "")
    {
        return new User
        {
            Id = id > 0 ? id : 0,
            Name = $"User{nameSuffix}{id}",
            Email = $"user{id}{nameSuffix}@example.com",
            Age = 20 + (id % 50),
            CreatedAt = DateTime.Now.AddDays(-id % 365),
            IsActive = id % 2 == 0
        };
    }

    /// <summary>
    /// 创建测试订单
    /// </summary>
    private static Order CreateTestOrder(int id, int parentId)
    {
        return new Order
        {
            Id = id > 0 ? id : 0,
            ParentId = parentId,
            OrderNumber = $"ORD-{parentId:D6}-{id:D4}",
            Amount = 100 + (id % 9900),
            OrderDate = DateTime.Now.AddDays(-(id % 30)),
            Status = new[] { "Pending", "Processing", "Completed", "Cancelled" }[id % 4]
        };
    }

    /// <summary>
    /// 测试性能 - 记录执行时间和操作数
    /// </summary>
    private static void RecordPerformance(string testName, long elapsedMs, int operationCount, string unit = "ops")
    {
        double throughput = operationCount > 0 ? (operationCount * 1000.0) / elapsedMs : 0;
        Console.WriteLine($"  → {testName}: {elapsedMs}ms | {operationCount} {unit} | {throughput:F2} {unit}/s");
    }

    /// <summary>
    /// 记录内存使用
    /// </summary>
    private static void RecordMemoryUsage(string testName)
    {
        long memoryUsed = GC.GetTotalMemory(false);
        Console.WriteLine($"  → {testName} 内存占用: {memoryUsed / (1024.0 * 1024.0):F2} MB");
    }

    #endregion

    #region 同步性能测试

    /// <summary>
    /// 测试 Push 方法性能（单条插入）
    /// </summary>
    public static void TestPushPerformance(int recordCount = 1000)
    {
        Console.WriteLine("\n=== Push 性能测试 ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<User>(TestDbPath, TestDbDirectory);
        var sw = Stopwatch.StartNew();

        for (int i = 1; i <= recordCount; i++)
        {
            var user = CreateTestUser(0);
            db.Push(user);
        }

        sw.Stop();
        RecordPerformance($"插入 {recordCount} 条记录", sw.ElapsedMilliseconds, recordCount);
        RecordMemoryUsage("Push");

        CleanupTestDatabase();
    }

    /// <summary>
    /// 测试 Query 方法性能
    /// </summary>
    public static void TestQueryPerformance(int recordCount = 1000)
    {
        Console.WriteLine("\n=== Query 性能测试 ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<User>(TestDbPath, TestDbDirectory);

        // 预加载数据
        for (int i = 0; i < recordCount; i++)
        {
            db.Push(CreateTestUser(0));
        }

        var sw = Stopwatch.StartNew();
        var results = db.Query("IsActive", true);
        sw.Stop();

        RecordPerformance($"查询 IsActive=true", sw.ElapsedMilliseconds, results.Count, "rows");

        // 测试多条件查询
        sw.Restart();
        results = db.Query("Age", 30);
        sw.Stop();
        RecordPerformance($"查询 Age=30", sw.ElapsedMilliseconds, results.Count, "rows");

        CleanupTestDatabase();
    }

    /// <summary>
    /// 测试 QueryById 性能
    /// </summary>
    public static void TestQueryByIdPerformance(int recordCount = 1000)
    {
        Console.WriteLine("\n=== QueryById 性能测试 ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<User>(TestDbPath, TestDbDirectory);
        var ids = new List<int>();

        // 预加载数据
        for (int i = 0; i < recordCount; i++)
        {
            var user = CreateTestUser(0);
            db.Push(user);
            ids.Add(user.Id);
        }

        var sw = Stopwatch.StartNew();
        int found = 0;
        for (int i = 0; i < recordCount; i++)
        {
            var user = db.QueryById(ids[i % ids.Count]);
            if (user != null) found++;
        }
        sw.Stop();

        RecordPerformance($"按ID查询 {recordCount} 次", sw.ElapsedMilliseconds, found, "queries");

        CleanupTestDatabase();
    }

    /// <summary>
    /// 测试 GetAll 性能
    /// </summary>
    public static void TestGetAllPerformance(int recordCount = 1000)
    {
        Console.WriteLine("\n=== GetAll 性能测试 ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<User>(TestDbPath, TestDbDirectory);

        // 预加载数据
        for (int i = 0; i < recordCount; i++)
        {
            db.Push(CreateTestUser(0));
        }

        var sw = Stopwatch.StartNew();
        var allUsers = db.GetAll();
        sw.Stop();

        RecordPerformance($"获取全部 {allUsers.Count} 条记录", sw.ElapsedMilliseconds, allUsers.Count, "rows");
        RecordMemoryUsage("GetAll");

        CleanupTestDatabase();
    }

    /// <summary>
    /// 测试 Update 性能
    /// </summary>
    public static void TestUpdatePerformance(int recordCount = 100)
    {
        Console.WriteLine("\n=== Update 性能测试 ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<User>(TestDbPath, TestDbDirectory);
        var ids = new List<int>();

        // 预加载数据
        for (int i = 0; i < recordCount; i++)
        {
            var user = CreateTestUser(0);
            db.Push(user);
            ids.Add(user.Id);
        }

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < recordCount; i++)
        {
            var user = db.QueryById(ids[i]);
            if (user != null)
            {
                user.Age += 1;
                user.Name = $"Updated_{i}";
                db.Update(user);
            }
        }
        sw.Stop();

        RecordPerformance($"更新 {recordCount} 条记录", sw.ElapsedMilliseconds, recordCount);

        CleanupTestDatabase();
    }

    /// <summary>
    /// 测试 EditById 性能
    /// </summary>
    public static void TestEditByIdPerformance(int recordCount = 100)
    {
        Console.WriteLine("\n=== EditById 性能测试 ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<User>(TestDbPath, TestDbDirectory);
        var ids = new List<int>();

        // 预加载数据
        for (int i = 0; i < recordCount; i++)
        {
            var user = CreateTestUser(0);
            db.Push(user);
            ids.Add(user.Id);
        }

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < recordCount; i++)
        {
            var updateUser = new User { Name = $"Edited_{i}", Email = $"edited{i}@test.com", Age = 25 };
            db.EditById(ids[i], updateUser);
        }
        sw.Stop();

        RecordPerformance($"按ID编辑 {recordCount} 条记录", sw.ElapsedMilliseconds, recordCount);

        CleanupTestDatabase();
    }

    /// <summary>
    /// 测试 EditWhere 性能（基于属性）
    /// </summary>
    public static void TestEditWherePerformance(int recordCount = 1000)
    {
        Console.WriteLine("\n=== EditWhere 性能测试 ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<User>(TestDbPath, TestDbDirectory);

        // 预加载数据
        for (int i = 0; i < recordCount; i++)
        {
            db.Push(CreateTestUser(0));
        }

        var sw = Stopwatch.StartNew();
        var updateUser = new User { IsActive = false };
        int affected = db.EditWhere("IsActive", true, updateUser);
        sw.Stop();

        RecordPerformance($"条件编辑 {affected} 条记录", sw.ElapsedMilliseconds, affected);

        CleanupTestDatabase();
    }

    /// <summary>
    /// 测试 Delete 性能
    /// </summary>
    public static void TestDeletePerformance(int recordCount = 100)
    {
        Console.WriteLine("\n=== Delete 性能测试 ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<User>(TestDbPath, TestDbDirectory);
        var ids = new List<int>();

        // 预加载数据
        for (int i = 0; i < recordCount; i++)
        {
            var user = CreateTestUser(0);
            db.Push(user);
            ids.Add(user.Id);
        }

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < recordCount / 2; i++)
        {
            db.Delete(ids[i]);
        }
        sw.Stop();

        RecordPerformance($"删除 {recordCount / 2} 条记录", sw.ElapsedMilliseconds, recordCount / 2);

        CleanupTestDatabase();
    }

    /// <summary>
    /// 测试 DeleteWhere 性能
    /// </summary>
    public static void TestDeleteWherePerformance(int recordCount = 1000)
    {
        Console.WriteLine("\n=== DeleteWhere 性能测试 ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<User>(TestDbPath, TestDbDirectory);

        // 预加载数据
        for (int i = 0; i < recordCount; i++)
        {
            db.Push(CreateTestUser(0));
        }

        var sw = Stopwatch.StartNew();
        int deleted = db.DeleteWhere("IsActive", false);
        sw.Stop();

        RecordPerformance($"条件删除 {deleted} 条记录", sw.ElapsedMilliseconds, deleted);

        CleanupTestDatabase();
    }

    #endregion

    #region 异步性能测试

    /// <summary>
    /// 测试 PushAsync 性能
    /// </summary>
    public static async Task TestPushAsyncPerformance(int recordCount = 1000)
    {
        Console.WriteLine("\n=== PushAsync 性能测试 ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<User>(TestDbPath, TestDbDirectory);
        var sw = Stopwatch.StartNew();

        for (int i = 0; i < recordCount; i++)
        {
            var user = CreateTestUser(0);
            await db.PushAsync(user);
        }

        sw.Stop();
        RecordPerformance($"异步插入 {recordCount} 条记录", sw.ElapsedMilliseconds, recordCount);

        CleanupTestDatabase();
    }

    /// <summary>
    /// 测试 PushBatchAsync 性能（批量插入）
    /// </summary>
    public static async Task TestPushBatchAsyncPerformance(int recordCount = 5000, int batchSize = 500)
    {
        Console.WriteLine($"\n=== PushBatchAsync 性能测试 (批大小: {batchSize}) ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<User>(TestDbPath, TestDbDirectory);
        var users = new List<User>();

        for (int i = 0; i < recordCount; i++)
        {
            users.Add(CreateTestUser(0));
        }

        var sw = Stopwatch.StartNew();
        await db.PushBatchAsync(users, batchSize);
        sw.Stop();

        RecordPerformance($"批量异步插入 {recordCount} 条记录", sw.ElapsedMilliseconds, recordCount);
        RecordMemoryUsage("PushBatchAsync");

        CleanupTestDatabase();
    }

    /// <summary>
    /// 测试 QueryAllAsync 性能
    /// </summary>
    public static async Task TestQueryAllAsyncPerformance(int recordCount = 1000)
    {
        Console.WriteLine("\n=== QueryAllAsync 性能测试 ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<User>(TestDbPath, TestDbDirectory);

        // 预加载数据
        for (int i = 0; i < recordCount; i++)
        {
            db.Push(CreateTestUser(0));
        }

        var sw = Stopwatch.StartNew();
        var results = await db.QueryAllAsync();
        var list = new List<User>();
        foreach (var user in results)
        {
            list.Add(user);
        }
        sw.Stop();

        RecordPerformance($"异步获取全部 {list.Count} 条记录", sw.ElapsedMilliseconds, list.Count, "rows");

        CleanupTestDatabase();
    }

    /// <summary>
    /// 测试 GetAllStreamAsync 性能（流式处理）
    /// </summary>
    public static async Task TestGetAllStreamAsyncPerformance(int recordCount = 5000)
    {
        Console.WriteLine("\n=== GetAllStreamAsync 性能测试 (流式) ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<User>(TestDbPath, TestDbDirectory);

        // 预加载数据
        for (int i = 0; i < recordCount; i++)
        {
            db.Push(CreateTestUser(0));
        }

        var sw = Stopwatch.StartNew();
        int count = 0;
        await foreach (var user in db.GetAllStreamAsync())
        {
            count++;
        }
        sw.Stop();

        RecordPerformance($"流式异步获取 {count} 条记录", sw.ElapsedMilliseconds, count, "rows");
        Console.WriteLine($"  → 内存占用较低（流式处理）");

        CleanupTestDatabase();
    }

    /// <summary>
    /// 测试 EditByIdAsync 性能
    /// </summary>
    public static async Task TestEditByIdAsyncPerformance(int recordCount = 100)
    {
        Console.WriteLine("\n=== EditByIdAsync 性能测试 ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<User>(TestDbPath, TestDbDirectory);
        var ids = new List<int>();

        // 预加载数据
        for (int i = 0; i < recordCount; i++)
        {
            var user = CreateTestUser(0);
            db.Push(user);
            ids.Add(user.Id);
        }

        var sw = Stopwatch.StartNew();
        for (int i = 0; i < recordCount; i++)
        {
            var updateUser = new User { Name = $"AsyncEdited_{i}", Age = 30 };
            await db.EditByIdAsync(ids[i], updateUser);
        }
        sw.Stop();

        RecordPerformance($"异步按ID编辑 {recordCount} 条记录", sw.ElapsedMilliseconds, recordCount);

        CleanupTestDatabase();
    }

    /// <summary>
    /// 测试 EditWhereAsync 性能（基于属性）
    /// </summary>
    public static async Task TestEditWhereAsyncPerformance(int recordCount = 1000)
    {
        Console.WriteLine("\n=== EditWhereAsync 性能测试 ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<User>(TestDbPath, TestDbDirectory);

        // 预加载数据
        for (int i = 0; i < recordCount; i++)
        {
            db.Push(CreateTestUser(0));
        }

        var sw = Stopwatch.StartNew();
        var updateUser = new User { IsActive = false };
        int affected = await db.EditWhereAsync("IsActive", true, updateUser);
        sw.Stop();

        RecordPerformance($"异步条件编辑 {affected} 条记录", sw.ElapsedMilliseconds, affected);

        CleanupTestDatabase();
    }

    /// <summary>
    /// 测试 DeleteWhereAsync 性能
    /// </summary>
    public static async Task TestDeleteWhereAsyncPerformance(int recordCount = 1000)
    {
        Console.WriteLine("\n=== DeleteWhereAsync 性能测试 ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<User>(TestDbPath, TestDbDirectory);

        // 预加载数据
        for (int i = 0; i < recordCount; i++)
        {
            db.Push(CreateTestUser(0));
        }

        var sw = Stopwatch.StartNew();
        int deleted = await db.DeleteWhereAsync("IsActive", false);
        sw.Stop();

        RecordPerformance($"异步条件删除 {deleted} 条记录", sw.ElapsedMilliseconds, deleted);

        CleanupTestDatabase();
    }

    #endregion

    #region 高级功能性能测试

    /// <summary>
    /// 测试 Lambda 表达式查询性能
    /// </summary>
    public static void TestLambdaQueryPerformance(int recordCount = 1000)
    {
        Console.WriteLine("\n=== Lambda 表达式查询性能测试 ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<User>(TestDbPath, TestDbDirectory);

        // 预加载数据
        for (int i = 0; i < recordCount; i++)
        {
            db.Push(CreateTestUser(0));
        }

        // 测试 EditWhere 与 Lambda
        var sw = Stopwatch.StartNew();
        var updateUser = new User { IsActive = false };
        int affected = db.EditWhere(u => u.Age > 50, updateUser);
        sw.Stop();

        RecordPerformance($"Lambda 编辑年龄 > 50 的记录 ({affected} 条)", sw.ElapsedMilliseconds, affected);

        CleanupTestDatabase();
    }

    /// <summary>
    /// 测试子表关联性能（带订单的用户）
    /// </summary>
    public static void TestChildTablePerformance(int userCount = 100, int orderPerUser = 10)
    {
        Console.WriteLine($"\n=== 子表关联性能测试 ({userCount} 用户, 每用户 {orderPerUser} 订单) ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<UserWithOrders>(TestDbPath, TestDbDirectory);

        // 插入含子表的主记录
        var sw = Stopwatch.StartNew();
        for (int i = 1; i <= userCount; i++)
        {
            var user = new UserWithOrders
            {
                Name = $"User{i}",
                Email = $"user{i}@test.com",
                Age = 20 + (i % 50),
                CreatedAt = DateTime.Now,
                IsActive = true,
                Orders = new List<Order>()
            };

            for (int j = 1; j <= orderPerUser; j++)
            {
                user.Orders.Add(CreateTestOrder(0, i));
            }

            db.Push(user);
        }
        sw.Stop();

        int totalOrders = userCount * orderPerUser;
        RecordPerformance($"插入 {userCount} 个主记录和 {totalOrders} 个子记录", sw.ElapsedMilliseconds, userCount + totalOrders);

        // 查询并加载子表
        sw.Restart();
        var users = db.GetAll();
        sw.Stop();

        int loadedOrders = users.Sum(u => u.Orders.Count);
        RecordPerformance($"查询并加载 {users.Count} 个主记录和 {loadedOrders} 个子记录", sw.ElapsedMilliseconds, users.Count);

        CleanupTestDatabase();
    }

    /// <summary>
    /// 并发操作性能测试
    /// </summary>
    public static async Task TestConcurrentOperationsPerformance(int concurrentTasks = 10, int operationsPerTask = 100)
    {
        Console.WriteLine($"\n=== 并发操作性能测试 ({concurrentTasks} 并发任务, 每个 {operationsPerTask} 次操作) ===");
        EnsureTestDirectory();
        CleanupTestDatabase();

        var db = new SqliteUnified<User>(TestDbPath, TestDbDirectory);

        var sw = Stopwatch.StartNew();
        var tasks = new List<Task>();

        for (int t = 0; t < concurrentTasks; t++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int i = 0; i < operationsPerTask; i++)
                {
                    await db.PushAsync(CreateTestUser(0));
                }
            }));
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        int totalInserts = concurrentTasks * operationsPerTask;
        RecordPerformance($"并发插入 {totalInserts} 条记录", sw.ElapsedMilliseconds, totalInserts);

        CleanupTestDatabase();
    }

    #endregion

    #region 主测试入口

    /// <summary>
    /// 运行所有性能测试
    /// </summary>
    public static async Task RunAllTests()
    {
        Console.WriteLine("╔════════════════════════════════════════════════════════╗");
        Console.WriteLine("║         SqliteUnified 性能测试套件                      ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════╝");

        try
        {
            // 同步测试
            TestPushPerformance(1000);
            TestQueryPerformance(1000);
            TestQueryByIdPerformance(500);
            TestGetAllPerformance(1000);
            TestUpdatePerformance(100);
            TestEditByIdPerformance(100);
            TestEditWherePerformance(500);
            TestDeletePerformance(200);
            TestDeleteWherePerformance(500);
            TestLambdaQueryPerformance(1000);
            TestChildTablePerformance(50, 5);

            // 异步测试
            await TestPushAsyncPerformance(1000);
            await TestPushBatchAsyncPerformance(5000, 500);
            await TestQueryAllAsyncPerformance(1000);
            await TestGetAllStreamAsyncPerformance(2000);
            await TestEditByIdAsyncPerformance(100);
            await TestEditWhereAsyncPerformance(500);
            await TestDeleteWhereAsyncPerformance(500);

            // 并发测试
            await TestConcurrentOperationsPerformance(5, 200);

            Console.WriteLine("\n╔════════════════════════════════════════════════════════╗");
            Console.WriteLine("║                  性能测试完成！                         ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════╝\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n❌ 测试过程中出错: {ex.Message}");
            Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
        }
        finally
        {
            CleanupTestDatabase();
        }
    }

    #endregion
}
