using System;
using NetworkCoreStandard.Interface;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;

namespace NetworkCoreStandard.Components;

public class HeartBeatComponent : IComponent
{
    private DateTime _lastHeartbeatTime;
    private int HEARTBEAT_TIMEOUT = 10 * 60 * 1000;  // 10 min to timeout (this is a default value)

    public void UpdateHeartbeat()
    {
        _lastHeartbeatTime = DateTime.Now;
    }

    public bool IsTimeout()
    {
        return (DateTime.Now - _lastHeartbeatTime).TotalMilliseconds > HEARTBEAT_TIMEOUT;
    }

    public DateTime GetLastHeartbeatTime()
    {
        return _lastHeartbeatTime;
    }

    public override void Start()
    {
        // set the last heartbeat
        _lastHeartbeatTime = DateTime.Now;
    }

    public override void Awake()
    {
        // Do nothing
    }

    public void SetHeartbeatTimeout(int timeout)
    {
        HEARTBEAT_TIMEOUT = timeout;
    }
}