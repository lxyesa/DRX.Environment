using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Drx.Sdk.Shared;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Network.Http.Entry;
using Drx.Sdk.Network.Http.Session;
using Drx.Sdk.Network.Http.Authorization;
using Drx.Sdk.Network.Http.Commands;

namespace Drx.Sdk.Network.Http
{
    /// <summary>
    /// DrxHttpServer 会话/授权/命令/数据持久化部分
    /// </summary>
    public partial class DrxHttpServer
    {
        /// <summary>
        /// 添加标准HTTP/S会话中间件。
        /// </summary>
        public void AddSessionMiddleware(string cookieName = "SessionId", CookieOptions? cookieOptions = null)
        {
            cookieOptions ??= new CookieOptions
            {
                HttpOnly = true,
                Secure = false,
                SameSite = "Lax",
                Path = "/",
                MaxAge = TimeSpan.FromMinutes(30)
            };

            AddMiddleware(async ctx =>
            {
                try
                {
                    var sessionId = ctx.Request.Cookies[cookieName]?.Value;
                    if (!string.IsNullOrEmpty(sessionId))
                    {
                        var session = _sessionManager.GetSession(sessionId);
                        if (session == null)
                        {
                            var expiredCookie = new Cookie(cookieName, string.Empty)
                            {
                                HttpOnly = cookieOptions.HttpOnly,
                                Secure = cookieOptions.Secure,
                                Path = cookieOptions.Path ?? "/",
                                Expires = DateTime.UtcNow.AddDays(-1)
                            };
                            ctx.Response.Cookies.Add(expiredCookie);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logger.Warn($"执行会话中间件时发生错误: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// 获取会话管理器
        /// </summary>
        public SessionManager SessionManager => _sessionManager;

        /// <summary>
        /// 获取授权管理器
        /// </summary>
        public AuthorizationManager AuthorizationManager => _authorizationManager;

        /// <summary>
        /// 从指定类型注册命令
        /// </summary>
        public void RegisterCommandsFromType(Type handlerType)
        {
            if (handlerType == null) throw new ArgumentNullException(nameof(handlerType));
            _commandManager.RegisterCommandsFromType(handlerType);
        }

        /// <summary>
        /// 执行命令输入字符串，返回执行结果。
        /// </summary>
        public string ExecuteCommand(string input)
        {
            return _commandManager.ExecuteCommand(input);
        }

        /// <summary>
        /// 获取所有已注册命令的帮助信息
        /// </summary>
        public string GetCommandsHelp()
        {
            return _commandManager.GetHelpText();
        }

        /// <summary>
        /// 异步提交命令到独立的命令处理线程。
        /// </summary>
        public async Task<bool> SubmitCommandAsync(string input, Func<string, Task>? onCompleted = null)
        {
            if (string.IsNullOrWhiteSpace(input))
                return false;

            try
            {
                var entry = new CommandQueueEntry
                {
                    CommandInput = input,
                    OnCompleted = onCompleted
                };

                await _commandInputChannel.Writer.WriteAsync(entry).ConfigureAwait(false);
                return true;
            }
            catch (ChannelClosedException)
            {
                Logger.Warn("命令处理队列已关闭，无法提交命令");
                return false;
            }
            catch (Exception ex)
            {
                Logger.Error($"提交命令到处理队列失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 异步提交命令，并等待其执行完成（带超时）。
        /// </summary>
        public async Task<string> SubmitCommandAndWaitAsync(string input, int timeoutMs = 0)
        {
            if (string.IsNullOrWhiteSpace(input))
                return "错误：命令不能为空";

            var taskCompletionSource = new TaskCompletionSource<string>();

            Func<string, Task> onCompleted = async (result) =>
            {
                taskCompletionSource.SetResult(result);
                await Task.CompletedTask.ConfigureAwait(false);
            };

            var submitted = await SubmitCommandAsync(input, onCompleted).ConfigureAwait(false);
            if (!submitted)
                return "错误：无法提交命令到处理队列";

            try
            {
                if (timeoutMs > 0)
                {
                    using var cts = new CancellationTokenSource(timeoutMs);
                    cts.Token.Register(() => taskCompletionSource.TrySetCanceled());
                    return await taskCompletionSource.Task.ConfigureAwait(false);
                }
                else
                {
                    return await taskCompletionSource.Task.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                return "错误：命令执行超时";
            }
            catch (Exception ex)
            {
                return $"错误：等待命令执行时发生异常: {ex.Message}";
            }
        }

        /// <summary>
        /// 命令处理线程入口，独立于 HTTP 请求处理线程。
        /// </summary>
        private async Task ProcessCommandsAsync(CancellationToken token)
        {
            try
            {
                await foreach (var entry in _commandInputChannel.Reader.ReadAllAsync(token))
                {
                    try
                    {
                        var result = ExecuteCommand(entry.CommandInput);

                        if (entry.OnCompleted != null)
                        {
                            try
                            {
                                await entry.OnCompleted(result).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"执行命令完成回调时发生错误: {ex.Message}");
                            }
                        }

                        if (OnCommandCompleted != null)
                        {
                            try
                            {
                                await OnCommandCompleted(entry.CommandInput, result).ConfigureAwait(false);
                            }
                            catch (Exception ex)
                            {
                                Logger.Error($"执行全局命令完成回调时发生错误: {ex.Message}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error($"处理命令输入时发生异常: {entry.CommandInput}, 错误: {ex.Message}");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.Info("命令处理线程已关闭");
            }
            catch (Exception ex)
            {
                Logger.Error($"命令处理线程发生异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 检查指定 id 的持久化分组是否存在对应的数据库文件。
        /// </summary>
        public bool ExistsDataPersistent<T>(string id, string? basePath = null) where T : Models.DataModelBase, new()
        {
            try
            {
                var path = string.IsNullOrEmpty(FileRootPath) ? basePath : FileRootPath;
                return _dataPersistentManager.Exists<T>(id, path);
            }
            catch (Exception ex)
            {
                Logger.Error($"ExistsDataPersistent 失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 从指定 id 的持久化分组加载实体到内存。
        /// </summary>
        public void LoadDataPersistent<T>(string id, string? basePath = null) where T : Models.DataModelBase, new()
        {
            try
            {
                var path = string.IsNullOrEmpty(FileRootPath) ? basePath : FileRootPath;
                _dataPersistentManager.Load<T>(id, path);
            }
            catch (Exception ex)
            {
                Logger.Error($"LoadDataPersistent 失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 向服务器添加一个持久化分组中的实体。
        /// </summary>
        public void AddDataPersistent<T>(T entity, string id) where T : Models.DataModelBase
        {
            try
            {
                _dataPersistentManager.Add(entity, id);
            }
            catch (Exception ex)
            {
                Logger.Error($"AddDataPersistent 失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 获取指定 id 的持久化分组（浅拷贝）。若不存在返回空列表。
        /// </summary>
        public List<T> GetDataPersistent<T>(string id) where T : Models.DataModelBase
        {
            try
            {
                return _dataPersistentManager.Get<T>(id);
            }
            catch (Exception ex)
            {
                Logger.Error($"GetDataPersistent 失败: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// 将指定 id 的分组持久化为 {id}.db（同步阻塞）。
        /// </summary>
        public void UpdateOrCreateDataPersistent<T>(string id, string? basePath = null) where T : Models.DataModelBase, new()
        {
            try
            {
                var path = string.IsNullOrEmpty(FileRootPath) ? basePath : FileRootPath;
                _dataPersistentManager.Save<T>(id, path);
            }
            catch (Exception ex)
            {
                Logger.Error($"UpdateOrCreateDataPersistent 失败: {ex.Message}");
                throw;
            }
        }
    }
}
