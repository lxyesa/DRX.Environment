using Microsoft.UI;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Collections.Generic;
using System.Linq;
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
    }
}
