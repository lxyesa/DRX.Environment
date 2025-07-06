using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Script.Attributes
{
    [AttributeUsage(AttributeTargets.Method, Inherited = false)]
    public class ScriptFuncAttribute : Attribute
    {
        public string Name { get; }

        public ScriptFuncAttribute(string name)
        {
            Name = name;
        }
    }
}
