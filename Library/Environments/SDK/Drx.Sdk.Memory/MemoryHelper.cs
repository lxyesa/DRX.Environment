using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Drx.Sdk.Memory
{
    public class MemoryHelper
    {
        private static unsafe IntPtr GetDirectValueAddress<T>(ref T value) where T : unmanaged
        {
            fixed (T* ptr = &value)
            {
                return new IntPtr(ptr);
            }
        }

        /// <summary>
        /// 获取托管对象的内存基地址
        /// </summary>
        /// <param name="obj">要获取内存地址的托管对象，可以是字段、属性、数组元素、指针等</param>
        /// <returns>对象在内存中的地址</returns>
        /// <exception cref="ArgumentNullException">当传入的对象为null时抛出</exception>
        public static IntPtr GetObjectAddress(object obj)
        {
            if (obj == null)
                throw new ArgumentNullException(nameof(obj));

            // 处理不同类型的对象
            if (obj is Array array)
            {
                // 对于数组，返回第一个元素的地址
                return GetArrayElementAddress(array, 0);
            }

            // 检查是否为数值类型、枚举或其他直接内存布局的类型
            if (obj.GetType().IsValueType || obj is string)
            {
                return GetValueTypeAddress(obj);
            }

            // 对于引用类型的其它情况，我们尝试获取对象的直接内存地址
            GCHandle handle = GCHandle.Alloc(obj, GCHandleType.Pinned);
            try
            {
                return handle.AddrOfPinnedObject();
            }
            finally
            {
                if (handle.IsAllocated)
                    handle.Free();
            }
        }

        /// <summary>
        /// 获取值类型对象的内存地址
        /// </summary>
        /// <param name="obj">值类型对象</param>
        /// <returns>值类型对象在内存中的地址</returns>
        private static unsafe IntPtr GetValueTypeAddress(object obj)
        {
            // 对于简单值类型，使用指针获取基本地址
            if (obj is bool boolVal)
            {
                bool copy = boolVal;
                return GetDirectValueAddress(ref copy);
            }
            if (obj is byte byteVal)
            {
                byte copy = byteVal;
                return GetDirectValueAddress(ref copy);
            }
            if (obj is sbyte sbyteVal)
            {
                sbyte copy = sbyteVal;
                return GetDirectValueAddress(ref copy);
            }
            if (obj is char charVal)
            {
                char copy = charVal;
                return GetDirectValueAddress(ref copy);
            }
            if (obj is short shortVal)
            {
                short copy = shortVal;
                return GetDirectValueAddress(ref copy);
            }
            if (obj is ushort ushortVal)
            {
                ushort copy = ushortVal;
                return GetDirectValueAddress(ref copy);
            }
            if (obj is int intVal)
            {
                int copy = intVal;
                return GetDirectValueAddress(ref copy);
            }
            if (obj is uint uintVal)
            {
                uint copy = uintVal;
                return GetDirectValueAddress(ref copy);
            }
            if (obj is long longVal)
            {
                long copy = longVal;
                return GetDirectValueAddress(ref copy);
            }
            if (obj is ulong ulongVal)
            {
                ulong copy = ulongVal;
                return GetDirectValueAddress(ref copy);
            }
            if (obj is float floatVal)
            {
                float copy = floatVal;
                return GetDirectValueAddress(ref copy);
            }
            if (obj is double doubleVal)
            {
                double copy = doubleVal;
                return GetDirectValueAddress(ref copy);
            }
            if (obj is decimal decimalVal)
            {
                decimal copy = decimalVal;
                return GetDirectValueAddress(ref copy);
            }
            if (obj is string stringVal)
            {
                // 字符串需要特殊处理，我们获取字符串内容的地址
                fixed (char* ptr = stringVal)
                {
                    return new IntPtr(ptr);
                }
            }
            if (obj is Enum)
            {
                // 枚举类型转换为底层整数类型
                Type enumType = obj.GetType();
                Type underlyingType = Enum.GetUnderlyingType(enumType);

                if (underlyingType == typeof(int))
                {
                    int value = (int)obj;
                    return GetDirectValueAddress(ref value);
                }
                else if (underlyingType == typeof(byte))
                {
                    byte value = (byte)obj;
                    return GetDirectValueAddress(ref value);
                }
                else if (underlyingType == typeof(short))
                {
                    short value = (short)obj;
                    return GetDirectValueAddress(ref value);
                }
                else if (underlyingType == typeof(long))
                {
                    long value = (long)obj;
                    return GetDirectValueAddress(ref value);
                }
                // 其他枚举底层类型...
            }


            // 对于其他值类型结构体，使用通用方法
            Type type = obj.GetType();
            if (type.IsValueType)
            {
                // 创建一个副本以获取地址
                object copy = obj;
                // 使用GCHandle固定对象
                GCHandle handle = GCHandle.Alloc(copy, GCHandleType.Pinned);
                try
                {
                    return handle.AddrOfPinnedObject();
                }
                finally
                {
                    if (handle.IsAllocated)
                        handle.Free();
                }
            }

            // 不支持的类型，返回对象本身的地址
            GCHandle objHandle = GCHandle.Alloc(obj, GCHandleType.Pinned);
            try
            {
                return objHandle.AddrOfPinnedObject();
            }
            finally
            {
                if (objHandle.IsAllocated)
                    objHandle.Free();
            }
        }

        /// <summary>
        /// 获取数组中特定元素的内存地址
        /// </summary>
        /// <param name="array">数组对象</param>
        /// <param name="index">元素索引</param>
        /// <returns>数组元素在内存中的地址</returns>
        /// <exception cref="ArgumentNullException">当数组为null时抛出</exception>
        /// <exception cref="ArgumentOutOfRangeException">当索引超出数组范围时抛出</exception>
        public static IntPtr GetArrayElementAddress(Array array, int index)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            if (index < 0 || index >= array.Length)
                throw new ArgumentOutOfRangeException(nameof(index));

            // 针对基本类型数组使用更高效的方法
            if (array is byte[] byteArray)
                return GetByteArrayAddress(byteArray, index);
            if (array is int[] intArray)
                return GetIntArrayAddress(intArray, index);
            if (array is long[] longArray)
                return GetLongArrayAddress(longArray, index);
            if (array is double[] doubleArray)
                return GetDoubleArrayAddress(doubleArray, index);

            // 其他类型的数组使用GCHandle方法
            GCHandle handle = GCHandle.Alloc(array, GCHandleType.Pinned);
            try
            {
                // 获取数组基地址
                IntPtr baseAddress = handle.AddrOfPinnedObject();

                // 获取数组元素类型
                Type elementType = array.GetType().GetElementType();
                if (elementType == null)
                    return baseAddress;

                // 计算元素大小和偏移量
                int elementSize = Marshal.SizeOf(elementType);
                IntPtr elementAddress = new IntPtr(baseAddress.ToInt64() + index * elementSize);

                return elementAddress;
            }
            finally
            {
                if (handle.IsAllocated)
                    handle.Free();
            }
        }

        // 针对特定类型的优化方法
        private static IntPtr GetByteArrayAddress(byte[] array, int index)
        {
            return Marshal.UnsafeAddrOfPinnedArrayElement(array, index);
        }

        private static IntPtr GetIntArrayAddress(int[] array, int index)
        {
            return Marshal.UnsafeAddrOfPinnedArrayElement(array, index);
        }

        private static IntPtr GetLongArrayAddress(long[] array, int index)
        {
            return Marshal.UnsafeAddrOfPinnedArrayElement(array, index);
        }

        private static IntPtr GetDoubleArrayAddress(double[] array, int index)
        {
            return Marshal.UnsafeAddrOfPinnedArrayElement(array, index);
        }

        /// <summary>
        /// 获取结构体的内存地址
        /// </summary>
        /// <typeparam name="T">结构体类型</typeparam>
        /// <param name="structure">结构体实例</param>
        /// <returns>结构体在内存中的地址</returns>
        public static unsafe IntPtr GetStructureAddress<T>(ref T structure) where T : struct
        {
            // 使用固定语句获取结构体地址
            fixed (T* ptr = &structure)
            {
                return new IntPtr(ptr);
            }
        }

        /// <summary>
        /// 获取COM对象的内存地址（通过IUnknown接口）
        /// </summary>
        /// <param name="comObject">COM对象</param>
        /// <returns>COM对象的IUnknown接口地址</returns>
        /// <exception cref="ArgumentNullException">当COM对象为null时抛出</exception>
        public static IntPtr GetComObjectAddress(object comObject)
        {
            if (comObject == null)
                throw new ArgumentNullException(nameof(comObject));

            return Marshal.GetIUnknownForObject(comObject);
        }

        /// <summary>
        /// 调用位于指定内存地址的函数
        /// </summary>
        /// <typeparam name="TDelegate">表示函数签名的委托类型</typeparam>
        /// <param name="functionAddress">函数在内存中的地址</param>
        /// <returns>可调用的委托实例</returns>
        /// <exception cref="ArgumentException">当函数地址无效或委托类型不是委托时抛出</exception>
        public static TDelegate CallFunction<TDelegate>(IntPtr functionAddress) where TDelegate : Delegate
        {
            if (functionAddress == IntPtr.Zero)
                throw new ArgumentException("函数地址不能为零", nameof(functionAddress));

            return Marshal.GetDelegateForFunctionPointer<TDelegate>(functionAddress);
        }

        /// <summary>
        /// 调用无参数无返回值的函数
        /// </summary>
        /// <param name="functionAddress">函数在内存中的地址</param>
        public static void CallAction(IntPtr functionAddress)
        {
            var action = CallFunction<Action>(functionAddress);
            action();
        }

        /// <summary>
        /// 调用有一个参数无返回值的函数
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <param name="functionAddress">函数在内存中的地址</param>
        /// <param name="arg">函数参数</param>
        public static void CallAction<T>(IntPtr functionAddress, T arg)
        {
            var action = CallFunction<Action<T>>(functionAddress);
            action(arg);
        }

        /// <summary>
        /// 调用有两个参数无返回值的函数
        /// </summary>
        /// <typeparam name="T1">第一个参数类型</typeparam>
        /// <typeparam name="T2">第二个参数类型</typeparam>
        /// <param name="functionAddress">函数在内存中的地址</param>
        /// <param name="arg1">第一个函数参数</param>
        /// <param name="arg2">第二个函数参数</param>
        public static void CallAction<T1, T2>(IntPtr functionAddress, T1 arg1, T2 arg2)
        {
            var action = CallFunction<Action<T1, T2>>(functionAddress);
            action(arg1, arg2);
        }

        /// <summary>
        /// 调用无参数有返回值的函数
        /// </summary>
        /// <typeparam name="TResult">返回值类型</typeparam>
        /// <param name="functionAddress">函数在内存中的地址</param>
        /// <returns>函数调用的结果</returns>
        public static TResult CallFunc<TResult>(IntPtr functionAddress)
        {
            var func = CallFunction<Func<TResult>>(functionAddress);
            return func();
        }

        /// <summary>
        /// 调用有一个参数有返回值的函数
        /// </summary>
        /// <typeparam name="T">参数类型</typeparam>
        /// <typeparam name="TResult">返回值类型</typeparam>
        /// <param name="functionAddress">函数在内存中的地址</param>
        /// <param name="arg">函数参数</param>
        /// <returns>函数调用的结果</returns>
        public static TResult CallFunc<T, TResult>(IntPtr functionAddress, T arg)
        {
            var func = CallFunction<Func<T, TResult>>(functionAddress);
            return func(arg);
        }

        /// <summary>
        /// 调用有两个参数有返回值的函数
        /// </summary>
        /// <typeparam name="T1">第一个参数类型</typeparam>
        /// <typeparam name="T2">第二个参数类型</typeparam>
        /// <typeparam name="TResult">返回值类型</typeparam>
        /// <param name="functionAddress">函数在内存中的地址</param>
        /// <param name="arg1">第一个函数参数</param>
        /// <param name="arg2">第二个函数参数</param>
        /// <returns>函数调用的结果</returns>
        public static TResult CallFunc<T1, T2, TResult>(IntPtr functionAddress, T1 arg1, T2 arg2)
        {
            var func = CallFunction<Func<T1, T2, TResult>>(functionAddress);
            return func(arg1, arg2);
        }

        /// <summary>
        /// 调用使用自定义调用约定的非托管函数
        /// </summary>
        /// <param name="functionAddress">函数地址</param>
        /// <param name="callConv">调用约定</param>
        /// <param name="returnType">返回值类型</param>
        /// <param name="parameterTypes">参数类型数组</param>
        /// <returns>可调用的委托</returns>
        public static Delegate CreateCustomFunctionCaller(IntPtr functionAddress, CallingConvention callConv, Type returnType, Type[] parameterTypes)
        {
            if (functionAddress == IntPtr.Zero)
                throw new ArgumentException("函数地址不能为零", nameof(functionAddress));
            
            // 创建动态方法签名
            Type delegateType = DynamicDelegateFactory.CreateDelegateType(returnType, parameterTypes, callConv);
            
            // 获取指定地址的委托
            return Marshal.GetDelegateForFunctionPointer(functionAddress, delegateType);
        }
    }

    /// <summary>
    /// 用于创建动态委托类型的辅助类
    /// </summary>
    internal static class DynamicDelegateFactory
    {
        /// <summary>
        /// 创建具有指定签名和调用约定的委托类型
        /// </summary>
        /// <param name="returnType">返回值类型</param>
        /// <param name="parameterTypes">参数类型数组</param>
        /// <param name="callingConvention">调用约定</param>
        /// <returns>生成的委托类型</returns>
        public static Type CreateDelegateType(Type returnType, Type[] parameterTypes, CallingConvention callingConvention)
        {
            // 对于标准调用约定和签名，使用预定义委托
            if (callingConvention == CallingConvention.StdCall || callingConvention == CallingConvention.Winapi)
            {
                if (parameterTypes.Length == 0)
                {
                    if (returnType == typeof(void))
                        return typeof(Action);
                    else
                        return typeof(Func<>).MakeGenericType(returnType);
                }
                else if (parameterTypes.Length == 1)
                {
                    if (returnType == typeof(void))
                        return typeof(Action<>).MakeGenericType(parameterTypes);
                    else
                        return typeof(Func<,>).MakeGenericType(parameterTypes[0], returnType);
                }
                // 其他参数数量的情况可以继续扩展...
            }

            // 对于其他情况，使用DynamicMethod或Reflection.Emit来创建委托类型
            // 这是一个简化的实现，实际生产代码应该有更完整的处理
            throw new NotImplementedException("动态创建自定义调用约定的委托目前尚未实现");
        }
    }
}
