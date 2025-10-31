using System;
using Drx.Sdk.Shared.Serialization;

namespace DrxSerializationExample
{
    public class ArrayTest
    {
        public static void Main()
        {
            Console.WriteLine("测试新增的数组类型重载功能...");

            var data = new DrxSerializationData();

            // 测试 int[] 数组
            int[] intArray = { 1, 2, 3, 4, 5 };
            data.SetArray("intArray", intArray);
            Console.WriteLine("设置 int[] 数组成功");

            // 测试其他新增的数组类型
            short[] shortArray = { 10, 20, 30 };
            data.SetArray("shortArray", shortArray);
            Console.WriteLine("设置 short[] 数组成功");

            uint[] uintArray = { 100, 200, 300 };
            data.SetArray("uintArray", uintArray);
            Console.WriteLine("设置 uint[] 数组成功");

            float[] floatArray = { 1.1f, 2.2f, 3.3f };
            data.SetArray("floatArray", floatArray);
            Console.WriteLine("设置 float[] 数组成功");

            char[] charArray = { 'a', 'b', 'c' };
            data.SetArray("charArray", charArray);
            Console.WriteLine("设置 char[] 数组成功");

            byte[] byteArray = { 1, 2, 3 };
            data.SetArray("byteArray", byteArray);
            Console.WriteLine("设置 byte[] 数组成功");

            sbyte[] sbyteArray = { -1, -2, -3 };
            data.SetArray("sbyteArray", sbyteArray);
            Console.WriteLine("设置 sbyte[] 数组成功");

            // 测试 null 数组
            data.SetArray("nullArray", (int[])null);
            Console.WriteLine("设置 null int[] 数组成功");

            // 序列化测试
            try
            {
                var serialized = data.Serialize();
                Console.WriteLine($"序列化成功，大小: {serialized.Length} 字节");

                // 反序列化测试
                var deserialized = DrxSerializationData.Deserialize(serialized);
                Console.WriteLine("反序列化成功");

                // 验证反序列化后的数据
                var restoredIntArray = deserialized.TryGetValue<int[]>("intArray");
                if (restoredIntArray is not null)
                {
                    Console.WriteLine($"恢复的 int[] 数组: [{string.Join(", ", restoredIntArray)}]");
                }
                else
                {
                    Console.WriteLine("恢复 int[] 数组失败");
                }

                var restoredShortArray = deserialized.TryGetValue<short[]>("shortArray");
                if (restoredShortArray is not null)
                {
                    Console.WriteLine($"恢复的 short[] 数组: [{string.Join(", ", restoredShortArray)}]");
                }
                else
                {
                    Console.WriteLine("恢复 short[] 数组失败");
                }

                var restoredFloatArray = deserialized.TryGetValue<float[]>("floatArray");
                if (restoredFloatArray is not null)
                {
                    Console.WriteLine($"恢复的 float[] 数组: [{string.Join(", ", restoredFloatArray)}]");
                }
                else
                {
                    Console.WriteLine("恢复 float[] 数组失败");
                }

                Console.WriteLine("所有测试通过！");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"序列化/反序列化测试失败: {ex.Message}");
            }
        }
    }
}