using System;
using System.Windows;
using Drx.Sdk.Input;

public class VkcExample
{
    private WpfHotkeyManager _hotkeyManager;
    
    public void Initialize(Window window)
    {
        _hotkeyManager = new WpfHotkeyManager(window);
        
        // 使用VKC静态类注册热键
        _hotkeyManager.RegisterHotkey(
            WpfHotkeyManager.MOD_CONTROL | WpfHotkeyManager.MOD_SHIFT, 
            VKC.KEY_S, 
            () => Console.WriteLine("Ctrl+Shift+S 被按下"));
            
        // 使用VKC静态类注册键组合
        uint[] customCombo = new uint[] {
            VKC.F1,
            VKC.KEY_Z
        };
        _hotkeyManager.RegisterKeyCombination(
            customCombo,
            () => Console.WriteLine("F1+Z 组合被按下"));
    }
    
    public void Cleanup()
    {
        _hotkeyManager?.Dispose();
    }
}
