using Drx.Sdk.Script.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Script.Functions
{
    public class ScriptFunctions
    {
        [ScriptFunc("add")]
        public int Add(int a, int b)
        {
            return a + b;
        }

        [ScriptFunc("print")]
        public void Print(string message)
        {
            Console.WriteLine(message);
        }

        [ScriptFunc("int_tostring")]
        public string ToString(int value)
        {
            return value.ToString();
        }
    }
}
