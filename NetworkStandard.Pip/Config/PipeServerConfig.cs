using System;
using System.IO.Pipes;
using NetworkCoreStandard.Utils;

namespace NetworkStandard.Pip.Config;

public class PipeServerConfig : ConfigItem
{
    public string PipeName { get; set; } = "NDV_Pipe";
    public int BufferSize { get; set; } = 4096;
    public int MaxQueueSize { get; set; } = 1000;
    public int MaxConnections { get; set; } = 10;
    public PipeTransmissionMode TransmissionMode { get; set; } = PipeTransmissionMode.Byte;
    public PipeOptions PipeOptions { get; set; } = PipeOptions.Asynchronous;
}