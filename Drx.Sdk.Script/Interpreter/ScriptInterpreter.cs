using Drx.Sdk.Script.AST;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Drx.Sdk.Script.Interpreter
{
    public class ScriptInterpreter
    {
        private readonly Environment environment;

        public ScriptInterpreter()
        {
            environment = new Environment();
        }

        public void RegisterFunction(string name, MethodInfo method, object instance)
        {
            environment.RegisterFunction(name, method, instance);
        }

        public void Execute(AstNode ast)
        {
            ExecuteNode(ast);
        }

        private void ExecuteNode(AstNode node)
        {
            switch (node)
            {
                case BlockNode blockNode:
                    foreach (var statement in blockNode.Statements)
                    {
                        ExecuteNode(statement);
                    }
                    break;
                case VariableDeclarationNode variableDeclarationNode:
                    var value = EvaluateExpression(variableDeclarationNode.Expression);
                    environment.SetVariable(variableDeclarationNode.Identifier, value);
                    break;
                case FunctionCallNode functionCallNode:
                    var args = new List<object>();
                    foreach (var arg in functionCallNode.Arguments)
                    {
                        args.Add(EvaluateExpression(arg));
                    }
                    environment.InvokeFunction(functionCallNode.Identifier, args.ToArray());
                    break;
                case IfNode ifNode:
                    var conditionValue = EvaluateExpression(ifNode.Condition);
                    // 判断条件，如果返回布尔型且为 true，就执行 then 块，否则执行 else 块（若存在）
                    if (conditionValue is bool b && b)
                    {
                        ExecuteNode(ifNode.ThenBlock);
                    }
                    else if (ifNode.ElseBlock != null)
                    {
                        ExecuteNode(ifNode.ElseBlock);
                    }
                    break;
                default:
                    throw new Exception("Unknown AST node type: " + node.GetType().Name);
            }
        }

        private object EvaluateExpression(AstNode node)
        {
            switch (node)
            {
                case LiteralNode literalNode:
                    // 假设字面量可以是整型，也可能是字符串，这里简单处理
                    int intResult;
                    if (int.TryParse(literalNode.Value, out intResult))
                        return intResult;
                    return literalNode.Value;
                case VariableNode variableNode:
                    return environment.GetVariable(variableNode.Identifier);
                case FunctionCallNode functionCallNode:
                    var args = new List<object>();
                    foreach (var arg in functionCallNode.Arguments)
                    {
                        args.Add(EvaluateExpression(arg));
                    }
                    return environment.InvokeFunction(functionCallNode.Identifier, args.ToArray());
                case BinaryExpressionNode binaryNode:
                    var left = EvaluateExpression(binaryNode.Left);
                    var right = EvaluateExpression(binaryNode.Right);
                    switch (binaryNode.Operator)
                    {
                        case "==":
                            return left.Equals(right);
                        default:
                            throw new Exception($"Unknown binary operator: {binaryNode.Operator}");
                    }
                default:
                    throw new Exception("Unknown expression type: " + node.GetType().Name);
            }
        }
    }
}
