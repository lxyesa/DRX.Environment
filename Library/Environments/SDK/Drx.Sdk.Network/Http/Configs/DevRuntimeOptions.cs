using System;
using System.Collections.Generic;

namespace Drx.Sdk.Network.Http.Configs
{
    /// <summary>
    /// 开发态前端运行时热更新策略。
    /// </summary>
    public enum DevHotReloadStrategy
    {
        /// <summary>
        /// 默认整页刷新（稳定优先）。
        /// </summary>
        FullReload = 0,

        /// <summary>
        /// 仅 CSS 变更尝试局部替换，其余整页刷新。
        /// </summary>
        CssReplacePreferred = 1
    }

    /// <summary>
    /// 开发态前端运行时配置。
    /// </summary>
    public sealed class DevRuntimeOptions
    {
        /// <summary>
        /// 是否启用开发态前端运行时能力。默认 false（生产安全）。
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// 受监控的资源目录（相对或绝对路径均可，实际解析由上层完成）。
        /// </summary>
        public List<string> WatchDirectories { get; set; } = new();

        /// <summary>
        /// 资源变更事件去抖窗口（毫秒）。默认 200ms。
        /// </summary>
        public int DebounceMilliseconds { get; set; } = 200;

        /// <summary>
        /// Dev 事件流端点路径。默认 /__drx/dev-events。
        /// </summary>
        public string DevEventsEndpoint { get; set; } = "/__drx/dev-events";

        /// <summary>
        /// 前端运行时脚本端点路径。默认 /__drx/runtime.js。
        /// </summary>
        public string RuntimeScriptEndpoint { get; set; } = "/__drx/runtime.js";

        /// <summary>
        /// 组件清单端点路径。默认 /__drx/components-manifest。
        /// </summary>
        public string ComponentsManifestEndpoint { get; set; } = "/__drx/components-manifest";

        /// <summary>
        /// 热更新策略。默认整页刷新。
        /// </summary>
        public DevHotReloadStrategy HotReloadStrategy { get; set; } = DevHotReloadStrategy.FullReload;

        /// <summary>
        /// 是否记录更详细的开发态诊断日志。
        /// </summary>
        public bool EnableVerboseLogging { get; set; } = false;

        /// <summary>
        /// 校验配置并修正不安全/不合理值。
        /// </summary>
        public void Validate()
        {
            if (DebounceMilliseconds <= 0)
            {
                DebounceMilliseconds = 200;
            }

            DevEventsEndpoint = NormalizeEndpoint(DevEventsEndpoint, "/__drx/dev-events");
            RuntimeScriptEndpoint = NormalizeEndpoint(RuntimeScriptEndpoint, "/__drx/runtime.js");
            ComponentsManifestEndpoint = NormalizeEndpoint(ComponentsManifestEndpoint, "/__drx/components-manifest");

            if (WatchDirectories is null)
            {
                WatchDirectories = new List<string>();
            }
        }

        /// <summary>
        /// 创建默认配置实例。
        /// </summary>
        public static DevRuntimeOptions CreateDefault() => new();

        private static string NormalizeEndpoint(string? endpoint, string fallback)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return fallback;
            }

            endpoint = endpoint.Trim();
            return endpoint.StartsWith('/') ? endpoint : "/" + endpoint;
        }
    }
}
