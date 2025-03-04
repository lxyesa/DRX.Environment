using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Script.AST
{
    public class VariableNode : AstNode
    {
        public string Identifier { get; }

        public VariableNode(string identifier)
        {
            Identifier = identifier;
        }
    }
}
