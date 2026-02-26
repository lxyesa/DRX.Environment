using System;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Http.Entry
{
    internal class TickerEntry
    {
        public int Id;
        public int IntervalMs;
        public long NextDueMs;
        public Action<DrxHttpServer>? SyncCallback;
        public Func<DrxHttpServer, Task>? AsyncCallback;
        public volatile bool Cancelled;
    }
}