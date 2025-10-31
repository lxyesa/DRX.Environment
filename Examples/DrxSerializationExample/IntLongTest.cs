using System;
using Drx.Sdk.Shared.Serialization;

class Program
{
    static void Main()
    {
        Console.WriteLine("Testing int vs long separation...");

        var data = new DrxSerializationData();

        // 设置 int 值
        data.SetInt32("intValue", 42);

        // 设置 long 值
        data.SetInt("longValue", 42L);

        // 序列化
        var bytes = data.Serialize();
        Console.WriteLine($"Serialized {bytes.Length} bytes");

        // 反序列化
        var data2 = DrxSerializationData.Deserialize(bytes);

        // 测试 TryGetValue<int> - 应该直接返回 int，不通过 long
        var intVal = data2.TryGetValue<int>("intValue");
        if (intVal is not null)
        {
            Console.WriteLine($"TryGetValue<int> for intValue: {intVal} (type: {intVal.GetType().Name})");
        }

        // 测试 TryGetValue<long> - 应该返回 long
        var longVal = data2.TryGetValue<long>("longValue");
        if (longVal is not null)
        {
            Console.WriteLine($"TryGetValue<long> for longValue: {longVal} (type: {longVal.GetType().Name})");
        }

        // 测试 TryGetInt32 - 直接返回 int
        var int32Val = data2.TryGetInt32("intValue");
        if (int32Val is not null)
        {
            Console.WriteLine($"TryGetInt32 for intValue: {int32Val} (type: {int32Val.GetType().Name})");
        }

        // 测试 TryGetInt - 返回 long
        var int64Val = data2.TryGetInt("longValue");
        if (int64Val is not null)
        {
            Console.WriteLine($"TryGetInt for longValue: {int64Val} (type: {int64Val.GetType().Name})");
        }

        // 验证类型
        var intValue = data2["intValue"];
        var longValue = data2["longValue"];

        Console.WriteLine($"int value type: {intValue.Type}");
        Console.WriteLine($"long value type: {longValue.Type}");

        Console.WriteLine("\nTest completed successfully!");
    }
}