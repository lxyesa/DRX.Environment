using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Drx.Sdk.Script;
using Drx.Sdk.Script.Attributes;
using Drx.Sdk.Script.Interfaces;

namespace Drx.Sdk.Input
{
    [ScriptClass("KeyboardListener")]
    public class KeyboardListener : IDisposable, IScript
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

        private readonly Dictionary<int, (uint Modifiers, uint Key, Action MacroAction)>? _hotkeys;
        private readonly Dictionary<KeyCombination, bool>? _hotkeyExecuting = new Dictionary<KeyCombination, bool>();
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

#pragma warning disable CS8618
        public KeyboardListener(IntPtr windowHandle)
#pragma warning restore CS8618
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
                        HandleKeyDown(hookStruct.vkCode);
                        break;

                    case WM_KEYUP:
                    case WM_SYSKEYUP:
                        _currentlyPressedKeys.Remove(hookStruct.vkCode);
                        HandleKeyUp(hookStruct.vkCode);
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
                if (hotkey.Key.Keys.All(key => _currentlyPressedKeys.Contains(key)))
                {
                    if (!_activeCustomHotkeys.Contains(hotkey.Key) && 
                        (!_hotkeyExecuting.TryGetValue(hotkey.Key, out bool isExecuting) || !isExecuting))
                    {
                        _activeCustomHotkeys.Add(hotkey.Key);
                        _hotkeyExecuting[hotkey.Key] = true;

                        Task.Run(async () =>
                        {
                            try
                            {
                                DebugLog($"开始执行宏 [{string.Join(", ", hotkey.Key.Keys)}]");
                                
                                // 改进的宏动作执行方式
                                if (hotkey.Value != null)
                                {
                                    try
                                    {
                                        if (hotkey.Value.Target is Delegate)
                                        {
                                            // 处理 JavaScript/动态函数
                                            await Task.Run(() => ((dynamic)hotkey.Value).DynamicInvoke());
                                        }
                                        else
                                        {
                                            // 处理普通 C# Action
                                            await Task.Run(() => hotkey.Value());
                                        }
                                    }
                                    catch (Microsoft.ClearScript.ScriptEngineException)
                                    {
                                        // 尝试直接调用
                                        await Task.Run(() => ((dynamic)hotkey.Value)());
                                    }
                                }

                                DebugLog($"宏 [{string.Join(", ", hotkey.Key.Keys)}] 执行完毕");
                            }
                            catch (Exception ex)
                            {
                                DebugLog($"执行宏时发生错误: {ex.Message}");
                            }
                            finally
                            {
                                await WaitForKeysRelease(hotkey.Key.Keys);
                                _activeCustomHotkeys.Remove(hotkey.Key);
                                _hotkeyExecuting[hotkey.Key] = false;
                                DebugLog($"宏 [{string.Join(", ", hotkey.Key.Keys)}] 重置完成，可以再次触发");
                            }
                        });
                    }
                    else
                    {
                        DebugLog($"宏 [{string.Join(", ", hotkey.Key.Keys)}] 正在执行中，忽略本次触发");
                    }
                }
            }
        }

        private async Task WaitForKeysRelease(IEnumerable<uint> keys)
        {
            while (keys.Any(key => _currentlyPressedKeys.Contains(key)))
            {
                await Task.Delay(10);
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
            
            // 如果已存在，先注销
            if (_customHotkeys.ContainsKey(combination))
            {
                UnregisterCustomHotkey(keys);
            }
            
            _customHotkeys[combination] = action;
            _hotkeyExecuting[combination] = false;
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

                if (_customHotkeys.Remove(combination))
                {
                    _hotkeyExecuting.Remove(combination);
                    DebugLog($"注销自定义按键组合: Keys=[{string.Join(", ", keys)}]");
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
            _holdEvents.Clear();
            _pressEvents.Clear();
            Cleanup();
        }


        public static KeyboardListener _new(IntPtr windowHandle)
        {
            return new KeyboardListener(windowHandle);
        }

        // ------------------------------------------------------------------- 长按事件处理

        private readonly Dictionary<uint, (Action Action, bool IsExecuting, DateTime LastExecuteTime, int HoldThresholdMs, int HoldRepeatDelayMs)> _holdEvents = new();
        private readonly Dictionary<uint, (Action DownAction, Action UpAction)> _pressEvents = new();


        /// <summary>
        /// 注册长按按键事件
        /// </summary>
        /// <param name="key">要监听的按键的虚拟键码</param>
        /// <param name="action">长按时要执行的动作</param>
        /// <param name="holdThresholdMs">长按阈值，单位毫秒</param>
        ///  <param name="holdRepeatDelayMs">长按重复触发间隔，单位毫秒</param>
        public void RegisterKeyHoldEvent(uint key, Action action, int holdThresholdMs = 500, int holdRepeatDelayMs = 50)
        {
            _holdEvents[key] = (action, false, DateTime.Now, holdThresholdMs, holdRepeatDelayMs);
            DebugLog($"注册长按事件: Key={key}, HoldThresholdMs={holdThresholdMs}, HoldRepeatDelayMs={holdRepeatDelayMs}");
        }

        /// <summary>
        /// 注销长按按键事件
        /// </summary>
        /// <param name="key">要注销的按键的虚拟键码</param>

        public void UnregisterKeyHoldEvent(uint key)
        {
            if (_holdEvents.Remove(key))
            {
                DebugLog($"注销长按事件: Key={key}");
            }
        }
        public void RegisterKeyPressEvent(uint key, Action downAction, Action upAction)
        {
            _pressEvents[key] = (downAction, upAction);
            DebugLog($"注册按键事件: Key={key}");
        }
        
        public void UnregisterKeyPressEvent(uint key)
        {
            if (_pressEvents.Remove(key))
            {
                DebugLog($"注销按键事件: Key={key}");
            }
        }

        private void HandleKeyDown(uint key)
        {
            // 处理按下事件
            if (_pressEvents.TryGetValue(key, out var pressEvent))
            {
                Task.Run(() =>
                {
                    try
                    {
                        pressEvent.DownAction?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        DebugLog($"执行按键按下事件时出错: {ex.Message}");
                    }
                });
            }

            // 处理长按事件
            if (_holdEvents.TryGetValue(key, out var holdEvent))
            {
                if (!holdEvent.IsExecuting)
                {
                    var updatedEvent = (holdEvent.Action, true, DateTime.Now, holdEvent.HoldThresholdMs, holdEvent.HoldRepeatDelayMs);
                    _holdEvents[key] = updatedEvent;

                    Task.Run(async () =>
                    {
                        try
                        {
                            // 等待长按阈值时间
                            await Task.Delay(holdEvent.HoldThresholdMs);

                            // 如果按键仍然被按下
                            while (_currentlyPressedKeys.Contains(key))
                            {
                                holdEvent.Action?.Invoke();
                                await Task.Delay(holdEvent.HoldRepeatDelayMs);
                            }
                        }
                        catch (Exception ex)
                        {
                            DebugLog($"执行长按事件时出错: {ex.Message}");
                        }
                        finally
                        {
                            var resetEvent = (holdEvent.Action, false, DateTime.Now, holdEvent.HoldThresholdMs, holdEvent.HoldRepeatDelayMs);
                            _holdEvents[key] = resetEvent;
                        }
                    });
                }
            }
        }

        private void HandleKeyUp(uint key)
        {
            // 处理松开事件
            if (_pressEvents.TryGetValue(key, out var pressEvent))
            {
                Task.Run(() =>
                {
                    try
                    {
                        pressEvent.UpAction?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        DebugLog($"执行按键松开事件时出错: {ex.Message}");
                    }
                });
            }

            // 重置长按状态
            if (_holdEvents.TryGetValue(key, out var holdEvent))
            {
                var resetEvent = (holdEvent.Action, false, DateTime.Now, holdEvent.HoldThresholdMs, holdEvent.HoldRepeatDelayMs);
                _holdEvents[key] = resetEvent;
            }
        }
    }
}
