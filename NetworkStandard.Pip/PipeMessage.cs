using System;

namespace NetworkStandard.Pip;

public class PipeMessage
{
    public string ClientId { get; set; }
    public byte[] Data { get; set; }
    public DateTime Timestamp { get; set; }

    public PipeMessage(string clientId, byte[] data)
    {
        ClientId = clientId;
        Data = data;
        Timestamp = DateTime.Now;
    }
}