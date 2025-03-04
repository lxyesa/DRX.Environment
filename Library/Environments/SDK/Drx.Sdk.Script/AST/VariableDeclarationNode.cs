using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Script.AST
{
    public class VariableDeclarationNode : AstNode
    {
        public string Identifier { get; }
        public AstNode Expression { get; }

        public VariableDeclarationNode(string identifier, AstNode expression)
        {
            Identifier = identifier;
            Expression = expression;
        }
    }
}
