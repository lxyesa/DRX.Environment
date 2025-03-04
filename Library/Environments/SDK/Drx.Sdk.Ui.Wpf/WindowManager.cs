using System;
using System.Windows;
using Drx.Sdk.Script.Attributes;

namespace Drx.Sdk.Ui.Wpf;

[ScriptClass(Name = "window")]
public class WindowManager
{
    public Window CreateWindow()
    {
        return new Window();
    }
}
