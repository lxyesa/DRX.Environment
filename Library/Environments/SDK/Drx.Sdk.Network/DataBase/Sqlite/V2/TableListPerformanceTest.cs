using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Drx.Sdk.Network.DataBase.Sqlite;

namespace Drx.Sdk.Network.DataBase.Sqlite.V2.Tests;

/// <summary>
/// TableList 性能测试 - 测试新的子表系统
/// 
/// 数据模型说明：
/// - Player (主表): 游戏玩家，实现 IDataBase
/// - ActiveMod (子表): 玩家激活的 Mod，实现 IDataTableV2，使用 String 类型 ID 和时间戳
/// - PlayerAchievement (子表): 玩家成就，实现 IDataTableV2
/// 
/// 测试场景模拟真实应用：
/// - 一个玩家平均激活 30-100 个 Mod
/// - 每个 Mod 有激活时间、过期时间
/// - 实时添加、移除、更新 Mod
/// - 查询特定条件的 Mod（如已过期的）
/// </summary>
public class TableListPerformanceTest
{
    #region 测试数据模型

    /// <summary>
    /// 游戏玩家 - 主表模型
    /// </summary>
    public class Player : IDataBase
    {
        public int Id { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public int Level { get; set; }
        public long RegisteredAt { get; set; }
        public long LastLoginAt { get; set; }

        /// <summary>
        /// 玩家激活的 Mod - 使用 TableList<T>
        /// </summary>
        public TableList<ActiveMod> ActiveMods { get; set; } = null!;

        /// <summary>
        /// 玩家成就 - 使用 TableList<T>
        /// </summary>
        public TableList<PlayerAchievement> Achievements { get; set; } = null!;

        public string TableName => null;
    }

    /// <summary>
    /// 激活的 Mod - 子表模型
    /// 实现 IDataTableV2 接口，使用 String 类型 ID
    /// </summary>
    public class ActiveMod : IDataTableV2
    {
        /// <summary>
        /// String 类型 ID（GUID）
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 父表 ID（玩家 ID）
        /// </summary>
        public int ParentId { get; set; }

        /// <summary>
        /// 创建时间（添加到活跃列表的时间）
        /// </summary>
        public long CreatedAt { get; set; }

        /// <summary>
        /// 最后更新时间
        /// </summary>
        public long UpdatedAt { get; set; }

        // 业务字段
        public int ModId { get; set; }
        public string ModName { get; set; } = string.Empty;
        public string ModAuthor { get; set; } = string.Empty;
        public string ModVersion { get; set; } = "1.0.0";
        public long ActivatedAt { get; set; }
        public long ExpiresAt { get; set; } // 0 = 永不过期

        public string TableName => nameof(ActiveMod);
    }

    /// <summary>
    /// 玩家成就 - 子表模型
    /// </summary>
    public class PlayerAchievement : IDataTableV2
    {
        public string Id { get; set; } = string.Empty;
        public int ParentId { get; set; }
        public long CreatedAt { get; set; }
        public long UpdatedAt { get; set; }

        // 业务字段
        public int AchievementId { get; set; }
        public string AchievementName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public long UnlockedAt { get; set; }
        public bool IsSecret { get; set; }

        public string TableName => nameof(PlayerAchievement);
    }

    #endregion

    #region 性能测试工具

    private static void LogPerformance(string testName, long elapsedMs, long count, string unit = "items")
    {
        double throughput = elapsedMs > 0 ? (count * 1000.0) / elapsedMs : count;
        Console.WriteLine($"  ✓ {testName}: {elapsedMs}ms | {count} {unit} | {throughput:F0} {unit}/s");
    }

    private static void LogHighPrecisionPerformance(string testName, long elapsedMs, long elapsedNs, long count, string unit = "items")
    {
        string timeStr = elapsedMs > 0 
            ? $"{elapsedMs}ms"
            : $"{elapsedNs / 1000.0:F3}μs";

        double throughput = elapsedMs > 0
            ? (count * 1000.0) / elapsedMs
            : (count * 1_000_000.0) / elapsedNs;

        Console.WriteLine($"  ✓ {testName}: {timeStr} | {count} {unit} | {throughput:F0} {unit}/s");
    }

    private static (long ms, long ns) MeasureHighPrecision(Action action)
    {
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();

        var ms = sw.ElapsedMilliseconds;
        var ns = (long)((sw.Elapsed.TotalMilliseconds % 1) * 1_000_000);

        return (ms, ns);
    }

    #endregion

    #region TableList 基础操作测试

    /// <summary>
    /// 测试 TableList 的 Add 操作性能
    /// 立即同步到数据库
    /// </summary>
    public static void TestTableListAddPerformance(int playerCount = 10, int modsPerPlayer = 100)
    {
        Console.WriteLine("\n=== TableList Add 操作性能测试 ===");
        var db = new SqliteV2<Player>("./test_tablelist_add.db", "./test_db");

        var players = Enumerable.Range(1, playerCount)
            .Select(i => new Player
            {
                PlayerName = $"Player{i}",
                Email = $"player{i}@game.com",
                Level = 10 + i,
                RegisteredAt = DateTimeOffset.UtcNow.AddDays(-i).ToUnixTimeMilliseconds(),
                LastLoginAt = DateTimeOffset.UtcNow.AddHours(-i).ToUnixTimeMilliseconds(),
                ActiveMods = new TableList<ActiveMod>(),
                Achievements = new TableList<PlayerAchievement>()
            })
            .ToList();

        // 插入玩家
        db.InsertBatch(players);
        Console.WriteLine($"已创建 {playerCount} 个玩家");

        // 测试为每个玩家添加 Mod
        var (totalMs, totalNs) = MeasureHighPrecision(() =>
        {
            foreach (var player in players)
            {
                for (int i = 0; i < modsPerPlayer; i++)
                {
                    player.ActiveMods.Add(new ActiveMod
                    {
                        ModId = i + 1,
                        ModName = $"Mod{i + 1}",
                        ModAuthor = "Author",
                        ModVersion = "1.0.0",
                        ActivatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        ExpiresAt = 0 // 永不过期
                    });
                }
            }
        });

        int totalMods = playerCount * modsPerPlayer;
        LogHighPrecisionPerformance($"添加 {totalMods} 个 Mod（{playerCount} 玩家 × {modsPerPlayer})，立即同步", totalMs, totalNs, totalMods);
    }

    /// <summary>
    /// 测试 TableList 的 AddRange 批量操作性能
    /// </summary>
    public static void TestTableListAddRangePerformance(int playerCount = 10, int modsPerPlayer = 100)
    {
        Console.WriteLine("\n=== TableList AddRange 批量操作性能测试 ===");
        var db = new SqliteV2<Player>("./test_tablelist_addrange.db", "./test_db");

        var players = Enumerable.Range(1, playerCount)
            .Select(i => new Player
            {
                PlayerName = $"Player{i}",
                Email = $"player{i}@game.com",
                Level = 10 + i,
                RegisteredAt = DateTimeOffset.UtcNow.AddDays(-i).ToUnixTimeMilliseconds(),
                LastLoginAt = DateTimeOffset.UtcNow.AddHours(-i).ToUnixTimeMilliseconds(),
                ActiveMods = new TableList<ActiveMod>(),
                Achievements = new TableList<PlayerAchievement>()
            })
            .ToList();

        db.InsertBatch(players);
        Console.WriteLine($"已创建 {playerCount} 个玩家");

        // 测试批量添加
        var (totalMs, totalNs) = MeasureHighPrecision(() =>
        {
            foreach (var player in players)
            {
                var modsToAdd = Enumerable.Range(1, modsPerPlayer)
                    .Select(i => new ActiveMod
                    {
                        ModId = i,
                        ModName = $"Mod{i}",
                        ModAuthor = "Author",
                        ModVersion = "1.0.0",
                        ActivatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        ExpiresAt = 0
                    })
                    .ToList();

                player.ActiveMods.AddRange(modsToAdd);
            }
        });

        int totalMods = playerCount * modsPerPlayer;
        LogHighPrecisionPerformance($"批量添加 {totalMods} 个 Mod（使用 AddRange），合并为单个 INSERT", totalMs, totalNs, totalMods);
    }

    /// <summary>
    /// 测试 TableList 的 Remove 操作性能
    /// </summary>
    public static void TestTableListRemovePerformance(int playerCount = 10, int modsPerPlayer = 100)
    {
        Console.WriteLine("\n=== TableList Remove 操作性能测试 ===");
        var db = new SqliteV2<Player>("./test_tablelist_remove.db", "./test_db");

        var players = Enumerable.Range(1, playerCount)
            .Select(i => new Player
            {
                PlayerName = $"Player{i}",
                Email = $"player{i}@game.com",
                Level = 10 + i,
                RegisteredAt = DateTimeOffset.UtcNow.AddDays(-i).ToUnixTimeMilliseconds(),
                LastLoginAt = DateTimeOffset.UtcNow.AddHours(-i).ToUnixTimeMilliseconds(),
                ActiveMods = new TableList<ActiveMod>(),
                Achievements = new TableList<PlayerAchievement>()
            })
            .ToList();

        db.InsertBatch(players);
        Console.WriteLine($"已创建 {playerCount} 个玩家");

        // 为每个玩家添加 Mod
        foreach (var player in players)
        {
            for (int i = 0; i < modsPerPlayer; i++)
            {
                player.ActiveMods.Add(new ActiveMod
                {
                    ModId = i + 1,
                    ModName = $"Mod{i + 1}",
                    ActivatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ExpiresAt = 0
                });
            }
        }
        Console.WriteLine($"已添加 {playerCount * modsPerPlayer} 个 Mod");

        // 测试删除一半的 Mod
        var (totalMs, totalNs) = MeasureHighPrecision(() =>
        {
            foreach (var player in players)
            {
                var modsToRemove = player.ActiveMods.Take(modsPerPlayer / 2).ToList();
                foreach (var mod in modsToRemove)
                {
                    player.ActiveMods.Remove(mod);
                }
            }
        });

        int totalRemoved = playerCount * (modsPerPlayer / 2);
        LogHighPrecisionPerformance($"删除 {totalRemoved} 个 Mod（50% 删除率）", totalMs, totalNs, totalRemoved);
    }

    /// <summary>
    /// 测试 TableList 的 Clear 操作性能
    /// </summary>
    public static void TestTableListClearPerformance(int playerCount = 10, int modsPerPlayer = 100)
    {
        Console.WriteLine("\n=== TableList Clear 操作性能测试 ===");
        var db = new SqliteV2<Player>("./test_tablelist_clear.db", "./test_db");

        var players = Enumerable.Range(1, playerCount)
            .Select(i => new Player
            {
                PlayerName = $"Player{i}",
                Email = $"player{i}@game.com",
                Level = 10 + i,
                RegisteredAt = DateTimeOffset.UtcNow.AddDays(-i).ToUnixTimeMilliseconds(),
                LastLoginAt = DateTimeOffset.UtcNow.AddHours(-i).ToUnixTimeMilliseconds(),
                ActiveMods = new TableList<ActiveMod>(),
                Achievements = new TableList<PlayerAchievement>()
            })
            .ToList();

        db.InsertBatch(players);

        // 为每个玩家添加 Mod
        foreach (var player in players)
        {
            for (int i = 0; i < modsPerPlayer; i++)
            {
                player.ActiveMods.Add(new ActiveMod
                {
                    ModId = i + 1,
                    ModName = $"Mod{i + 1}",
                    ActivatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ExpiresAt = 0
                });
            }
        }
        Console.WriteLine($"已添加 {playerCount * modsPerPlayer} 个 Mod");

        // 测试清空所有 Mod
        var (totalMs, totalNs) = MeasureHighPrecision(() =>
        {
            foreach (var player in players)
            {
                player.ActiveMods.Clear();
            }
        });

        int totalCleared = playerCount * modsPerPlayer;
        LogHighPrecisionPerformance($"清空 {totalCleared} 个 Mod（{playerCount} 玩家）", totalMs, totalNs, totalCleared);
    }

    #endregion

    #region LINQ 查询性能测试

    /// <summary>
    /// 测试 TableList 的 Where 查询性能
    /// 模拟查询已过期的 Mod
    /// </summary>
    public static void TestTableListWherePerformance(int playerCount = 10, int modsPerPlayer = 100)
    {
        Console.WriteLine("\n=== TableList Where 查询性能测试 ===");
        var db = new SqliteV2<Player>("./test_tablelist_where.db", "./test_db");

        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var players = Enumerable.Range(1, playerCount)
            .Select(i => new Player
            {
                PlayerName = $"Player{i}",
                Email = $"player{i}@game.com",
                Level = 10 + i,
                RegisteredAt = DateTimeOffset.UtcNow.AddDays(-i).ToUnixTimeMilliseconds(),
                LastLoginAt = DateTimeOffset.UtcNow.AddHours(-i).ToUnixTimeMilliseconds(),
                ActiveMods = new TableList<ActiveMod>(),
                Achievements = new TableList<PlayerAchievement>()
            })
            .ToList();

        db.InsertBatch(players);

        // 为每个玩家添加 Mod，其中 30% 已过期
        foreach (var player in players)
        {
            for (int i = 0; i < modsPerPlayer; i++)
            {
                var isExpired = i % 10 < 3; // 30% 已过期
                var expiresAt = isExpired 
                    ? now - (100 - i) * 3600000 // 已过期（负数）
                    : now + (100 + i) * 3600000; // 未来过期

                player.ActiveMods.Add(new ActiveMod
                {
                    ModId = i + 1,
                    ModName = $"Mod{i + 1}",
                    ActivatedAt = now - (1000 - i) * 3600000,
                    ExpiresAt = expiresAt
                });
            }
        }
        Console.WriteLine($"已添加 {playerCount * modsPerPlayer} 个 Mod（30% 已过期）");

        // 测试查询已过期的 Mod
        var (totalMs, totalNs) = MeasureHighPrecision(() =>
        {
            foreach (var player in players)
            {
                var expiredMods = player.ActiveMods.Where(m => m.ExpiresAt > 0 && m.ExpiresAt < now).ToList();
            }
        });

        int expectedExpired = (int)(playerCount * modsPerPlayer * 0.3);
        LogHighPrecisionPerformance($"查询 {playerCount} 玩家的已过期 Mod（期望结果 ~{expectedExpired}）", totalMs, totalNs, playerCount);
    }

    /// <summary>
    /// 测试 TableList 的 FirstOrDefault 查询性能
    /// </summary>
    public static void TestTableListFirstOrDefaultPerformance(int playerCount = 10, int modsPerPlayer = 100)
    {
        Console.WriteLine("\n=== TableList FirstOrDefault 查询性能测试 ===");
        var db = new SqliteV2<Player>("./test_tablelist_first.db", "./test_db");

        var players = Enumerable.Range(1, playerCount)
            .Select(i => new Player
            {
                PlayerName = $"Player{i}",
                Email = $"player{i}@game.com",
                Level = 10 + i,
                RegisteredAt = DateTimeOffset.UtcNow.AddDays(-i).ToUnixTimeMilliseconds(),
                LastLoginAt = DateTimeOffset.UtcNow.AddHours(-i).ToUnixTimeMilliseconds(),
                ActiveMods = new TableList<ActiveMod>(),
                Achievements = new TableList<PlayerAchievement>()
            })
            .ToList();

        db.InsertBatch(players);

        // 为每个玩家添加 Mod
        foreach (var player in players)
        {
            for (int i = 0; i < modsPerPlayer; i++)
            {
                player.ActiveMods.Add(new ActiveMod
                {
                    ModId = i + 1,
                    ModName = $"Mod{i + 1}",
                    ActivatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ExpiresAt = 0
                });
            }
        }

        // 测试获取第一个 Mod
        var (totalMs, totalNs) = MeasureHighPrecision(() =>
        {
            foreach (var player in players)
            {
                var firstMod = player.ActiveMods.FirstOrDefault(m => m.ModId > 50);
            }
        });

        LogHighPrecisionPerformance($"查询各玩家的第一个符合条件 Mod（{playerCount} 次查询）", totalMs, totalNs, playerCount);
    }

    /// <summary>
    /// 测试 TableList 的 Any 查询性能
    /// </summary>
    public static void TestTableListAnyPerformance(int playerCount = 10, int modsPerPlayer = 100)
    {
        Console.WriteLine("\n=== TableList Any 查询性能测试 ===");
        var db = new SqliteV2<Player>("./test_tablelist_any.db", "./test_db");

        var players = Enumerable.Range(1, playerCount)
            .Select(i => new Player
            {
                PlayerName = $"Player{i}",
                Email = $"player{i}@game.com",
                Level = 10 + i,
                RegisteredAt = DateTimeOffset.UtcNow.AddDays(-i).ToUnixTimeMilliseconds(),
                LastLoginAt = DateTimeOffset.UtcNow.AddHours(-i).ToUnixTimeMilliseconds(),
                ActiveMods = new TableList<ActiveMod>(),
                Achievements = new TableList<PlayerAchievement>()
            })
            .ToList();

        db.InsertBatch(players);

        // 为每个玩家添加 Mod
        foreach (var player in players)
        {
            for (int i = 0; i < modsPerPlayer; i++)
            {
                player.ActiveMods.Add(new ActiveMod
                {
                    ModId = i + 1,
                    ModName = $"Mod{i + 1}",
                    ActivatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ExpiresAt = 0
                });
            }
        }

        // 测试检查是否存在特定 Mod
        var (totalMs, totalNs) = MeasureHighPrecision(() =>
        {
            foreach (var player in players)
            {
                var hasHighVersionMod = player.ActiveMods.Any(m => m.ModId > 80);
            }
        });

        LogHighPrecisionPerformance($"检查各玩家是否有符合条件的 Mod（{playerCount} 次检查）", totalMs, totalNs, playerCount);
    }

    /// <summary>
    /// 测试 TableList 的 GroupBy 分组性能
    /// </summary>
    public static void TestTableListGroupByPerformance(int playerCount = 10, int modsPerPlayer = 100)
    {
        Console.WriteLine("\n=== TableList GroupBy 分组性能测试 ===");
        var db = new SqliteV2<Player>("./test_tablelist_groupby.db", "./test_db");

        var players = Enumerable.Range(1, playerCount)
            .Select(i => new Player
            {
                PlayerName = $"Player{i}",
                Email = $"player{i}@game.com",
                Level = 10 + i,
                RegisteredAt = DateTimeOffset.UtcNow.AddDays(-i).ToUnixTimeMilliseconds(),
                LastLoginAt = DateTimeOffset.UtcNow.AddHours(-i).ToUnixTimeMilliseconds(),
                ActiveMods = new TableList<ActiveMod>(),
                Achievements = new TableList<PlayerAchievement>()
            })
            .ToList();

        db.InsertBatch(players);

        // 为每个玩家添加 Mod，版本各异
        foreach (var player in players)
        {
            for (int i = 0; i < modsPerPlayer; i++)
            {
                var versionNum = (i % 5) + 1;
                player.ActiveMods.Add(new ActiveMod
                {
                    ModId = i + 1,
                    ModName = $"Mod{i + 1}",
                    ModVersion = $"{versionNum}.0.0",
                    ActivatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ExpiresAt = 0
                });
            }
        }

        // 测试按 ModVersion 分组
        var (totalMs, totalNs) = MeasureHighPrecision(() =>
        {
            foreach (var player in players)
            {
                var groupedByVersion = player.ActiveMods.GroupBy(m => m.ModVersion).ToList();
            }
        });

        LogHighPrecisionPerformance($"按版本分组（{playerCount} 玩家，每个 {modsPerPlayer} 个 Mod）", totalMs, totalNs, playerCount);
    }

    #endregion

    #region 主表更新时的同步测试

    /// <summary>
    /// 测试主表 Update 时的智能同步
    /// 修改子表后调用主表的 Update 方法
    /// </summary>
    public static void TestSmartSyncOnUpdate(int playerCount = 5, int modsPerPlayer = 50)
    {
        Console.WriteLine("\n=== 主表 Update 时的智能同步测试 ===");

        // 清理旧数据库文件，避免数据累积
        var dbPath = "./test_smart_sync.db";
        if (File.Exists(dbPath))
        {
            File.Delete(dbPath);
            Console.WriteLine("已删除旧数据库文件");
        }

        var db = new SqliteV2<Player>(dbPath, "./test_db");

        var players = Enumerable.Range(1, playerCount)
            .Select(i => new Player
            {
                PlayerName = $"Player{i}",
                Email = $"player{i}@game.com",
                Level = 10 + i,
                RegisteredAt = DateTimeOffset.UtcNow.AddDays(-i).ToUnixTimeMilliseconds(),
                LastLoginAt = DateTimeOffset.UtcNow.AddHours(-i).ToUnixTimeMilliseconds(),
                ActiveMods = new TableList<ActiveMod>(),
                Achievements = new TableList<PlayerAchievement>()
            })
            .ToList();

        db.InsertBatch(players);

        // 为每个玩家添加初始 Mod
        Console.WriteLine("向 TableList 添加 Mod...");
        foreach (var player in players)
        {
            for (int i = 0; i < modsPerPlayer; i++)
            {
                player.ActiveMods.Add(new ActiveMod
                {
                    ModId = i + 1,
                    ModName = $"Mod{i + 1}",
                    ActivatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    ExpiresAt = 0
                });
            }
        }
        Console.WriteLine($"已为 {playerCount} 玩家添加 {playerCount * modsPerPlayer} 个 Mod");

        // ★ 关键：需要调用 Update 来同步 Insert 后添加的 TableList 内容
        Console.WriteLine("同步 Mod 数据到数据库...");
        foreach (var player in players)
        {
            db.Update(player);
        }
        Console.WriteLine("同步完成");

        // 查询数据库中的数据
        var retrievedPlayers = db.SelectAll();
        Console.WriteLine($"数据库查询验证: 玩家数 = {retrievedPlayers.Count}");
        Console.WriteLine();

        if (retrievedPlayers.Count > 0)
        {
            foreach (var player in retrievedPlayers)
            {
                Console.WriteLine($"玩家 {player.PlayerName}:");
                Console.WriteLine($"  - 激活 Mod 数: {player.ActiveMods.Count}");
                Console.WriteLine($"  - 成就数: {player.Achievements.Count}");
                
                if (player.ActiveMods.Count > 0)
                {
                    var modNames = string.Join(", ", player.ActiveMods.Take(3).Select(m => m.ModName));
                    if (player.ActiveMods.Count > 3)
                        modNames += $", ... (还有 {player.ActiveMods.Count - 3} 个)";
                    Console.WriteLine($"  - Mod 列表: {modNames}");
                }
            }
        }
        Console.WriteLine();

        // 模拟修改子表（移除 50% 的 Mod）
        Console.WriteLine("\n修改子表内容...");
        foreach (var player in retrievedPlayers)
        {
            var modsToRemove = player.ActiveMods.Take(player.ActiveMods.Count / 2).ToList();
            foreach (var mod in modsToRemove)
            {
                player.ActiveMods.Remove(mod);
            }
        }

        Console.WriteLine($"已移除进度: {retrievedPlayers.Sum(p => modsPerPlayer / 2)} 个 Mod");

        // 测试更新主表时的数据库同步
        var (totalMs, totalNs) = MeasureHighPrecision(() =>
        {
            foreach (var player in retrievedPlayers)
            {
                player.Level++;
                player.LastLoginAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                db.Update(player);
            }
        });

        LogHighPrecisionPerformance($"更新 {playerCount} 个玩家及其修改的 Mod（智能同步）", totalMs, totalNs, playerCount);

        // 验证更新结果
        var finalPlayers = db.SelectAll();
        Console.WriteLine($"数据库最终验证: 玩家数 = {finalPlayers.Count}");
        Console.WriteLine();
        
        if (finalPlayers.Count > 0)
        {
            foreach (var player in finalPlayers)
            {
                Console.WriteLine($"玩家 {player.PlayerName}:");
                Console.WriteLine($"  - Level: {player.Level}");
                Console.WriteLine($"  - 激活 Mod 数: {player.ActiveMods.Count}（期望：~{modsPerPlayer / 2}）");
                
                if (player.ActiveMods.Count > 0)
                {
                    var modNames = string.Join(", ", player.ActiveMods.Take(3).Select(m => m.ModName));
                    if (player.ActiveMods.Count > 3)
                        modNames += $", ... (还有 {player.ActiveMods.Count - 3} 个)";
                    Console.WriteLine($"  - 剩余 Mod: {modNames}");
                }
            }
        }
    }

    #endregion

    /// <summary>
    /// 诊断测试：逐步验证 Insert → Add → Update → Select 的完整流程
    /// </summary>
    public static void TestDiagnosticFlow()
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║              诊断测试：TableList 完整数据流                ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

        // 清理
        var dbPath = "./test_diagnostic.db";
        if (File.Exists(dbPath)) File.Delete(dbPath);

        var db = new SqliteV2<Player>(dbPath, "./test_db");
        
        // Step 1: 插入一个玩家
        Console.WriteLine("【Step 1】插入玩家...");
        var player1 = new Player
        {
            PlayerName = "TestPlayer1",
            Email = "test1@game.com",
            Level = 10,
            RegisteredAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            LastLoginAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ActiveMods = new TableList<ActiveMod>(),
            Achievements = new TableList<PlayerAchievement>()
        };
        db.Insert(player1);
        Console.WriteLine($"✓ 插入玩家 ID={player1.Id}");

        // Step 2: 在内存中添加 Mod
        Console.WriteLine("\n【Step 2】在内存中添加 3 个 Mod...");
        for (int i = 0; i < 3; i++)
        {
            player1.ActiveMods.Add(new ActiveMod
            {
                ModId = i + 1,
                ModName = $"TestMod{i + 1}",
                ActivatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                ExpiresAt = 0
            });
        }
        Console.WriteLine($"✓ 内存中 ActiveMods.Count = {player1.ActiveMods.Count}");

        // Step 3: 调用 Update 同步到数据库
        Console.WriteLine("\n【Step 3】调用 Update 同步到数据库...");
        db.Update(player1);
        Console.WriteLine($"✓ Update 完成");

        // Step 4: 从数据库重新加载玩家
        Console.WriteLine("\n【Step 4】从数据库重新加载玩家...");
        var reloadedPlayer1 = db.SelectAll().FirstOrDefault(p => p.Id == player1.Id);
        if (reloadedPlayer1 != null)
        {
            Console.WriteLine($"✓ 成功加载玩家 {reloadedPlayer1.PlayerName}");
            Console.WriteLine($"  - ActiveMods.Count = {reloadedPlayer1.ActiveMods.Count}（期望：3）");
            if (reloadedPlayer1.ActiveMods.Count > 0)
            {
                foreach (var mod in reloadedPlayer1.ActiveMods)
                {
                    Console.WriteLine($"    * {mod.ModName} (ID={mod.Id}, ParentId={mod.ParentId})");
                }
            }
            else
            {
                Console.WriteLine($"  ⚠️ 警告：Mod 加载失败！");
            }
        }
        else
        {
            Console.WriteLine($"❌ 加载玩家失败！");
        }

        // Step 5: 修改 Mod（删除 1 个）
        if (reloadedPlayer1 != null && reloadedPlayer1.ActiveMods.Count > 0)
        {
            Console.WriteLine("\n【Step 5】删除 1 个 Mod...");
            var modToRemove = reloadedPlayer1.ActiveMods.First();
            reloadedPlayer1.ActiveMods.Remove(modToRemove);
            Console.WriteLine($"✓ 内存中已删除 {modToRemove.ModName}");
            Console.WriteLine($"  - 删除后 ActiveMods.Count = {reloadedPlayer1.ActiveMods.Count}");

            // Step 6: 再次 Update
            Console.WriteLine("\n【Step 6】再次 Update 同步删除...");
            db.Update(reloadedPlayer1);
            Console.WriteLine($"✓ Update 完成");

            // Step 7: 最终验证
            Console.WriteLine("\n【Step 7】最终数据库验证...");
            var finalPlayer = db.SelectAll().FirstOrDefault(p => p.Id == player1.Id);
            if (finalPlayer != null)
            {
                Console.WriteLine($"✓ 最终玩家状态：{finalPlayer.PlayerName}");
                Console.WriteLine($"  - ActiveMods.Count = {finalPlayer.ActiveMods.Count}（期望：2）");
                if (finalPlayer.ActiveMods.Count > 0)
                {
                    foreach (var mod in finalPlayer.ActiveMods)
                    {
                        Console.WriteLine($"    * {mod.ModName}");
                    }
                }
            }
        }

        Console.WriteLine("\n" + new string('=', 60));
        Console.WriteLine("诊断测试完成\n");
    }

    /// <summary>
    /// 性能对比：TableList 内存操作 vs 直接数据库操作
    /// </summary>
    public static void TestMemoryVsDatabasePerformance(int playerCount = 5, int modsPerPlayer = 100)
    {
        Console.WriteLine("\n=== 性能对比：内存操作 vs 数据库操作 ===");

        // 1. 内存操作（TableList）- 所有操作都在内存中
        Console.WriteLine("\n方案 A: 使用 TableList（内存优先，立即同步）");
        var (memMs, memNs) = MeasureHighPrecision(() =>
        {
            var players = Enumerable.Range(1, playerCount)
                .Select(i => new Player
                {
                    PlayerName = $"Player{i}",
                    Email = $"player{i}@game.com",
                    Level = 10 + i,
                    ActiveMods = new TableList<ActiveMod>(),
                    Achievements = new TableList<PlayerAchievement>()
                })
                .ToList();

            // 批量添加 Mod
            foreach (var player in players)
            {
                var mods = Enumerable.Range(1, modsPerPlayer)
                    .Select(j => new ActiveMod
                    {
                        ModId = j,
                        ModName = $"Mod{j}",
                        ActivatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        ExpiresAt = 0
                    })
                    .ToList();

                player.ActiveMods.AddRange(mods);
            }

            // 查询操作
            foreach (var player in players)
            {
                var query = player.ActiveMods.Where(m => m.ModId > 50).ToList();
            }
        });

        int totalOps = playerCount * modsPerPlayer;
        LogHighPrecisionPerformance($"TableList（添加+查询 {totalOps} 项）", memMs, memNs, totalOps);

        // 2. 数据库操作 - 每条插入都立即同步到数据库
        Console.WriteLine("\nMethod B: 使用 TableList with Database Sync（实际场景）");
        var db = new SqliteV2<Player>("./test_perf_compare.db", "./test_db");

        var (dbMs, dbNs) = MeasureHighPrecision(() =>
        {
            var players = Enumerable.Range(1, playerCount)
                .Select(i => new Player
                {
                    PlayerName = $"Player{i}",
                    Email = $"player{i}@game.com",
                    Level = 10 + i,
                    ActiveMods = new TableList<ActiveMod>(),
                    Achievements = new TableList<PlayerAchievement>()
                })
                .ToList();

            db.InsertBatch(players);

            // 添加 Mod（每次添加都同步到数据库）
            foreach (var player in players)
            {
                for (int j = 0; j < modsPerPlayer; j++)
                {
                    player.ActiveMods.Add(new ActiveMod
                    {
                        ModId = j + 1,
                        ModName = $"Mod{j + 1}",
                        ActivatedAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        ExpiresAt = 0
                    });
                }
            }

            // 查询操作
            var allPlayers = db.SelectAll();
            foreach (var player in allPlayers)
            {
                var query = player.ActiveMods.Where(m => m.ModId > 50).ToList();
            }
        });

        LogHighPrecisionPerformance($"TableList with DB（添加+查询+同步 {totalOps} 项）", dbMs, dbNs, totalOps);

        // 性能对比
        double ratio = dbMs > 0 && memMs > 0 
            ? (double)dbMs / memMs 
            : (dbNs > 0 && memNs > 0)
                ? (double)dbNs / memNs
                : 1.0;

        Console.WriteLine($"\n性能分析: 数据库同步比纯内存慢 {ratio:F1}x 倍（这是正常的）");
        Console.WriteLine($"立即同步的优势: 数据一致性强，适合频繁修改的场景");
    }

    #region 主测试入口

    public static async Task RunAllTests()
    {
        Console.WriteLine("\n╔════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        TableList 高效子表系统 性能测试套件                  ║");
        Console.WriteLine("║                                                            ║");
        Console.WriteLine("║  数据模型：Player (主表) → ActiveMod (子表, TableList)      ║");
        Console.WriteLine("║  特性：String 类型 ID、时间戳、立即同步、完整 LINQ 支持    ║");
        Console.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

        // 先运行诊断测试
        TestDiagnosticFlow();

        Console.WriteLine("【TableList 基础操作测试】");
        TestTableListAddPerformance(10, 100);
        TestTableListAddRangePerformance(10, 100);
        TestTableListRemovePerformance(10, 100);
        TestTableListClearPerformance(10, 100);

        Console.WriteLine("\n【LINQ 查询性能测试】");
        TestTableListWherePerformance(10, 100);
        TestTableListFirstOrDefaultPerformance(10, 100);
        TestTableListAnyPerformance(10, 100);
        TestTableListGroupByPerformance(10, 100);

        Console.WriteLine("\n【主表同步测试】");
        TestSmartSyncOnUpdate(5, 50);

        Console.WriteLine("\n【性能对比测试】");
        TestMemoryVsDatabasePerformance(5, 100);

        Console.WriteLine("\n✅ 所有 TableList 性能测试完成！");
    }

    #endregion
}
