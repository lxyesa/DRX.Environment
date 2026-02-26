using System;
using System.Threading;

namespace Drx.Sdk.Network.Http.Entry
{
    internal class TickerRegistration : IDisposable
    {
        private readonly DrxHttpServer _server;
        private readonly int _id;
        private int _disposed;
        public TickerRegistration(DrxHttpServer server, int id) { _server = server; _id = id; }
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _server.UnregisterTicker(_id);
            }
        }
    }
}