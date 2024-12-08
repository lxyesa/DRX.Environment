using System;
using System.Runtime.InteropServices;

namespace NetworkCoreStandard.Utils.Extensions;

public static class HandleExtensions
{
    public static IntPtr GetHandle<T>(T obj) where T : class
    {
        if (obj == null) throw new ArgumentNullException(nameof(obj));

        var handle = GCHandle.Alloc(obj, GCHandleType.Normal);
        return GCHandle.ToIntPtr(handle);
    }

    public static T? GetObject<T>(this IntPtr handle) where T : class
    {
        if (handle == IntPtr.Zero) return null;

        var gcHandle = GCHandle.FromIntPtr(handle);
        try
        {
            var target = gcHandle.Target;
            return target as T ?? throw new InvalidCastException($"无法将对象转换为 {typeof(T).Name}");
        }
        finally
        {
            gcHandle.Free(); // 立即释放句柄
        }
    }
}
