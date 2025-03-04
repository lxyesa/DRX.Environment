using System;
using System.Windows;
using Drx.Sdk.Input;

public class KeyCombinationExample
{
    private WpfHotkeyManager _hotkeyManager;
    private string _registeredComboId;
    
    public void Initialize(Window window)
    {
        _hotkeyManager = new WpfHotkeyManager(window);
        _hotkeyManager.SetDebugMode(true);
        _hotkeyManager.DebugMessageReceived += message => Console.WriteLine(message);
        
        // 注册常规热键 (Ctrl+Alt+A)
        _hotkeyManager.RegisterHotkey(
            WpfHotkeyManager.MOD_CONTROL | WpfHotkeyManager.MOD_ALT, 
            (uint)VirtualKeyCode.KEY_A, 
            () => MessageBox.Show("标准热键 Ctrl+Alt+A 被触发"));
        
        // 注册任意键组合 (例如: J+K+L 组合)
        uint[] keyCombination = new uint[] { 
            (uint)VirtualKeyCode.KEY_J, 
            (uint)VirtualKeyCode.KEY_K, 
            (uint)VirtualKeyCode.KEY_L 
        };
        _registeredComboId = _hotkeyManager.RegisterKeyCombination(
            keyCombination,
            () => MessageBox.Show("键组合 J+K+L 被触发"));
            
        // 使用更多VirtualKeyCode的示例
        RegisterMoreHotkeys();
    }
    
    private void RegisterMoreHotkeys()
    {
        // 注册Shift+F5组合
        _hotkeyManager.RegisterHotkey(
            WpfHotkeyManager.MOD_SHIFT,
            (uint)VirtualKeyCode.F5,
            () => Console.WriteLine("Shift+F5 被按下"));
            
        // 注册Tab+空格+W的组合键
        uint[] customCombo = new uint[] {
            (uint)VirtualKeyCode.TAB,
            (uint)VirtualKeyCode.SPACE,
            (uint)VirtualKeyCode.KEY_W
        };
        _hotkeyManager.RegisterKeyCombination(
            customCombo,
            () => Console.WriteLine("Tab+空格+W 组合被按下"));
    }
    
    public void Cleanup()
    {
        // 清理资源
        _hotkeyManager?.Dispose();
    }
}
