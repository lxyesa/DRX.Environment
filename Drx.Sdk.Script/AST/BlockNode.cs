using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Script.AST
{
    public class BlockNode : AstNode
    {
        public List<AstNode> Statements { get; }

        public BlockNode(List<AstNode> statements)
        {
            Statements = statements;
        }
    }
}
