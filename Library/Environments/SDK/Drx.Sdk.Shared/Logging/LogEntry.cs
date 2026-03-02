namespace Drx.Sdk.Shared;

/// <summary>
/// 日志条目的内部传输结构体，用于 Channel 队列。
/// </summary>
internal struct LogEntry
{
    public LogLevel Level;
    public string Header;
    public string? Message;
    public Text? Text;
    public string ClassName;
    public int LineNumber;
    public DateTime Time;
    public string? ScopeContext;
}
