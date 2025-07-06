using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Script.AST
{
    public class LiteralNode : AstNode
    {
        public string Value { get; }

        public LiteralNode(string value)
        {
            Value = value;
        }
    }
}
