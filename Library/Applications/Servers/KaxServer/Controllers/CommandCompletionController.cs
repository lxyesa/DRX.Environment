using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Drx.Sdk.Shared.JavaScript;
using System;

namespace Library.Applications.Servers.KaxServer.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class CommandCompletionController : ControllerBase
    {
        /// <summary>
        /// 获取所有已注册的类名，支持前缀过滤
        /// </summary>
        /// <param name="prefix">可选，按前缀过滤</param>
        /// <returns>JSON格式的类名列表</returns>
        [HttpGet("classes")]
        public IActionResult GetRegisteredClasses([FromQuery] string? prefix = null)
        {
            try
            {
                // 获取所有注册的类名（Class）
                var classNames = JavaScript.GetRegisteredClasses() ?? new List<string>();
                // 获取所有注册的静态类名（StaticClass）
                var staticClassNames = ScriptRegistry.Instance.GetExportedStaticClasses()?.Select(m => m.ExportName).ToList() ?? new List<string>();
                // 合并并去重
                var allNames = classNames.Concat(staticClassNames).Distinct().ToList();
                
                System.Diagnostics.Debug.WriteLine($"[API][GetRegisteredClasses] 请求前缀: {prefix ?? "(无)"}，原始类名: {string.Join(", ", allNames)}");

                if (!string.IsNullOrEmpty(prefix))
                {
                    allNames = allNames.Where(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                var suggestions = allNames.Select(name => new
                {
                    text = name,
                    type = "class",
                    description = $"JavaScript类: {name}"
                }).ToList();

                System.Diagnostics.Debug.WriteLine($"[API][GetRegisteredClasses] 返回类名: {string.Join(", ", suggestions.Select(s => s.text))}");
                return new JsonResult(suggestions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API][GetRegisteredClasses][异常] {ex}");
                return StatusCode(500, "获取类名失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 获取指定类的成员（方法、属性、字段）
        /// </summary>
        /// <param name="className">类名</param>
        /// <param name="prefix">可选，按前缀过滤成员名</param>
        /// <returns>JSON格式的成员列表</returns>
        [HttpGet("members/{className}")]
        public IActionResult GetClassMembers(string className, [FromQuery] string? prefix = null)
        {
            try
            {
                if (string.IsNullOrEmpty(className))
                {
                    return BadRequest("类名不能为空");
                }

                if (!ScriptRegistry.Instance.TryGetExportedType(className, out var meta) || meta == null)
                {
                    return NotFound($"未找到类: {className}");
                }

                var suggestions = new List<object>();

                // 添加方法
                if (meta.ExportedMethods != null)
                {
                    foreach (var method in meta.ExportedMethods)
                    {
                        if (string.IsNullOrEmpty(prefix) || method.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            var parameters = method.GetParameters();
                            var paramList = string.Join(", ", parameters.Select(p => $"{GetSimpleTypeName(p.ParameterType)} {p.Name}"));
                            var returnType = GetSimpleTypeName(method.ReturnType);
                            
                            suggestions.Add(new
                            {
                                text = method.Name,
                                type = "method",
                                description = $"方法: {returnType} {method.Name}({paramList})",
                                parameters = parameters.Select(p => new
                                {
                                    name = p.Name,
                                    type = GetSimpleTypeName(p.ParameterType),
                                    hasDefaultValue = p.HasDefaultValue,
                                    defaultValue = p.HasDefaultValue ? p.DefaultValue?.ToString() : null
                                }).ToArray(),
                                returnType = returnType
                            });
                        }
                    }
                }

                // 添加属性
                if (meta.ExportedProperties != null)
                {
                    foreach (var prop in meta.ExportedProperties)
                    {
                        if (string.IsNullOrEmpty(prefix) || prop.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            var propType = GetSimpleTypeName(prop.PropertyType);
                            suggestions.Add(new
                            {
                                text = prop.Name,
                                type = "property",
                                description = $"属性: {propType} {prop.Name}",
                                propertyType = propType,
                                canRead = prop.CanRead,
                                canWrite = prop.CanWrite
                            });
                        }
                    }
                }

                // 添加字段
                if (meta.ExportedFields != null)
                {
                    foreach (var field in meta.ExportedFields)
                    {
                        if (string.IsNullOrEmpty(prefix) || field.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            var fieldType = GetSimpleTypeName(field.FieldType);
                            suggestions.Add(new
                            {
                                text = field.Name,
                                type = "field",
                                description = $"字段: {fieldType} {field.Name}",
                                fieldType = fieldType,
                                isReadOnly = field.IsInitOnly
                            });
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[API][GetClassMembers] 类: {className}, 前缀: {prefix ?? "(无)"}, 返回: {suggestions.Count} 个成员");
                return new JsonResult(suggestions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API][GetClassMembers][异常] {ex}");
                return StatusCode(500, "获取类成员失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 智能自动补全，根据当前输入的上下文提供建议
        /// </summary>
        /// <param name="input">当前输入的完整文本</param>
        /// <param name="cursorPosition">光标位置</param>
        /// <returns>JSON格式的建议列表</returns>
        [HttpPost("suggest")]
        public IActionResult GetSmartSuggestions([FromBody] CompletionRequest request)
        {
            try
            {
                if (request == null || string.IsNullOrEmpty(request.Input))
                {
                    return BadRequest("输入内容不能为空");
                }

                var input = request.Input;
                var cursorPos = Math.Min(request.CursorPosition, input.Length);
                var beforeCursor = input.Substring(0, cursorPos);

                System.Diagnostics.Debug.WriteLine($"[API][GetSmartSuggestions] 输入: '{input}', 光标位置: {cursorPos}, 光标前: '{beforeCursor}'");

                // 解析当前上下文
                var context = ParseCompletionContext(beforeCursor);
                var suggestions = new List<object>();

                switch (context.Type)
                {
                    case CompletionContextType.None:
                        // 在字符串内或其他不应提供补全的上下文中，返回空建议
                        break;

                    case CompletionContextType.ClassName:
                        // 类名补全
                        suggestions.AddRange(GetClassNameSuggestions(context.Prefix));
                        break;

                    case CompletionContextType.MemberAccess:
                        // 成员访问补全（如 help.xxx）
                        suggestions.AddRange(GetMemberSuggestions(context.ClassName, context.Prefix));
                        break;

                    case CompletionContextType.MethodParameter:
                        // 方法参数补全
                        suggestions.AddRange(GetParameterSuggestions(context.ClassName, context.MethodName, context.ParameterIndex));
                        break;

                    default:
                        // 默认提供类名建议
                        suggestions.AddRange(GetClassNameSuggestions(""));
                        break;
                }

                System.Diagnostics.Debug.WriteLine($"[API][GetSmartSuggestions] 返回 {suggestions.Count} 个建议");
                return new JsonResult(suggestions);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[API][GetSmartSuggestions][异常] {ex}");
                return StatusCode(500, "获取智能建议失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 解析补全上下文
        /// </summary>
        private CompletionContext ParseCompletionContext(string beforeCursor)
        {
            beforeCursor = beforeCursor.Trim();

            // 检查是否在字符串字面量中
            if (IsInsideStringLiteral(beforeCursor))
            {
                // 在字符串内部，不提供补全
                return new CompletionContext
                {
                    Type = CompletionContextType.None,
                    Prefix = ""
                };
            }

            // 检查是否是方法调用中的参数（包含未闭合的括号）
            var lastOpenParen = beforeCursor.LastIndexOf('(');
            var lastCloseParen = beforeCursor.LastIndexOf(')');
            
            if (lastOpenParen > lastCloseParen) // 在方法调用中
            {
                var methodCall = beforeCursor.Substring(0, lastOpenParen);
                var paramPart = beforeCursor.Substring(lastOpenParen + 1);
                var paramIndex = paramPart.Split(',').Length - 1;

                // 获取当前参数的内容
                var currentParam = paramPart.Split(',').LastOrDefault()?.Trim() ?? "";
                
                // 再次检查当前参数是否在字符串中
                if (IsInsideStringLiteral(currentParam))
                {
                    return new CompletionContext
                    {
                        Type = CompletionContextType.None,
                        Prefix = ""
                    };
                }
                
                // 检查当前参数是否包含点号（如 MathUtils.Add）
                var dotInParam = currentParam.LastIndexOf('.');
                if (dotInParam > 0)
                {
                    // 参数中的成员访问补全
                    var paramClassName = currentParam.Substring(0, dotInParam);
                    var paramMemberPrefix = currentParam.Substring(dotInParam + 1);
                    
                    return new CompletionContext
                    {
                        Type = CompletionContextType.MemberAccess,
                        ClassName = paramClassName,
                        Prefix = paramMemberPrefix
                    };
                }
                else if (!string.IsNullOrEmpty(currentParam))
                {
                    // 参数中的类名补全
                    return new CompletionContext
                    {
                        Type = CompletionContextType.ClassName,
                        Prefix = currentParam
                    };
                }

                // 如果当前参数为空，提供参数提示
                var lastDot = methodCall.LastIndexOf('.');
                if (lastDot > 0)
                {
                    var className = methodCall.Substring(0, lastDot);
                    var methodName = methodCall.Substring(lastDot + 1);
                    
                    return new CompletionContext
                    {
                        Type = CompletionContextType.MethodParameter,
                        ClassName = className,
                        MethodName = methodName,
                        ParameterIndex = paramIndex,
                        Prefix = currentParam
                    };
                }
            }

            // 检查是否是成员访问（表达式.成员名）
            var dotIndex = beforeCursor.LastIndexOf('.');
            if (dotIndex > 0)
            {
                var expr = beforeCursor.Substring(0, dotIndex).Trim();
                var memberPrefix = beforeCursor.Substring(dotIndex + 1);
                // 这里 expr 可能是类名、变量名、或表达式
                return new CompletionContext
                {
                    Type = CompletionContextType.MemberAccess,
                    ClassName = expr, // 这里的 ClassName 可能是表达式
                    Prefix = memberPrefix
                };
            }

            // 默认为类名补全
            return new CompletionContext
            {
                Type = CompletionContextType.ClassName,
                Prefix = beforeCursor
            };
        }

        /// <summary>
        /// 检查是否在字符串字面量内部
        /// </summary>
        private bool IsInsideStringLiteral(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            int doubleQuoteCount = 0;
            int singleQuoteCount = 0;
            bool inDoubleQuote = false;
            bool inSingleQuote = false;
            bool isEscaped = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (isEscaped)
                {
                    isEscaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    isEscaped = true;
                    continue;
                }

                if (c == '"' && !inSingleQuote)
                {
                    if (!inDoubleQuote)
                    {
                        inDoubleQuote = true;
                        doubleQuoteCount++;
                    }
                    else
                    {
                        inDoubleQuote = false;
                    }
                }
                else if (c == '\'' && !inDoubleQuote)
                {
                    if (!inSingleQuote)
                    {
                        inSingleQuote = true;
                        singleQuoteCount++;
                    }
                    else
                    {
                        inSingleQuote = false;
                    }
                }
            }

            // 如果在双引号或单引号内，返回true
            return inDoubleQuote || inSingleQuote;
        }

        /// <summary>
        /// 获取类名建议
        /// </summary>
        private List<object> GetClassNameSuggestions(string prefix)
        {
            var classNames = JavaScript.GetRegisteredClasses() ?? new List<string>();
            var staticClassNames = ScriptRegistry.Instance.GetExportedStaticClasses()?.Select(m => m.ExportName).ToList() ?? new List<string>();
            var allNames = classNames.Concat(staticClassNames).Distinct();

            if (!string.IsNullOrEmpty(prefix))
            {
                allNames = allNames.Where(c => c.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            }

            return allNames.Select(name => new
            {
                text = name,
                type = "class",
                description = $"JavaScript类: {name}",
                insertText = name
            }).Cast<object>().ToList();
        }

        /// <summary>
        /// 获取成员建议
        /// </summary>
        private List<object> GetMemberSuggestions(string classOrExpr, string prefix)
        {
            var suggestions = new List<object>();

            // 1. 先尝试作为注册类处理
            if (ScriptRegistry.Instance.TryGetExportedType(classOrExpr, out var meta) && meta != null)
            {
                // ...existing code...
                if (meta.ExportedMethods != null)
                {
                    foreach (var method in meta.ExportedMethods)
                    {
                        if (string.IsNullOrEmpty(prefix) || method.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            var parameters = method.GetParameters();
                            var paramList = string.Join(", ", parameters.Select(p => $"{GetSimpleTypeName(p.ParameterType)} {p.Name}"));
                            suggestions.Add(new
                            {
                                text = method.Name,
                                type = "method",
                                description = $"{GetSimpleTypeName(method.ReturnType)} {method.Name}({paramList})",
                                insertText = parameters.Length > 0 ? $"{method.Name}(" : $"{method.Name}()",
                                hasParameters = parameters.Length > 0
                            });
                        }
                    }
                }
                if (meta.ExportedProperties != null)
                {
                    foreach (var prop in meta.ExportedProperties)
                    {
                        if (string.IsNullOrEmpty(prefix) || prop.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            suggestions.Add(new
                            {
                                text = prop.Name,
                                type = "property",
                                description = $"{GetSimpleTypeName(prop.PropertyType)} {prop.Name}",
                                insertText = prop.Name
                            });
                        }
                    }
                }
                if (meta.ExportedFields != null)
                {
                    foreach (var field in meta.ExportedFields)
                    {
                        if (string.IsNullOrEmpty(prefix) || field.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                        {
                            suggestions.Add(new
                            {
                                text = field.Name,
                                type = "field",
                                description = $"{GetSimpleTypeName(field.FieldType)} {field.Name}",
                                insertText = field.Name
                            });
                        }
                    }
                }
                return suggestions;
            }

            // 2. 尝试作为C#基础类型处理
            var type = GetDotExpressionType(classOrExpr);
            if (type != null)
            {
                // 只补全公共实例方法和属性
                var members = type.GetMembers(BindingFlags.Public | BindingFlags.Instance);
                foreach (var m in members)
                {
                    if (m.MemberType == MemberTypes.Method)
                    {
                        var mi = (MethodInfo)m;
                        if (mi.IsSpecialName) continue; // 排除get/set等
                        if (!string.IsNullOrEmpty(prefix) && !mi.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                        suggestions.Add(new
                        {
                            text = mi.Name,
                            type = "method",
                            description = mi.ToString(),
                            insertText = mi.GetParameters().Length > 0 ? $"{mi.Name}(" : $"{mi.Name}()"
                        });
                    }
                    else if (m.MemberType == MemberTypes.Property)
                    {
                        var pi = (PropertyInfo)m;
                        if (!string.IsNullOrEmpty(prefix) && !pi.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
                        suggestions.Add(new
                        {
                            text = pi.Name,
                            type = "property",
                            description = pi.PropertyType.Name + " " + pi.Name,
                            insertText = pi.Name
                        });
                    }
                }
            }
            return suggestions;
        }

        /// <summary>
        /// 推断点表达式的类型（如 MathUtils.Add(1,2) => int）
        /// </summary>
        private Type GetDotExpressionType(string expr)
        {
            // 只支持简单的静态方法调用：Class.Method(...)
            var methodCallMatch = System.Text.RegularExpressions.Regex.Match(expr, @"^(\w+)\.(\w+)\(.*\)$");
            if (methodCallMatch.Success)
            {
                var className = methodCallMatch.Groups[1].Value;
                var methodName = methodCallMatch.Groups[2].Value;
                if (ScriptRegistry.Instance.TryGetExportedType(className, out var meta) && meta != null)
                {
                    var method = meta.ExportedMethods?.FirstOrDefault(m => m.Name == methodName);
                    if (method != null)
                    {
                        return method.ReturnType;
                    }
                }
            }
            // 支持基础类型字面量
            if (int.TryParse(expr, out _)) return typeof(int);
            if (double.TryParse(expr, out _)) return typeof(double);
            if (expr.StartsWith("\"") && expr.EndsWith("\"")) return typeof(string);
            if (expr.StartsWith("'") && expr.EndsWith("'")) return typeof(string);
            return null;
        }

        /// <summary>
        /// 获取参数建议
        /// </summary>
        private List<object> GetParameterSuggestions(string className, string methodName, int parameterIndex)
        {
            var suggestions = new List<object>();

            if (ScriptRegistry.Instance.TryGetExportedType(className, out var meta) && meta != null)
            {
                var method = meta.ExportedMethods?.FirstOrDefault(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase));
                if (method != null)
                {
                    var parameters = method.GetParameters();
                    if (parameterIndex < parameters.Length)
                    {
                        var param = parameters[parameterIndex];
                        var paramType = GetSimpleTypeName(param.ParameterType);
                        
                        suggestions.Add(new
                        {
                            text = param.Name,
                            type = "parameter",
                            description = $"参数 {parameterIndex + 1}: {paramType} {param.Name}",
                            parameterType = paramType,
                            parameterName = param.Name,
                            hasDefaultValue = param.HasDefaultValue,
                            defaultValue = param.HasDefaultValue ? param.DefaultValue?.ToString() : null,
                            insertText = ""
                        });
                    }
                }
            }

            return suggestions;
        }

        /// <summary>
        /// 获取简化的类型名称
        /// </summary>
        private string GetSimpleTypeName(Type type)
        {
            if (type == typeof(string)) return "string";
            if (type == typeof(int)) return "int";
            if (type == typeof(bool)) return "bool";
            if (type == typeof(double)) return "double";
            if (type == typeof(float)) return "float";
            if (type == typeof(void)) return "void";
            if (type == typeof(object)) return "object";
            
            return type.Name;
        }
    }

    /// <summary>
    /// 补全请求模型
    /// </summary>
    public class CompletionRequest
    {
        public string Input { get; set; } = "";
        public int CursorPosition { get; set; }
    }

    /// <summary>
    /// 补全上下文
    /// </summary>
    public class CompletionContext
    {
        public CompletionContextType Type { get; set; }
        public string ClassName { get; set; } = "";
        public string MethodName { get; set; } = "";
        public string Prefix { get; set; } = "";
        public int ParameterIndex { get; set; }
    }

    /// <summary>
    /// 补全上下文类型
    /// </summary>
    public enum CompletionContextType
    {
        None,           // 无补全（如在字符串内）
        ClassName,      // 类名补全
        MemberAccess,   // 成员访问补全
        MethodParameter // 方法参数补全
    }
}