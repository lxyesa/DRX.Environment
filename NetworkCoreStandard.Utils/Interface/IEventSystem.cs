using NetworkCoreStandard.EventArgs;

namespace NetworkCoreStandard.Utils.Interface;

public interface IEventSystem
{
    void AddListener(string eventName, EventHandler<NetworkEventArgs> handler);
    void AddListener(uint eventId, EventHandler<NetworkEventArgs> handler);
    void RemoveListener(string eventName, EventHandler<NetworkEventArgs> handler);
    Task PushEventAsync(string eventName, NetworkEventArgs args);
    Task PushEventAsync(uint eventId, NetworkEventArgs args);
    void StartEventProcessing();
    void StopEventProcessing();
}
