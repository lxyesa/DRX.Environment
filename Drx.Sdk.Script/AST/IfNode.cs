using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Script.AST
{
    public class IfNode : AstNode
    {
        public AstNode Condition { get; }
        public BlockNode ThenBlock { get; }
        public BlockNode ElseBlock { get; }

        public IfNode(AstNode condition, BlockNode thenBlock, BlockNode elseBlock = null)
        {
            Condition = condition;
            ThenBlock = thenBlock;
            ElseBlock = elseBlock;
        }
    }
}
