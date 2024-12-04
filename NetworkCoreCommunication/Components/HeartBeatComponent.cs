using System;
using NetworkCoreStandard.Interface;
using NetworkCoreStandard.Models;
using NetworkCoreStandard.Utils;

namespace NetworkCoreCommunication.Components;

public class HeartBeatComponent : IComponent
{
    private DateTime _lastHeartbeatTime;
    private const int HEARTBEAT_TIMEOUT = 10 * 60 * 1000;  // 10 min to timeout (this is a default value)


    public void UpdateHeartbeat()
    {
        _lastHeartbeatTime = DateTime.Now;
    }

    public bool IsTimeout()
    {
        return (DateTime.Now - _lastHeartbeatTime).TotalMilliseconds > HEARTBEAT_TIMEOUT;
    }

    public override void Start()
    {
        // set the last heartbeat
        _lastHeartbeatTime = DateTime.Now;
        Logger.Log("HeartBeatComponent", $"HeartBeatComponent started {_lastHeartbeatTime}");
    }

    public override void Awake()
    {
        // Do nothing
    }
}