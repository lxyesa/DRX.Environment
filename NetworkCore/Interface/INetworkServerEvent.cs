using System.Net.Sockets;

public interface INetworkServerEvent{
    void OnServerStarted(Socket serverSocket);
    void OnServerStopped(Socket serverSocket);
    void OnServerTick(Socket serverSocket);
    void OnClientMessage(Socket clientSocket, byte[] message);
    void OnClientDisconnected(Socket clientSocket);
    void OnClientConnected(Socket clientSocket);
}