using System;
using System.Threading.Tasks;

namespace Drx.Sdk.Network.Http.Entry
{
    internal class CommandQueueEntry
    {
        public string CommandInput { get; set; }
        public Func<string, Task>? OnCompleted { get; set; }
    }
}