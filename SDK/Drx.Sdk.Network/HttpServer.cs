using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Drx.Sdk.Network.Attributes;
using Drx.Sdk.Network.Helpers;
using Drx.Sdk.Network.Interfaces;

namespace Drx.Sdk.Network
{
    /// <summary>
    /// HTTP服务器类，用于接受请求，返回客户端信息，并管理API和中间件
    /// </summary>
    public class HttpServer
    {
        private readonly HttpListener _listener;
        private readonly List<IMiddleware> _middlewares;
        private readonly Dictionary<(string Method, string Path), MethodInfo> _routeTable;
        private readonly List<object> _apiInstances; // 改为List存储多个API实例

        public HttpServer(string prefix)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _middlewares = new List<IMiddleware>();
            _routeTable = new Dictionary<(string Method, string Path), MethodInfo>();
            _apiInstances = new List<object>(); // 初始化API实例列表
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="apiInstance"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public void RegisterApi(object apiInstance)
        {
            if (apiInstance == null)
                throw new ArgumentNullException(nameof(apiInstance));

            _apiInstances.Add(apiInstance);
            RegisterRoutesForApi(apiInstance);
        }

        /// <summary>
        /// 启动HTTP服务器
        /// </summary>
        public async Task StartAsync()
        {
            _listener.Start();
            Logger($"服务器已启动，监听地址：{string.Join(", ", _listener.Prefixes)}");
            await HandleRequests();
        }

        /// <summary>
        /// 停止HTTP服务器
        /// </summary>
        public void Stop()
        {
            _listener.Stop();
        }

        private void RegisterRoutesForApi(object apiInstance)
        {
            var apiType = apiInstance.GetType();

            // 获取类级别的APIAttribute
            var apiAttr = apiType.GetCustomAttribute<APIAttribute>();
            if (apiAttr == null)
            {
                Logger($"API类 {apiType.Name} 未标记 [API] 属性");
                return;
            }

            var basePath = apiAttr.BasePath.TrimEnd('/');
            var methods = apiType.GetMethods(BindingFlags.Public | BindingFlags.Instance);

            foreach (var method in methods)
            {
                var getAttr = method.GetCustomAttribute<HttpGetAttribute>();
                if (getAttr != null)
                {
                    var fullPath = $"{basePath}/{getAttr.Path.TrimStart('/')}";
                    _routeTable.Add(("GET", fullPath), method);
                    Logger($"注册GET路由: GET {fullPath}");
                }

                var postAttr = method.GetCustomAttribute<HttpPostAttribute>();
                if (postAttr != null)
                {
                    var fullPath = $"{basePath}/{postAttr.Path.TrimStart('/')}";
                    _routeTable.Add(("POST", fullPath), method);
                    Logger($"注册POST路由: POST {fullPath}");
                }
            }
        }

        private async Task HandleRequests()
        {
            while (_listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => ProcessRequest(context));
                }
                catch (HttpListenerException)
                {
                    // Listener was stopped, exit gracefully
                    break;
                }
                catch (Exception ex)
                {
                    Logger($"错误处理请求: {ex.Message}");
                }
            }
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            // 处理中间件
            foreach (var middleware in _middlewares)
            {
                await middleware.Invoke(context);
            }

            // 处理API
            string path = context.Request.Url.AbsolutePath.TrimStart('/');
            string method = context.Request.HttpMethod.ToUpper();

            if (_routeTable.TryGetValue((method, path), out var targetMethod))
            {
                try
                {
                    // 找到对应的API实例
                    var apiInstance = _apiInstances.FirstOrDefault(api =>
                        targetMethod.DeclaringType == api.GetType());

                    if (apiInstance == null)
                    {
                        await ResponseHelper.NotFound(context, "找不到对应的API实例");
                        return;
                    }

                    object[] parameters = null;

                    if (method == "POST")
                    {
                        using (var reader = new System.IO.StreamReader(context.Request.InputStream, context.Request.ContentEncoding))
                        {
                            string body = await reader.ReadToEndAsync();
                            parameters = new object[] { body, context };
                        }
                    }
                    else if (method == "GET")
                    {
                        parameters = new object[] { context };
                    }

                    var result = targetMethod.Invoke(apiInstance, parameters);
                    if (result is Task task)
                    {
                        await task;
                    }
                }
                catch (TargetInvocationException tie)
                {
                    Logger($"API执行错误: {tie.InnerException?.Message}");
                    await ResponseHelper.BadRequest(context, "API执行过程中发生错误");
                }
                catch (Exception ex)
                {
                    Logger($"处理请求时出错: {ex.Message}");
                    await ResponseHelper.BadRequest(context, "服务器内部错误");
                }
            }
            else
            {
                // 未注册的API路径，返回404
                await ResponseHelper.NotFound(context, "API 未找到");
            }
        }

        /// <summary>
        /// 添加中间件组件
        /// </summary>
        /// <param name="middleware">中间件实例</param>
        public void AddComponent(IMiddleware middleware)
        {
            _middlewares.Add(middleware);
        }

        private void Logger(string message)
        {
            // 日志格式：[时间] [LoggerType] [消息]
            Console.WriteLine($"[{DateTime.Now}] [Logger] {message}");
        }
    }
}
