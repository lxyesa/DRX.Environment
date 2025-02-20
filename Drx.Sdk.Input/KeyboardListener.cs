using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Drx.Sdk.Input
{
    public class KeyboardListener : IDisposable
    {
        #region Win32 API
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        // 添加新的低级键盘钩子相关 API
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        // 修改 SetWindowsHookEx 的声明，将其改为使用通用委托类型
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, Delegate lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        private const int WH_MOUSE_LL = 14;
        private const int WM_LBUTTONDOWN = 0x0201;
        private const int WM_LBUTTONUP = 0x0202;
        private const int WM_RBUTTONDOWN = 0x0204;
        private const int WM_RBUTTONUP = 0x0205;
        private const int WM_MBUTTONDOWN = 0x0207;
        private const int WM_MBUTTONUP = 0x0208;
        private const int WM_XBUTTONDOWN = 0x020B;
        private const int WM_XBUTTONUP = 0x020C;

        [StructLayout(LayoutKind.Sequential)]
        private struct KBDLLHOOKSTRUCT
        {
            public uint vkCode;
            public uint scanCode;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MSLLHOOKSTRUCT
        {
            public POINT pt;
            public uint mouseData;
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        // 修饰键常量
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;
        #endregion

        private readonly Dictionary<int, (uint Modifiers, uint Key, Action MacroAction)> _hotkeys;
        private readonly IntPtr _windowHandle;
        private int _currentId;
        private bool _isDebugMode = true; // 调试模式开关
        private Dictionary<KeyCombination, Action> _customHotkeys;
        private HashSet<uint> _currentlyPressedKeys;
        private IntPtr _hookHandle;
        private LowLevelKeyboardProc _hookProc;
        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private IntPtr _mouseHookHandle;
        private LowLevelMouseProc _mouseHookProc;

        public event Action<string> OnDebugMessage;

        public KeyboardListener(IntPtr windowHandle)
        {
            _windowHandle = windowHandle;
            _hotkeys = new Dictionary<int, (uint Modifiers, uint Key, Action MacroAction)>();
            Initialize();
        }

        private void Initialize()
        {
            _customHotkeys = new Dictionary<KeyCombination, Action>();
            _currentlyPressedKeys = new HashSet<uint>();
            _currentId = 1;
            InstallHook();
        }

        public void Reset()
        {
            Cleanup();
            Initialize();
        }

        private void DebugLog(string message)
        {
            if (_isDebugMode)
            {
                Debug.WriteLine($"[KeyboardListener] {message}");
                OnDebugMessage?.Invoke($"[KeyboardListener] {message}");
            }
        }

        private void InstallHook()
        {
            // 安装键盘钩子
            _hookProc = new LowLevelKeyboardProc(LowLevelKeyboardCallback);
            // 安装鼠标钩子
            _mouseHookProc = new LowLevelMouseProc(LowLevelMouseCallback);

            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc,
                    GetModuleHandle(curModule.ModuleName), 0);
                _mouseHookHandle = SetWindowsHookEx(WH_MOUSE_LL, _mouseHookProc,
                    GetModuleHandle(curModule.ModuleName), 0);
            }

            if (_hookHandle == IntPtr.Zero || _mouseHookHandle == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
        }

        private IntPtr LowLevelMouseCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var mouseStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);

                switch ((int)wParam)
                {
                    case WM_LBUTTONDOWN:
                        _currentlyPressedKeys.Add((uint)VirtualKeyCode.LBUTTON);
                        CheckCustomHotkeys();
                        break;
                    case WM_LBUTTONUP:
                        _currentlyPressedKeys.Remove((uint)VirtualKeyCode.LBUTTON);
                        break;
                    case WM_RBUTTONDOWN:
                        _currentlyPressedKeys.Add((uint)VirtualKeyCode.RBUTTON);
                        CheckCustomHotkeys();
                        break;
                    case WM_RBUTTONUP:
                        _currentlyPressedKeys.Remove((uint)VirtualKeyCode.RBUTTON);
                        break;
                    case WM_MBUTTONDOWN:
                        _currentlyPressedKeys.Add((uint)VirtualKeyCode.MBUTTON);
                        CheckCustomHotkeys();
                        break;
                    case WM_MBUTTONUP:
                        _currentlyPressedKeys.Remove((uint)VirtualKeyCode.MBUTTON);
                        break;
                    case WM_XBUTTONDOWN:
                        uint xButton = (mouseStruct.mouseData >> 16) & 0xFFFF;
                        _currentlyPressedKeys.Add(xButton == 1 ? (uint)VirtualKeyCode.XBUTTON1 : (uint)VirtualKeyCode.XBUTTON2);
                        CheckCustomHotkeys();
                        break;
                    case WM_XBUTTONUP:
                        xButton = (mouseStruct.mouseData >> 16) & 0xFFFF;
                        _currentlyPressedKeys.Remove(xButton == 1 ? (uint)VirtualKeyCode.XBUTTON1 : (uint)VirtualKeyCode.XBUTTON2);
                        break;
                }
            }

            return CallNextHookEx(_mouseHookHandle, nCode, wParam, lParam);
        }

        private IntPtr LowLevelKeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                var hookStruct = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

                switch ((int)wParam)
                {
                    case WM_KEYDOWN:
                    case WM_SYSKEYDOWN:
                        _currentlyPressedKeys.Add(hookStruct.vkCode);
                        CheckCustomHotkeys();
                        break;

                    case WM_KEYUP:
                    case WM_SYSKEYUP:
                        _currentlyPressedKeys.Remove(hookStruct.vkCode);
                        break;
                }
            }

            return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
        }

        private readonly HashSet<KeyCombination> _activeCustomHotkeys = new HashSet<KeyCombination>();

        private void CheckCustomHotkeys()
        {
            foreach (var hotkey in _customHotkeys)
            {
                // 检查当前按下的键是否包含快捷键所需的所有键
                if (hotkey.Key.Keys.All(key => _currentlyPressedKeys.Contains(key)))
                {
                    // 如果该组合尚未触发，则调用宏动作
                    if (!_activeCustomHotkeys.Contains(hotkey.Key))
                    {
                        _activeCustomHotkeys.Add(hotkey.Key);

                        Task.Run(async () =>
                        {
                            // 立即执行宏
                            hotkey.Value?.Invoke();

                            // 等待直到该组合中的所有键被释放后，移除激活状态
                            while (hotkey.Key.Keys.All(key => _currentlyPressedKeys.Contains(key)))
                            {
                                await Task.Delay(10);
                            }
                            _activeCustomHotkeys.Remove(hotkey.Key);
                        });
                    }
                }
            }
        }

        /// <summary>
        /// 清理所有资源
        /// </summary>
        public void Cleanup()
        {
            DebugLog("开始清理资源");

            // 清理钩子
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }

            // 清理鼠标钩子
            if (_mouseHookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookHandle);
                _mouseHookHandle = IntPtr.Zero;
            }

            // 清理自定义热键
            _customHotkeys.Clear();
            _currentlyPressedKeys.Clear();

            // 清理系统热键
            foreach (var hotkeyId in _hotkeys.Keys.ToList())
            {
                try
                {
                    UnregisterHotKey(_windowHandle, hotkeyId);
                }
                catch (Exception ex)
                {
                    DebugLog($"清理热键 {hotkeyId} 时出错: {ex.Message}");
                }
            }
            _hotkeys.Clear();

            DebugLog("资源清理完成");
        }

        /// <summary>
        /// 注册自定义按键组合
        /// </summary>
        /// <param name="action">要执行的动作</param>
        /// <param name="keys">触发按键的虚拟键码</param>
        public void RegisterCustomHotkey(Action action, params uint[] keys)
        {
            var combination = new KeyCombination(keys);
            _customHotkeys[combination] = action;
            DebugLog($"注册自定义按键组合: Keys=[{string.Join(", ", keys)}]");
        }

        public void UnregisterCustomHotkey(params uint[] keys)
        {
            var combination = new KeyCombination(keys);
            if (_customHotkeys.Remove(combination))
            {
                // 清理钩子
                if (_hookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookHandle);
                    _hookHandle = IntPtr.Zero;
                }

                // 清理鼠标钩子
                if (_mouseHookHandle != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_mouseHookHandle);
                    _mouseHookHandle = IntPtr.Zero;
                }

                DebugLog($"注销自定义按键组合: Keys=[{string.Join(", ", keys)}]");
            }
        }

        public void UnregisterAllCustomHotkeys()
        {
            _customHotkeys.Clear();
            // 清理钩子
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }

            // 清理鼠标钩子
            if (_mouseHookHandle != IntPtr.Zero)
            {
                UnhookWindowsHookEx(_mouseHookHandle);
                _mouseHookHandle = IntPtr.Zero;
            }
            DebugLog("已移除所有自定义按键组合");
        }

        public int RegisterHotKeyMacro(uint modifiers, uint key, Action macroAction)
        {
            try
            {
                int id = _currentId++;
                if (RegisterHotKey(_windowHandle, id, modifiers, key))
                {
                    _hotkeys.Add(id, (modifiers, key, macroAction));
                    DebugLog($"注册热键成功: Modifiers={modifiers}, Key={key}, ID={id}");
                    return id;
                }
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error);
            }
            catch (Exception ex)
            {
                DebugLog($"注册热键失败: {ex.Message}");
                throw;
            }
        }

        public void ProcessHotKeyMessage(int message, IntPtr wParam)
        {
            const int WM_HOTKEY = 0x0312;
            if (message == WM_HOTKEY && _hotkeys.TryGetValue(wParam.ToInt32(), out var hotkey))
            {
                DebugLog($"收到热键消息: ID={wParam.ToInt32()}");
                try
                {
                    // 等待修饰键释放
                    WaitForModifierKeysRelease(hotkey.Modifiers);
                    hotkey.MacroAction?.Invoke();
                }
                catch (Exception ex)
                {
                    DebugLog($"执行热键动作时出错: {ex.Message}");
                }
            }
        }

        private async void WaitForModifierKeysRelease(uint modifiers)
        {
            bool IsKeyPressed(int vKey) => (GetAsyncKeyState(vKey) & 0x8000) != 0;

            while (
                ((modifiers & MOD_CONTROL) != 0 && IsKeyPressed(0x11)) || // CONTROL
                ((modifiers & MOD_ALT) != 0 && IsKeyPressed(0x12)) ||     // ALT
                ((modifiers & MOD_SHIFT) != 0 && IsKeyPressed(0x10)) ||   // SHIFT
                ((modifiers & MOD_WIN) != 0 && (IsKeyPressed(0x5B) || IsKeyPressed(0x5C))) // WIN
            )
            {
                await Task.Delay(50);
            }
        }

        public void UnregisterHotKeyMacro(int id)
        {
            try
            {
                if (_hotkeys.ContainsKey(id))
                {
                    if (UnregisterHotKey(_windowHandle, id))
                    {
                        _hotkeys.Remove(id);
                        DebugLog($"注销热键成功: ID={id}");
                    }
                    else
                    {
                        int error = Marshal.GetLastWin32Error();
                        DebugLog($"注销热键失败: ID={id}, Error={error}");
                    }
                }
            }
            catch (Exception ex)
            {
                DebugLog($"注销热键异常: {ex.Message}");
                throw;
            }
        }

        // 设置调试模式
        public void SetDebugMode(bool enabled)
        {
            _isDebugMode = enabled;
            DebugLog($"调试模式已{(enabled ? "启用" : "禁用")}");
        }

        public void Dispose()
        {
            
        }
    }
}
