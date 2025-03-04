using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Drx.Sdk.Script.Parser
{
    public class Tokenizer
    {
        // 修改字符串匹配：同时支持双引号和单引号包围的字符串
        // 并在标点中增加冒号 ':'
        private static readonly Regex TokenRegex = new Regex(
            @"(?<Whitespace>\s+)|" +
            @"(?<Identifier>[a-zA-Z_][a-zA-Z0-9_]*(?:[:][a-zA-Z_][a-zA-Z0-9_]*)?)|" +
            @"(?<Number>\d+)|" +
            @"(?<String>""[^""]*""|'[^']*')|" +
            @"(?<Operator>==|[+\-*/=])|" +
            @"(?<Punctuation>[,;()])",
            RegexOptions.Compiled
        );

        public List<Token> Tokenize(string script)
        {
            var tokens = new List<Token>();
            var matches = TokenRegex.Matches(script);

            foreach (Match match in matches)
            {
                if (match.Groups["Whitespace"].Success)
                {
                    continue;
                }
                if (match.Groups["Identifier"].Success)
                {
                    tokens.Add(new Token(TokenType.Identifier, match.Groups["Identifier"].Value));
                }
                else if (match.Groups["Number"].Success)
                {
                    tokens.Add(new Token(TokenType.Number, match.Groups["Number"].Value));
                }
                else if (match.Groups["String"].Success)
                {
                    tokens.Add(new Token(TokenType.String, match.Groups["String"].Value));
                }
                else if (match.Groups["Operator"].Success)
                {
                    tokens.Add(new Token(TokenType.Operator, match.Groups["Operator"].Value));
                }
                else if (match.Groups["Punctuation"].Success)
                {
                    tokens.Add(new Token(TokenType.Punctuation, match.Groups["Punctuation"].Value));
                }
            }

            tokens.Add(new Token(TokenType.EndOfFile, string.Empty));
            return tokens;
        }
    }
}
