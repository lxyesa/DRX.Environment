using NetworkCoreStandard.Common.Interface;
using NetworkCoreStandard.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworkCoreStandard.Common.Components
{
    public class Verify : IComponent
    {
        public object? Owner { get; set; }
        public DateTime LastActiveTime { get; private set; } = DateTime.Now;
        public void UpdateLastActiveTime()
        {
            LastActiveTime = DateTime.Now;
        }
        public DateTime GetLastActiveTime()
        {
            return LastActiveTime;
        }



        public void Awake()
        {
        }

        public void Dispose()
        {
            
        }

        public void OnDestroy()
        {
            
        }

        public void Start()
        {
            
        }

    }
}
