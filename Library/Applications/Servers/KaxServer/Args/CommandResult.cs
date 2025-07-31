using Drx.Sdk.Network.Socket;

public struct CommandResult
{
    public SocketStatusCode StatusCode { get; set; }
    public string Message { get; set; }
    public object Data { get; set; }
}