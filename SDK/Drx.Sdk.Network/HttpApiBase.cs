using Drx.Sdk.Network.Attributes;
using Drx.Sdk.Network.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Network
{
    [API("HttpApiBase")]
    public abstract class HttpApiBase : IAPI
    {
        protected HttpListenerContext? Context { get; set; }

        public void SetContext(HttpListenerContext context)
        {
            Context = context;
        }

        public virtual async Task ResultAsync(string result, 
            int statusCode = 200, string statusDescription = "Ok")
        {
            if (Context == null) throw new InvalidOperationException("Context is not set.");
            byte[] buffer = Encoding.UTF8.GetBytes(result);
            Context.Response.StatusCode = statusCode;
            Context.Response.StatusDescription = statusDescription;
            Context.Response.ContentLength64 = buffer.Length;
            await Context.Response.OutputStream.WriteAsync(buffer);
            await Context.Response.OutputStream.FlushAsync();
            Context.Response.OutputStream.Close();
        }

        public virtual async Task Ok(string statusDescription = "Ok")
        {
            if (Context == null) throw new InvalidOperationException("Context is not set.");
            Context.Response.StatusCode = 200;
            Context.Response.StatusDescription = statusDescription;
            await Context.Response.OutputStream.FlushAsync();
            Context.Response.OutputStream.Close();
        }
    }
}
