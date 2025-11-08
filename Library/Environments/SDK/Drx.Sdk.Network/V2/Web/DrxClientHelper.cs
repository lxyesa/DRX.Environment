using System;
using System.Collections.Specialized;

namespace Drx.Sdk.Network.V2.Web;

public static class DrxClientHelper
{
    public static void AddDefaultHeaders(HttpRequest request)
    {
        request.Headers ??= new NameValueCollection();
        request.Headers["User-Agent"] = "DrxHttpClient/1.0";
        request.Headers["Accept"] = "application/json";
    }

    public static void AddAuthorizationHeader(HttpRequest request, string token)
    {
        request.Headers ??= new NameValueCollection();
        request.Headers["Authorization"] = $"Bearer {token}";
    }
}
