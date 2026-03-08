{
  "updated": "2026-03-08",
  "entries": [
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Engine/ClearScriptRuntime.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Engine",
      "exports": [
        {
          "kind": "class",
          "name": "ClearScriptRuntime",
          "summary": "ClearScript V8 runtime implementation of IScriptEngineRuntime; sole ClearScript reference point."
        }
      ]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Engine/NullScriptRuntime.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Engine",
      "exports": [
        {
          "kind": "class",
          "name": "NullScriptRuntime",
          "summary": "Null/fallback IScriptEngineRuntime; throws InvalidOperationException on any script execution call."
        }
      ]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Exceptions/JavaScriptException.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Exceptions",
      "exports": [
        {
          "kind": "class",
          "name": "JavaScriptException",
          "summary": "JavaScript runtime exception wrapping error type, location, script stack, and execution context."
        }
      ]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Exceptions/ScriptExecutionContext.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Exceptions",
      "exports": [
        {
          "kind": "class",
          "name": "ScriptExecutionContext",
          "summary": "Captures script execution snapshot: script content, file path, start time, duration, retry count, caller."
        }
      ]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Network/Http/Server/DrxHttpServer.StaticContent.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Network.Http",
      "exports": [
        {
          "kind": "class",
          "name": "StaticFileCacheEntry",
          "summary": "Serializable static file cache entry: ETag, LastModifiedUtc, FileSize, optional cached bytes, content-type."
        }
      ]
    },
    {
      "file": "Library/Applications/Servers/KaxSocket/Cache/AvatarCacheManager.cs",
      "language": "csharp",
      "namespace": "KaxSocket.Cache",
      "exports": [
        {
          "kind": "class",
          "name": "AvatarEntry",
          "summary": "Serializable avatar data record holding image bytes and content-type for FusionCache/Redis."
        },
        {
          "kind": "class",
          "name": "AvatarCacheManager",
          "summary": "FusionCache-backed avatar cache with optional Redis support; replaces LRU Dictionary+lock."
        },
        {
          "kind": "method",
          "name": "TryGetAvatar",
          "summary": "Reads avatar bytes and content-type from cache; returns false on miss or expiry."
        },
        {
          "kind": "method",
          "name": "SetAvatar",
          "summary": "Inserts or refreshes avatar entry; skips when imageData is null or empty."
        },
        {
          "kind": "method",
          "name": "InvalidateAvatar",
          "summary": "Removes a single user avatar entry from the cache."
        },
        {
          "kind": "method",
          "name": "Clear",
          "summary": "Expires all avatar cache entries."
        },
        {
          "kind": "method",
          "name": "GetStats",
          "summary": "Returns hit/miss counters and max-size configuration."
        }
      ]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Network/Http/Cache/DrxCacheOptions.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Network.Http.Cache",
      "exports": [
        {
          "kind": "class",
          "name": "DrxCacheOptions",
          "summary": "Centralized cache limits, TTL, and optional Redis toggles."
        }
      ]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Network/Http/Cache/DrxCacheProvider.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Network.Http.Cache",
      "exports": [
        {
          "kind": "class",
          "name": "DrxCacheProvider",
          "summary": "Creates and owns named FusionCache instances with optional Redis integration."
        },
        {
          "kind": "property",
          "name": "StaticContent",
          "summary": "FusionCache instance for static content metadata and small file entries."
        },
        {
          "kind": "property",
          "name": "RouteMatch",
          "summary": "FusionCache instance for route match results and miss caching."
        },
        {
          "kind": "property",
          "name": "MiddlewarePath",
          "summary": "FusionCache instance for path-to-middleware-list resolution cache."
        },
        {
          "kind": "property",
          "name": "TokenBucket",
          "summary": "FusionCache instance for token bucket lifecycle management."
        },
        {
          "kind": "property",
          "name": "RateLimitKey",
          "summary": "FusionCache instance for rate-limit key string caching."
        }
      ]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Network/Http/Performance/DrxHttpServerOptions.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Network.Http.Performance",
      "exports": [
        {
          "kind": "class",
          "name": "DrxHttpServerOptions",
          "summary": "Server performance options including cache and adaptive concurrency settings."
        },
        {
          "kind": "property",
          "name": "CacheOptions",
          "summary": "Nested cache options used by cache provider instances."
        },
        {
          "kind": "property",
          "name": "RouteCacheMaxSize",
          "summary": "Legacy route cache size delegated to CacheOptions and marked obsolete."
        }
      ]
    },
    {
      "file": "Library/Applications/Servers/KaxSocket/Model/DataModel.cs",
      "language": "csharp",
      "namespace": "KaxSocket",
      "exports": [
        {
          "kind": "class",
          "name": "DataModel",
          "summary": "Base persisted model exposing integer primary key Id."
        },
        {
          "kind": "class",
          "name": "UserData",
          "summary": "User aggregate including profile, security, and dual JWT token fields."
        },
        {
          "kind": "class",
          "name": "LoginDevice",
          "summary": "Sub-table recording per-login device info: HID, OS, device name, timestamp."
        }
      ]
    },
    {
      "file": "Library/Applications/Servers/KaxSocket/Global/KaxGlobal.cs",
      "language": "csharp",
      "namespace": "KaxSocket",
      "exports": [
        {
          "kind": "class",
          "name": "KaxGlobal",
          "summary": "Global repositories and token issuance helpers for KaxSocket server."
        },
        {
          "kind": "method",
          "name": "GenerateLoginToken",
          "summary": "Issues default JWT for compatibility with legacy login flow."
        },
        {
          "kind": "method",
          "name": "GenerateClientToken",
          "summary": "Issues HID-bound client JWT with device name and OS claims, regenerated on each login."
        },
        {
          "kind": "method",
          "name": "GenerateWebToken",
          "summary": "Issues browser-only JWT, refreshed only when expired."
        },
        {
          "kind": "method",
          "name": "ResolveLoginTokens",
          "summary": "Builds dual tokens with client HID binding and web token reuse policy."
        }
      ]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Network/Http/Auth/JwtHelper.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Network.Http.Auth",
      "exports": [
        {
          "kind": "class",
          "name": "JwtHelper",
          "summary": "JWT helper for signing, validating, and revoking bearer tokens."
        },
        {
          "kind": "method",
          "name": "GenerateToken",
          "summary": "Generates JWT with optional custom expiration override."
        }
      ]
    },
    {
      "file": "Library/Applications/Servers/KaxSocket/Handlers/Helpers/Api.cs",
      "language": "csharp",
      "namespace": "KaxSocket.Handlers.Helpers",
      "exports": [
        {
          "kind": "class",
          "name": "ApiEnvelope",
          "summary": "Unified API envelope containing code message data traceId fields."
        },
        {
          "kind": "class",
          "name": "ApiPagedData<T>",
          "summary": "Standard paginated payload with items page pageSize total fields."
        },
        {
          "kind": "class",
          "name": "ApiErrorCodes",
          "summary": "Centralized API error code constants shared by handlers and frontend."
        },
        {
          "kind": "enum",
          "name": "ApiAccessPolicy",
          "summary": "Semantic authorization policies for system and admin endpoint access checks."
        },
        {
          "kind": "method",
          "name": "EnvelopeOk",
          "summary": "Builds success envelope response with generated or propagated traceId."
        },
        {
          "kind": "method",
          "name": "SendVerificationCodeEmail",
          "summary": "Unified email handler for sending verification codes with retry and error handling."
        },
        {
          "kind": "method",
          "name": "SendEmailUnified",
          "summary": "Centralized email sending entry point with param validation and error handling."
        },
        {
          "kind": "method",
          "name": "SendEmailUnifiedAsync",
          "summary": "Async email sending with CancellationToken support and unified error handling."
        },
        {
          "kind": "method",
          "name": "SendVerificationCodeEmailAsync",
          "summary": "Async verification code email sending with configurable expiration hint."
        },
        {
          "kind": "method",
          "name": "EnvelopeFail",
          "summary": "Builds failure envelope response with status and traceId for diagnostics."
        },
        {
          "kind": "method",
          "name": "EnvelopePaged",
          "summary": "Builds paginated envelope payload under standardized data structure."
        },
        {
          "kind": "method",
          "name": "RequirePolicyAsync",
          "summary": "Authorizes request user with semantic policy and returns validated user context."
        },
        {
          "kind": "method",
          "name": "RequirePolicyNameAsync",
          "summary": "Authorizes by policy and returns username for lightweight privileged handlers."
        }
      ]
    },
    {
      "file": "Library/Applications/Servers/KaxSocket/Views/js/core/auth-state.js",
      "language": "javascript",
      "exports": [
        {
          "kind": "function",
          "name": "getToken",
          "summary": "Returns active auth token (kax_web_token preferred, fallback kax_login_token)."
        },
        {
          "kind": "function",
          "name": "clearToken",
          "summary": "Removes both auth tokens from localStorage."
        },
        {
          "kind": "function",
          "name": "fetchCurrentUser",
          "summary": "Calls /api/user/verify/account and caches result for session."
        },
        {
          "kind": "function",
          "name": "getRoleState",
          "summary": "Returns semantic role object: isAdmin, isSystem, role, permissionGroup, label."
        },
        {
          "kind": "function",
          "name": "mapPermissionToLabel",
          "summary": "Maps raw permissionGroup integer to Chinese label string."
        },
        {
          "kind": "function",
          "name": "permissionGroupToCssClass",
          "summary": "Maps raw permissionGroup integer to CSS badge class string."
        },
        {
          "kind": "function",
          "name": "permissionGroupToEnglishText",
          "summary": "Maps raw permissionGroup integer to English role label string."
        }
      ]
    },
    {
      "file": "Library/Applications/Servers/KaxSocket/Views/js/core/api-client.js",
      "language": "javascript",
      "exports": [
        {
          "kind": "object",
          "name": "ApiClient",
          "summary": "Unified API client: token injection, timeout, 401 handling, JSON parse."
        },
        {
          "kind": "function",
          "name": "request",
          "summary": "Low-level fetch wrapper with auth header, timeout, and 401 redirect."
        },
        {
          "kind": "function",
          "name": "requestJson",
          "summary": "Fetches JSON response; throws ApiError on non-ok status or parse failure."
        },
        {
          "kind": "function",
          "name": "requestJsonPost",
          "summary": "POST JSON request shorthand using requestJson."
        }
      ]
    },
    {
      "file": "Library/Applications/Servers/KaxSocket/Views/js/core/error-presenter.js",
      "language": "javascript",
      "exports": [
        {
          "kind": "object",
          "name": "ErrorPresenter",
          "summary": "Global error-code-to-message mapping and unified notification presenter."
        },
        {
          "kind": "function",
          "name": "resolveError",
          "summary": "Maps HTTP status or business code to structured { title, message, type, action }."
        },
        {
          "kind": "function",
          "name": "notifyError",
          "summary": "Displays error using showMsgBox if available, falls back to alert."
        },
        {
          "kind": "function",
          "name": "notifySuccess",
          "summary": "Displays success notification via showMsgBox or alert fallback."
        }
      ]
    },
    {
      "file": "Library/Applications/Servers/KaxSocket/Views/js/profile.shared.js",
      "language": "javascript",
      "exports": [
        {
          "kind": "object",
          "name": "ProfileShared",
          "summary": "Cross-module utilities for profile pages: token check, element display, HTML escape, button loading."
        },
        {
          "kind": "function",
          "name": "checkToken",
          "summary": "Returns active auth token via AuthState or localStorage fallback; redirects to /login if absent."
        }
      ]
    },
    {
      "file": "Library/Applications/Servers/KaxSocket/Views/js/profile.user.js",
      "language": "javascript",
      "exports": [
        {
          "kind": "object",
          "name": "ProfileUser",
          "summary": "User profile domain module: load/edit profile, security, assets, CDK activation."
        },
        {
          "kind": "function",
          "name": "initializePage",
          "summary": "Bootstraps user profile page: fetches current user, profile data, and active assets."
        },
        {
          "kind": "function",
          "name": "loadProfileFromServer",
          "summary": "Fetches user profile from API and populates all DOM fields."
        },
        {
          "kind": "function",
          "name": "loadActiveAssets",
          "summary": "Fetches and renders active asset subscriptions for current user."
        }
      ]
    },
    {
      "file": "Library/Applications/Servers/KaxSocket/Views/js/profile.admin.js",
      "language": "javascript",
      "exports": [
        {
          "kind": "object",
          "name": "ProfileAdmin",
          "summary": "Admin domain module: asset list/edit/delete and CDK management for profile admin tab."
        },
        {
          "kind": "function",
          "name": "loadAdminAssets",
          "summary": "Fetches paginated admin asset list with search and soft-delete filter."
        },
        {
          "kind": "function",
          "name": "loadAdminCdks",
          "summary": "Fetches paginated CDK admin list with keyword search support."
        }
      ]
    },
    {
      "file": "Library/Applications/Servers/KaxSocket/Views/js/shared/utils.js",
      "language": "javascript",
      "exports": [
        {
          "kind": "object",
          "name": "ShopUtils",
          "summary": "Shared utility functions for shop domain: date formatting, HTML escaping, price display."
        },
        {
          "kind": "function",
          "name": "formatDate",
          "summary": "Converts timestamp or date value to zh-CN locale date string."
        },
        {
          "kind": "function",
          "name": "escapeHtml",
          "summary": "Escapes HTML special characters in a value to prevent XSS."
        },
        {
          "kind": "function",
          "name": "formatDownloadCount",
          "summary": "Formats numeric download/purchase count with K suffix for large values."
        },
        {
          "kind": "function",
          "name": "formatCurrency",
          "summary": "Formats a numeric price in yuan to a display string with coin emoji prefix."
        }
      ]
    },
    {
      "file": "Examples/AssetManagementAcceptanceTests/DecouplingSmokeRegressionTests.cs",
      "language": "csharp",
      "namespace": "AssetManagementAcceptanceTests",
      "exports": [
        {
          "kind": "class",
          "name": "DecouplingSmokeRegressionTests",
          "summary": "Smoke regression coverage for envelope contract and key frontend path migration."
        }
      ]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Abstractions/IScriptEngineRuntime.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Abstractions",
      "exports": [{ "kind": "interface", "name": "IScriptEngineRuntime", "summary": "底层脚本引擎运行时抽象，隔离 V8/Jint 等具体实现。" }]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Abstractions/IJavaScriptEngine.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Abstractions",
      "exports": [{ "kind": "interface", "name": "IJavaScriptEngine", "summary": "公共 JavaScript 引擎接口：执行、文件执行、函数调用、全局注册。" }]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Abstractions/IScriptBinder.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Abstractions",
      "exports": [{ "kind": "interface", "name": "IScriptBinder", "summary": "将 .NET 类型和方法绑定到脚本运行时的抽象。" }]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Abstractions/ITypeConverter.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Abstractions",
      "exports": [{ "kind": "interface", "name": "ITypeConverter", "summary": ".NET 与 JS 值之间的类型转换抽象，支持策略注册。" }]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Abstractions/IScriptRegistry.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Abstractions",
      "exports": [{ "kind": "interface", "name": "IScriptRegistry", "summary": "脚本类型元数据注册表：注册、按名/类型查询。" }]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Abstractions/IScriptTypeScanner.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Abstractions",
      "exports": [{ "kind": "interface", "name": "IScriptTypeScanner", "summary": "扫描程序集以发现带 ScriptExport 标注类型的元数据。" }]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Abstractions/ITypeConversionStrategy.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Abstractions",
      "exports": [{ "kind": "interface", "name": "ITypeConversionStrategy", "summary": "可插拔类型转换策略，支持优先级排序。" }]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Abstractions/IBindingStrategy.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Abstractions",
      "exports": [{ "kind": "interface", "name": "IBindingStrategy", "summary": "自定义类型绑定策略，决定如何将元数据绑定到运行时。" }]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Abstractions/ITypeFilter.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Abstractions",
      "exports": [{ "kind": "interface", "name": "ITypeFilter", "summary": "过滤器决定某类型是否应导出到脚本引擎。" }]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Abstractions/IJavaScriptPlugin.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Abstractions",
      "exports": [{ "kind": "interface", "name": "IJavaScriptPlugin", "summary": "引擎插件抽象：命名、初始化、释放。" }]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Abstractions/IScriptBridge.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Abstractions",
      "exports": [{ "kind": "interface", "name": "IScriptBridge", "summary": "脚本桥接抽象，自动发现并向引擎注册宿主对象。" }]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Abstractions/IJavaScriptEngineFactory.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Abstractions",
      "exports": [{ "kind": "interface", "name": "IJavaScriptEngineFactory", "summary": "引擎工厂抽象，支持创建默认或自定义配置的引擎实例。" }]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Attributes/ScriptExportAttribute.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Attributes",
      "exports": [{ "kind": "class", "name": "ScriptExportAttribute", "summary": "标记类/方法/属性/字段可导出到JS，支持自定义名称和导出类型。" }]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Attributes/ScriptExportType.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Attributes",
      "exports": [{ "kind": "enum", "name": "ScriptExportType", "summary": "JS导出类型：Class / Function / StaticClass。" }]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Attributes/ScriptIgnoreAttribute.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Attributes",
      "exports": [{ "kind": "class", "name": "ScriptIgnoreAttribute", "summary": "标记方法/属性/字段不导出到JS。" }]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Attributes/ScriptNameAttribute.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Attributes",
      "exports": [{ "kind": "class", "name": "ScriptNameAttribute", "summary": "指定类/方法/属性/字段在JS中使用的自定义名称。" }]
    },
    {
      "file": "Library/Environments/SDK/Drx.Sdk.Shared.JavaScript/Attributes/ScriptConstructorAttribute.cs",
      "language": "csharp",
      "namespace": "Drx.Sdk.Shared.JavaScript.Attributes",
      "exports": [{ "kind": "class", "name": "ScriptConstructorAttribute", "summary": "指定构造函数作为JS侧创建实例时调用的目标构造函数。" }]
    }
  ]
}
