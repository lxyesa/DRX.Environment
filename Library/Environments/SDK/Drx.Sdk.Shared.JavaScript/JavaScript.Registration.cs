using System;
using System.Collections.Generic;
using System.Linq;

namespace Drx.Sdk.Shared.JavaScript
{
    /// <summary>
    /// JavaScript 静态门面注册与查询能力（Registration）。
    /// </summary>
    public static partial class JavaScript
    {
        /// <summary>
        /// 为 JS 全局注册一个对象。
        /// </summary>
        public static void RegisterGlobal(string name, object value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("全局名称不能为空", nameof(name));
            if (value == null)
                throw new ArgumentNullException(nameof(value), "注册的值不能为 null");

            Engine.RegisterGlobal(name, value);
        }

        /// <summary>
        /// 为 JS 全局注册一个方法（支持任何 Delegate）。
        /// </summary>
        public static void RegisterGlobal(string name, Delegate method)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("全局名称不能为空", nameof(name));
            if (method == null)
                throw new ArgumentNullException(nameof(method));

            Engine.RegisterGlobal(name, method);
        }

        /// <summary>
        /// 为 JS 全局注册一个宿主类型（可直接调用静态成员并用于构造）。
        /// </summary>
        public static void RegisterHostType(string name, Type type)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("全局名称不能为空", nameof(name));
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            Engine.RegisterHostType(name, type);
        }

        /// <summary>
        /// 为 JS 全局注册一个宿主类型（泛型重载）。
        /// </summary>
        public static void RegisterHostType<T>(string name)
            => RegisterHostType(name, typeof(T));

        /// <summary>
        /// 为 JS 全局注册一个无参方法。
        /// </summary>
        public static void RegisterGlobal(string name, Action action)
            => RegisterGlobal(name, (Delegate)action);

        /// <summary>
        /// 为 JS 全局注册一个有返回值且无参的方法。
        /// </summary>
        public static void RegisterGlobal<TResult>(string name, Func<TResult> func)
            => RegisterGlobal(name, (Delegate)func);

        /// <summary>
        /// 为 JS 全局注册一个属性 Getter（延迟读取，允许返回 null）。
        /// </summary>
        public static void RegisterGlobal(string name, Func<object?> getter)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("全局名称不能为空", nameof(name));
            if (getter == null)
                throw new ArgumentNullException(nameof(getter));

            Engine.RegisterGlobal(name, getter);
        }

        /// <summary>
        /// 获取所有已注册的 JavaScript 类名。
        /// </summary>
        public static List<string> GetRegisteredClasses()
        {
            return Engine.GetRegisteredClasses().ToList();
        }

        /// <summary>
        /// 获取所有通过 RegisterGlobal(...) 注册的全局对象。
        /// 返回字典：名称 -&gt; .NET 类型。
        /// </summary>
        public static Dictionary<string, Type> GetRegisteredGlobals()
        {
            return new Dictionary<string, Type>(Engine.GetRegisteredGlobals(), StringComparer.Ordinal);
        }
    }
}
