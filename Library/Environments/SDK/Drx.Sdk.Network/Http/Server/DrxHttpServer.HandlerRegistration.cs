using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;
using System.Xml.Linq;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Network.Http.Entry;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpServer 处理器注册部分：反射注册路由与中间件、Linker 描述生成、委托创建
    /// </summary>
    public partial class DrxHttpServer
    {
        /// <summary>
        /// 基于提供的类型标记（marker type），注册带有 HttpHandle 特性的方法。
        /// </summary>
        public void RegisterHandlersFromAssembly([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods | DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicNestedTypes)] Type markerType, bool emitLinkerDescriptor = false, string? descriptorPath = null, bool emitPreserveSource = false, string? preserveSourcePath = null)
        {
            if (markerType == null) throw new ArgumentNullException(nameof(markerType));

            try
            {
                var targetType = markerType;

                var methods = targetType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance).ToList();

                int registeredHandlers = 0;
                int registeredMiddlewares = 0;

                foreach (var method in methods)
                {
                    var middlewareAttrs = method.GetCustomAttributes(typeof(HttpMiddlewareAttribute), false).Cast<HttpMiddlewareAttribute>();
                    foreach (var ma in middlewareAttrs)
                    {
                        var parameters = method.GetParameters();

                        if (parameters.Length == 1 && parameters[0].ParameterType == typeof(HttpListenerContext))
                        {
                            Func<HttpListenerContext, Task> mw;
                            if (typeof(Task).IsAssignableFrom(method.ReturnType))
                            {
                                if (method.IsStatic)
                                {
                                    mw = ctx => (Task)method.Invoke(null, new object[] { ctx })!;
                                }
                                else
                                {
                                    mw = ctx => (Task)method.Invoke(Activator.CreateInstance(targetType), new object[] { ctx })!;
                                }
                            }
                            else if (method.ReturnType == typeof(void))
                            {
                                if (method.IsStatic)
                                {
                                    mw = ctx => { method.Invoke(null, new object[] { ctx }); return Task.CompletedTask; };
                                }
                                else
                                {
                                    mw = ctx => { method.Invoke(Activator.CreateInstance(targetType), new object[] { ctx }); return Task.CompletedTask; };
                                }
                            }
                            else
                            {
                                Logger.Warn($"不能注册中间件: 方法 {method.Name} 返回类型不受支持: {method.ReturnType}");
                                continue;
                            }

                            AddMiddleware(mw, ma.Path, ma.Priority, ma.OverrideGlobal);
                            registeredMiddlewares++;
                            continue;
                        }

                        if (parameters.Length == 2 && parameters[0].ParameterType == typeof(HttpListenerContext) && parameters[1].ParameterType == typeof(DrxHttpServer))
                        {
                            Func<HttpListenerContext, Task> mw;
                            if (typeof(Task).IsAssignableFrom(method.ReturnType))
                            {
                                if (method.IsStatic)
                                {
                                    mw = ctx => (Task)method.Invoke(null, new object[] { ctx, this })!;
                                }
                                else
                                {
                                    mw = ctx => (Task)method.Invoke(Activator.CreateInstance(targetType), new object[] { ctx, this })!;
                                }
                            }
                            else if (method.ReturnType == typeof(void))
                            {
                                if (method.IsStatic)
                                {
                                    mw = ctx => { method.Invoke(null, new object[] { ctx, this }); return Task.CompletedTask; };
                                }
                                else
                                {
                                    mw = ctx => { method.Invoke(Activator.CreateInstance(targetType), new object[] { ctx, this }); return Task.CompletedTask; };
                                }
                            }
                            else
                            {
                                Logger.Warn($"不能注册中间件: 方法 {method.Name} 返回类型不受支持: {method.ReturnType}");
                                continue;
                            }

                            AddMiddleware(mw, ma.Path, ma.Priority, ma.OverrideGlobal);
                            registeredMiddlewares++;
                            continue;
                        }

                        if ((parameters.Length == 2 || parameters.Length == 3) && parameters[0].ParameterType == typeof(HttpRequest))
                        {
                            Func<HttpRequest, Func<HttpRequest, Task<HttpResponse?>>, Task<HttpResponse?>> requestMw = async (req, next) =>
                            {
                                try
                                {
                                    object? instance = null;
                                    if (!method.IsStatic)
                                    {
                                        instance = Activator.CreateInstance(targetType);
                                    }

                                    var nextParamType = parameters[1].ParameterType;
                                    object? secondArg = null;

                                    if (nextParamType == typeof(Func<HttpRequest, Task<HttpResponse?>>) || nextParamType == typeof(Func<HttpRequest, Task<HttpResponse>>))
                                    {
                                        secondArg = next;
                                    }
                                    else if (nextParamType == typeof(Func<HttpRequest, HttpResponse?>) || nextParamType == typeof(Func<HttpRequest, HttpResponse>))
                                    {
                                        Func<HttpRequest, HttpResponse?> syncNext = (r) =>
                                        {
                                            var t = next(r);
                                            t.Wait();
                                            return t.Result;
                                        };
                                        secondArg = syncNext;
                                    }
                                    else if (nextParamType.IsGenericType && nextParamType.GetGenericTypeDefinition() == typeof(Func<,>))
                                    {
                                        secondArg = next;
                                    }
                                    else
                                    {
                                        return null;
                                    }

                                    object? result;
                                    if (parameters.Length == 3 && parameters[2].ParameterType == typeof(DrxHttpServer))
                                    {
                                        var args = new object?[] { req, secondArg, this };
                                        result = method.Invoke(instance, args);
                                    }
                                    else
                                    {
                                        var args = new object?[] { req, secondArg };
                                        result = method.Invoke(instance, args);
                                    }

                                    if (result is Task task)
                                    {
                                        await task.ConfigureAwait(false);
                                        var prop = task.GetType().GetProperty("Result");
                                        if (prop != null)
                                        {
                                            return prop.GetValue(task) as HttpResponse;
                                        }
                                        return null;
                                    }

                                    return result as HttpResponse;
                                }
                                catch (TargetInvocationException tie)
                                {
                                    throw tie.InnerException ?? tie;
                                }
                            };

                            var entry = new MiddlewareEntry
                            {
                                RequestMiddleware = requestMw,
                                Path = ma.Path,
                                Priority = ma.Priority,
                                OverrideGlobal = ma.OverrideGlobal,
                                AddOrder = _middlewareCounter++
                            };

                            _middlewares.Add(entry);
                            registeredMiddlewares++;
                            continue;
                        }
                    }

                    var handleAttrs = method.GetCustomAttributes(typeof(HttpHandleAttribute), false).Cast<HttpHandleAttribute>();
                    foreach (var ha in handleAttrs)
                    {
                        var handlerDelegate = CreateHandlerDelegate(method);
                        if (handlerDelegate == null)
                        {
                            Logger.Warn($"无法为方法 {method.Name} 创建处理委托，跳过注册");
                            continue;
                        }

                        var httpMethod = ParseHttpMethod(ha.Method) ?? new HttpMethod("GET");

                        var rateLimitCallback = BindRateLimitCallback(method.DeclaringType ?? targetType, ha);

                        AddRoute(httpMethod, ha.Path, handlerDelegate, ha.RateLimitMaxRequests, ha.RateLimitWindowSeconds, rateLimitCallback);
                        registeredHandlers++;
                    }
                }

                Logger.Info($"从类型 {targetType.FullName} 注册了 {registeredHandlers} 个 HTTP 处理方法和 {registeredMiddlewares} 个中间件 (仅该类型)");

                if (emitLinkerDescriptor)
                {
                    try
                    {
                        GenerateLinkerDescriptorForType(targetType, descriptorPath);
                        Logger.Info($"已为类型 {targetType.FullName} 生成 linker 描述文件");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"为类型生成 linker 描述文件失败: {ex.Message}");
                    }
                }

                if (emitPreserveSource)
                {
                    try
                    {
                        GeneratePreserveSourceForType(targetType, preserveSourcePath);
                        Logger.Info($"已为类型 {targetType.FullName} 生成 Preserve 源文件");
                    }
                    catch (Exception ex)
                    {
                        Logger.Warn($"为类型生成 Preserve 源文件失败: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error($"注册 HTTP 处理方法时发生错误: {ex}");
            }
        }

        private static void GenerateLinkerDescriptorForType(Type type, string? descriptorPath)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var assemblyName = type.Assembly.GetName().Name ?? "UnknownAssembly";
            var root = new XElement("linker");
            var asmElem = new XElement("assembly", new XAttribute("fullname", assemblyName));
            asmElem.Add(new XElement("type", new XAttribute("fullname", type.FullName ?? type.Name), new XAttribute("preserve", "all")));
            root.Add(asmElem);
            var doc = new XDocument(new XComment(" Auto-generated by DrxHttpServer.RegisterHandlersFromAssembly - preserve single type "), root);

            string outPath;
            if (!string.IsNullOrEmpty(descriptorPath)) outPath = descriptorPath!;
            else outPath = Path.Combine(AppContext.BaseDirectory ?? Directory.GetCurrentDirectory(), $"linker.{assemblyName}.{type.Name}.xml");

            var dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None);
            var settings = new System.Xml.XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 };
            using var xw = System.Xml.XmlWriter.Create(fs, settings);
            doc.WriteTo(xw);
        }

        private static void GeneratePreserveSourceForType(Type type, string? outputPath)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));

            var assemblyName = type.Assembly.GetName().Name ?? "UnknownAssembly";
            var sb = new StringBuilder();
            sb.AppendLine("// Auto-generated by DrxHttpServer.GeneratePreserveSourceForType");
            sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
            sb.AppendLine();
            var safeNamespace = assemblyName.Replace('-', '_').Replace('.', '_');
            sb.AppendLine($"namespace {safeNamespace}");
            sb.AppendLine("{");
            sb.AppendLine($"    internal static class Preserve_{type.Name}_Generated");
            sb.AppendLine("    {");
            sb.AppendLine($"        [DynamicDependency(DynamicallyAccessedMemberTypes.All, \"{type.FullName}\", \"{assemblyName}\")] ");
            sb.AppendLine("        private static void Preserve() { }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            string outPath;
            if (!string.IsNullOrEmpty(outputPath)) outPath = outputPath!;
            else outPath = Path.Combine(AppContext.BaseDirectory ?? Directory.GetCurrentDirectory(), $"PreserveHandlers.{assemblyName}.{type.Name}.Generated.cs");

            var dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        }

        private static void GenerateLinkerDescriptorForAssembly(Assembly assembly, string? descriptorPath)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            var types = assembly.GetTypes()
                .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Any(m => m.GetCustomAttributes(typeof(HttpHandleAttribute), false).Length > 0 || m.GetCustomAttributes(typeof(HttpMiddlewareAttribute), false).Length > 0))
                .Select(t => t.FullName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .ToList();

            var assemblyName = assembly.GetName().Name ?? "UnknownAssembly";

            var root = new XElement("linker");
            var asmElem = new XElement("assembly", new XAttribute("fullname", assemblyName));

            foreach (var t in types)
            {
                asmElem.Add(new XElement("type", new XAttribute("fullname", t!), new XAttribute("preserve", "all")));
            }

            root.Add(asmElem);

            var doc = new XDocument(new XComment(" Auto-generated by DrxHttpServer.RegisterHandlersFromAssembly - contains types to preserve for ILLink trimming "), root);

            string outPath;
            if (!string.IsNullOrEmpty(descriptorPath))
            {
                outPath = descriptorPath!;
            }
            else
            {
                var fileName = $"linker.{assemblyName}.xml";
                outPath = Path.Combine(AppContext.BaseDirectory ?? Directory.GetCurrentDirectory(), fileName);
            }

            var dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            using (var fs = new FileStream(outPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var settings = new System.Xml.XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 };
                using (var xw = System.Xml.XmlWriter.Create(fs, settings))
                {
                    doc.WriteTo(xw);
                }
            }
        }

        private static void GeneratePreserveSourceForAssembly(Assembly assembly, string? outputPath)
        {
            if (assembly == null) throw new ArgumentNullException(nameof(assembly));

            var assemblyName = assembly.GetName().Name ?? "UnknownAssembly";

            var types = assembly.GetTypes()
                .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Any(m => m.GetCustomAttributes(typeof(HttpHandleAttribute), false).Length > 0 || m.GetCustomAttributes(typeof(HttpMiddlewareAttribute), false).Length > 0))
                .Select(t => t.FullName)
                .Where(n => !string.IsNullOrEmpty(n))
                .Distinct()
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("// Auto-generated by DrxHttpServer.GeneratePreserveSourceForAssembly");
            sb.AppendLine("// Purpose: provide DynamicDependency annotations so ILLink preserves handler types accessed by reflection");
            sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
            sb.AppendLine();

            var safeNamespace = assemblyName.Replace('-', '_').Replace('.', '_');
            sb.AppendLine($"namespace {safeNamespace}");
            sb.AppendLine("{");
            sb.AppendLine("    internal static class PreserveHandlersGenerated");
            sb.AppendLine("    {");

            if (types.Count == 0)
            {
            }
            else
            {
                foreach (var t in types)
                {
                    sb.AppendLine($"        [DynamicDependency(DynamicallyAccessedMemberTypes.All, \"{t}\", \"{assemblyName}\")] ");
                }
            }

            sb.AppendLine("        private static void Preserve() { /* for trimmer */ }");
            sb.AppendLine("    }");
            sb.AppendLine("}");

            string outPath;
            if (!string.IsNullOrEmpty(outputPath)) outPath = outputPath!;
            else outPath = Path.Combine(AppContext.BaseDirectory ?? Directory.GetCurrentDirectory(), $"PreserveHandlers.{assemblyName}.Generated.cs");

            var dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(outPath, sb.ToString(), Encoding.UTF8);
        }

        private Func<HttpRequest, Task<HttpResponse>> CreateHandlerDelegate(MethodInfo method)
        {
            try
            {
                var parameters = method.GetParameters();

                foreach (var p in parameters)
                {
                    if (p.ParameterType != typeof(HttpRequest) && p.ParameterType != typeof(DrxHttpServer) && p.ParameterType != typeof(HttpListenerContext))
                    {
                        Logger.Warn($"方法 {method.Name} 的参数类型不受支持: {p.ParameterType}");
                        return null;
                    }
                }

                var returnType = method.ReturnType;
                var returnsHttpResponse = returnType == typeof(HttpResponse);
                var returnsTaskHttpResponse = returnType == typeof(Task<HttpResponse>) || (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>) && returnType.GetGenericArguments()[0] == typeof(HttpResponse));
                var returnsActionResult = typeof(IActionResult).IsAssignableFrom(returnType);
                var returnsTaskActionResult = returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>) && typeof(IActionResult).IsAssignableFrom(returnType.GetGenericArguments()[0]);

                if (!returnsHttpResponse && !returnsTaskHttpResponse && !returnsActionResult && !returnsTaskActionResult)
                {
                    Logger.Warn($"方法 {method.Name} 的返回类型不受支持，应为 HttpResponse/Task<HttpResponse>/IActionResult/Task<IActionResult>}}");
                    return null;
                }

                return async (HttpRequest request) =>
                {
                    try
                    {
                        var args = new List<object?>();
                        foreach (var p in parameters)
                        {
                            if (p.ParameterType == typeof(HttpRequest)) args.Add(request);
                            else if (p.ParameterType == typeof(DrxHttpServer)) args.Add(this);
                            else if (p.ParameterType == typeof(HttpListenerContext)) args.Add(request.ListenerContext);
                            else args.Add(null);
                        }

                        var result = method.Invoke(null, args.ToArray());

                        if (result is Task task)
                        {
                            await task.ConfigureAwait(false);

                            if (returnsTaskHttpResponse)
                            {
                                var prop = task.GetType().GetProperty("Result");
                                var resp = (HttpResponse)prop!.GetValue(task)!;
                                return resp ?? new HttpResponse(500, "Internal Server Error");
                            }

                            if (returnsTaskActionResult)
                            {
                                var prop = task.GetType().GetProperty("Result");
                                var action = (IActionResult)prop!.GetValue(task)!;
                                if (action == null) return new HttpResponse(500, "Internal Server Error");
                                return await action.ExecuteAsync(request, this).ConfigureAwait(false);
                            }

                            return new HttpResponse(204, "");
                        }

                        if (returnsHttpResponse)
                        {
                            return (HttpResponse)result!;
                        }

                        if (returnsActionResult)
                        {
                            var action = (IActionResult)result!;
                            return await action.ExecuteAsync(request, this).ConfigureAwait(false);
                        }

                        return new HttpResponse(500, "Internal Server Error");
                    }
                    catch (TargetInvocationException tie)
                    {
                        Logger.Error($"执行 HTTP处理方法 {method.Name} 时发生错误: {tie.InnerException?.Message ?? tie.Message}\n{tie.InnerException?.StackTrace ?? tie.StackTrace}");
                        return new HttpResponse(500, $"Internal Server Error: {tie.InnerException?.Message ?? tie.Message}");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"执行 HTTP处理方法 {method.Name} 时发生错误: {ex}");
                        return new HttpResponse(500, $"Internal Server Error: {ex.Message}");
                    }
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"创建处理委托时发生错误: {ex}");
                return null;
            }
        }

        /// <summary>
        /// 绑定路由级速率限制回调
        /// </summary>
        private static Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>? BindRateLimitCallback(Type declaringType, HttpHandleAttribute attr)
        {
            try
            {
                var callbackMethodName = attr.RateLimitCallbackMethodName;
                if (string.IsNullOrEmpty(callbackMethodName))
                    return null;

                var targetType = attr.RateLimitCallbackType ?? declaringType;
                var method = targetType.GetMethod(callbackMethodName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                if (method == null)
                {
                    Logger.Warn($"未找到速率限制回调方法: {targetType.FullName}.{callbackMethodName}");
                    return null;
                }

                var parameters = method.GetParameters();
                if (parameters.Length != 3
                    || parameters[0].ParameterType != typeof(int)
                    || parameters[1].ParameterType != typeof(HttpRequest)
                    || parameters[2].ParameterType != typeof(OverrideContext))
                {
                    Logger.Warn($"速率限制回调方法 {targetType.FullName}.{callbackMethodName} 的签名不匹配，应为 (int, HttpRequest, OverrideContext)");
                    return null;
                }

                var returnType = method.ReturnType;
                var returnsHttpResponse = returnType == typeof(HttpResponse);
                var returnsTaskHttpResponse = returnType == typeof(Task<HttpResponse>) || returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>) && Nullable.GetUnderlyingType(returnType.GetGenericArguments()[0]) == typeof(HttpResponse);

                if (!returnsHttpResponse && !returnsTaskHttpResponse)
                {
                    Logger.Warn($"速率限制回调方法 {targetType.FullName}.{callbackMethodName} 的返回类型不受支持，应为 HttpResponse、HttpResponse?、Task<HttpResponse> 或 Task<HttpResponse?>");
                    return null;
                }

                if (returnsTaskHttpResponse)
                {
                    var compiledDelegate = (Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>)Delegate.CreateDelegate(
                        typeof(Func<int, HttpRequest, OverrideContext, Task<HttpResponse?>>), method, throwOnBindFailure: false);

                    if (compiledDelegate != null)
                    {
                        return async (count, req, ctx) =>
                        {
                            try
                            {
                                return await compiledDelegate(count, req, ctx).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"执行速率限制回调 {targetType.FullName}.{callbackMethodName} 时发生错误: {ex.Message}");
                                return null;
                            }
                        };
                    }
                }
                else if (returnsHttpResponse)
                {
                    var compiledDelegate = (Func<int, HttpRequest, OverrideContext, HttpResponse?>)Delegate.CreateDelegate(
                        typeof(Func<int, HttpRequest, OverrideContext, HttpResponse?>), method, throwOnBindFailure: false);

                    if (compiledDelegate != null)
                    {
                        return (count, req, ctx) =>
                        {
                            try
                            {
                                var result = compiledDelegate(count, req, ctx);
                                return Task.FromResult(result);
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"执行速率限制回调 {targetType.FullName}.{callbackMethodName} 时发生错误: {ex.Message}");
                                return Task.FromResult<HttpResponse?>(null);
                            }
                        };
                    }
                }

                Logger.Warn($"无法为 {targetType.FullName}.{callbackMethodName} 创建编译委托，使用反射调用");
                return async (count, req, ctx) =>
                {
                    try
                    {
                        var result = method.Invoke(null, new object[] { count, req, ctx });
                        if (result is Task<HttpResponse?> taskResp)
                            return await taskResp.ConfigureAwait(false);
                        else if (result is Task<HttpResponse> taskResp2)
                            return await taskResp2.ConfigureAwait(false);
                        else
                            return (HttpResponse?)result;
                    }
                    catch (TargetInvocationException tie)
                    {
                        Logger.Error($"执行速率限制回调 {targetType.FullName}.{callbackMethodName} 时发生错误: {tie.InnerException?.Message ?? tie.Message}");
                        return null;
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"执行速率限制回调 {targetType.FullName}.{callbackMethodName} 时发生错误: {ex.Message}");
                        return null;
                    }
                };
            }
            catch (Exception ex)
            {
                Logger.Error($"绑定速率限制回调时发生错误: {ex}");
                return null;
            }
        }
    }
}
