#nullable enable
using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace DRX.Framework.Media.UI
{
    public static class UiElementEx
    {
        /// <summary>
        /// 查找并返回当前元素的第一个父元素，该父元素是指定类型。
        /// </summary>
        /// <typeparam name="T">父元素的类型，必须继承自 <see cref="DependencyObject"/>。</typeparam>
        /// <param name="child">当前的依赖对象。</param>
        /// <returns>如果找到符合条件的父元素，则返回该元素；否则返回 <c>null</c>。</returns>
        public static T? FindParent<T>(this DependencyObject child) where T : DependencyObject
        {
            // 如果当前元素为空，则抛出异常
            if (child == null)
                throw new ArgumentNullException(nameof(child));

            // 获取当前元素的父元素
            var parent = VisualTreeHelper.GetParent(child);

            while (parent != null)
            {
                if (parent is T typedParent)
                {
                    return typedParent;
                }

                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;
        }
    }
}
