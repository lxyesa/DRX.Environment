using DRX.Framework.Common.Interface;

namespace DRX.Framework.Common.Components
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
