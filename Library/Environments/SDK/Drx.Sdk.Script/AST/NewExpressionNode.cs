using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Script.AST
{
    public class NewExpressionNode : AstNode
    {
        public string ClassName { get; }

        public NewExpressionNode(string className)
        {
            ClassName = className;
        }
    }
}
