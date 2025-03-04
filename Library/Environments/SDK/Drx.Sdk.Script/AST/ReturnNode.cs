using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Script.AST
{
    public class ReturnNode : AstNode
    {
        public AstNode Expression { get; }

        public ReturnNode(AstNode expression)
        {
            Expression = expression;
        }
    }
}
