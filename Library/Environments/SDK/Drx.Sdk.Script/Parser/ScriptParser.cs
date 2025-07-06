using Drx.Sdk.Script.AST;
using System;
using System.Collections.Generic;

namespace Drx.Sdk.Script.Parser
{
    public class ScriptParser
    {
        private List<Token> tokens;
        private int position;

        public AstNode Parse(string scriptText)
        {
            var tokenizer = new Tokenizer();
            tokens = tokenizer.Tokenize(scriptText);
            position = 0;

            return ParseStatements();
        }

        private AstNode ParseStatements()
        {
            var statements = new List<AstNode>();

            while (CurrentToken.Type != TokenType.EndOfFile &&
                   CurrentToken.Value != "end" &&
                   CurrentToken.Value != "else")
            {
                statements.Add(ParseStatement());
            }

            return new BlockNode(statements);
        }

        private AstNode ParseStatement()
        {
            if (CurrentToken.Type == TokenType.Identifier)
            {
                if (CurrentToken.Value == "var")
                {
                    return ParseVariableDeclaration();
                }
                else if (CurrentToken.Value == "if")
                {
                    return ParseIfStatement();
                }
                else
                {
                    return ParseFunctionCall();
                }
            }

            throw new Exception("Unexpected token: " + CurrentToken.Value);
        }

        private AstNode ParseVariableDeclaration()
        {
            ConsumeToken(); // consume "var"

            if (CurrentToken.Type != TokenType.Identifier)
                throw new Exception("Expected variable name after 'var'.");

            var identifier = CurrentToken.Value;
            ConsumeToken(); // consume variable name

            if (CurrentToken.Type != TokenType.Operator || CurrentToken.Value != "=")
                throw new Exception("Expected '=' after variable name.");

            ConsumeToken(); // consume '='

            var expression = ParseExpression();

            if (CurrentToken.Type != TokenType.Punctuation || CurrentToken.Value != ";")
                throw new Exception("Expected ';' after expression.");

            ConsumeToken(); // consume ';'

            return new VariableDeclarationNode(identifier, expression);
        }

        private AstNode ParseFunctionCall()
        {
            var identifier = CurrentToken.Value;
            ConsumeToken(); // consume identifier

            if (CurrentToken.Type != TokenType.Punctuation || CurrentToken.Value != "(")
                throw new Exception("Expected '(' after function name.");

            ConsumeToken(); // consume '('
            var arguments = new List<AstNode>();

            // 处理空参数的情况
            if (!(CurrentToken.Type == TokenType.Punctuation && CurrentToken.Value == ")"))
            {
                while (true)
                {
                    arguments.Add(ParseExpression());
                    if (CurrentToken.Type == TokenType.Punctuation && CurrentToken.Value == ",")
                    {
                        ConsumeToken(); // consume ','
                    }
                    else
                    {
                        break;
                    }
                }
            }

            if (CurrentToken.Type != TokenType.Punctuation || CurrentToken.Value != ")")
                throw new Exception("Expected ')' after function arguments.");

            ConsumeToken(); // consume ')'

            if (CurrentToken.Type != TokenType.Punctuation || CurrentToken.Value != ";")
                throw new Exception("Expected ';' after function call.");

            ConsumeToken(); // consume ';'

            return new FunctionCallNode(identifier, arguments);
        }

        /// <summary>
        /// 解析 if 语句：
        /// 格式：
        /// if condition then
        ///    ...statements...
        /// [else
        ///    ...statements...]
        /// end;
        /// </summary>
        private AstNode ParseIfStatement()
        {
            ConsumeToken(); // consume "if"

            // 解析条件（支持 a == b 形式）
            var condition = ParseCondition();

            // 条件之后应为关键字 then
            if (CurrentToken.Type != TokenType.Identifier || CurrentToken.Value != "then")
                throw new Exception("Expected 'then' after if condition.");

            ConsumeToken(); // consume "then"

            // 解析 then 块，直到遇到 else 或 end
            var thenBlock = ParseStatements();

            BlockNode elseBlock = null;
            if (CurrentToken.Type == TokenType.Identifier && CurrentToken.Value == "else")
            {
                ConsumeToken(); // consume "else"
                elseBlock = (BlockNode)ParseStatements();
            }

            // 解析结束标识 end;
            if (CurrentToken.Type != TokenType.Identifier || CurrentToken.Value != "end")
                throw new Exception("Expected 'end' to close if statement.");

            ConsumeToken(); // consume "end"

            if (CurrentToken.Type != TokenType.Punctuation || CurrentToken.Value != ";")
                throw new Exception("Expected ';' after end.");

            ConsumeToken(); // consume ';'

            return new IfNode(condition, (BlockNode)thenBlock, elseBlock);
        }

        /// <summary>
        /// 解析条件，目前支持 '==' 运算符
        /// </summary>
        private AstNode ParseCondition()
        {
            var left = ParseExpression();
            if (CurrentToken.Type == TokenType.Operator && CurrentToken.Value == "==")
            {
                string op = CurrentToken.Value;
                ConsumeToken(); // consume "=="
                var right = ParseExpression();
                return new BinaryExpressionNode(left, op, right);
            }
            return left;
        }

        private AstNode ParseExpression()
        {
            if (CurrentToken.Type == TokenType.Number)
            {
                var number = CurrentToken.Value;
                ConsumeToken(); // consume number
                return new LiteralNode(number);
            }
            else if (CurrentToken.Type == TokenType.String)
            {
                // 获取原始字符串，去除首尾的引号
                var str = CurrentToken.Value;
                ConsumeToken(); // consume string
                                // 去除单引号或双引号
                str = str.Trim('\'', '\"');
                return new LiteralNode(str);
            }
            else if (CurrentToken.Type == TokenType.Identifier)
            {
                var identifier = CurrentToken.Value;
                ConsumeToken(); // consume identifier

                // 如果后面紧跟着左括号，则解析为函数调用表达式
                if (CurrentToken.Type == TokenType.Punctuation && CurrentToken.Value == "(")
                {
                    ConsumeToken(); // consume '('
                    var arguments = new List<AstNode>();

                    // 处理没有参数的情况
                    if (!(CurrentToken.Type == TokenType.Punctuation && CurrentToken.Value == ")"))
                    {
                        while (true)
                        {
                            arguments.Add(ParseExpression());
                            if (CurrentToken.Type == TokenType.Punctuation && CurrentToken.Value == ",")
                            {
                                ConsumeToken(); // consume ','
                            }
                            else
                            {
                                break;
                            }
                        }
                    }

                    if (CurrentToken.Type != TokenType.Punctuation || CurrentToken.Value != ")")
                        throw new Exception("Expected ')' after function arguments.");

                    ConsumeToken(); // consume ')'
                    return new FunctionCallNode(identifier, arguments);
                }
                else
                {
                    return new VariableNode(identifier);
                }
            }

            throw new Exception("Unexpected token: " + CurrentToken.Value);
        }

        private Token CurrentToken => tokens[position];
        private Token PeekToken(int offset) => tokens[position + offset];
        private void ConsumeToken() => position++;
    }
}
