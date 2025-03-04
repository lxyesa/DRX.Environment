using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Interfaces
{

    /// <summary>
    /// 中间件接口
    /// </summary>
    public interface IMiddleware
    {
        Task Invoke(HttpListenerContext context);
    }
}
