using System;

namespace NetworkCoreStandard.Utils;

public static class PathFinder
{
    // 获取当前程序的执行路径
    public static string GetAppPath()
    {
        return AppDomain.CurrentDomain.BaseDirectory;
    }
}
