using System;
using Drx.Sdk.Script.Attributes;
using Drx.Sdk.Script.Interfaces;

namespace Drx.Sdk.Script.Functions;

[ScriptClass("console")]
public class Console : IScript
{
    public static void log(object message)
    {
        var messageString = message?.ToString();
        System.Console.WriteLine(messageString);
    }

    public static void setcolor(string color)
    {
        if (Enum.TryParse(color, out ConsoleColor consoleColor))
        {
            System.Console.ForegroundColor = consoleColor;
        }
        else
        {
            System.Console.WriteLine($"Invalid color name: {color}");
        }
    }

    public static void resetcolor()
    {
        System.Console.ResetColor();
    }

    public static void read()
    {
        System.Console.ReadLine();
    }
}
