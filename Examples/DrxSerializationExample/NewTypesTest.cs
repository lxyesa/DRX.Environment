using System;
using Drx.Sdk.Shared.Serialization;

class Program
{
    static void Main()
    {
        Console.WriteLine("Testing DrxValue with all C# types and As<T> method...");

        var data = new DrxSerializationData();

        // 测试所有新添加的类型
        data.SetShort("shortValue", (short)123);
        data.SetInt32("intValue", 456);
        data.SetUInt32("uintValue", 789U);
        data.SetUInt64("ulongValue", 123456789UL);
        data.SetFloat("floatValue", 3.14f);
        data.SetDecimal("decimalValue", 123.456m);
        data.SetChar("charValue", 'A');
        data.SetByte("byteValue", (byte)255);
        data.SetSByte("sbyteValue", (sbyte)-128);
        data.SetIntPtr("intPtrValue", new IntPtr(42));
        data.SetUIntPtr("uintPtrValue", new UIntPtr(84));

        // 测试数组
        data.SetArray("shortArray", new short[] { 1, 2, 3 });
        data.SetArray("intArray", new int[] { 4, 5, 6 });
        data.SetArray("floatArray", new float[] { 1.1f, 2.2f, 3.3f });

        // 序列化
        var bytes = data.Serialize();
        Console.WriteLine($"Serialized {bytes.Length} bytes");

        // 反序列化
        var data2 = DrxSerializationData.Deserialize(bytes);

        // 使用 As<T> 方法测试所有类型
        Console.WriteLine("\nTesting As<T> method:");
        Console.WriteLine($"short: {data2["shortValue"].As<short>()}");
        Console.WriteLine($"int: {data2["intValue"].As<int>()}");
        Console.WriteLine($"uint: {data2["uintValue"].As<uint>()}");
        Console.WriteLine($"ulong: {data2["ulongValue"].As<ulong>()}");
        Console.WriteLine($"float: {data2["floatValue"].As<float>()}");
        Console.WriteLine($"decimal: {data2["decimalValue"].As<decimal>()}");
        Console.WriteLine($"char: {data2["charValue"].As<char>()}");
        Console.WriteLine($"byte: {data2["byteValue"].As<byte>()}");
        Console.WriteLine($"sbyte: {data2["sbyteValue"].As<sbyte>()}");
        Console.WriteLine($"IntPtr: {data2["intPtrValue"].As<IntPtr>()}");
        Console.WriteLine($"UIntPtr: {data2["uintPtrValue"].As<UIntPtr>()}");

        // 测试数组 As<T>
        Console.WriteLine("\nTesting array As<T>:");
        var shortArray = data2["shortArray"].As<short[]>();
        Console.WriteLine($"short[]: [{string.Join(", ", shortArray)}]");

        var intArray = data2["intArray"].As<int[]>();
        Console.WriteLine($"int[]: [{string.Join(", ", intArray)}]");

        var floatArray = data2["floatArray"].As<float[]>();
        Console.WriteLine($"float[]: [{string.Join(", ", floatArray)}]");

        // 测试 TryGetValue<T>
        Console.WriteLine("\nTesting TryGetValue<T>:");
        var shortVal = data2.TryGetValue<short>("shortValue");
        if (shortVal is not null)
            Console.WriteLine($"TryGetValue short: {shortVal}");

        var intArr = data2.TryGetValue<int[]>("intArray");
        if (intArr is not null)
            Console.WriteLine($"TryGetValue int[]: [{string.Join(", ", intArr)}]");

        Console.WriteLine("\nAll tests completed successfully!");
    }
}