using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Auth;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Results;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Network.Http.Sse;
using Drx.Sdk.Shared;
using KaxSocket;

namespace KaxSocket.Handlers;

/// <summary>
/// 远程控制台模块 - 提供 Web 端命令执行、命令列表查询及 SSE 实时日志推送接口
/// 仅限 Console/Root/Admin 权限组的用户访问
/// </summary>
public partial class KaxHttp
{
    #region 日志广播

    private static bool _logHookInstalled = false;
    private static readonly object _hookLock = new();
    private static readonly ConcurrentDictionary<string, Channel<LogEntry>> _logSubscribers = new();

    /// 全局日志环形缓冲区，即使没有 SSE 客户端也持续记录，客户端重连时可回放历史
    private static readonly ConcurrentQueue<LogEntry> _logRingBuffer = new();
    private const int LogRingBufferSize = 500;
    /// 每次客户端连接时，历史日志最多回放条数，避免首次加载渲染大量 DOM 造成性能问题
    private const int LogHistoryReplayLimit = 100;

    private class LogEntry
    {
        public string Message { get; set; } = "";
        public string Level { get; set; } = "INFO";
        public string Time { get; set; } = "";
    }

    /// <summary>
    /// 安装全局日志钩子，将每条日志分发到所有已订阅的 Channel，同时写入环形缓冲区
    /// </summary>
    private static void EnsureLogHookInstalled()
    {
        if (_logHookInstalled) return;
        lock (_hookLock)
        {
            if (_logHookInstalled) return;
            Logger.OnLogEntry = (formatted, level, time) =>
            {
                if (formatted.Contains("/api/console/logs"))
                    return;

                var entry = new LogEntry
                {
                    Message = formatted,
                    Level = level.ToString().ToUpper(),
                    Time = time.ToString("yyyy/MM/dd HH:mm:ss")
                };

                // 写入环形缓冲区（无论是否有订阅者）
                _logRingBuffer.Enqueue(entry);
                while (_logRingBuffer.Count > LogRingBufferSize)
                    _logRingBuffer.TryDequeue(out _);

                // 分发到实时订阅者
                foreach (var kvp in _logSubscribers)
                {
                    kvp.Value.Writer.TryWrite(entry);
                }
            };
            _logHookInstalled = true;
        }
    }

    /// <summary>
    /// 创建日志订阅 Channel（供 SSE 处理方法使用）
    /// </summary>
    private static (string Id, Channel<LogEntry> Channel) SubscribeLogChannel()
    {
        var id = Guid.NewGuid().ToString("N");
        var ch = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(256) { FullMode = BoundedChannelFullMode.DropOldest });
        _logSubscribers.TryAdd(id, ch);
        return (id, ch);
    }

    private static void UnsubscribeLogChannel(string id)
    {
        _logSubscribers.TryRemove(id, out _);
    }

    /// <summary>
    /// SSE 日志流端点，使用 [HttpSse] 特性自动注册。
    /// 客户端通过 EventSource 连接，实时接收服务器日志推送。
    /// 认证令牌通过 ?token=xxx 查询参数传递。
    /// </summary>
    [HttpSse("/api/console/logs/stream", HeartbeatSeconds = 15)]
    public static async Task StreamConsoleLogs(ISseWriter sse, HttpRequest request, CancellationToken ct)
    {
        EnsureLogHookInstalled();

        var token = request.Query["token"];
        if (string.IsNullOrWhiteSpace(token))
        {
            await sse.RejectAsync(401, "未提供登录令牌");
            return;
        }

        var principal = JwtHelper.ValidateToken(token);
        var userName = principal?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            await sse.RejectAsync(401, "无效的登录令牌");
            return;
        }

        if (await KaxGlobal.IsUserBanned(userName))
        {
            await sse.RejectAsync(403, "账号已被封禁");
            return;
        }

        var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null)
        {
            await sse.RejectAsync(401, "用户不存在");
            return;
        }

        var group = user.PermissionGroup;
        if (group != UserPermissionGroup.Console && group != UserPermissionGroup.Root && group != UserPermissionGroup.Admin)
        {
            await sse.RejectAsync(403, "权限不足，仅管理员可使用控制台");
            return;
        }

        Logger.Info($"[Console] SSE 日志流已建立: {userName} ({sse.ClientId})");
        await sse.SendAsync("connected", $"{{\"user\":\"{userName}\"}}");

        // 回放环形缓冲区中的历史日志，最多取最近 LogHistoryReplayLimit 条，避免大量渲染造成性能问题
        var allHistory = _logRingBuffer.ToArray();
        var history = allHistory.Length > LogHistoryReplayLimit
            ? allHistory[^LogHistoryReplayLimit..]
            : allHistory;
        if (history.Length > 0)
        {
            foreach (var h in history)
            {
                var hJson = JsonSerializer.Serialize(new
                {
                    message = h.Message,
                    level = h.Level,
                    time = h.Time
                });
                await sse.SendAsync("log", hJson);
            }
            await sse.SendAsync("history-end", $"{{\"count\":{history.Length},\"total\":{allHistory.Length}}}");
        }

        var (subId, channel) = SubscribeLogChannel();
        try
        {
            await foreach (var entry in channel.Reader.ReadAllAsync(ct))
            {
                var json = JsonSerializer.Serialize(new
                {
                    message = entry.Message,
                    level = entry.Level,
                    time = entry.Time
                });
                await sse.SendAsync("log", json);
            }
        }
        finally
        {
            UnsubscribeLogChannel(subId);
            Logger.Info($"[Console] SSE 日志流已断开: {userName} ({sse.ClientId})");
        }
    }

    #endregion

    #region 控制台 (Console)

    /// <summary>
    /// 验证请求用户是否具有控制台访问权限
    /// </summary>
    private static async Task<(bool Allowed, string? UserName, string? ErrorMessage, int StatusCode)> VerifyConsoleAccess(HttpRequest request)
    {
        var userToken = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        if (string.IsNullOrWhiteSpace(userToken))
            return (false, null, "未提供登录令牌", 401);

        var principal = JwtHelper.ValidateToken(userToken);
        var userName = principal?.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
            return (false, null, "无效的登录令牌", 401);

        if (await KaxGlobal.IsUserBanned(userName))
            return (false, userName, "您的账号已被封禁", 403);

        var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null)
            return (false, userName, "用户不存在", 401);

        var group = user.PermissionGroup;
        if (group != UserPermissionGroup.Console && group != UserPermissionGroup.Root && group != UserPermissionGroup.Admin)
            return (false, userName, "权限不足，仅管理员可使用控制台", 403);

        return (true, userName, null, 200);
    }

    /// <summary>
    /// 执行远程命令。接收 JSON 请求体 { "command": "..." }，
    /// 通过 DrxHttpServer.SubmitCommandAndWaitAsync 执行并返回结果。
    /// </summary>
    [HttpHandle("/api/console/execute", "POST", RateLimitMaxRequests = 30, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> PostConsoleExecute(HttpRequest request, DrxHttpServer server)
    {
        var verify = await VerifyConsoleAccess(request);
        if (!verify.Allowed)
            return new JsonResult(new { success = false, error = verify.ErrorMessage }, verify.StatusCode);

        if (string.IsNullOrWhiteSpace(request.Body))
            return new JsonResult(new { success = false, error = "请求体不能为空" }, 400);

        try
        {
            var body = JsonNode.Parse(request.Body);
            var command = body?["command"]?.GetValue<string>();

            if (string.IsNullOrWhiteSpace(command))
                return new JsonResult(new { success = false, error = "命令不能为空" }, 400);

            Logger.Info($"[Console] 用户 {verify.UserName} 执行命令: {command}");

            // 捕获 Console.WriteLine 的输出
            var originalOut = Console.Out;
            var stringWriter = new StringWriter();
            Console.SetOut(stringWriter);

            string result;
            try
            {
                result = await server.SubmitCommandAndWaitAsync(command, 30000);
            }
            finally
            {
                Console.SetOut(originalOut);
            }

            var capturedOutput = stringWriter.ToString();
            if (!string.IsNullOrWhiteSpace(capturedOutput))
            {
                result = capturedOutput.TrimEnd() + (string.IsNullOrWhiteSpace(result) ? "" : "\n" + result);
            }

            Logger.Info($"[Console] 命令执行完成: {command} -> {(result?.Length > 80 ? result.Substring(0, 80) + "..." : result)}");

            return new JsonResult(new { success = true, result = result });
        }
        catch (Exception ex)
        {
            Logger.Error($"[Console] 执行命令时发生异常: {ex.Message}");
            return new JsonResult(new { success = false, error = $"命令执行异常: {ex.Message}" }, 500);
        }
    }

    /// <summary>
    /// 获取所有已注册的命令列表，返回 JSON 数组
    /// </summary>
    [HttpHandle("/api/console/commands", "GET")]
    public static async Task<IActionResult> GetConsoleCommands(HttpRequest request, DrxHttpServer server)
    {
        var verify = await VerifyConsoleAccess(request);
        if (!verify.Allowed)
            return new JsonResult(new { error = verify.ErrorMessage }, verify.StatusCode);

        try
        {
            var helpText = server.GetCommandsHelp();
            var lines = helpText.Split('\n');
            var commandList = new System.Collections.Generic.List<object>();
            var currentCategory = "";

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line) || line == "可用命令：") continue;

                if (line.StartsWith("【") && line.Contains("】"))
                {
                    currentCategory = line.Trim('【', '】');
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(currentCategory) && line.Length > 2)
                {
                    var parts = Regex.Split(line.TrimStart(), @"\s{2,}");
                    if (parts.Length >= 1)
                    {
                        var format = parts[0].Trim();
                        var description = parts.Length >= 2 ? parts[1].Trim() : "";
                        var name = format.Split(' ')[0];

                        commandList.Add(new
                        {
                            name = name,
                            format = format,
                            category = currentCategory,
                            description = description
                        });
                    }
                }
            }

            return new JsonResult(commandList);
        }
        catch (Exception ex)
        {
            Logger.Error($"[Console] 获取命令列表时发生异常: {ex.Message}");
            return new JsonResult(new { error = "获取命令列表失败" }, 500);
        }
    }

    #endregion
}
