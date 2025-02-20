using Drx.Sdk.Network;
using Drx.Sdk.Network.Attributes;
using Drx.Sdk.Network.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DRXServer.Apis
{
    [API("ConnectTestApi")]
    public class ConnectTestApi : HttpApiBase
    {
        [HttpGet("/check")]
        public async Task CheckConnect(HttpListenerContext context)
        {
            SetContext(context);
            await Ok();
        }
    }
}
