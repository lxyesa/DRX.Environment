using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Windows.Foundation;
using Windows.Graphics;

namespace DRX.Framework.Media.UI
{
    public static class WindowHelper
    {
        // ----------------------------------------------------- 标题栏
        public static void ExtendIntoTitleBar(this Window window, FrameworkElement titleBar)
        {
            window.ExtendsContentIntoTitleBar = true;
            window.SetTitleBar(titleBar);

            var appWindow = window.GetAppWindow();
            if (appWindow?.TitleBar == null) return;

            titleBar.SizeChanged += (s, e) => UpdateDragRects();
            UpdateDragRects();
            return;

            void UpdateDragRects()
            {
                if (titleBar.ActualWidth == 0 || titleBar.ActualHeight == 0) return;

                var dragRects = new List<RectInt32>();
                CalculateDragRegions(titleBar, window, dragRects);

                if (dragRects.Count > 0)
                {
                    appWindow.TitleBar.SetDragRectangles(dragRects.ToArray());
                }
            }
        }

        private static void CalculateDragRegions(FrameworkElement element, Window window, List<RectInt32> dragRects)
        {
            var elementRect = element.TransformToVisual(window.Content)
                                   .TransformBounds(new Windows.Foundation.Rect(0, 0, element.ActualWidth, element.ActualHeight));

            if (element is Panel panel)
            {
                // 基础矩形（整个标题栏区域）
                var baseRect = new Windows.Foundation.Rect(
                    elementRect.X,
                    elementRect.Y,
                    elementRect.Width,
                    elementRect.Height
                );

                // 收集所有可见子元素的区域
                var childRects = (from FrameworkElement child in panel.Children
                    where child.Visibility == Visibility.Visible
                    select child.TransformToVisual(window.Content)
                        .TransformBounds(new Windows.Foundation.Rect(0, 0, child.ActualWidth, child.ActualHeight))).ToList();

                if (childRects.Count == 0)
                {
                    // 如果没有子元素，整个区域都是可拖拽的
                    dragRects.Add(new RectInt32
                    {
                        X = (int)baseRect.X,
                        Y = (int)baseRect.Y,
                        Width = (int)baseRect.Width,
                        Height = (int)baseRect.Height
                    });
                }
                else
                {
                    // 添加左侧区域（从标题栏左边到第一个控件）
                    if (childRects.Any())
                    {
                        var leftMost = childRects.Min(r => r.X);
                        if (leftMost > baseRect.X)
                        {
                            dragRects.Add(new RectInt32
                            {
                                X = (int)baseRect.X,
                                Y = (int)baseRect.Y,
                                Width = (int)(leftMost - baseRect.X),
                                Height = (int)baseRect.Height
                            });
                        }
                    }

                    // 添加右侧区域（从最后一个控件到标题栏右边）
                    if (childRects.Any())
                    {
                        var rightMost = childRects.Max(r => r.X + r.Width);
                        if (rightMost < baseRect.Right)
                        {
                            dragRects.Add(new RectInt32
                            {
                                X = (int)rightMost,
                                Y = (int)baseRect.Y,
                                Width = (int)(baseRect.Right - rightMost),
                                Height = (int)baseRect.Height
                            });
                        }
                    }

                    // 添加控件之间的间隙
                    var sortedRects = childRects.OrderBy(r => r.X).ToList();
                    for (var i = 0; i < sortedRects.Count - 1; i++)
                    {
                        var current = sortedRects[i];
                        var next = sortedRects[i + 1];
                        var gap = next.X - (current.X + current.Width);

                        if (gap > 1) // 如果间隙大于1像素
                        {
                            dragRects.Add(new RectInt32
                            {
                                X = (int)(current.X + current.Width),
                                Y = (int)baseRect.Y,
                                Width = (int)gap,
                                Height = (int)baseRect.Height
                            });
                        }
                    }
                }
            }
            else
            {
                // 如果不是面板，整个区域都是可拖拽的
                dragRects.Add(new RectInt32
                {
                    X = (int)elementRect.X,
                    Y = (int)elementRect.Y,
                    Width = (int)elementRect.Width,
                    Height = (int)elementRect.Height
                });
            }
        }

        private static AppWindow GetAppWindow(this Window window)
        {
            var windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            var windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
            return AppWindow.GetFromWindowId(windowId);
        }

        // ----------------------------------------------------- WindowBackDrop
        public enum WindowBackdropType
        {
            Mica,
            Acrylic
        }

        public static bool SetBackdrop(this Window window, WindowBackdropType type)
        {
            if (window.Content is null) return false;

            // 检查系统是否支持背景效果
            if (!MicaController.IsSupported() && type == WindowBackdropType.Mica)
                return false;
            if (!DesktopAcrylicController.IsSupported() && type == WindowBackdropType.Acrylic)
                return false;

            switch (type)
            {
                case WindowBackdropType.Mica:
                    window.SystemBackdrop = new MicaBackdrop();
                    break;

                case WindowBackdropType.Acrylic:
                    window.SystemBackdrop = new DesktopAcrylicBackdrop();
                    break;
            }

            return true;
        }

        // ----------------------------------------------------- WindowSize
        // Win32 结构体和常量
        private const int GWLP_MINMAXINFO = -16;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);


        ///// <summary>
        ///// 设置窗口的大小
        ///// </summary>
        ///// <param name="window">要设置大小的窗口实例</param>
        ///// <param name="width">要设置的窗口宽度（以像素为单位）</param>
        ///// <param name="height">要设置的窗口高度（以像素为单位）</param>
        ///// <exception cref="ArgumentNullException">当 window 参数为 null 时抛出</exception>
        //public static void SetWindowSize(this Window window, double width, double height)
        //{
        //    ArgumentNullException.ThrowIfNull(window);

        //    var appWindow = window.GetAppWindow();
        //    var size = new Windows.Graphics.SizeInt32((int)width, (int)height);
        //    appWindow?.Resize(size);
        //}

        ///// <summary>
        ///// 设置窗口的最小大小限制
        ///// </summary>
        ///// <param name="window">要设置最小大小的窗口实例</param>
        ///// <param name="width">最小宽度（以像素为单位）</param>
        ///// <param name="height">最小高度（以像素为单位）</param>
        ///// <exception cref="ArgumentNullException">当 window 参数为 null 时抛出</exception>
        ///// <remarks>
        ///// 此方法使用 Win32 API 设置窗口的最小大小限制。
        ///// 设置后用户将无法将窗口调整得比指定尺寸更小。
        ///// </remarks>
        //public static void SetMinSize(this Window window, double width, double height)
        //{
        //    ArgumentNullException.ThrowIfNull(window);

        //    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        //    if (hwnd == IntPtr.Zero) return;

        //    var minTrackSize = new MINMAXINFO { ptMinTrackSize = new POINT { X = (int)width, Y = (int)height } };
        //    var ptrMinTrackSize = Marshal.AllocHGlobal(Marshal.SizeOf<MINMAXINFO>());
        //    try
        //    {
        //        Marshal.StructureToPtr(minTrackSize, ptrMinTrackSize, false);
        //        SetWindowLongPtr(hwnd, GWLP_MINMAXINFO, ptrMinTrackSize);
        //    }
        //    finally
        //    {
        //        Marshal.FreeHGlobal(ptrMinTrackSize);
        //    }
        //}

        ///// <summary>
        ///// 设置窗口的最大大小限制
        ///// </summary>
        ///// <param name="window">要设置最大大小的窗口实例</param>
        ///// <param name="width">最大宽度（以像素为单位）</param>
        ///// <param name="height">最大高度（以像素为单位）</param>
        ///// <exception cref="ArgumentNullException">当 window 参数为 null 时抛出</exception>
        ///// <remarks>
        ///// 此方法使用 Win32 API 设置窗口的最大大小限制。
        ///// 设置后用户将无法将窗口调整得比指定尺寸更大。
        ///// </remarks>
        //public static void SetMaxSize(this Window window, double width, double height)
        //{
        //    ArgumentNullException.ThrowIfNull(window);

        //    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        //    if (hwnd == IntPtr.Zero) return;

        //    var maxTrackSize = new MINMAXINFO { ptMaxTrackSize = new POINT { X = (int)width, Y = (int)height } };
        //    var ptrMaxTrackSize = Marshal.AllocHGlobal(Marshal.SizeOf<MINMAXINFO>());
        //    try
        //    {
        //        Marshal.StructureToPtr(maxTrackSize, ptrMaxTrackSize, false);
        //        SetWindowLongPtr(hwnd, GWLP_MINMAXINFO, ptrMaxTrackSize);
        //    }
        //    finally
        //    {
        //        Marshal.FreeHGlobal(ptrMaxTrackSize);
        //    }
        //}

        ///// <summary>
        ///// 将窗口居中显示在主显示器上
        ///// </summary>
        ///// <param name="window">要居中显示的窗口实例</param>
        ///// <exception cref="ArgumentNullException">当 window 参数为 null 时抛出</exception>
        ///// <remarks>
        ///// 此方法会将窗口移动到主显示器的中心位置。
        ///// 居中计算基于主显示器的工作区域（排除任务栏等系统区域）。
        ///// </remarks>
        //public static void CenterOnScreen(this Window window)
        //{
        //    ArgumentNullException.ThrowIfNull(window);

        //    var appWindow = window.GetAppWindow();
        //    if (appWindow == null) return;

        //    var displayArea = DisplayArea.Primary;
        //    var windowSize = appWindow.Size;
        //    var centerX = (displayArea.WorkArea.Width - windowSize.Width) / 2;
        //    var centerY = (displayArea.WorkArea.Height - windowSize.Height) / 2;

        //    appWindow.Move(new Windows.Graphics.PointInt32(centerX, centerY));
        //}

        ///// <summary>
        ///// 设置窗口是否可以调整大小
        ///// </summary>
        ///// <param name="window">要设置的窗口实例</param>
        ///// <param name="canResize">true 允许调整大小；false 禁止调整大小</param>
        ///// <exception cref="ArgumentNullException">当 window 参数为 null 时抛出</exception>
        ///// <remarks>
        ///// 当设置为 false 时，窗口的最大化按钮将被禁用，并且用户无法通过拖动窗口边缘来调整大小。
        ///// </remarks>
        //public static void SetResizable(this Window window, bool canResize)
        //{
        //    ArgumentNullException.ThrowIfNull(window);

        //    var appWindow = window.GetAppWindow();
        //    if (appWindow?.Presenter is OverlappedPresenter presenter)
        //    {
        //        presenter.IsResizable = canResize;
        //    }
        //}

        ///// <summary>
        ///// 设置窗口是否可以最大化
        ///// </summary>
        ///// <param name="window">要设置的窗口实例</param>
        ///// <param name="canMaximize">true 允许最大化；false 禁止最大化</param>
        ///// <exception cref="ArgumentNullException">当 window 参数为 null 时抛出</exception>
        ///// <remarks>
        ///// 当设置为 false 时，窗口的最大化按钮将被禁用，用户无法通过双击标题栏或使用系统菜单来最大化窗口。
        ///// </remarks>
        //public static void SetMaximizable(this Window window, bool canMaximize)
        //{
        //    ArgumentNullException.ThrowIfNull(window);

        //    var appWindow = window.GetAppWindow();
        //    if (appWindow?.Presenter is OverlappedPresenter presenter)
        //    {
        //        presenter.IsMaximizable = canMaximize;
        //    }
        //}

        ///// <summary>
        ///// 设置窗口是否可以最小化
        ///// </summary>
        ///// <param name="window">要设置的窗口实例</param>
        ///// <param name="canMinimize">true 允许最小化；false 禁止最小化</param>
        ///// <exception cref="ArgumentNullException">当 window 参数为 null 时抛出</exception>
        ///// <remarks>
        ///// 当设置为 false 时，窗口的最小化按钮将被禁用，用户无法通过系统菜单来最小化窗口。
        ///// </remarks>
        //public static void SetMinimizable(this Window window, bool canMinimize)
        //{
        //    ArgumentNullException.ThrowIfNull(window);

        //    var appWindow = window.GetAppWindow();
        //    if (appWindow?.Presenter is OverlappedPresenter presenter)
        //    {
        //        presenter.IsMinimizable = canMinimize;
        //    }
        //}

        /// <summary>
        /// 获取窗口的高度
        /// </summary>
        /// <param name="window">要获取高度的窗口实例</param>
        /// <returns>窗口的当前高度（以像素为单位）；如果无法获取高度则返回 0</returns>
        /// <exception cref="ArgumentNullException">当 window 参数为 null 时抛出</exception>
        /// <remarks>
        /// 此方法返回窗口的实际高度，包括标题栏和边框。
        /// </remarks>
        public static double GetHeight(this Window window)
        {
            ArgumentNullException.ThrowIfNull(window);
            var appWindow = window.GetAppWindow();
            return appWindow?.Size.Height ?? 0;
        }

        /// <summary>
        /// 获取窗口的宽度
        /// </summary>
        /// <param name="window">要获取宽度的窗口实例</param>
        /// <returns>窗口的当前宽度（以像素为单位）；如果无法获取宽度则返回 0</returns>
        /// <exception cref="ArgumentNullException">当 window 参数为 null 时抛出</exception>
        /// <remarks>
        /// 此方法返回窗口的实际宽度，包括边框。
        /// </remarks>
        public static double GetWidth(this Window window)
        {
            ArgumentNullException.ThrowIfNull(window);
            var appWindow = window.GetAppWindow();
            return appWindow?.Size.Width ?? 0;
        }



        // ----------------------------------------------------- WindowEvents

        /// <summary>
        /// 当窗口大小发生变化时触发事件
        /// </summary>
        public static void OnWindowSizeChanged(this Window window, TypedEventHandler<object, WindowSizeChangedEventArgs> handler)
        {
            ArgumentNullException.ThrowIfNull(window);
            window.SizeChanged += handler;
        }

        /// <summary>
        /// 当窗口大小发生变化时触发事件（带窗口参数）
        /// </summary>
        public static void OnWindowSizeChanged(this Window window, Action<Window, WindowSizeChangedEventArgs> handler)
        {
            ArgumentNullException.ThrowIfNull(window);
            ArgumentNullException.ThrowIfNull(handler);

            window.SizeChanged += (sender, args) =>
            {
                if (sender is Window w)
                {
                    handler(w, args);
                }
            };
        }
    }
}
