using System;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Protocol;

namespace KaxSocket.Examples
{
    /// <summary>
    /// JSON 序列化示例
    /// 展示如何在不同场景下使用 DrxHttpServer 的 JSON 序列化功能
    /// </summary>
    public class JsonSerializationExample
    {
        /// <summary>
        /// 示例 DTO 类
        /// </summary>
        public class UserDto
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Email { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        /// <summary>
        /// 示例：默认配置（推荐）
        /// 使用链式回退模式，自动支持所有环境
        /// </summary>
        public async Task Example_DefaultConfiguration()
        {
            Console.WriteLine("=== 示例 1: 默认配置（链式回退模式）===");

            var server = new DrxHttpServer(new[] { "http://+:8080/" });

            // 添加一个返回对象的路由
            server.AddRoute(
                System.Net.Http.HttpMethod.Get,
                "/api/user/{id}",
                req =>
                {
                    // 框架会自动使用 DrxJsonSerializerManager 序列化这个对象
                    var user = new UserDto
                    {
                        Id = 1,
                        Name = "张三",
                        Email = "zhangsan@example.com",
                        CreatedAt = DateTime.UtcNow
                    };

                    // 返回包含对象的响应
                    var response = new HttpResponse(200)
                    {
                        BodyObject = user  // 框架会自动序列化为 JSON
                    };

                    return response;
                }
            );

            Console.WriteLine("✓ 服务器已配置，支持自动 JSON 序列化");
            Console.WriteLine("✓ 默认使用链式回退模式（反射 → 安全模式）");
        }

        /// <summary>
        /// 示例：启用代码裁剪时的安全模式
        /// </summary>
        public async Task Example_SafeMode()
        {
            Console.WriteLine("\n=== 示例 2: 安全模式（用于代码裁剪环境）===");

            // 在应用启动时配置为安全模式
            DrxHttpServer.ConfigureJsonSerializerSafeMode();
            Console.WriteLine("✓ 已切换到安全模式");

            var server = new DrxHttpServer(new[] { "http://+:8080/" });

            server.AddRoute(
                System.Net.Http.HttpMethod.Post,
                "/api/users",
                req =>
                {
                    var user = new UserDto
                    {
                        Id = 2,
                        Name = "李四",
                        Email = "lisi@example.com",
                        CreatedAt = DateTime.UtcNow
                    };

                    return new HttpResponse(201)
                    {
                        BodyObject = user
                    };
                }
            );

            Console.WriteLine("✓ 即使序列化失败，也会返回有意义的错误响应");
        }

        /// <summary>
        /// 示例：反射模式（结合 DynamicDependency 注解）
        /// 性能最好，但需要为所有要序列化的类型添加注解
        /// </summary>
        public async Task Example_ReflectionMode()
        {
            Console.WriteLine("\n=== 示例 3: 反射模式（高性能）===");

            // 配置为反射模式
            DrxHttpServer.ConfigureJsonSerializerReflectionMode();
            Console.WriteLine("✓ 已切换到反射模式");
            Console.WriteLine("⚠ 注：启用裁剪时需要为相关类型添加 DynamicDependency 注解");

            var server = new DrxHttpServer(new[] { "http://+:8080/" });

            server.AddRoute(
                System.Net.Http.HttpMethod.Get,
                "/api/users",
                req =>
                {
                    var users = new[]
                    {
                        new UserDto { Id = 1, Name = "张三", Email = "zhangsan@example.com", CreatedAt = DateTime.UtcNow },
                        new UserDto { Id = 2, Name = "李四", Email = "lisi@example.com", CreatedAt = DateTime.UtcNow }
                    };

                    return new HttpResponse(200)
                    {
                        BodyObject = users
                    };
                }
            );

            Console.WriteLine("✓ 使用反射序列化，支持任意 .NET 类型");
        }

        /// <summary>
        /// 示例：自定义序列化器实现
        /// </summary>
        public class CustomFastJsonSerializer : IDrxJsonSerializer
        {
            public string SerializerName => "Custom Fast JSON Serializer";

            public bool TrySerialize(object obj, out string? json)
            {
                json = null;
                if (obj == null) return false;

                try
                {
                    // 这里可以使用任何高性能的 JSON 库（如 Newtonsoft.Json、Jil 等）
                    // 示例：使用 System.Text.Json
                    json = System.Text.Json.JsonSerializer.Serialize(obj, new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
                    });
                    return true;
                }
                catch
                {
                    return false;
                }
            }
        }

        public async Task Example_CustomSerializer()
        {
            Console.WriteLine("\n=== 示例 4: 自定义序列化器===");

            // 创建并配置自定义序列化器
            var customSerializer = new CustomFastJsonSerializer();
            DrxHttpServer.ConfigureJsonSerializer(customSerializer);
            Console.WriteLine("✓ 已配置自定义序列化器");

            var server = new DrxHttpServer(new[] { "http://+:8080/" });

            server.AddRoute(
                System.Net.Http.HttpMethod.Get,
                "/api/profile",
                req =>
                {
                    return new HttpResponse(200)
                    {
                        BodyObject = new { message = "自定义序列化器示例" }
                    };
                }
            );

            Console.WriteLine("✓ 使用自定义序列化器处理所有 JSON 响应");
        }

        /// <summary>
        /// 示例：直接使用序列化管理器 API
        /// </summary>
        public void Example_DirectApiUsage()
        {
            Console.WriteLine("\n=== 示例 5: 直接使用 API===");

            var user = new UserDto
            {
                Id = 100,
                Name = "王五",
                Email = "wangwu@example.com",
                CreatedAt = DateTime.UtcNow
            };

            // 使用全局序列化管理器尝试序列化
            if (DrxJsonSerializerManager.TrySerialize(user, out var json))
            {
                Console.WriteLine("✓ 序列化成功:");
                Console.WriteLine(json);
            }
            else
            {
                Console.WriteLine("✗ 序列化失败");
            }
        }

        /// <summary>
        /// 示例：处理序列化失败
        /// </summary>
        public void Example_ErrorHandling()
        {
            Console.WriteLine("\n=== 示例 6: 错误处理===");

            // 不可序列化的对象（例如包含循环引用的对象）
            var circularRefObj = new { };

            // 尝试序列化
            if (!DrxJsonSerializerManager.TrySerialize(circularRefObj, out var json))
            {
                Console.WriteLine("⚠ 序列化失败，使用回退处理");
                // 可以在这里做特殊处理
                json = @"{ ""error"": ""无法序列化该对象"" }";
            }

            Console.WriteLine("✓ 最终响应: " + json);
        }

        /// <summary>
        /// 运行所有示例
        /// </summary>
        public async Task RunAllExamples()
        {
            Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║  DrxHttpServer JSON 序列化示例                               ║");
            Console.WriteLine("╚══════════════════════════════════════════════════════════════╝\n");

            try
            {
                await Example_DefaultConfiguration();
                await Example_SafeMode();
                await Example_ReflectionMode();
                await Example_CustomSerializer();
                Example_DirectApiUsage();
                Example_ErrorHandling();

                Console.WriteLine("\n╔══════════════════════════════════════════════════════════════╗");
                Console.WriteLine("║  所有示例完成！                                              ║");
                Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n✗ 示例执行出错: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 为 JSON 序列化的类型保留元数据的示例
    /// 在启用代码裁剪时，将此文件包含到项目中
    /// </summary>
    public static class PreserveJsonSerializableTypes
    {
        [System.Diagnostics.CodeAnalysis.DynamicDependency(
            System.Diagnostics.CodeAnalysis.DynamicallyAccessedMemberTypes.All,
            typeof(JsonSerializationExample.UserDto))]
        public static void PreserveTypes() { }
    }
}
