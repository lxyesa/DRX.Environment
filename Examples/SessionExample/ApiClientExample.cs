using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;

namespace SessionApiClientExample
{
    /// <summary>
    /// 会话API客户端示例
    /// 演示如何在API客户端中使用DRX.Environment会话系统
    /// </summary>
    public class SessionApiClient
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string _cookieFile;
        private CookieContainer _cookieContainer;
        private HttpClientHandler _handler;

        public SessionApiClient(string baseUrl = "http://localhost:8080", string cookieFile = "session_cookies.txt")
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _cookieFile = cookieFile;

            // 初始化Cookie容器
            _cookieContainer = new CookieContainer();
            _handler = new HttpClientHandler { CookieContainer = _cookieContainer };
            _httpClient = new HttpClient(_handler);

            // 设置默认请求头
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "DRX.SessionApiClient/1.0");

            // 加载保存的Cookie
            LoadCookies();
        }

        /// <summary>
        /// 登录并建立会话
        /// </summary>
        public async Task<bool> LoginAsync(string username, string password)
        {
            try
            {
                Console.WriteLine($"正在登录用户: {username}");

                var loginData = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("username", username),
                    new KeyValuePair<string, string>("password", password)
                });

                var response = await _httpClient.PostAsync($"{_baseUrl}/login", loginData);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"登录成功: {result}");

                    // 保存Cookie到文件
                    SaveCookies();

                    return true;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Console.WriteLine($"登录失败: {error}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"登录时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 检查当前登录状态
        /// </summary>
        public async Task<LoginStatus> CheckLoginStatusAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/auth/status");

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var status = JsonSerializer.Deserialize<LoginStatus>(json);
                    status.IsLoggedIn = true;
                    return status;
                }
                else
                {
                    return new LoginStatus { IsLoggedIn = false, Message = "未登录" };
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"检查登录状态时发生错误: {ex.Message}");
                return new LoginStatus { IsLoggedIn = false, Message = $"错误: {ex.Message}" };
            }
        }

        /// <summary>
        /// 尝试自动登录（如果有保存的会话）
        /// </summary>
        public async Task<bool> TryAutoLoginAsync()
        {
            try
            {
                var status = await CheckLoginStatusAsync();
                if (status.IsLoggedIn && status.AutoLogin)
                {
                    Console.WriteLine($"自动登录成功，用户: {status.User}");
                    return true;
                }
                else if (status.IsLoggedIn)
                {
                    Console.WriteLine($"会话有效，用户: {status.User} (非自动登录)");
                    return true;
                }
                else
                {
                    Console.WriteLine("无有效会话，需要重新登录");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"自动登录检查失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 访问受保护的资源
        /// </summary>
        public async Task<string> AccessProtectedResourceAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/protected");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return "需要登录才能访问此资源";
                }
                else
                {
                    return $"访问失败: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                return $"访问时发生错误: {ex.Message}";
            }
        }

        /// <summary>
        /// 添加商品到购物车
        /// </summary>
        public async Task<string> AddToCartAsync(string item)
        {
            try
            {
                var url = $"{_baseUrl}/cart/add?item={Uri.EscapeDataString(item)}";
                var response = await _httpClient.PostAsync(url, null);

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return "请先登录";
                }
                else
                {
                    return $"添加失败: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                return $"添加商品时发生错误: {ex.Message}";
            }
        }

        /// <summary>
        /// 查看购物车
        /// </summary>
        public async Task<string> ViewCartAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/cart");

                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadAsStringAsync();
                }
                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return "请先登录";
                }
                else
                {
                    return $"查看失败: {response.StatusCode}";
                }
            }
            catch (Exception ex)
            {
                return $"查看购物车时发生错误: {ex.Message}";
            }
        }

        /// <summary>
        /// 注销
        /// </summary>
        public async Task<bool> LogoutAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync($"{_baseUrl}/logout", null);

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine("注销成功");

                    // 清除本地Cookie
                    _cookieContainer = new CookieContainer();
                    _handler = new HttpClientHandler { CookieContainer = _cookieContainer };
                    var newClient = new HttpClient(_handler);
                    newClient.DefaultRequestHeaders.Add("User-Agent", "DRX.SessionApiClient/1.0");

                    // 替换旧的HttpClient
                    _httpClient.Dispose();
                    _httpClient = newClient;

                    // 删除保存的Cookie文件
                    if (File.Exists(_cookieFile))
                    {
                        File.Delete(_cookieFile);
                    }

                    return true;
                }
                else
                {
                    Console.WriteLine($"注销失败: {response.StatusCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"注销时发生错误: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 保存Cookie到文件
        /// </summary>
        private void SaveCookies()
        {
            try
            {
                var cookies = _cookieContainer.GetCookies(new Uri(_baseUrl));
                var cookieData = new List<string>();

                foreach (Cookie cookie in cookies)
                {
                    cookieData.Add($"{cookie.Name}={cookie.Value}");
                }

                File.WriteAllLines(_cookieFile, cookieData);
                Console.WriteLine($"Cookie已保存到: {_cookieFile}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"保存Cookie失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 从文件加载Cookie
        /// </summary>
        private void LoadCookies()
        {
            try
            {
                if (!File.Exists(_cookieFile))
                {
                    Console.WriteLine("未找到保存的Cookie文件");
                    return;
                }

                var cookieLines = File.ReadAllLines(_cookieFile);
                foreach (var line in cookieLines)
                {
                    var parts = line.Split('=', 2);
                    if (parts.Length == 2)
                    {
                        var cookie = new Cookie(parts[0], parts[1]);
                        _cookieContainer.Add(new Uri(_baseUrl), cookie);
                    }
                }

                Console.WriteLine($"已加载 {cookieLines.Length} 个Cookie");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载Cookie失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    /// <summary>
    /// 登录状态信息
    /// </summary>
    public class LoginStatus
    {
        public bool IsLoggedIn { get; set; }
        public string? User { get; set; }
        public bool AutoLogin { get; set; }
        public string? Message { get; set; }
    }

    class Program
    {
        static async Task Main()
        {
            Console.WriteLine("=== DRX.Environment 会话API客户端示例 ===\n");

            using var client = new SessionApiClient();

            // 1. 尝试自动登录
            Console.WriteLine("1. 检查自动登录...");
            var autoLoginSuccess = await client.TryAutoLoginAsync();

            if (!autoLoginSuccess)
            {
                // 2. 手动登录
                Console.WriteLine("\n2. 执行手动登录...");
                var loginSuccess = await client.LoginAsync("admin", "123456");

                if (!loginSuccess)
                {
                    Console.WriteLine("登录失败，程序退出");
                    return;
                }
            }

            // 3. 检查登录状态
            Console.WriteLine("\n3. 检查登录状态...");
            var status = await client.CheckLoginStatusAsync();
            Console.WriteLine($"状态: {(status.IsLoggedIn ? "已登录" : "未登录")}");
            if (status.IsLoggedIn)
            {
                Console.WriteLine($"用户: {status.User}");
                Console.WriteLine($"自动登录: {status.AutoLogin}");
            }

            // 4. 访问受保护资源
            Console.WriteLine("\n4. 访问受保护资源...");
            var protectedContent = await client.AccessProtectedResourceAsync();
            Console.WriteLine($"结果: {protectedContent}");

            // 5. 购物车操作
            Console.WriteLine("\n5. 购物车操作...");
            var addResult = await client.AddToCartAsync("苹果");
            Console.WriteLine($"添加商品: {addResult}");

            var cartResult = await client.ViewCartAsync();
            Console.WriteLine($"查看购物车: {cartResult}");

            // 添加更多商品
            await client.AddToCartAsync("香蕉");
            await client.AddToCartAsync("橙子");

            cartResult = await client.ViewCartAsync();
            Console.WriteLine($"更新后购物车: {cartResult}");

            // 6. 模拟程序重启 - 创建新客户端实例
            Console.WriteLine("\n6. 模拟程序重启...");
            using var newClient = new SessionApiClient();

            Console.WriteLine("新客户端检查自动登录...");
            var newAutoLogin = await newClient.TryAutoLoginAsync();

            if (newAutoLogin)
            {
                var newCart = await newClient.ViewCartAsync();
                Console.WriteLine($"重启后购物车: {newCart}");
            }

            // 7. 注销
            Console.WriteLine("\n7. 注销...");
            await client.LogoutAsync();

            // 验证注销
            var finalStatus = await client.CheckLoginStatusAsync();
            Console.WriteLine($"注销后状态: {(finalStatus.IsLoggedIn ? "仍登录" : "已注销")}");

            Console.WriteLine("\n=== 示例完成 ===");
            Console.WriteLine("注意: Cookie已保存到 session_cookies.txt");
            Console.WriteLine("下次运行程序时会自动加载Cookie尝试自动登录");
        }
    }
}