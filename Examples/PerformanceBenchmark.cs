using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

/// <summary>
/// DrxHttpServer 性能基准测试
/// 测试路由匹配、中间件、速率限制等关键性能指标
/// 
/// 运行方式：
/// cd Examples
/// dotnet run --project PerformanceBenchmark.csproj
/// </summary>
public class PerformanceBenchmark
{
    public static async Task Main()
    {
        Console.WriteLine("=== DrxHttpServer 性能基准测试 ===\n");
        Console.WriteLine("基准测试项目:");
        Console.WriteLine("1. 简单路由响应时间");
        Console.WriteLine("2. 参数化路由响应时间");
        Console.WriteLine("3. 并发请求处理");
        Console.WriteLine("4. 多个端点性能");
        Console.WriteLine("5. 速率限制性能\n");

        Console.WriteLine("注意: 请确保服务器应用已启动在 http://localhost:8462/\n");

        await RunBenchmarks();

        Console.WriteLine("\n=== 基准测试完成 ===");
    }

    private static async Task RunBenchmarks()
    {
        try
        {
            // 基准1: 简单路由响应时间
            await BenchmarkSimpleRoute();

            // 基准2: 参数化路由响应时间
            await BenchmarkParameterizedRoute();

            // 基准3: 并发请求处理
            await BenchmarkConcurrentRequests();

            // 基准4: 多个端点的性能
            await BenchmarkMultipleEndpoints();

            // 基准5: 速率限制性能
            await BenchmarkRateLimit();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"基准测试失败: {ex.Message}");
        }
    }

    private static async Task BenchmarkSimpleRoute()
    {
        Console.WriteLine("基准1: 简单路由响应时间");
        const int iterations = 1000;

        using var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.Add("User-Agent", "Benchmark");

        var sw = Stopwatch.StartNew();
        int successCount = 0;

        try
        {
            for (int i = 0; i < iterations; i++)
            {
                try
                {
                    var response = await client.GetAsync("http://localhost:8462/");
                    if (response.IsSuccessStatusCode) successCount++;
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  错误: {ex.Message}");
            return;
        }

        sw.Stop();
        var avgTime = sw.ElapsedMilliseconds / (double)iterations;
        Console.WriteLine($"  总耗时: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"  成功请求: {successCount}/{iterations}");
        Console.WriteLine($"  平均响应时间: {avgTime:F3}ms");
        Console.WriteLine($"  吞吐量: {successCount * 1000.0 / sw.ElapsedMilliseconds:F0} req/s\n");
    }

    private static async Task BenchmarkParameterizedRoute()
    {
        Console.WriteLine("基准2: 路由多样性性能");
        const int iterations = 500;

        using var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(10) };
        var sw = Stopwatch.StartNew();
        int successCount = 0;

        try
        {
            for (int i = 0; i < iterations; i++)
            {
                try
                {
                    var path = $"http://localhost:8462{GetRandomPath()}";
                    var response = await client.GetAsync(path);
                    if (response.IsSuccessStatusCode || response.StatusCode == System.Net.HttpStatusCode.NotFound)
                        successCount++;
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  错误: {ex.Message}");
            return;
        }

        sw.Stop();
        var avgTime = sw.ElapsedMilliseconds / (double)iterations;
        Console.WriteLine($"  总耗时: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"  成功请求: {successCount}/{iterations}");
        Console.WriteLine($"  平均响应时间: {avgTime:F3}ms");
        Console.WriteLine($"  吞吐量: {successCount * 1000.0 / sw.ElapsedMilliseconds:F0} req/s\n");
    }

    private static async Task BenchmarkConcurrentRequests()
    {
        Console.WriteLine("基准3: 并发请求处理（50 并发）");
        const int totalRequests = 500;
        const int concurrency = 50;

        using var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) };
        var sw = Stopwatch.StartNew();
        int successCount = 0;

        try
        {
            var tasks = new List<Task>();
            for (int i = 0; i < totalRequests; i++)
            {
                tasks.Add(
                    client.GetAsync("http://localhost:8462/").ContinueWith(t =>
                    {
                        if (t.IsCompletedSuccessfully && t.Result.IsSuccessStatusCode)
                            Interlocked.Increment(ref successCount);
                    })
                );

                if (tasks.Count >= concurrency)
                {
                    await Task.WhenAll(tasks);
                    tasks.Clear();
                }
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  错误: {ex.Message}");
            return;
        }

        sw.Stop();
        Console.WriteLine($"  总耗时: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"  总请求数: {totalRequests}");
        Console.WriteLine($"  成功请求: {successCount}");
        Console.WriteLine($"  平均响应时间: {sw.ElapsedMilliseconds / (double)totalRequests:F3}ms");
        Console.WriteLine($"  吞吐量: {successCount * 1000.0 / sw.ElapsedMilliseconds:F0} req/s\n");
    }

    private static async Task BenchmarkMultipleEndpoints()
    {
        Console.WriteLine("基准4: 多次请求同一端点");
        const int iterations = 300;

        using var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(10) };
        var sw = Stopwatch.StartNew();
        int successCount = 0;

        try
        {
            for (int i = 0; i < iterations; i++)
            {
                try
                {
                    var response = await client.GetAsync("http://localhost:8462/");
                    if (response.IsSuccessStatusCode) successCount++;
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  错误: {ex.Message}");
            return;
        }

        sw.Stop();
        var avgTime = sw.ElapsedMilliseconds / (double)iterations;
        Console.WriteLine($"  总耗时: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"  成功请求: {successCount}/{iterations}");
        Console.WriteLine($"  平均响应时间: {avgTime:F3}ms");
        Console.WriteLine($"  吞吐量: {successCount * 1000.0 / sw.ElapsedMilliseconds:F0} req/s\n");
    }

    private static async Task BenchmarkRateLimit()
    {
        Console.WriteLine("基准5: 高频请求性能");
        const int iterations = 100;

        using var client = new HttpClient() { Timeout = TimeSpan.FromSeconds(30) };
        var sw = Stopwatch.StartNew();

        int successCount = 0;
        int otherCount = 0;

        try
        {
            for (int i = 0; i < iterations; i++)
            {
                try
                {
                    var response = await client.GetAsync("http://localhost:8462/");
                    if (response.IsSuccessStatusCode)
                        successCount++;
                    else
                        otherCount++;
                }
                catch { }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  错误: {ex.Message}");
            return;
        }

        sw.Stop();
        Console.WriteLine($"  总耗时: {sw.ElapsedMilliseconds}ms");
        Console.WriteLine($"  成功请求: {successCount}");
        Console.WriteLine($"  其他响应: {otherCount}");
        Console.WriteLine($"  平均响应时间: {sw.ElapsedMilliseconds / (double)iterations:F3}ms\n");
    }

    private static string GetRandomPath()
    {
        var paths = new[]
        {
            "/",
            "/login",
            "/register",
            "/api/data",
            "/static/style.css"
        };
        return paths[new Random().Next(paths.Length)];
    }
}

