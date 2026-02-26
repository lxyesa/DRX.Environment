// 快速测试异步单操作性能
using Drx.Sdk.Network.Database.Sqlite.V2;
using System.Diagnostics;

var sw = Stopwatch.StartNew();

// 运行异步单操作测试
Console.WriteLine("【异步单操作性能测试 - 快速版】\n");

// 测试 1：同步单查询 vs 异步单查询
Console.WriteLine("=== 同步 vs 异步单查询对比 (100条数据,200次查询) ===");
{
    Console.WriteLine("同步查询...");
    var db = new Sqlite<SqliteV2Test.TestUser>("./perf_sync_query.db", "./test_db");
    var users = Enumerable.Range(1, 100)
        .Select(i => new SqliteV2Test.TestUser { Name = $"User{i}", Email = $"u{i}@test", Age = 25 })
        .ToList();
    db.InsertBatch(users);

    var swSync = Stopwatch.StartNew();
    for (int i = 0; i < 200; i++)
    {
        var u = db.SelectById((i % 100) + 1);
    }
    swSync.Stop();
    Console.WriteLine($"  ✓ 同步: {swSync.ElapsedMilliseconds}ms");

    Console.WriteLine("异步查询...");
    var dbAsync = new Sqlite<SqliteV2Test.TestUser>("./perf_async_query.db", "./test_db");
    await dbAsync.InsertBatchAsync(users);

    var swAsync = Stopwatch.StartNew();
    for (int i = 0; i < 200; i++)
    {
        var u = await dbAsync.SelectByIdAsync((i % 100) + 1);
    }
    swAsync.Stop();
    Console.WriteLine($"  ✓ 异步: {swAsync.ElapsedMilliseconds}ms");

    double ratio = (double)swSync.ElapsedMilliseconds / swAsync.ElapsedMilliseconds;
    Console.WriteLine($"  📊 性能: {(ratio >= 1 ? $"异步快 {ratio:F1}x" : $"同步快 {1/ratio:F1}x")}\n");
}

// 测试 2：同步单更新 vs 异步单更新 vs 异步批更新
Console.WriteLine("=== 同步单更 vs 异步单更 vs 异步批更对比 (300条数据) ===");
{
    var data = Enumerable.Range(1, 300)
        .Select(i => new SqliteV2Test.TestUser { Name = $"User{i}", Email = $"u{i}@test", Age = 25 })
        .ToList();

    // 同步单更
    Console.WriteLine("同步单条更新...");
    var dbS = new Sqlite<SqliteV2Test.TestUser>("./perf_sync_update.db", "./test_db");
    dbS.InsertBatch(data);
    var dataS = dbS.SelectAll();
    var swS = Stopwatch.StartNew();
    foreach (var u in dataS) { u.Age++; dbS.Update(u); }
    swS.Stop();
    Console.WriteLine($"  ✓ 同步单更: {swS.ElapsedMilliseconds}ms");

    // 异步单更
    Console.WriteLine("异步单条更新...");
    var dbA = new Sqlite<SqliteV2Test.TestUser>("./perf_async_update.db", "./test_db");
    await dbA.InsertBatchAsync(data);
    var dataA = await dbA.SelectAllAsync();
    var swA = Stopwatch.StartNew();
    foreach (var u in dataA) { u.Age++; await dbA.UpdateAsync(u); }
    swA.Stop();
    Console.WriteLine($"  ✓ 异步单更: {swA.ElapsedMilliseconds}ms");

    // 异步批更
    Console.WriteLine("异步批量更新...");
    var dbB = new Sqlite<SqliteV2Test.TestUser>("./perf_batch_update.db", "./test_db");
    await dbB.InsertBatchAsync(data);
    var dataB = await dbB.SelectAllAsync();
    foreach (var u in dataB) u.Age++;
    var swB = Stopwatch.StartNew();
    await dbB.UpdateBatchAsync(dataB);
    swB.Stop();
    Console.WriteLine($"  ✓ 异步批更: {swB.ElapsedMilliseconds}ms");

    Console.WriteLine("\n【性能分析】");
    Console.WriteLine($"  同步单更 vs 异步单更: {(double)swS.ElapsedMilliseconds / swA.ElapsedMilliseconds:F1}x");
    Console.WriteLine($"  异步单更 vs 异步批更: {(double)swA.ElapsedMilliseconds / swB.ElapsedMilliseconds:F1}x");
    Console.WriteLine($"  同步单更 vs 异步批更: {(double)swS.ElapsedMilliseconds / swB.ElapsedMilliseconds:F1}x\n");
}

sw.Stop();
Console.WriteLine($"【总耗时】{sw.ElapsedMilliseconds}ms");
