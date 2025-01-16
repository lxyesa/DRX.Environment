namespace DRX.Framework.Models;

public class TickTaskState
{
    // 现有属性
    public string Name { get; set; }
    public CancellationTokenSource CancellationSource { get; set; }
    public int RemainingTime { get; set; }
    public bool IsPaused { get; set; }
    public DateTime? PausedTime { get; set; }
    public int Interval { get; set; }

    //
    public int MaxCount { get; set; }  // 最大执行次数
    public int CurrentCount { get; set; }  // 当前执行次数
    public bool CanPause { get; set; }  // 是否可以暂停

    // 
    public TickTaskState(string name, CancellationTokenSource cts, int interval)
    {
        Name = name;
        CancellationSource = cts;
        Interval = interval;
        RemainingTime = interval;
        IsPaused = false;
        MaxCount = 0;  // 默认无限执行
        CurrentCount = 0;
        CanPause = true;  // 默认可以暂停
    }
}
