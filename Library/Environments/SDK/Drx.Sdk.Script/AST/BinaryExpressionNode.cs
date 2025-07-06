using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Script.AST
{
    public class BinaryExpressionNode : AstNode
    {
        public AstNode Left { get; }
        public string Operator { get; }
        public AstNode Right { get; }

        public BinaryExpressionNode(AstNode left, string op, AstNode right)
        {
            Left = left;
            Operator = op;
            Right = right;
        }
    }
}
