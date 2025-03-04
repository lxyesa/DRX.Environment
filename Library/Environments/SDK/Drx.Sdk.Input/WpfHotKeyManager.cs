using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Drx.Sdk.Input
{
    /// <summary>
    /// WPF全局热键监听器
    /// </summary>
    public class WpfHotkeyManager : IDisposable
    {
        #region Win32 API
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        // 修饰键常量
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;
        public const uint MOD_NOREPEAT = 0x4000;

        private const int WM_HOTKEY = 0x0312;

        // 低级键盘钩子相关API
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);
        
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);
        
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
        #endregion

        private IntPtr _windowHandle;
        private readonly Dictionary<int, (uint Modifiers, uint Key, Action Callback)> _registeredHotkeys;
        private readonly Dictionary<string, int> _hotkeyIds;
        private int _currentId;
        private bool _isDebugging = false;

        public event Action<string> DebugMessageReceived;

        // 键盘钩子相关字段
        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc _hookProc;
        private readonly HashSet<uint> _currentPressedKeys = new HashSet<uint>();
        private readonly Dictionary<string, (HashSet<uint> KeyCombination, Action Callback)> _keyCombinations = 
            new Dictionary<string, (HashSet<uint> KeyCombination, Action Callback)>();

        /// <summary>
        /// 初始化WPF热键管理器
        /// </summary>
        /// <param name="window">WPF窗口实例</param>
        public WpfHotkeyManager(Window window)
        {
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            // 获取窗口句柄
            var windowInteropHelper = new WindowInteropHelper(window);
            _windowHandle = windowInteropHelper.Handle;

            // 如果窗口尚未初始化，添加加载事件来处理
            if (_windowHandle == IntPtr.Zero)
            {
                window.SourceInitialized += (s, e) =>
                {
                    _windowHandle = new WindowInteropHelper(window).Handle;
                    HookupHwndSource();
                };
            }
            else
            {
                HookupHwndSource();
            }

            _registeredHotkeys = new Dictionary<int, (uint, uint, Action)>();
            _hotkeyIds = new Dictionary<string, int>();
            _currentId = 1;
            
            // 初始化键盘钩子
            _hookProc = HookCallback;
        }

        /// <summary>
        /// 启用或禁用调试模式
        /// </summary>
        /// <param name="enabled">是否启用</param>
        public void SetDebugMode(bool enabled)
        {
            _isDebugging = enabled;
            LogDebug($"调试模式已{(enabled ? "启用" : "禁用")}");
        }

        private void LogDebug(string message)
        {
            if (_isDebugging)
            {
                DebugMessageReceived?.Invoke($"[WpfHotkeyManager] {message}");
            }
        }

        private void HookupHwndSource()
        {
            HwndSource source = HwndSource.FromHwnd(_windowHandle);
            source?.AddHook(WndProc);
            LogDebug($"已挂接窗口句柄: {_windowHandle}");
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int id = wParam.ToInt32();
                if (_registeredHotkeys.TryGetValue(id, out var hotkey))
                {
                    LogDebug($"收到热键消息: ID={id}, 键={hotkey.Key}, 修饰键={hotkey.Modifiers}");

                    try
                    {
                        hotkey.Callback?.Invoke();
                        handled = true;
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"执行热键回调时出错: {ex.Message}");
                    }
                }
            }
            return IntPtr.Zero;
        }

        #region 标准热键方法
        /// <summary>
        /// 注册热键
        /// </summary>
        /// <param name="modifiers">修饰键组合</param>
        /// <param name="key">虚拟键码</param>
        /// <param name="callback">触发回调</param>
        /// <returns>热键的唯一ID</returns>
        public int RegisterHotkey(uint modifiers, uint key, Action callback)
        {
            // 生成唯一标识符
            string hotkeyName = $"{modifiers}_{key}";

            // 如果已经注册过，先注销
            if (_hotkeyIds.TryGetValue(hotkeyName, out int existingId))
            {
                UnregisterHotkey(existingId);
            }

            int id = _currentId++;

            if (RegisterHotKey(_windowHandle, id, modifiers, key))
            {
                _registeredHotkeys[id] = (modifiers, key, callback);
                _hotkeyIds[hotkeyName] = id;
                LogDebug($"成功注册热键: ID={id}, 键={key}, 修饰键={modifiers}");
                return id;
            }
            else
            {
                int error = Marshal.GetLastWin32Error();
                LogDebug($"注册热键失败: ID={id}, 键={key}, 修饰键={modifiers}, 错误码={error}");
                throw new Win32Exception(error, $"注册热键失败，错误码: {error}");
            }
        }

        /// <summary>
        /// 通过热键ID注销热键
        /// </summary>
        /// <param name="id">热键ID</param>
        /// <returns>是否成功注销</returns>
        public bool UnregisterHotkey(int id)
        {
            if (_registeredHotkeys.TryGetValue(id, out var hotkeyInfo))
            {
                if (UnregisterHotKey(_windowHandle, id))
                {
                    _registeredHotkeys.Remove(id);

                    // 查找并移除对应的名称映射
                    string keyToRemove = null;
                    foreach (var pair in _hotkeyIds)
                    {
                        if (pair.Value == id)
                        {
                            keyToRemove = pair.Key;
                            break;
                        }
                    }

                    if (keyToRemove != null)
                        _hotkeyIds.Remove(keyToRemove);

                    LogDebug($"成功注销热键: ID={id}");
                    return true;
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    LogDebug($"注销热键失败: ID={id}, 错误码={error}");
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// 注销所有注册的热键
        /// </summary>
        public void UnregisterAllHotkeys()
        {
            foreach (int id in new List<int>(_registeredHotkeys.Keys))
            {
                UnregisterHotkey(id);
            }

            _registeredHotkeys.Clear();
            _hotkeyIds.Clear();
            LogDebug("已注销所有热键");
        }

        /// <summary>
        /// 通过修饰键和键码注销热键
        /// </summary>
        /// <param name="modifiers">修饰键组合</param>
        /// <param name="key">虚拟键码</param>
        /// <returns>是否成功注销</returns>
        public bool UnregisterHotkey(uint modifiers, uint key)
        {
            string hotkeyName = $"{modifiers}_{key}";
            if (_hotkeyIds.TryGetValue(hotkeyName, out int id))
            {
                return UnregisterHotkey(id);
            }
            return false;
        }
        #endregion

        #region 任意键组合方法
        /// <summary>
        /// 安装键盘钩子用于监听任意键组合
        /// </summary>
        public void InstallKeyboardHook()
        {
            if (_hookId != IntPtr.Zero)
                return;

            using (var process = System.Diagnostics.Process.GetCurrentProcess())
            using (var module = process.MainModule)
            {
                IntPtr hModule = GetModuleHandle(module.ModuleName);
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, hModule, 0);
                
                if (_hookId == IntPtr.Zero)
                {
                    int error = Marshal.GetLastWin32Error();
                    LogDebug($"安装键盘钩子失败, 错误码: {error}");
                    throw new Win32Exception(error, $"安装键盘钩子失败，错误码: {error}");
                }
                
                LogDebug("已成功安装键盘钩子");
            }
        }

        /// <summary>
        /// 卸载键盘钩子
        /// </summary>
        public void UninstallKeyboardHook()
        {
            if (_hookId != IntPtr.Zero)
            {
                if (UnhookWindowsHookEx(_hookId))
                {
                    _hookId = IntPtr.Zero;
                    LogDebug("已成功卸载键盘钩子");
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    LogDebug($"卸载键盘钩子失败, 错误码: {error}");
                }
            }
        }

        /// <summary>
        /// 键盘钩子回调函数
        /// </summary>
        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0)
            {
                int msgType = wParam.ToInt32();
                
                // 获取按键代码
                uint vkCode = (uint)Marshal.ReadInt32(lParam);
                
                // 处理按键按下和释放
                if (msgType == WM_KEYDOWN || msgType == WM_SYSKEYDOWN)
                {
                    if (!_currentPressedKeys.Contains(vkCode))
                    {
                        _currentPressedKeys.Add(vkCode);
                        LogDebug($"按键按下: {vkCode}");
                        CheckKeyboardCombinations();
                    }
                }
                else if (msgType == WM_KEYUP || msgType == WM_SYSKEYUP)
                {
                    if (_currentPressedKeys.Contains(vkCode))
                    {
                        _currentPressedKeys.Remove(vkCode);
                        LogDebug($"按键释放: {vkCode}");
                    }
                }
            }
            
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        /// <summary>
        /// 检查当前按下的键是否匹配已注册的组合
        /// </summary>
        private void CheckKeyboardCombinations()
        {
            foreach (var combo in _keyCombinations)
            {
                var keyCombination = combo.Value.KeyCombination;
                // 修改检测逻辑：只要注册的组合键中的所有键都被按下即可触发，不需要完全相等
                if (keyCombination.IsSubsetOf(_currentPressedKeys))
                {
                    LogDebug($"触发键组合: {combo.Key}");
                    try
                    {
                        combo.Value.Callback?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        LogDebug($"执行键组合回调时出错: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 注册任意键组合
        /// </summary>
        /// <param name="keys">要监听的按键代码集合</param>
        /// <param name="callback">当按键组合触发时执行的回调</param>
        /// <returns>唯一的组合名称，用于后续解除注册</returns>
        public string RegisterKeyCombination(uint[] keys, Action callback)
        {
            if (keys == null || keys.Length == 0)
                throw new ArgumentException("键组合不能为空", nameof(keys));
                
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
                
            // 确保钩子已安装
            if (_hookId == IntPtr.Zero)
            {
                InstallKeyboardHook();
            }
            
            // 创建按键组合的唯一标识
            string comboName = string.Join("_", keys);
            var keySet = new HashSet<uint>(keys);
            
            // 注册组合
            _keyCombinations[comboName] = (keySet, callback);
            LogDebug($"已注册键组合: {comboName}");
            
            return comboName;
        }

        /// <summary>
        /// 注销键组合
        /// </summary>
        /// <param name="comboName">注册时返回的组合名称</param>
        /// <returns>是否成功注销</returns>
        public bool UnregisterKeyCombination(string comboName)
        {
            if (string.IsNullOrEmpty(comboName))
                return false;
                
            bool result = _keyCombinations.Remove(comboName);
            if (result)
            {
                LogDebug($"已注销键组合: {comboName}");
                
                // 如果没有任何组合需要监听，可以考虑卸载钩子
                if (_keyCombinations.Count == 0)
                {
                    UninstallKeyboardHook();
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 注销所有键组合
        /// </summary>
        public void UnregisterAllKeyCombinations()
        {
            _keyCombinations.Clear();
            UninstallKeyboardHook();
            LogDebug("已注销所有键组合");
        }
        #endregion

        /// <summary>
        /// 修改已存在的键组合
        /// </summary>
        /// <param name="comboName">原始组合名称</param>
        /// <param name="newKeys">新的按键代码集合</param>
        /// <returns>新的组合名称，如果修改失败则返回null</returns>
        public string ModifyKeyCombination(string comboName, uint[] newKeys)
        {
            if (string.IsNullOrEmpty(comboName) || newKeys == null || newKeys.Length == 0)
            {
                LogDebug("修改键组合失败：无效的参数");
                return null;
            }
            
            // 检查原始组合是否存在
            if (!_keyCombinations.TryGetValue(comboName, out var comboInfo))
            {
                LogDebug($"修改键组合失败：找不到原始组合 '{comboName}'");
                return null;
            }
            
            // 保存原始回调
            Action callback = comboInfo.Callback;
            
            // 注销原始组合
            UnregisterKeyCombination(comboName);
            
            // 注册新组合，使用相同的回调
            string newComboName = RegisterKeyCombination(newKeys, callback);
            
            LogDebug($"已将键组合从 '{comboName}' 修改为 '{newComboName}'");
            return newComboName;
        }
        
        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            UnregisterAllHotkeys();
            UninstallKeyboardHook();
        }
    }
}