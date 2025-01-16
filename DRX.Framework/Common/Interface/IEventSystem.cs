using DRX.Framework.Common.Args;

namespace DRX.Framework.Common.Interface;

public interface IEventSystem
{
    Guid AddListener(string eventName, EventHandler<NetworkEventArgs> handler);
    Guid AddListener(string eventName, EventHandler<NetworkEventArgs> handler, string uniqueId);
    void AddListener(uint eventId, EventHandler<NetworkEventArgs> handler);
    void RemoveListener(Guid handlerId);
    void RemoveListener(string eventName, Guid handlerId);
    Task PushEventAsync(string eventName, NetworkEventArgs args);
    Task PushEventAsync(uint eventId, NetworkEventArgs args);
    void StartEventProcessing();
    void StopEventProcessing();
}
