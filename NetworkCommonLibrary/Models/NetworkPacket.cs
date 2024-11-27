using System;

namespace NetworkCommonLibrary.Models;

public class NetworkPacket
{
    public string? Header { get; set; }
    public object? Body { get; set; }  // 改为 object 类型
    public string? Key { get; set; }
    public int Type { get; set; }
}

