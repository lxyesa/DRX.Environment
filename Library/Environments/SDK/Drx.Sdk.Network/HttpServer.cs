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
    /// HTTP�������࣬���ڽ������󣬷��ؿͻ�����Ϣ��������API���м��
    /// </summary>
    public class HttpServer
    {
        private readonly HttpListener _listener;
        private readonly List<IMiddleware> _middlewares;
        private readonly Dictionary<(string Method, string Path), MethodInfo> _routeTable;
        private readonly List<object> _apiInstances; // ��ΪList�洢���APIʵ��

        public HttpServer(string prefix)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _middlewares = new List<IMiddleware>();
            _routeTable = new Dictionary<(string Method, string Path), MethodInfo>();
            _apiInstances = new List<object>(); // ��ʼ��APIʵ���б�
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
        /// ����HTTP������
        /// </summary>
        public async Task StartAsync()
        {
            _listener.Start();
            Logger($"��������������������ַ��{string.Join(", ", _listener.Prefixes)}");
            await HandleRequests();
        }

        /// <summary>
        /// ֹͣHTTP������
        /// </summary>
        public void Stop()
        {
            _listener.Stop();
        }

        private void RegisterRoutesForApi(object apiInstance)
        {
            var apiType = apiInstance.GetType();

            // ��ȡ�༶���APIAttribute
            var apiAttr = apiType.GetCustomAttribute<APIAttribute>();
            if (apiAttr == null)
            {
                Logger($"API�� {apiType.Name} δ��� [API] ����");
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
                    Logger($"ע��GET·��: GET {fullPath}");
                }

                var postAttr = method.GetCustomAttribute<HttpPostAttribute>();
                if (postAttr != null)
                {
                    var fullPath = $"{basePath}/{postAttr.Path.TrimStart('/')}";
                    _routeTable.Add(("POST", fullPath), method);
                    Logger($"ע��POST·��: POST {fullPath}");
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
                    Logger($"����������: {ex.Message}");
                }
            }
        }

        private async Task ProcessRequest(HttpListenerContext context)
        {
            // �����м��
            foreach (var middleware in _middlewares)
            {
                await middleware.Invoke(context);
            }

            // ����API
            string path = context.Request.Url.AbsolutePath.TrimStart('/');
            string method = context.Request.HttpMethod.ToUpper();

            if (_routeTable.TryGetValue((method, path), out var targetMethod))
            {
                try
                {
                    // �ҵ���Ӧ��APIʵ��
                    var apiInstance = _apiInstances.FirstOrDefault(api =>
                        targetMethod.DeclaringType == api.GetType());

                    if (apiInstance == null)
                    {
                        await ResponseHelper.NotFound(context, "�Ҳ�����Ӧ��APIʵ��");
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
                    Logger($"APIִ�д���: {tie.InnerException?.Message}");
                    await ResponseHelper.BadRequest(context, "APIִ�й����з�������");
                }
                catch (Exception ex)
                {
                    Logger($"��������ʱ����: {ex.Message}");
                    await ResponseHelper.BadRequest(context, "�������ڲ�����");
                }
            }
            else
            {
                // δע���API·��������404
                await ResponseHelper.NotFound(context, "API δ�ҵ�");
            }
        }

        /// <summary>
        /// �����м�����
        /// </summary>
        /// <param name="middleware">�м��ʵ��</param>
        public void AddComponent(IMiddleware middleware)
        {
            _middlewares.Add(middleware);
        }


        private void Logger(string message)
        {
            // 日志格式：[时间] [LoggerType] [信息]
            Console.WriteLine($"[{DateTime.Now}] [Logger] {message}");
        }
    }
}
