using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
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
        /// 获取 Auth App 数据库（用于底层扩展或调试）
        /// </summary>
        public DataBase.SqliteV2<Models.AuthAppDataModel> AuthAppDatabase => _authAppDatabase;

        /// <summary>
        /// 快速注册（或更新）Auth App。
        /// </summary>
        /// <param name="clientId">客户端标识</param>
        /// <param name="redirectUri">回调地址</param>
        /// <param name="applicationName">应用显示名</param>
        /// <param name="applicationDescription">应用描述</param>
        /// <param name="scopes">默认权限范围</param>
        /// <param name="clientSecret">客户端密钥（可空，空表示公开客户端）</param>
        /// <param name="enabled">是否启用</param>
        /// <returns>注册后的 Auth App 模型</returns>
        public Models.AuthAppDataModel RegisterAuthApp(
            string clientId,
            string redirectUri,
            string applicationName,
            string applicationDescription = "",
            string scopes = "",
            string? clientSecret = null,
            bool enabled = true)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentException("clientId 不能为空", nameof(clientId));

            if (string.IsNullOrWhiteSpace(redirectUri))
                throw new ArgumentException("redirectUri 不能为空", nameof(redirectUri));

            if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var redirectUriObj)
                || (redirectUriObj.Scheme != Uri.UriSchemeHttp && redirectUriObj.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("redirectUri 必须是有效的 http/https 地址", nameof(redirectUri));
            }

            var normalizedClientId = clientId.Trim();
            var normalizedRedirect = redirectUriObj.ToString();
            var normalizedName = string.IsNullOrWhiteSpace(applicationName) ? normalizedClientId : applicationName.Trim();
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var secretHash = ComputeSha256Hex(clientSecret);

            var existing = _authAppDatabase.SelectWhere("ClientId", normalizedClientId).FirstOrDefault();
            if (existing == null)
            {
                var model = new Models.AuthAppDataModel
                {
                    ClientId = normalizedClientId,
                    ClientSecretHash = secretHash,
                    ApplicationName = normalizedName,
                    ApplicationDescription = applicationDescription?.Trim() ?? string.Empty,
                    RedirectUri = normalizedRedirect,
                    Scopes = scopes?.Trim() ?? string.Empty,
                    IsEnabled = enabled,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _authAppDatabase.Insert(model);
                Logger.Info($"[AuthApp] 注册成功: {normalizedClientId} -> {normalizedRedirect}");
                return model;
            }

            existing.ApplicationName = normalizedName;
            existing.ApplicationDescription = applicationDescription?.Trim() ?? string.Empty;
            existing.RedirectUri = normalizedRedirect;
            existing.Scopes = scopes?.Trim() ?? string.Empty;
            existing.IsEnabled = enabled;
            existing.UpdatedAt = now;
            if (!string.IsNullOrWhiteSpace(clientSecret))
            {
                existing.ClientSecretHash = secretHash;
            }

            _authAppDatabase.Update(existing);
            Logger.Info($"[AuthApp] 更新成功: {normalizedClientId} -> {normalizedRedirect}");
            return existing;
        }

        /// <summary>
        /// 根据 clientId 获取 Auth App。
        /// </summary>
        public Models.AuthAppDataModel? GetAuthApp(string clientId)
        {
            if (string.IsNullOrWhiteSpace(clientId)) return null;
            return _authAppDatabase.SelectWhere("ClientId", clientId.Trim()).FirstOrDefault();
        }

        /// <summary>
        /// 校验 Auth App 是否允许参与 OpenAuth。
        /// </summary>
        /// <param name="clientId">客户端标识</param>
        /// <param name="redirectUri">回调地址</param>
        /// <param name="clientSecret">客户端密钥（可空）</param>
        /// <param name="app">命中的 Auth App</param>
        /// <param name="error">失败原因</param>
        /// <returns>是否通过</returns>
        public bool ValidateAuthApp(string clientId, string redirectUri, string? clientSecret, out Models.AuthAppDataModel? app, out string? error)
        {
            app = null;
            error = null;

            if (string.IsNullOrWhiteSpace(clientId))
            {
                error = "client_id 不能为空";
                return false;
            }

            if (string.IsNullOrWhiteSpace(redirectUri))
            {
                error = "redirect_uri 不能为空";
                return false;
            }

            if (!Uri.TryCreate(redirectUri, UriKind.Absolute, out var redirectUriObj)
                || (redirectUriObj.Scheme != Uri.UriSchemeHttp && redirectUriObj.Scheme != Uri.UriSchemeHttps))
            {
                error = "redirect_uri 非法";
                return false;
            }

            app = GetAuthApp(clientId);
            if (app == null)
            {
                error = "Auth App 未注册";
                return false;
            }

            if (!app.IsEnabled)
            {
                error = "Auth App 已被禁用";
                return false;
            }

            var normalizedRedirect = redirectUriObj.ToString();
            if (!string.Equals(app.RedirectUri?.Trim(), normalizedRedirect, StringComparison.OrdinalIgnoreCase))
            {
                error = "redirect_uri 不匹配";
                return false;
            }

            if (!string.IsNullOrWhiteSpace(app.ClientSecretHash))
            {
                if (string.IsNullOrWhiteSpace(clientSecret))
                {
                    error = "client_secret 不能为空";
                    return false;
                }

                var inputHash = ComputeSha256Hex(clientSecret);
                if (!string.Equals(app.ClientSecretHash, inputHash, StringComparison.OrdinalIgnoreCase))
                {
                    error = "client_secret 错误";
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 快速判断是否已注册并通过校验（不返回错误详情）。
        /// </summary>
        public bool IsAuthAppRegistered(string clientId, string redirectUri, string? clientSecret = null)
        {
            return ValidateAuthApp(clientId, redirectUri, clientSecret, out _, out _);
        }

        private static string ComputeSha256Hex(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(raw.Trim());
            var hash = sha.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

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
