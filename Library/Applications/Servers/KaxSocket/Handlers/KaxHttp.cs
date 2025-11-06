using System;
using Drx.Sdk.Network.V2.Web;

namespace KaxSocket.Handlers;

public class KaxHttp
{
    [HttpHandle("/api/user/register", "POST")]
    public static HttpResponse PostRegister(HttpRequest request)
    {
        // todo
        return null;
    }

    [HttpHandle("/api/user/login", "POST")]
    public static HttpResponse PostLogin(HttpRequest request)
    {
        // todo
        return null;
    }
}
