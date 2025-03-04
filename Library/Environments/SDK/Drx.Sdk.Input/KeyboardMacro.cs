using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Drx.Sdk.Input
{
    public class KeyboardMacro
    {
        #region Win32 API
        [DllImport("user32.dll")]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint uCode, uint uMapType);

        [DllImport("user32.dll")]
        private static extern IntPtr GetMessageExtraInfo();

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, IntPtr dwExtraInfo);

        private const uint MAPVK_VK_TO_VSC = 0x00;
        private const uint INPUT_KEYBOARD = 1;
        private const uint INPUT_MOUSE = 0;
        private const uint KEYEVENTF_KEYDOWN = 0x0000;
        private const uint KEYEVENTF_KEYUP = 0x0002;
        private const uint KEYEVENTF_SCANCODE = 0x0008;
        private const uint KEYEVENTF_UNICODE = 0x0004;
        private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
        private const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
        private const uint MOUSEEVENTF_LEFTUP = 0x0004;
        private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
        private const uint MOUSEEVENTF_RIGHTUP = 0x0010;
        private const uint MOUSEEVENTF_MIDDLEDOWN = 0x0020;
        private const uint MOUSEEVENTF_MIDDLEUP = 0x0040;
        private const uint MOUSEEVENTF_XDOWN = 0x0080;
        private const uint MOUSEEVENTF_XUP = 0x0100;
        #endregion

        #region Structs
        [StructLayout(LayoutKind.Sequential)]
        public struct INPUT
        {
            public uint type;
            public InputUnion U;
            public static int Size => Marshal.SizeOf(typeof(INPUT));
        }

        [StructLayout(LayoutKind.Explicit)]
        public struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
            [FieldOffset(0)] public HARDWAREINPUT hi;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct KEYBDINPUT
        {
            public ushort wVk;
            public ushort wScan;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MOUSEINPUT
        {
            public int dx;
            public int dy;
            public uint mouseData;
            public uint dwFlags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct HARDWAREINPUT
        {
            public uint uMsg;
            public ushort wParamL;
            public ushort wParamH;
        }
        #endregion

        private bool _isDebugMode = true; // 调试模式开关

        public event Action<string>? OnDebugMessage;

        private void DebugLog(string message)
        {
            if (_isDebugMode)
            {
                Debug.WriteLine($"[KeyboardMacro] {message}");
                OnDebugMessage?.Invoke($"[KeyboardMacro] {message}");
            }
        }

        public void SendKeyDown(VirtualKeyCode keyCode)
        {
            try
            {
                ushort scanCode = (ushort)MapVirtualKey((uint)keyCode, MAPVK_VK_TO_VSC);
                var input = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = (ushort)keyCode,
                            wScan = scanCode,
                            dwFlags = KEYEVENTF_SCANCODE,
                            time = 0,
                            dwExtraInfo = GetMessageExtraInfo()
                        }
                    }
                };

                uint result = SendInput(1, new[] { input }, INPUT.Size);
                if (result != 1)
                {
                    int error = Marshal.GetLastWin32Error();
                    DebugLog($"SendKeyDown 失败: keyCode={keyCode}, error={error}");
                }
                else
                {
                    DebugLog($"SendKeyDown 成功: keyCode={keyCode}");
                }
            }
            catch (Exception ex)
            {
                DebugLog($"SendKeyDown 异常: {ex.Message}");
                throw;
            }
        }

        public void SendKeyUp(VirtualKeyCode keyCode)
        {
            try
            {
                ushort scanCode = (ushort)MapVirtualKey((uint)keyCode, MAPVK_VK_TO_VSC);
                var input = new INPUT
                {
                    type = INPUT_KEYBOARD,
                    U = new InputUnion
                    {
                        ki = new KEYBDINPUT
                        {
                            wVk = (ushort)keyCode,
                            wScan = scanCode,
                            dwFlags = KEYEVENTF_KEYUP | KEYEVENTF_SCANCODE,
                            time = 0,
                            dwExtraInfo = GetMessageExtraInfo()
                        }
                    }
                };

                uint result = SendInput(1, new[] { input }, INPUT.Size);
                if (result != 1)
                {
                    int error = Marshal.GetLastWin32Error();
                    DebugLog($"SendKeyUp 失败: keyCode={keyCode}, error={error}");
                }
                else
                {
                    DebugLog($"SendKeyUp 成功: keyCode={keyCode}");
                }
            }
            catch (Exception ex)
            {
                DebugLog($"SendKeyUp 异常: {ex.Message}");
                throw;
            }
        }

        public async Task SendKeyPressAsync(VirtualKeyCode keyCode, int delay = 50)
        {
            try
            {
                SendKeyDown(keyCode);
                await AddRandomDelay(delay);
                SendKeyUp(keyCode);
                DebugLog($"按键完成: keyCode={keyCode}, delay={delay}ms");
            }
            catch (Exception ex)
            {
                DebugLog($"SendKeyPress 异常: {ex.Message}");
                throw;
            }
        }

        private async Task AddRandomDelay(int baseDelay = 50)
        {
            Random random = new Random();
            int randomDelay = baseDelay + random.Next(-10, 10);
            await Task.Delay(Math.Max(1, randomDelay));
        }

        public async Task SendKeySequenceAsync(VirtualKeyCode[] keyCodes, int baseDelay = 50)
        {
            DebugLog($"开始执行按键序列，共 {keyCodes.Length} 个按键");

            try
            {
                foreach (var keyCode in keyCodes)
                {
                    // 检查是否是延迟键
                    if (IsDelayKey(keyCode))
                    {
                        int delayTime = GetDelayTime(keyCode);
                        DebugLog($"执行延迟: {delayTime}ms");
                        await Task.Delay(delayTime);
                        continue;
                    }

                    // 检查是否是鼠标事件
                    if (IsMouseEvent(keyCode))
                    {
                        ExecuteMouseEvent(keyCode);
                        await AddRandomDelay(baseDelay);
                        continue;
                    }

                    DebugLog($"正在处理按键: {keyCode}");
                    await SendKeyPressAsync(keyCode, baseDelay);
                    await AddRandomDelay(baseDelay);
                }

                DebugLog("按键序列执行完成");
            }
            catch (Exception ex)
            {
                DebugLog($"执行按键序列时出错: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 检查是否是鼠标事件
        /// </summary>
        private bool IsMouseEvent(VirtualKeyCode keyCode)
        {
            return keyCode == VirtualKeyCode.MOUSE_LEFT_CLICK ||
                   keyCode == VirtualKeyCode.MOUSE_RIGHT_CLICK ||
                   keyCode == VirtualKeyCode.MOUSE_MIDDLE_CLICK ||
                   keyCode == VirtualKeyCode.MOUSE_XBUTTON1_CLICK ||
                   keyCode == VirtualKeyCode.MOUSE_XBUTTON2_CLICK;
        }


        /// <summary>
        /// 执行鼠标事件
        /// </summary>
        private void ExecuteMouseEvent(VirtualKeyCode keyCode)
        {
            switch (keyCode)
            {
                case VirtualKeyCode.MOUSE_LEFT_CLICK:
                    SendMouseLeftClick();
                    break;
                case VirtualKeyCode.MOUSE_RIGHT_CLICK:
                    SendMouseRightClick();
                    break;
                case VirtualKeyCode.MOUSE_MIDDLE_CLICK:
                    SendMouseMiddleClick();
                    break;
                case VirtualKeyCode.MOUSE_XBUTTON1_CLICK:
                    SendMouseXButton1Click();
                    break;
                case VirtualKeyCode.MOUSE_XBUTTON2_CLICK:
                    SendMouseXButton2Click();
                    break;
            }
        }

        /// <summary>
        /// 检查是否是延迟键
        /// </summary>
        private bool IsDelayKey(VirtualKeyCode keyCode)
        {
            return keyCode == VirtualKeyCode.DELAY_1 ||
                   keyCode == VirtualKeyCode.DELAY_10 ||
                   keyCode == VirtualKeyCode.DELAY_100 ||
                   keyCode == VirtualKeyCode.DELAY_1000;
        }

        /// <summary>
        /// 获取延迟时间（毫秒）
        /// </summary>
        private int GetDelayTime(VirtualKeyCode keyCode)
        {
            return keyCode switch
            {
                VirtualKeyCode.DELAY_1 => 1,
                VirtualKeyCode.DELAY_10 => 10,
                VirtualKeyCode.DELAY_100 => 100,
                VirtualKeyCode.DELAY_1000 => 1000,
                _ => 0
            };
        }

        // 设置调试模式
        public void SetDebugMode(bool enabled)
        {
            _isDebugMode = enabled;
            DebugLog($"调试模式已{(enabled ? "启用" : "禁用")}");
        }

        public void SendMouseLeftClick()
        {
            try
            {
                mouse_event(MOUSEEVENTF_LEFTDOWN, 0, 0, 0, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_LEFTUP, 0, 0, 0, IntPtr.Zero);
                DebugLog("Mouse left click sent");
            }
            catch (Exception ex)
            {
                DebugLog($"SendMouseLeftClick 异常: {ex.Message}");
                throw;
            }
        }

        public void SendMouseRightClick()
        {
            try
            {
                mouse_event(MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_RIGHTUP, 0, 0, 0, IntPtr.Zero);
                DebugLog("Mouse right click sent");
            }
            catch (Exception ex)
            {
                DebugLog($"SendMouseRightClick 异常: {ex.Message}");
                throw;
            }
        }

        public void SendMouseMiddleClick()
        {
            try
            {
                mouse_event(MOUSEEVENTF_MIDDLEDOWN, 0, 0, 0, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_MIDDLEUP, 0, 0, 0, IntPtr.Zero);
                DebugLog("Mouse middle click sent");
            }
            catch (Exception ex)
            {
                DebugLog($"SendMouseMiddleClick 异常: {ex.Message}");
                throw;
            }
        }

        public void SendMouseXButton1Click()
        {
            try
            {
                mouse_event(MOUSEEVENTF_XDOWN, 0, 0, 0x0001, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_XUP, 0, 0, 0x0001, IntPtr.Zero);
                DebugLog("Mouse XButton1 click sent");
            }
            catch (Exception ex)
            {
                DebugLog($"SendMouseXButton1Click 异常: {ex.Message}");
                throw;
            }
        }

        public void SendMouseXButton2Click()
        {
            try
            {
                mouse_event(MOUSEEVENTF_XDOWN, 0, 0, 0x0002, IntPtr.Zero);
                mouse_event(MOUSEEVENTF_XUP, 0, 0, 0x0002, IntPtr.Zero);
                DebugLog("Mouse XButton2 click sent");
            }
            catch (Exception ex)
            {
                DebugLog($"SendMouseXButton2Click 异常: {ex.Message}");
                throw;
            }
        }
    }
}
