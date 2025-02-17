using Drx.Sdk.Network;
using Drx.Sdk.Network.Helpers;
using Drx.Sdk.Network.Interfaces;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Drx.Sdk
{
    public class HttpServerSetup
    {
        public async Task ConfigureAsync()
        {
            var server = new HttpServer("http://localhost:5000/");

            // ��ʼ����ע��API�Ѿ���HttpServer���캯�������

            // ����м��ʾ��
            server.AddComponent(new LoggingMiddleware());

            await server.StartAsync();
        }
    }

    /// <summary>
    /// ʾ���м����������־��¼
    /// </summary>
    public class LoggingMiddleware : IMiddleware
    {
        public Task Invoke(HttpListenerContext context)
        {
            Console.WriteLine($"�յ�����: {context.Request.HttpMethod} {context.Request.Url}");
            return Task.CompletedTask;
        }
    }
}
