using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Script.AST
{
    public class ParameterNode : AstNode
    {
        public string ParamType { get; }
        public string ParamName { get; }

        public ParameterNode(string paramType, string paramName)
        {
            ParamType = paramType;
            ParamName = paramName;
        }
    }
}
