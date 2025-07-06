using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Script.AST
{
    public class FunctionCallNode : AstNode
    {
        public string Identifier { get; }
        public List<AstNode> Arguments { get; }

        public FunctionCallNode(string identifier, List<AstNode> arguments)
        {
            Identifier = identifier;
            Arguments = arguments;
        }
    }
}
