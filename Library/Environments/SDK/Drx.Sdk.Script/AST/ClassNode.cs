using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Script.AST
{
    public class ClassNode : AstNode
    {
        public string ClassName { get; }
        public List<MethodDefinitionNode> Methods { get; }

        public ClassNode(string className, List<MethodDefinitionNode> methods)
        {
            ClassName = className;
            Methods = methods;
        }
    }
}
