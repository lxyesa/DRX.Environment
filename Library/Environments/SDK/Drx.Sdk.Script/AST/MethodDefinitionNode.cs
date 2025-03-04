using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Drx.Sdk.Script.AST
{
    public class MethodDefinitionNode : AstNode
    {
        public string ReturnType { get; }       // 如 "int"
        public string ClassName { get; }          // 如 "Abc"
        public string MethodName { get; }         // 如 "Add"
        public List<ParameterNode> Parameters { get; }
        public BlockNode Body { get; }            // 方法体，由若干语句组成
        public ReturnNode ReturnStatement { get; }  // 方法的返回语句

        public MethodDefinitionNode(string returnType, string className, string methodName, List<ParameterNode> parameters, BlockNode body, ReturnNode returnStatement)
        {
            ReturnType = returnType;
            ClassName = className;
            MethodName = methodName;
            Parameters = parameters;
            Body = body;
            ReturnStatement = returnStatement;
        }
    }
}
