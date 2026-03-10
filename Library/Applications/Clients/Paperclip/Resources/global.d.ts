// Paperclip built-in global functions

/**
 * 向控制台输出一个值（等同于 console.log）。
 * @param value - 要打印的值，省略时输出空行。
 */
declare function print(value?: any): void;

/**
 * 暂停脚本执行并等待用户按 Enter 键。
 * @param prompt - 可选提示文本，默认显示"Press Enter to continue..."。
 */
declare function pause(prompt?: string): void;

/**
 * 获取当前脚本运行的项目根目录。
 * @returns 项目根目录的绝对路径。
 */
declare function getdir(): string;

// Paperclip host types

/**
 * 脚本友好型 HTTP 服务器。
 *
 * 封装 {@link DrxHttpServer}，提供链式 API、快捷路由方法、中间件与 SSE 支持。
 *
 * @example
 * const server = new HttpServer("http://localhost:8080/");
 * server.get("/ping", (req) => "pong");
 * await server.startAsync();
 */
declare class HttpServer {
	/**
	 * 使用单个 URL 前缀创建服务器。
	 * @param prefix - 监听地址，如 `"http://localhost:8080/"`。
	 */
	constructor(prefix: string);

	/**
	 * 使用 URL 前缀和可选配置创建服务器。
	 * @param prefix - 监听地址。
	 * @param staticFileRoot - 静态文件根目录路径，`null` 不提供文件服务。
	 * @param sessionTimeoutMinutes - 会话超时（分钟），默认 20。
	 */
	constructor(prefix: string, staticFileRoot: string | null, sessionTimeoutMinutes?: number);

	/** 获取底层 DrxHttpServer 实例（高级用法）。 */
	readonly Server: DrxHttpServer;

	// ── 生命周期 ──────────────────────────────────────

	/** 启动服务器并开始监听请求。 */
	startAsync(): Promise<void>;

	/** 停止服务器监听。 */
	stop(): void;

	/** 异步释放服务器占用的所有资源。 */
	disposeAsync(): Promise<void>;

	// ── 事件钩子（链式调用） ──────────────────────────

	/**
	 * 注册启动完成钩子。
	 * @param handler - 回调签名支持：`() => any` / `(server) => any`。
	 */
	onStart(handler: (server?: DrxHttpServer) => any): HttpServer;

	/**
	 * 注册停止钩子。
	 * @param handler - 回调签名支持：`() => any` / `(server) => any`。
	 */
	onStop(handler: (server?: DrxHttpServer) => any): HttpServer;

	/**
	 * 注册请求进入钩子。
	 * @param handler - 回调签名支持：`(request, server?) => any`。
	 */
	onRequest(handler: (request: any, server?: DrxHttpServer) => any): HttpServer;

	/**
	 * 注册响应输出钩子。
	 * @param handler - 回调签名支持：`(request, response, server?) => any`。
	 */
	onResponse(handler: (request: any, response: any, server?: DrxHttpServer) => any): HttpServer;

	/**
	 * 注册错误钩子。
	 * @param handler - 回调签名支持：`(request, error, server?) => any`。
	 */
	onError(handler: (request: any | null, error: any, server?: DrxHttpServer) => any): HttpServer;

	/**
	 * 通用事件注册。
	 * @param eventName - 事件名：`"start" | "stop" | "request" | "response" | "error"`。
	 * @param handler - 对应事件回调函数。
	 */
	on(eventName: "start" | "stop" | "request" | "response" | "error", handler: (...args: any[]) => any): HttpServer;

	// ── 配置（链式调用） ──────────────────────────────

	/**
	 * 启用或禁用调试模式（详细日志输出）。
	 * @param enable - `true` 开启 / `false` 关闭。
	 */
	debugMode(enable: boolean): HttpServer;

	/**
	 * 启用或禁用日志输出。
	 * @param enable - `true` 开启 / `false` 关闭。
	 */
	noLog(enable: boolean): HttpServer;

	/**
	 * 设置静态文件根目录。
	 * @param path - 文件系统路径，`null` 关闭文件服务。
	 */
	setFileRoot(path: string | null): HttpServer;

	/**
	 * 设置视图模板根目录。
	 * @param path - 文件系统路径，`null` 无视图。
	 */
	setViewRoot(path: string | null): HttpServer;

	/**
	 * 设置自定义 404 页面路径。
	 * @param path - HTML 文件路径，`null` 使用默认响应。
	 */
	setNotFoundPage(path: string | null): HttpServer;

	/**
	 * 配置全局请求限流。
	 * @param maxRequests - 时间窗口内允许的最大请求数。
	 * @param timeValue   - 时间窗口数值。
	 * @param timeUnit    - 时间单位，如 `"seconds"` / `"minutes"`。
	 */
	setRateLimit(maxRequests: number, timeValue: number, timeUnit: string): HttpServer;

	/**
	 * 添加额外的静态文件路由映射。
	 * @param urlPrefix      - URL 前缀，如 `"/assets"`。
	 * @param rootDirectory  - 映射到的本地目录。
	 */
	addFileRoute(urlPrefix: string, rootDirectory: string): HttpServer;

	// ── 路由（链式调用） ──────────────────────────────

	/**
	 * 注册通用路由。
	 * @param method                 - HTTP 方法（GET/POST/...）。
	 * @param path                   - 路由路径。
	 * @param handler                - 请求处理函数，返回 HttpResponse / string / object / null。可接收第二个参数 server 实例。
	 * @param rateLimitMaxRequests   - 可选路由级限流最大请求数。
	 * @param rateLimitWindowSeconds - 可选路由级限流窗口（秒）。
	 */
	map(method: string, path: string, handler: (request: any, server?: DrxHttpServer) => any, rateLimitMaxRequests?: number, rateLimitWindowSeconds?: number): HttpServer;

	/**
	 * 注册带限流超限回调的通用路由。
	 * @param method                 - HTTP 方法。
	 * @param path                   - 路由路径。
	 * @param handler                - 请求处理函数。可接收第二个参数 server 实例。
	 * @param rateLimitMaxRequests   - 限流最大请求数。
	 * @param rateLimitWindowSeconds - 限流窗口（秒）。
	 * @param rateLimitCallback      - 超限回调 `(count, request, context) => response`，`null` 使用默认 429。
	 */
	mapWithRateCallback(method: string, path: string, handler: (request: any, server?: DrxHttpServer) => any, rateLimitMaxRequests: number, rateLimitWindowSeconds: number, rateLimitCallback?: ((count: number, request: any, context: any) => any | Promise<any>) | null): HttpServer;

	/**
	 * 注册 GET 路由。
	 * @param path    - 路由路径。
	 * @param handler - 请求处理函数。可接收第二个参数 server 实例。
	 */
	get(path: string, handler: (request: any, server?: DrxHttpServer) => any): HttpServer;

	/**
	 * 注册 POST 路由。
	 * @param path    - 路由路径。
	 * @param handler - 请求处理函数。可接收第二个参数 server 实例。
	 */
	post(path: string, handler: (request: any, server?: DrxHttpServer) => any): HttpServer;

	/**
	 * 注册 PUT 路由。
	 * @param path    - 路由路径。
	 * @param handler - 请求处理函数。可接收第二个参数 server 实例。
	 */
	put(path: string, handler: (request: any, server?: DrxHttpServer) => any): HttpServer;

	/**
	 * 注册 DELETE 路由。
	 * @param path    - 路由路径。
	 * @param handler - 请求处理函数。可接收第二个参数 server 实例。
	 */
	delete(path: string, handler: (request: any, server?: DrxHttpServer) => any): HttpServer;

	/**
	 * 注册 PATCH 路由。
	 * @param path    - 路由路径。
	 * @param handler - 请求处理函数。可接收第二个参数 server 实例。
	 */
	patch(path: string, handler: (request: any, server?: DrxHttpServer) => any): HttpServer;

	/**
	 * 注册 HEAD 路由。
	 * @param path    - 路由路径。
	 * @param handler - 请求处理函数。可接收第二个参数 server 实例。
	 */
	head(path: string, handler: (request: any, server?: DrxHttpServer) => any): HttpServer;

	/**
	 * 注册 OPTIONS 路由。
	 * @param path    - 路由路径。
	 * @param handler - 请求处理函数。可接收第二个参数 server 实例。
	 */
	options(path: string, handler: (request: any, server?: DrxHttpServer) => any): HttpServer;

	// ── 中间件（链式调用） ────────────────────────────

	/**
	 * 注册全局中间件。返回 `null` 继续下一个处理；返回非 null 值终止请求。
	 * @param middleware - 中间件函数。可接收第二个参数 server 实例。
	 */
	use(middleware: (request: any, server?: DrxHttpServer) => any): HttpServer;

	/**
	 * 注册路径范围中间件。
	 * @param middleware - 中间件函数。可接收第二个参数 server 实例。
	 * @param path       - 匹配路径前缀，`null` 表示全局。
	 */
	use(middleware: (request: any, server?: DrxHttpServer) => any, path: string | null): HttpServer;

	/**
	 * 注册中间件（完整参数）。
	 * @param middleware      - 中间件函数。可接收第二个参数 server 实例。
	 * @param path            - 匹配路径前缀，`null` 表示全局。
	 * @param priority        - 执行优先级，数值越小越先执行。
	 * @param overrideGlobal  - 是否覆盖同优先级的全局中间件。
	 */
	use(middleware: (request: any, server?: DrxHttpServer) => any, path: string | null, priority: number, overrideGlobal?: boolean): HttpServer;

	// ── SSE (Server-Sent Events) ─────────────────────

	/**
	 * 获取当前 SSE 连接客户端数量。
	 * @param path - 可选路径过滤，`null` 返回全部。
	 */
	getSseClientCount(path?: string | null): number;

	/**
	 * 向指定路径的所有 SSE 客户端广播事件。
	 * @param path      - SSE 端点路径。
	 * @param eventName - 事件名，`null` 发送无名事件。
	 * @param data      - 事件数据（字符串）。
	 */
	broadcastSse(path: string, eventName: string | null, data: string): Promise<void>;

	/**
	 * 向所有 SSE 客户端广播事件。
	 * @param eventName - 事件名，`null` 发送无名事件。
	 * @param data      - 事件数据。
	 */
	broadcastSseToAll(eventName: string | null, data: string): Promise<void>;

	/**
	 * 断开指定 SSE 客户端连接。
	 * @param clientId - 客户端唯一标识。
	 */
	disconnectSseClient(clientId: string): void;

	/**
	 * 断开路径下全部 SSE 客户端。
	 * @param path - 可选路径过滤，`null` 断开所有。
	 */
	disconnectAllSseClients(path?: string | null): void;

	// ── 缓存 ─────────────────────────────────────────

	/** 清空全部静态资源缓存。 */
	clearCache(): void;

	/**
	 * 使指定文件的缓存失效。
	 * @param filePath - 需要失效的文件路径。
	 */
	invalidateCache(filePath: string): void;
}

/**
 * 底层 HTTP 服务器实例。
 *
 * 一般通过 {@link HttpServer} 或 {@link HttpServerFactory} 使用，
 * 不建议直接实例化。
 */
declare class DrxHttpServer {
	/**
	 * 创建底层服务器实例。
	 * @param prefixes              - 监听地址数组。
	 * @param staticFileRoot        - 静态文件根目录。
	 * @param sessionTimeoutMinutes - 会话超时（分钟）。
	 */
	constructor(prefixes: string[], staticFileRoot?: string | null, sessionTimeoutMinutes?: number);

	/** 启动服务器监听。 */
	StartAsync(): Promise<void>;

	/** 停止服务器。 */
	Stop(): void;

	/**
	 * 设置调试模式。
	 * @param enable - `true` 开启 / `false` 关闭。
	 */
	DebugMode(enable: boolean): DrxHttpServer;

	/**
	 * 启用或关闭静默模式（仅保留错误日志，大幅提升吞吐性能）。
	 * @param enable - `true` 开启 / `false` 关闭。
	 */
	NoLog(enable: boolean): DrxHttpServer;

	/**
	 * 解析文件物理路径。
	 * @param pathOrIndicator - 路径或指示符。
	 * @returns 解析后的绝对路径，未找到返回 `null`。
	 */
	ResolveFilePath(pathOrIndicator: string): string | null;
}

/**
 * DrxHttpServer 静态工厂类。
 *
 * 提供服务器创建、配置、路由注册、中间件注册、SSE 管理等全部静态方法。
 * 适合不使用 {@link HttpServer} 链式 API 的场景。
 *
 * @example
 * const server = HttpServerFactory.CreateFromPrefix("http://localhost:8080/");
 * HttpServerFactory.Map(server, "GET", "/ping", (req) => "pong");
 * await HttpServerFactory.StartAsync(server);
 */
declare class HttpServerFactory {
	/**
	 * 使用前缀数组创建服务器。
	 * @param prefixes              - 监听地址数组。
	 * @param staticFileRoot        - 静态文件根目录。
	 * @param sessionTimeoutMinutes - 会话超时（分钟）。
	 */
	static Create(prefixes: string[], staticFileRoot?: string | null, sessionTimeoutMinutes?: number): DrxHttpServer;

	/**
	 * 使用单个前缀创建服务器。
	 * @param prefix                - 监听地址。
	 * @param staticFileRoot        - 静态文件根目录。
	 * @param sessionTimeoutMinutes - 会话超时（分钟）。
	 */
	static CreateFromPrefix(prefix: string, staticFileRoot?: string | null, sessionTimeoutMinutes?: number): DrxHttpServer;

	/**
	 * 启动服务器监听。
	 * @param server - 服务器实例。
	 */
	static StartAsync(server: DrxHttpServer): Promise<void>;

	/**
	 * 停止服务器。
	 * @param server - 服务器实例。
	 */
	static Stop(server: DrxHttpServer): void;

	/**
	 * 设置调试模式。
	 * @param server - 服务器实例。
	 * @param enable - `true` 开启 / `false` 关闭。
	 */
	static SetDebugMode(server: DrxHttpServer, enable: boolean): DrxHttpServer;

	/**
	 * 启用或关闭静默模式（仅保留错误日志，大幅提升吞吐性能）。
	 * @param server - 服务器实例。
	 * @param enable - `true` 开启 / `false` 关闭。
	 */
	static SetNoLog(server: DrxHttpServer, enable: boolean): DrxHttpServer;

	/**
	 * 批量配置路径：文件根目录、视图根目录、404 页面。
	 * @param server           - 服务器实例。
	 * @param fileRootPath     - 静态文件根目录。
	 * @param viewRoot         - 视图模板根目录。
	 * @param notFoundPagePath - 自定义 404 页面路径。
	 */
	static ConfigurePaths(server: DrxHttpServer, fileRootPath?: string | null, viewRoot?: string | null, notFoundPagePath?: string | null): DrxHttpServer;

	/**
	 * 配置全局请求限流。
	 * @param server      - 服务器实例。
	 * @param maxRequests - 时间窗口内允许的最大请求数。
	 * @param timeValue   - 时间窗口数值。
	 * @param timeUnit    - 时间单位（`"seconds"` / `"minutes"`）。
	 */
	static SetRateLimit(server: DrxHttpServer, maxRequests: number, timeValue: number, timeUnit: string): void;

	/**
	 * 添加静态文件路由。
	 * @param server        - 服务器实例。
	 * @param urlPrefix     - URL 路径前缀。
	 * @param rootDirectory - 本地目录。
	 */
	static AddFileRoute(server: DrxHttpServer, urlPrefix: string, rootDirectory: string): void;

	/**
	 * 解析文件物理路径。
	 * @param server          - 服务器实例。
	 * @param pathOrIndicator - 路径或指示符。
	 * @returns 绝对路径或 `null`。
	 */
	static ResolveFilePath(server: DrxHttpServer, pathOrIndicator: string): string | null;

	/** 清空全部静态资源缓存。 */
	static ClearStaticContentCache(server: DrxHttpServer): void;

	/**
	 * 使指定文件的静态缓存失效。
	 * @param server   - 服务器实例。
	 * @param filePath - 资源路径。
	 */
	static InvalidateStaticContentCache(server: DrxHttpServer, filePath: string): void;

	/**
	 * 获取 SSE 连接客户端数量。
	 * @param server - 服务器实例。
	 * @param path   - 可选路径过滤。
	 */
	static GetSseClientCount(server: DrxHttpServer, path?: string | null): number;

	/**
	 * 向指定路径广播 SSE 事件。
	 * @param server    - 服务器实例。
	 * @param path      - SSE 端点路径。
	 * @param eventName - 事件名，`null` 无名事件。
	 * @param data      - 事件数据。
	 */
	static BroadcastSseAsync(server: DrxHttpServer, path: string, eventName: string | null, data: string): Promise<void>;

	/**
	 * 向所有 SSE 客户端广播事件。
	 * @param server    - 服务器实例。
	 * @param eventName - 事件名。
	 * @param data      - 事件数据。
	 */
	static BroadcastSseToAllAsync(server: DrxHttpServer, eventName: string | null, data: string): Promise<void>;

	/**
	 * 断开指定 SSE 客户端。
	 * @param server   - 服务器实例。
	 * @param clientId - 客户端标识。
	 */
	static DisconnectSseClient(server: DrxHttpServer, clientId: string): void;

	/**
	 * 断开路径下全部 SSE 客户端。
	 * @param server - 服务器实例。
	 * @param path   - 可选路径过滤。
	 */
	static DisconnectAllSseClients(server: DrxHttpServer, path?: string | null): void;

	/**
	 * 异步释放服务器资源。
	 * @param server - 服务器实例。
	 */
	static DisposeAsync(server: DrxHttpServer): Promise<void>;

	/**
	 * 注册同步路由（函数驱动）。
	 * @param server                 - 服务器实例。
	 * @param method                 - HTTP 方法。
	 * @param path                   - 路由路径。
	 * @param handler                - 处理函数，返回 HttpResponse / string / object / null。可接收第二个参数 server 实例。
	 * @param rateLimitMaxRequests   - 可选路由级限流。
	 * @param rateLimitWindowSeconds - 可选限流窗口（秒）。
	 */
	static Map(server: DrxHttpServer, method: string, path: string, handler: (request: any, server?: DrxHttpServer) => any, rateLimitMaxRequests?: number, rateLimitWindowSeconds?: number): void;

	/**
	 * 注册异步路由（函数驱动）。
	 * @param server                 - 服务器实例。
	 * @param method                 - HTTP 方法。
	 * @param path                   - 路由路径。
	 * @param handler                - 异步处理函数。可接收第二个参数 server 实例。
	 * @param rateLimitMaxRequests   - 可选路由级限流。
	 * @param rateLimitWindowSeconds - 可选限流窗口（秒）。
	 */
	static MapAsync(server: DrxHttpServer, method: string, path: string, handler: (request: any, server?: DrxHttpServer) => Promise<any>, rateLimitMaxRequests?: number, rateLimitWindowSeconds?: number): void;

	/**
	 * 注册同步路由并附带限流超限回调。
	 * @param server                 - 服务器实例。
	 * @param method                 - HTTP 方法。
	 * @param path                   - 路由路径。
	 * @param handler                - 处理函数。可接收第二个参数 server 实例。
	 * @param rateLimitMaxRequests   - 最大请求数。
	 * @param rateLimitWindowSeconds - 限流窗口（秒）。
	 * @param rateLimitCallback      - 超限回调或函数名，`null` 使用默认 429。
	 */
	static MapWithRateCallback(server: DrxHttpServer, method: string, path: string, handler: (request: any, server?: DrxHttpServer) => any, rateLimitMaxRequests: number, rateLimitWindowSeconds: number, rateLimitCallback?: ((count: number, request: any, context: any) => any | Promise<any>) | string | null): void;

	/**
	 * 注册异步路由并附带限流超限回调。
	 * @param server                 - 服务器实例。
	 * @param method                 - HTTP 方法。
	 * @param path                   - 路由路径。
	 * @param handler                - 异步处理函数。可接收第二个参数 server 实例。
	 * @param rateLimitMaxRequests   - 最大请求数。
	 * @param rateLimitWindowSeconds - 限流窗口（秒）。
	 * @param rateLimitCallback      - 超限回调或函数名。
	 */
	static MapAsyncWithRateCallback(server: DrxHttpServer, method: string, path: string, handler: (request: any, server?: DrxHttpServer) => Promise<any>, rateLimitMaxRequests: number, rateLimitWindowSeconds: number, rateLimitCallback?: ((count: number, request: any, context: any) => any | Promise<any>) | string | null): void;

	/**
	 * 注册同步中间件。返回 `null` 继续下一个处理。
	 * @param server         - 服务器实例。
	 * @param middleware     - 中间件函数。
	 * @param path           - 路径前缀过滤。
	 * @param priority       - 优先级。
	 * @param overrideGlobal - 是否覆盖全局中间件。
	 */
	static Use(server: DrxHttpServer, middleware: (request: any, server?: DrxHttpServer) => any, path?: string | null, priority?: number, overrideGlobal?: boolean): void;

	/**
	 * 注册异步中间件。返回 `null` 继续下一个处理。
	 * @param server         - 服务器实例。
	 * @param middleware     - 异步中间件函数。可接收第二个参数 server 实例。
	 * @param path           - 路径前缀过滤。
	 * @param priority       - 优先级。
	 * @param overrideGlobal - 是否覆盖全局中间件。
	 */
	static UseAsync(server: DrxHttpServer, middleware: (request: any, server?: DrxHttpServer) => Promise<any>, path?: string | null, priority?: number, overrideGlobal?: boolean): void;

	/**
	 * 按全局函数名注册同步路由，运行时调用 `globalThis[functionName](request)`。
	 * @param server                 - 服务器实例。
	 * @param method                 - HTTP 方法。
	 * @param path                   - 路由路径。
	 * @param functionName           - 全局函数名。
	 * @param rateLimitMaxRequests   - 可选限流。
	 * @param rateLimitWindowSeconds - 可选限流窗口。
	 */
	static MapByName(server: DrxHttpServer, method: string, path: string, functionName: string, rateLimitMaxRequests?: number, rateLimitWindowSeconds?: number): void;

	/**
	 * 按函数名注册路由并附带限流超限回调。
	 * @param server                 - 服务器实例。
	 * @param method                 - HTTP 方法。
	 * @param path                   - 路由路径。
	 * @param functionName           - 全局函数名。
	 * @param rateLimitMaxRequests   - 最大请求数。
	 * @param rateLimitWindowSeconds - 限流窗口（秒）。
	 * @param rateLimitCallback      - 超限回调或函数名。
	 */
	static MapByNameWithRateCallback(server: DrxHttpServer, method: string, path: string, functionName: string, rateLimitMaxRequests: number, rateLimitWindowSeconds: number, rateLimitCallback?: ((count: number, request: any, context: any) => any | Promise<any>) | string | null): void;

	/**
	 * 按全局函数名注册中间件，返回 `null` 继续 next。
	 * @param server         - 服务器实例。
	 * @param functionName   - 全局函数名。
	 * @param path           - 路径前缀过滤。
	 * @param priority       - 优先级。
	 * @param overrideGlobal - 是否覆盖全局中间件。
	 */
	static UseByName(server: DrxHttpServer, functionName: string, path?: string | null, priority?: number, overrideGlobal?: boolean): void;
}

/**
 * HTTP 响应工厂类。
 *
 * 提供静态方法快速构建各类 HTTP 响应，供路由处理函数返回。
 *
 * @example
 * server.get("/index", (req) => HttpResponse.file("html/index.html"));
 * server.get("/api/data", (req) => HttpResponse.json({ ok: true }));
 * server.get("/old", (req) => HttpResponse.redirect("/new"));
 */
declare class HttpResponse {
	// ── 文件响应 ──────────────────────────────────────

	/**
	 * 返回文件内容响应，自动推断 Content-Type。
	 *
	 * 路径解析顺序：ViewRoot → FileRoot → 工作目录 → 绝对路径。
	 * 文件不存在时返回 404。
	 *
	 * @param path - 文件路径（相对或绝对）。
	 *
	 * @example
	 * // 返回 views/html/index.html 的内容
	 * HttpResponse.file("html/index.html");
	 */
	static file(path: string): HttpResponse;

	/**
	 * 返回文件下载响应（Content-Disposition: attachment）。
	 *
	 * @param path     - 文件路径（相对或绝对）。
	 * @param fileName - 下载文件名，省略时使用原始文件名。
	 *
	 * @example
	 * HttpResponse.download("reports/annual.pdf", "年度报告.pdf");
	 */
	static download(path: string, fileName?: string | null): HttpResponse;

	// ── 内容响应 ──────────────────────────────────────

	/**
	 * 返回 JSON 响应，自动序列化对象。
	 *
	 * @param data       - 要序列化的数据（对象/数组/基元/字符串）。
	 * @param statusCode - HTTP 状态码，默认 200。
	 *
	 * @example
	 * HttpResponse.json({ users: [], total: 0 });
	 * HttpResponse.json({ error: "not found" }, 404);
	 */
	static json(data: any, statusCode?: number): HttpResponse;

	/**
	 * 返回纯文本响应。
	 *
	 * @param text       - 文本内容。
	 * @param statusCode - HTTP 状态码，默认 200。
	 */
	static text(text: string, statusCode?: number): HttpResponse;

	/**
	 * 返回 HTML 响应。
	 *
	 * @param html       - HTML 内容字符串。
	 * @param statusCode - HTTP 状态码，默认 200。
	 *
	 * @example
	 * HttpResponse.html("<h1>Hello</h1>");
	 */
	static html(html: string, statusCode?: number): HttpResponse;

	// ── 重定向 ────────────────────────────────────────

	/**
	 * 返回重定向响应。
	 *
	 * @param url       - 目标 URL。
	 * @param permanent - `true` 使用 301 永久重定向，默认 `false`（302 临时重定向）。
	 *
	 * @example
	 * HttpResponse.redirect("/new-page");
	 * HttpResponse.redirect("/moved", true);  // 301
	 */
	static redirect(url: string, permanent?: boolean): HttpResponse;

	// ── 状态码快捷方法 ───────────────────────────────

	/**
	 * 返回指定状态码的响应。
	 *
	 * @param statusCode - HTTP 状态码。
	 * @param body       - 可选响应体。
	 */
	static status(statusCode: number, body?: string | null): HttpResponse;

	/**
	 * 返回 200 OK 响应。
	 * @param body - 可选响应体文本。
	 * @returns 状态码 200 的 {@link HttpResponse}。
	 */
	static ok(body?: string | null): HttpResponse;

	/**
	 * 返回 204 No Content 空响应。
	 *
	 * 适用于成功执行但无需返回内容的操作（如 DELETE、更新类接口）。
	 * @returns 状态码 204、无响应体的 {@link HttpResponse}。
	 */
	static noContent(): HttpResponse;

	/**
	 * 返回 400 Bad Request 响应。
	 *
	 * 请求参数无效或格式错误时使用。
	 * @param message - 可选错误说明文本，会写入响应体。
	 * @returns 状态码 400 的 {@link HttpResponse}。
	 */
	static badRequest(message?: string | null): HttpResponse;

	/**
	 * 返回 401 Unauthorized 响应。
	 *
	 * 用户未登录或 Token 失效时返回，提示客户端需进行身份认证。
	 * @param message - 可选提示文本，默认 `"Unauthorized"`。
	 * @returns 状态码 401 的 {@link HttpResponse}。
	 */
	static unauthorized(message?: string | null): HttpResponse;

	/**
	 * 返回 403 Forbidden 响应。
	 *
	 * 已认证但无权访问目标资源时返回。
	 * @param message - 可选提示文本，默认 `"Forbidden"`。
	 * @returns 状态码 403 的 {@link HttpResponse}。
	 */
	static forbidden(message?: string | null): HttpResponse;

	/**
	 * 返回 404 Not Found 响应。
	 *
	 * 请求的资源不存在时返回。
	 * @param message - 可选提示文本，默认 `"Not Found"`。
	 * @returns 状态码 404 的 {@link HttpResponse}。
	 */
	static notFound(message?: string | null): HttpResponse;

	/**
	 * 返回 500 Internal Server Error 响应。
	 *
	 * 服务器内部未预期异常时使用，不应将敏感错误细节暴露给客户端。
	 * @param message - 可选错误说明，用于调试（注意避免泄露内部实现细节）。
	 * @returns 状态码 500 的 {@link HttpResponse}。
	 */
	static serverError(message?: string | null): HttpResponse;
}

// ── Drx.Sdk.Network 扩展桥接 ───────────────────────

/**
 * HTTP 客户端实例（由 {@link HttpClient.create} 创建）。
 *
 * 该类型为宿主对象句柄，通常配合 `HttpClient.instance*` 系列静态方法使用。
 */
declare class DrxHttpClient {}

/**
 * 网络客户端实例（由 {@link TcpClient.createTcp}/{@link TcpClient.createUdp} 创建）。
 *
 * 该类型为宿主对象句柄，可通过 `TcpClient.*` 静态方法进行连接、收发与释放。
 */
declare class NetworkClient {}

/**
 * SMTP 邮件发送器实例（由 {@link Email.createSender} 创建）。
 *
 * 该类型为宿主对象句柄，可用于发送纯文本/HTML/Markdown 邮件。
 */
declare class SmtpEmailSender {}

/**
 * 脚本友好型 HTTP 客户端桥接。
 *
 * 提供两种调用模式：
 * - 共享实例：`HttpClient.get/post/...`
 * - 独立实例：`const c = HttpClient.create(); HttpClient.instancePost(c, ...)`
 *
 * 返回的 `HttpResponse` 为宿主桥接对象，通常可继续读取状态码、响应头或内容。
 * 当请求失败（网络异常、超时、TLS 错误等）时，Promise 会 reject。
 */
declare class HttpClient {
	/**
	 * 创建独立 HTTP 客户端实例。
	 * @param baseAddress - 可选基础地址（如 `"https://api.example.com"`），后续实例请求可传相对路径。
	 * @returns 客户端句柄，供 `instance*` 系列方法复用连接与默认配置。
	 */
	static create(baseAddress?: string | null): DrxHttpClient;

	/**
	 * 发送 GET 请求。
	 * @param url - 目标地址（绝对地址或相对地址）。
	 * @returns Promise，完成后返回 HTTP 响应对象。
	 */
	static get(url: string): Promise<HttpResponse>;
	/**
	 * 发送带请求头的 GET 请求。
	 * @param url - 目标地址。
	 * @param headers - 请求头字典，`null` 表示不附加额外头。
	 * @returns Promise，完成后返回 HTTP 响应对象。
	 */
	static getWithHeaders(url: string, headers?: Record<string, any> | null): Promise<HttpResponse>;

	/**
	 * 发送 POST 请求。
	 * @param url - 目标地址。
	 * @param body - 请求体，可为对象/字符串/字节等宿主支持类型。
	 * @returns Promise，完成后返回 HTTP 响应对象。
	 */
	static post(url: string, body?: any): Promise<HttpResponse>;
	/**
	 * 发送带请求头的 POST 请求。
	 * @param url - 目标地址。
	 * @param body - 请求体。
	 * @param headers - 请求头字典。
	 * @returns Promise，完成后返回 HTTP 响应对象。
	 */
	static postWithHeaders(url: string, body?: any, headers?: Record<string, any> | null): Promise<HttpResponse>;

	/**
	 * 发送 PUT 请求。
	 * @param url - 目标地址。
	 * @param body - 请求体。
	 * @returns Promise，完成后返回 HTTP 响应对象。
	 */
	static put(url: string, body?: any): Promise<HttpResponse>;
	/**
	 * 发送带请求头的 PUT 请求。
	 * @param url - 目标地址。
	 * @param body - 请求体。
	 * @param headers - 请求头字典。
	 * @returns Promise，完成后返回 HTTP 响应对象。
	 */
	static putWithHeaders(url: string, body?: any, headers?: Record<string, any> | null): Promise<HttpResponse>;

	/**
	 * 发送 DELETE 请求。
	 * @param url - 目标地址。
	 * @returns Promise，完成后返回 HTTP 响应对象。
	 */
	static del(url: string): Promise<HttpResponse>;
	/**
	 * 发送带请求头的 DELETE 请求。
	 * @param url - 目标地址。
	 * @param headers - 请求头字典。
	 * @returns Promise，完成后返回 HTTP 响应对象。
	 */
	static delWithHeaders(url: string, headers?: Record<string, any> | null): Promise<HttpResponse>;

	/**
	 * 下载远程文件到本地路径。
	 * @param url - 文件下载地址。
	 * @param destPath - 本地保存路径；目录不存在时由宿主按实现策略处理。
	 * @returns Promise，下载完成后 resolve。
	 */
	static downloadFile(url: string, destPath: string): Promise<void>;
	/**
	 * 上传本地文件（通常为 multipart/form-data）。
	 * @param url - 上传接口地址。
	 * @param filePath - 本地文件路径。
	 * @param fieldName - 表单字段名，默认由宿主实现决定。
	 * @returns Promise，完成后返回 HTTP 响应对象。
	 */
	static uploadFile(url: string, filePath: string, fieldName?: string): Promise<HttpResponse>;

	/**
	 * 使用指定实例发送 GET 请求。
	 * @param client - 由 `create` 创建的客户端实例。
	 * @param url - 目标地址。
	 * @returns Promise，完成后返回 HTTP 响应对象。
	 */
	static instanceGet(client: DrxHttpClient, url: string): Promise<HttpResponse>;
	/**
	 * 使用指定实例发送 POST 请求。
	 * @param client - 客户端实例。
	 * @param url - 目标地址。
	 * @param body - 请求体。
	 * @returns Promise，完成后返回 HTTP 响应对象。
	 */
	static instancePost(client: DrxHttpClient, url: string, body?: any): Promise<HttpResponse>;
	/**
	 * 使用指定实例发送 PUT 请求。
	 * @param client - 客户端实例。
	 * @param url - 目标地址。
	 * @param body - 请求体。
	 * @returns Promise，完成后返回 HTTP 响应对象。
	 */
	static instancePut(client: DrxHttpClient, url: string, body?: any): Promise<HttpResponse>;
	/**
	 * 使用指定实例发送 DELETE 请求。
	 * @param client - 客户端实例。
	 * @param url - 目标地址。
	 * @returns Promise，完成后返回 HTTP 响应对象。
	 */
	static instanceDelete(client: DrxHttpClient, url: string): Promise<HttpResponse>;

	/**
	 * 设置实例默认请求头。
	 * @param client - 客户端实例。
	 * @param name - 请求头名称。
	 * @param value - 请求头值。
	 */
	static setDefaultHeader(client: DrxHttpClient, name: string, value: string): void;
	/**
	 * 设置实例请求超时（秒）。
	 * @param client - 客户端实例。
	 * @param seconds - 超时秒数，建议为正整数。
	 */
	static setTimeout(client: DrxHttpClient, seconds: number): void;

	/**
	 * 使用指定实例下载文件。
	 * @param client - 客户端实例。
	 * @param url - 文件地址。
	 * @param destPath - 本地保存路径。
	 * @returns Promise，下载完成后 resolve。
	 */
	static instanceDownloadFile(client: DrxHttpClient, url: string, destPath: string): Promise<void>;
	/**
	 * 使用指定实例上传文件。
	 * @param client - 客户端实例。
	 * @param url - 上传接口地址。
	 * @param filePath - 本地文件路径。
	 * @param fieldName - 表单字段名。
	 * @returns Promise，完成后返回 HTTP 响应对象。
	 */
	static instanceUploadFile(client: DrxHttpClient, url: string, filePath: string, fieldName?: string): Promise<HttpResponse>;
}

/**
 * TCP/UDP 客户端桥接。
 *
 * @example
 * const client = TcpClient.createTcp("127.0.0.1", 9000);
 * await TcpClient.connect(client);
 * await TcpClient.sendTextAsync(client, "hello");
 */
declare class TcpClient {
	/**
	 * 创建 TCP 客户端。
	 * @param host - 目标主机名或 IP。
	 * @param port - 目标端口。
	 * @returns 网络客户端句柄。
	 */
	static createTcp(host: string, port: number): NetworkClient;
	/**
	 * 创建 UDP 客户端。
	 * @param host - 目标主机名或 IP。
	 * @param port - 目标端口。
	 * @returns 网络客户端句柄。
	 */
	static createUdp(host: string, port: number): NetworkClient;

	/**
	 * 建立网络连接。
	 * @param client - 客户端句柄。
	 * @returns Promise，连接成功返回 `true`，失败返回 `false`。
	 */
	static connect(client: NetworkClient): Promise<boolean>;
	/**
	 * 断开连接。
	 * @param client - 客户端句柄。
	 */
	static disconnect(client: NetworkClient): void;

	/**
	 * 发送字节数据（同步）。
	 * @param client - 客户端句柄。
	 * @param data - 字节数组（0-255）。
	 */
	static sendBytes(client: NetworkClient, data: number[]): void;
	/**
	 * 发送文本（UTF-8，同步）。
	 * @param client - 客户端句柄。
	 * @param text - 待发送文本。
	 */
	static sendText(client: NetworkClient, text: string): void;

	/**
	 * 异步发送字节数据。
	 * @param client - 客户端句柄。
	 * @param data - 字节数组。
	 * @returns Promise，成功返回 `true`。
	 */
	static sendBytesAsync(client: NetworkClient, data: number[]): Promise<boolean>;
	/**
	 * 异步发送文本（UTF-8）。
	 * @param client - 客户端句柄。
	 * @param text - 待发送文本。
	 * @returns Promise，成功返回 `true`。
	 */
	static sendTextAsync(client: NetworkClient, text: string): Promise<boolean>;

	/**
	 * 查询当前连接状态。
	 * @param client - 客户端句柄。
	 * @returns `true` 表示已连接。
	 */
	static isConnected(client: NetworkClient): boolean;
	/**
	 * 设置连接/发送超时（秒）。
	 * @param client - 客户端句柄。
	 * @param seconds - 超时秒数。
	 */
	static setTimeout(client: NetworkClient, seconds: number): void;
	/**
	 * 释放客户端资源。
	 * @param client - 客户端句柄。
	 */
	static dispose(client: NetworkClient): void;
}

/**
 * SMTP 邮件发送桥接。
 */
declare class Email {
	/**
	 * 创建 SMTP 发送器。
	 * @param senderAddress - 发件人邮箱地址。
	 * @param password - 邮箱密码或授权码。
	 * @param smtpHost - SMTP 主机地址（如 `smtp.qq.com`）。
	 * @param smtpPort - SMTP 端口（常见 25/465/587）。
	 * @param enableSsl - 是否启用 SSL/TLS。
	 * @param displayName - 发件人显示名称。
	 * @returns SMTP 发送器句柄。
	 */
	static createSender(
		senderAddress: string,
		password: string,
		smtpHost?: string,
		smtpPort?: number,
		enableSsl?: boolean,
		displayName?: string
	): SmtpEmailSender;

	/**
	 * 发送纯文本邮件。
	 * @param sender - SMTP 发送器句柄。
	 * @param to - 收件人地址。
	 * @param subject - 邮件主题。
	 * @param body - 纯文本正文。
	 */
	static send(sender: SmtpEmailSender, to: string, subject: string, body: string): Promise<void>;
	/**
	 * 发送 HTML 邮件。
	 * @param sender - SMTP 发送器句柄。
	 * @param to - 收件人地址。
	 * @param subject - 邮件主题。
	 * @param htmlBody - HTML 正文。
	 */
	static sendHtml(sender: SmtpEmailSender, to: string, subject: string, htmlBody: string): Promise<void>;
	/**
	 * 发送 Markdown 邮件（由宿主转换为最终可发送内容）。
	 * @param sender - SMTP 发送器句柄。
	 * @param to - 收件人地址。
	 * @param subject - 邮件主题。
	 * @param markdownBody - Markdown 正文。
	 */
	static sendMarkdown(sender: SmtpEmailSender, to: string, subject: string, markdownBody: string): Promise<void>;
	/**
	 * 安全发送纯文本邮件。
	 * 与 `send` 的区别：发生异常时不抛出，而是返回 `false`。
	 * @param sender - SMTP 发送器句柄。
	 * @param to - 收件人地址。
	 * @param subject - 邮件主题。
	 * @param body - 纯文本正文。
	 * @returns Promise，成功为 `true`，失败为 `false`。
	 */
	static trySend(sender: SmtpEmailSender, to: string, subject: string, body: string): Promise<boolean>;
}

/**
 * SQLite 数据库桥接。
 *
 * 提供原始 SQL 操作与便利 CRUD / DDL 方法。
 *
 * @example
 * const db = Database.open("data.db");
 * Database.createTable(db, "users", [
 *   { name: "id", type: "INTEGER", primaryKey: true },
 *   { name: "name", type: "TEXT", notNull: true },
 *   { name: "age", type: "INTEGER" }
 * ]);
 * Database.insert(db, "users", { name: "Alice", age: 30 });
 * const rows = Database.query(db, "SELECT * FROM users");
 * Database.close(db);
 */
declare class Database {
	/**
	 * 打开或创建 SQLite 数据库。
	 * @param databasePath - 数据库文件路径。
	 * @returns 连接字符串句柄，可用于后续 `execute/query/scalar`。
	 */
	static open(databasePath: string): string;

	// ── 原始 SQL ──────────────────────────────────────

	/**
	 * 执行非查询 SQL（INSERT/UPDATE/DELETE/DDL）。
	 * @param connectionString - 由 `open` 返回的连接句柄。
	 * @param sql - SQL 语句。
	 * @param parameters - 参数字典（命名参数）。
	 * @returns 受影响行数。
	 */
	static execute(connectionString: string, sql: string, parameters?: Record<string, any> | null): number;
	/**
	 * 执行查询 SQL。
	 * @param connectionString - 连接句柄。
	 * @param sql - 查询语句。
	 * @param parameters - 参数字典。
	 * @returns 行对象数组（字段名到值的映射）。
	 */
	static query(connectionString: string, sql: string, parameters?: Record<string, any> | null): any[];
	/**
	 * 查询单个标量值（如 COUNT(*)）。
	 * @param connectionString - 连接句柄。
	 * @param sql - 标量查询语句。
	 * @param parameters - 参数字典。
	 * @returns 第一行第一列的值；无结果时返回宿主定义的空值。
	 */
	static scalar(connectionString: string, sql: string, parameters?: Record<string, any> | null): any;

	/**
	 * 在单个事务中按顺序执行多条 SQL。
	 * 任一语句失败时将回滚（具体异常行为由宿主决定）。
	 * @param connectionString - 连接句柄。
	 * @param sqlStatements - SQL 语句列表。
	 */
	static transaction(connectionString: string, sqlStatements: string[]): void;
	/**
	 * 获取数据库中的所有表名。
	 * @param connectionString - 连接句柄。
	 * @returns 表名数组。
	 */
	static tables(connectionString: string): string[];
	/**
	 * 关闭连接池缓存并释放相关资源。
	 * @param connectionString - 连接句柄。
	 */
	static close(connectionString: string): void;

	// ── 便利查询 ──────────────────────────────────────

	/**
	 * 查询单行，无结果返回 `null`。
	 *
	 * @param connectionString - 连接句柄。
	 * @param sql - 查询语句（应返回 0 或 1 行）。
	 * @param parameters - 参数字典。
	 * @returns 单行对象或 `null`。
	 *
	 * @example
	 * const user = Database.queryOne(db, "SELECT * FROM users WHERE id = @id", { id: 1 });
	 */
	static queryOne(connectionString: string, sql: string, parameters?: Record<string, any> | null): any | null;

	/**
	 * 快速计数。`where` 为空则统计全表。
	 *
	 * @param connectionString - 连接句柄。
	 * @param table - 表名。
	 * @param where - 可选 WHERE 子句（不含 `WHERE` 关键字）。
	 * @param parameters - 可选参数字典。
	 * @returns 行数。
	 *
	 * @example
	 * const total = Database.count(db, "users");
	 * const active = Database.count(db, "users", "age > @min", { min: 18 });
	 */
	static count(connectionString: string, table: string, where?: string | null, parameters?: Record<string, any> | null): number;

	/**
	 * 检查行是否存在。
	 *
	 * @param connectionString - 连接句柄。
	 * @param table - 表名。
	 * @param where - WHERE 子句（不含 `WHERE` 关键字）。
	 * @param parameters - 可选参数字典。
	 * @returns 存在返回 `true`。
	 *
	 * @example
	 * if (Database.exists(db, "users", "name = @name", { name: "Alice" })) { ... }
	 */
	static exists(connectionString: string, table: string, where: string, parameters?: Record<string, any> | null): boolean;

	// ── 便利 CRUD ─────────────────────────────────────

	/**
	 * 用对象直接插入一行，键值对自动映射为列名和值。
	 * 返回新行的 `rowid`。
	 *
	 * @param connectionString - 连接句柄。
	 * @param table - 表名。
	 * @param data - 键值对对象（键 = 列名，值 = 列值）。
	 * @returns 新插入行的 `rowid`。
	 *
	 * @example
	 * const id = Database.insert(db, "users", { name: "Bob", age: 25 });
	 */
	static insert(connectionString: string, table: string, data: Record<string, any>): number;

	/**
	 * 在单个事务内批量插入多行。
	 *
	 * @param connectionString - 连接句柄。
	 * @param table - 表名。
	 * @param items - 对象数组，每个对象的键值对映射一行。
	 * @returns 受影响总行数。
	 *
	 * @example
	 * Database.insertBatch(db, "users", [
	 *   { name: "Alice", age: 30 },
	 *   { name: "Bob", age: 25 }
	 * ]);
	 */
	static insertBatch(connectionString: string, table: string, items: Record<string, any>[]): number;

	/**
	 * 按条件更新行。`data` 中的键值对生成 `SET` 子句。
	 *
	 * @param connectionString - 连接句柄。
	 * @param table - 表名。
	 * @param data - 要更新的键值对。
	 * @param where - WHERE 子句（不含 `WHERE` 关键字）。
	 * @param parameters - WHERE 子句的命名参数。
	 * @returns 受影响行数。
	 *
	 * @example
	 * Database.update(db, "users", { age: 31 }, "name = @name", { name: "Alice" });
	 */
	static update(connectionString: string, table: string, data: Record<string, any>, where: string, parameters?: Record<string, any> | null): number;

	/**
	 * 按条件删除行。
	 *
	 * @param connectionString - 连接句柄。
	 * @param table - 表名。
	 * @param where - WHERE 子句（不含 `WHERE` 关键字）。
	 * @param parameters - 可选参数字典。
	 * @returns 受影响行数。
	 *
	 * @example
	 * Database.deleteWhere(db, "users", "age < @min", { min: 18 });
	 */
	static deleteWhere(connectionString: string, table: string, where: string, parameters?: Record<string, any> | null): number;

	/**
	 * 插入或更新（SQLite UPSERT）。当冲突列的值已存在时更新其余列。
	 *
	 * @param connectionString - 连接句柄。
	 * @param table - 表名。
	 * @param data - 完整行键值对。
	 * @param conflictColumns - 冲突判定列名数组（需有 UNIQUE 约束或主键）。
	 * @returns 操作行的 `rowid`。
	 *
	 * @example
	 * Database.upsert(db, "users", { name: "Alice", age: 32 }, ["name"]);
	 */
	static upsert(connectionString: string, table: string, data: Record<string, any>, conflictColumns: string[]): number;

	// ── DDL 便利方法 ──────────────────────────────────

	/**
	 * 获取表的列信息（返回 PRAGMA table_info 结果）。
	 *
	 * @param connectionString - 连接句柄。
	 * @param table - 表名。
	 * @returns 列信息对象数组，每项含 `cid`、`name`、`type`、`notnull`、`dflt_value`、`pk`。
	 *
	 * @example
	 * const cols = Database.columns(db, "users");
	 * // [{ cid: 0, name: "id", type: "INTEGER", notnull: 0, dflt_value: null, pk: 1 }, ...]
	 */
	static columns(connectionString: string, table: string): any[];

	/**
	 * 便捷建表。
	 *
	 * @param connectionString - 连接句柄。
	 * @param table - 表名。
	 * @param columnDefs - 列定义对象数组。
	 *
	 * @example
	 * Database.createTable(db, "products", [
	 *   { name: "id", type: "INTEGER", primaryKey: true },
	 *   { name: "title", type: "TEXT", notNull: true },
	 *   { name: "price", type: "REAL", defaultValue: 0 }
	 * ]);
	 */
	static createTable(connectionString: string, table: string, columnDefs: ColumnDefinition[]): void;

	/**
	 * 删除表（`DROP TABLE IF EXISTS`）。
	 *
	 * @param connectionString - 连接句柄。
	 * @param table - 表名。
	 */
	static dropTable(connectionString: string, table: string): void;

	/**
	 * 为表添加列。
	 *
	 * @param connectionString - 连接句柄。
	 * @param table - 表名。
	 * @param columnName - 新列名。
	 * @param columnType - SQLite 类型（`TEXT`/`INTEGER`/`REAL`/`BLOB`）。
	 * @param defaultValue - 可选默认值。
	 *
	 * @example
	 * Database.addColumn(db, "users", "email", "TEXT");
	 */
	static addColumn(connectionString: string, table: string, columnName: string, columnType: string, defaultValue?: any): void;

	/**
	 * 创建索引。
	 *
	 * @param connectionString - 连接句柄。
	 * @param table - 表名。
	 * @param columnNames - 索引列名数组。
	 * @param unique - 是否为唯一索引，默认 `false`。
	 *
	 * @example
	 * Database.createIndex(db, "users", ["name"], true);
	 */
	static createIndex(connectionString: string, table: string, columnNames: string[], unique?: boolean): void;
}

/** `Database.createTable` 列定义对象。 */
interface ColumnDefinition {
	/** 列名。 */
	name: string;
	/** SQLite 类型（`TEXT`/`INTEGER`/`REAL`/`BLOB`），默认 `TEXT`。 */
	type?: string;
	/** 是否为主键。 */
	primaryKey?: boolean;
	/** 是否非空。 */
	notNull?: boolean;
	/** 默认值。 */
	defaultValue?: any;
}

/**
 * JSON 序列化桥接（.NET 实现）。
 */
declare class Json {
	/**
	 * 将值序列化为 JSON 字符串。
	 * @param value - 待序列化对象。
	 * @param pretty - 是否输出带缩进的可读格式。
	 * @returns JSON 字符串。
	 */
	static stringify(value: any, pretty?: boolean): string;
	/**
	 * 将 JSON 字符串解析为对象。
	 * @param json - JSON 文本。
	 * @returns 解析结果对象。
	 */
	static parse(json: string): any;
	/**
	 * 从文件读取并解析 JSON。
	 * @param filePath - JSON 文件路径。
	 * @returns 解析结果对象。
	 */
	static readFile(filePath: string): any;
	/**
	 * 序列化并写入 JSON 文件。
	 * @param filePath - 输出文件路径。
	 * @param value - 待序列化对象。
	 * @param pretty - 是否格式化输出。
	 */
	static writeFile(filePath: string, value: any, pretty?: boolean): void;
}

/**
 * AES 密钥对（Base64 编码）。
 *
 * 由 {@link PaperclipCryptoBridge.generateAesKey} 生成，
 * 可存储后传入 `aesEncryptWithKey` / `aesDecryptWithKey` 使用。
 */
interface AesKeyPair {
	/** AES-256 密钥，Base64 编码的 32 字节随机数据。 */
	key: string;
	/** 初始化向量（IV），Base64 编码的 16 字节随机数据；加解密必须使用相同 IV。 */
	iv: string;
}

/**
 * 加密与哈希工具桥接实例类型。
 */
interface PaperclipCryptoBridge {
	/**
	 * 使用默认密钥进行 AES 加密。
	 * @param plainText - 明文。
	 * @returns Base64 编码密文。
	 */
	aesEncrypt(plainText: string): string;
	/**
	 * 使用默认密钥进行 AES 解密。
	 * @param base64Cipher - Base64 编码密文。
	 * @returns 解密后的明文。
	 */
	aesDecrypt(base64Cipher: string): string;

	/**
	 * 使用自定义 key/iv 进行 AES 加密。
	 * @param plainText - 明文。
	 * @param keyBase64 - Base64 编码 AES Key。
	 * @param ivBase64 - Base64 编码 IV。
	 * @returns Base64 编码密文。
	 */
	aesEncryptWithKey(plainText: string, keyBase64: string, ivBase64: string): string;
	/**
	 * 使用自定义 key/iv 进行 AES 解密。
	 * @param base64Cipher - Base64 编码密文。
	 * @param keyBase64 - Base64 编码 AES Key。
	 * @param ivBase64 - Base64 编码 IV。
	 * @returns 解密后的明文。
	 */
	aesDecryptWithKey(base64Cipher: string, keyBase64: string, ivBase64: string): string;

	/**
	 * 生成 AES-256 密钥对。
	 * @returns 包含 `key` 与 `iv` 的 Base64 键值对象。
	 */
	generateAesKey(): AesKeyPair;

	/**
	 * 计算 SHA-256 哈希。
	 * @param input - 输入文本。
	 * @returns 十六进制小写哈希字符串。
	 */
	sha256(input: string): string;
	/**
	 * 计算 MD5 哈希。
	 * @param input - 输入文本。
	 * @returns 十六进制小写哈希字符串。
	 */
	md5(input: string): string;
	/**
	 * 计算 HMAC-SHA256 签名。
	 * @param input - 输入文本。
	 * @param keyBase64 - Base64 编码密钥。
	 * @returns Base64 编码签名。
	 */
	hmacSha256(input: string, keyBase64: string): string;

	/**
	 * Base64 编码。
	 * @param input - 原始文本。
	 * @returns Base64 字符串。
	 */
	base64Encode(input: string): string;
	/**
	 * Base64 解码。
	 * @param base64 - Base64 字符串。
	 * @returns 原始文本。
	 */
	base64Decode(base64: string): string;

	/**
	 * 生成随机字节。
	 * @param length - 字节长度，默认 32。
	 * @returns Base64 编码随机字节串。
	 */
	randomBytes(length?: number): string;
	/**
	 * 生成 UUID v4。
	 * @returns UUID 字符串。
	 */
	uuid(): string;
}

/**
 * 说明：运行时会注入全局对象 `Crypto`（大写），可按下述类型使用：
 * `const c = (globalThis as any).Crypto as PaperclipCryptoBridge;`
 */

// ── TS 友好类型包装 ─────────────────────────────────

/**
 * HTTP 请求对象，路由 handler 的入参类型。
 */
interface HttpRequest {
	/** HTTP 方法（GET/POST/PUT/DELETE/PATCH 等）。 */
	readonly method: string;
	/** 请求 URL 路径。 */
	readonly url: string;
	/** 请求头字典。 */
	readonly headers: Record<string, string>;
	/** 查询字符串参数字典。 */
	readonly query: Record<string, string>;
	/** 路由参数字典（如 /user/:id 中的 id）。 */
	readonly params: Record<string, string>;
	/** 请求体原始字符串。 */
	readonly body: string;
	/** 客户端 IP 地址。 */
	readonly remoteAddress: string;
	/** 会话对象，支持 get/set。 */
	readonly session: HttpSession;
}

/**
 * HTTP 会话对象。
 */
interface HttpSession {
	/**
	 * 获取会话值。
	 * @param key - 键名。
	 * @returns 对应值；不存在时返回空值。
	 */
	get(key: string): any;
	/**
	 * 设置会话值。
	 * @param key - 键名。
	 * @param value - 任意可序列化值。
	 */
	set(key: string, value: any): void;
	/**
	 * 删除会话中的指定键。
	 * @param key - 键名。
	 */
	remove(key: string): void;
	/** 清空当前会话中的全部键值。 */
	clear(): void;
	/** 会话 ID。 */
	readonly id: string;
}

/**
 * SSE 客户端信息。
 *
 * 可通过 {@link HttpServer.getSseClientCount} 查询连接数，
 * 或通过 {@link HttpServer.disconnectSseClient} 按 `clientId` 主动断开。
 */
interface SseClient {
	/** 客户端唯一标识（UUID 格式），可用于 {@link HttpServer.disconnectSseClient}。 */
	readonly clientId: string;
	/** 该 SSE 客户端连接的端点路径，如 `"/events"`。 */
	readonly path: string;
}

// ── C# 内置 SDK 桥接 ────────────────────────────────

/**
 * 目录操作桥接。
 *
 * @example
 * Directory.create("output/logs");
 * const files = Directory.getFiles(".", "*.ts", true);
 */
declare class Directory {
	/**
	 * 判断目录是否存在。
	 * @param path - 目录路径（相对或绝对）。
	 * @returns 目录存在返回 `true`，否则返回 `false`。
	 */
	static exists(path: string): boolean;
	/**
	 * 创建目录（含中间目录），返回绝对路径。
	 * @param path - 要创建的目录路径，支持多级嵌套，已存在时不报错。
	 * @returns 创建后的绝对路径。
	 */
	static create(path: string): string;
	/**
	 * 删除目录。
	 * @param path - 要删除的目录路径。
	 * @param recursive - `true` 同时删除所有子目录和文件；`false`（默认）仅删除空目录，非空时抛出异常。
	 */
	static delete(path: string, recursive?: boolean): void;
	/**
	 * 移动（重命名）目录。
	 * @param sourceDirName - 源目录路径。
	 * @param destDirName   - 目标目录路径；目标父级需已存在。
	 */
	static move(sourceDirName: string, destDirName: string): void;
	/**
	 * 获取目录下的文件列表。
	 * @param path          - 目标目录路径。
	 * @param searchPattern - 通配符模式，默认 `"*"`（匹配全部文件）；如 `"*.ts"` 只匹配 TS 文件。
	 * @param recursive     - `true` 递归子目录；`false`（默认）仅当前层级。
	 * @returns 匹配文件的绝对路径数组。
	 */
	static getFiles(path: string, searchPattern?: string, recursive?: boolean): string[];
	/**
	 * 获取子目录列表。
	 * @param path          - 目标目录路径。
	 * @param searchPattern - 通配符模式，默认 `"*"`。
	 * @param recursive     - `true` 递归搜索；`false`（默认）仅一级子目录。
	 * @returns 匹配子目录的绝对路径数组。
	 */
	static getDirectories(path: string, searchPattern?: string, recursive?: boolean): string[];
	/**
	 * 获取所有条目（文件 + 目录）。
	 * @param path          - 目标目录路径。
	 * @param searchPattern - 通配符模式，默认 `"*"`。
	 * @param recursive     - `true` 递归；`false`（默认）仅当前层级。
	 * @returns 匹配条目的绝对路径数组（文件与目录混合）。
	 */
	static getEntries(path: string, searchPattern?: string, recursive?: boolean): string[];
	/**
	 * 获取当前工作目录。
	 * @returns 当前工作目录的绝对路径。
	 */
	static getCurrent(): string;
	/**
	 * 设置当前工作目录。
	 * @param path - 目标目录路径；路径不存在时抛出异常。
	 */
	static setCurrent(path: string): void;
	/**
	 * 获取系统临时目录路径。
	 * @returns 系统 Temp 目录的绝对路径，如 `"C:\Users\xxx\AppData\Local\Temp"`。
	 */
	static getTempPath(): string;
	/**
	 * 在临时目录下创建唯一子目录，返回路径。
	 * @returns 已创建的临时子目录绝对路径（使用后应自行清理）。
	 */
	static createTemp(): string;
	/**
	 * 递归复制整个目录。
	 * @param sourceDir - 源目录路径。
	 * @param destDir   - 目标目录路径；不存在时自动创建。
	 * @param overwrite - `true` 覆盖目标中已存在的同名文件；默认 `false`（跳过冲突文件）。
	 */
	static copy(sourceDir: string, destDir: string, overwrite?: boolean): void;
	/**
	 * 获取目录总大小（字节）。
	 * @param path - 目录路径。
	 * @returns 目录下所有文件的累计字节数。
	 */
	static getSize(path: string): number;
}

/**
 * 路径操作桥接。
 *
 * @example
 * const full = Path.combine("src", "main.ts");
 * const ext = Path.getExtension("file.txt"); // ".txt"
 */
declare class Path {
	/**
	 * 拼接两个路径片段。
	 * @param path1 - 第一个路径片段。
	 * @param path2 - 第二个路径片段。
	 * @returns 使用系统分隔符拼接后的路径字符串。
	 */
	static combine(path1: string, path2: string): string;
	/**
	 * 拼接三个路径片段。
	 * @param path1 - 第一个路径片段。
	 * @param path2 - 第二个路径片段。
	 * @param path3 - 第三个路径片段。
	 * @returns 使用系统分隔符拼接后的路径字符串。
	 */
	static combine3(path1: string, path2: string, path3: string): string;
	/**
	 * 获取绝对路径。
	 * @param path - 相对或绝对路径。
	 * @returns 基于当前工作目录解析后的绝对路径。
	 */
	static getFullPath(path: string): string;
	/**
	 * 获取文件名（含扩展名）。
	 * @param path - 文件路径。
	 * @returns 文件名部分，如 `"readme.md"`。
	 */
	static getFileName(path: string): string;
	/**
	 * 获取文件名（不含扩展名）。
	 * @param path - 文件路径。
	 * @returns 不含扩展名的文件名，如 `"readme"`。
	 */
	static getFileNameWithoutExtension(path: string): string;
	/**
	 * 获取扩展名（含 `'.'`）。
	 * @param path - 文件路径。
	 * @returns 扩展名字符串，如 `".ts"`；无扩展名时返回空字符串。
	 */
	static getExtension(path: string): string;
	/**
	 * 获取父目录路径。
	 * @param path - 文件或目录路径。
	 * @returns 父目录的路径字符串；路径为根目录时返回 `null`。
	 */
	static getDirectoryName(path: string): string | null;
	/**
	 * 修改扩展名。
	 * @param path      - 文件路径。
	 * @param extension - 新扩展名（含 `'.'`，如 `".js"`）；传空字符串可移除扩展名。
	 * @returns 修改了扩展名后的路径字符串。
	 */
	static changeExtension(path: string, extension: string): string;
	/**
	 * 判断路径是否有扩展名。
	 * @param path - 文件路径。
	 * @returns 有扩展名返回 `true`，否则返回 `false`。
	 */
	static hasExtension(path: string): boolean;
	/**
	 * 判断是否为绝对路径。
	 * @param path - 路径字符串。
	 * @returns `true` 表示是绝对路径（以根目录或驱动器字母开头）。
	 */
	static isPathRooted(path: string): boolean;
	/**
	 * 获取路径根部分。
	 * @param path - 文件路径。
	 * @returns 路径的根部分，如 `"C:\\"` 或 `"/"`；无根时返回 `null`。
	 */
	static getPathRoot(path: string): string | null;
	/**
	 * 生成唯一临时文件路径（文件已在磁盘上创建为空文件）。
	 * @returns 系统临时目录下的唯一文件绝对路径。
	 */
	static getTempFileName(): string;
	/**
	 * 生成随机文件名（不含路径，不在磁盘创建文件）。
	 * @returns 类似 `"tmp3j4k5l"` 的随机文件名字符串。
	 */
	static getRandomFileName(): string;
	/**
	 * 获取系统临时目录。
	 * @returns 系统 Temp 目录的绝对路径。
	 */
	static getTempPath(): string;
	/**
	 * 获取相对路径。
	 * @param relativeTo - 基准路径（通常为某目录的绝对路径）。
	 * @param path       - 目标路径（绝对路径）。
	 * @returns `path` 相对于 `relativeTo` 的相对路径字符串。
	 */
	static getRelativePath(relativeTo: string, path: string): string;
	/**
	 * 获取目录分隔符。
	 * @returns Windows 返回 `"\\"`, Linux/macOS 返回 `"/"`。
	 */
	static directorySeparator(): string;
	/**
	 * 规范化路径（处理 `..`、`.`、多余分隔符）。
	 * @param path - 需要规范化的路径字符串。
	 * @returns 规范化后的路径。
	 */
	static normalize(path: string): string;
}

/**
 * 环境信息桥接。
 *
 * @example
 * const home = Env.userHome();
 * Env.setVariable("MY_KEY", "value");
 */
declare class Env {
	/**
	 * 获取环境变量值。
	 * @param name - 环境变量名称（大小写敏感，Windows 下不区分大小写）。
	 * @returns 变量值字符串；未设置时返回 `null`。
	 */
	static getVariable(name: string): string | null;
	/**
	 * 设置环境变量（仅当前进程，不影响系统全局）。
	 * @param name  - 环境变量名称。
	 * @param value - 变量值；传 `null` 表示删除该变量。
	 */
	static setVariable(name: string, value: string | null): void;
	/**
	 * 获取所有环境变量。
	 * @returns 包含所有当前进程环境变量的键值字典。
	 */
	static getAllVariables(): Record<string, string>;
	/**
	 * 获取操作系统描述。
	 * @returns 如 `"Microsoft Windows 11.0.22631"` 或 `"Ubuntu 22.04.3 LTS"`。
	 */
	static osDescription(): string;
	/**
	 * 获取系统架构。
	 * @returns 如 `"X64"`、`"Arm64"` 等 .NET `RuntimeInformation.OSArchitecture` 字符串。
	 */
	static osArchitecture(): string;
	/**
	 * 获取处理器数量。
	 * @returns 逻辑处理器（线程）数量，对应 `Environment.ProcessorCount`。
	 */
	static processorCount(): number;
	/**
	 * 获取机器名。
	 * @returns 当前计算机的 NetBIOS / 主机名。
	 */
	static machineName(): string;
	/**
	 * 获取当前用户名。
	 * @returns 运行此进程的操作系统用户名。
	 */
	static userName(): string;
	/**
	 * 获取 .NET 运行时版本。
	 * @returns 如 `"9.0.0"` 的版本字符串。
	 */
	static runtimeVersion(): string;
	/**
	 * 判断是否为 64 位操作系统。
	 * @returns `true` 表示当前 OS 为 64 位。
	 */
	static is64BitOS(): boolean;
	/**
	 * 判断是否为 64 位进程。
	 * @returns `true` 表示当前进程以 64 位模式运行。
	 */
	static is64BitProcess(): boolean;
	/**
	 * 获取系统运行时间（毫秒）。
	 * @returns 系统启动后经过的毫秒数（`Environment.TickCount64`），溢出约 49 天后从 0 重新计数。
	 */
	static tickCount(): number;
	/**
	 * 获取系统换行符。
	 * @returns Windows 返回 `"\r\n"`，Linux/macOS 返回 `"\n"`。
	 */
	static newLine(): string;
	/**
	 * 获取用户主目录。
	 * @returns 如 `"C:\Users\Alice"` 或 `"/home/alice"`。
	 */
	static userHome(): string;
	/**
	 * 获取桌面路径。
	 * @returns 当前用户桌面的绝对路径（`SpecialFolder.Desktop`）。
	 */
	static desktop(): string;
	/**
	 * 获取文档目录。
	 * @returns 当前用户"我的文档"目录的绝对路径（`SpecialFolder.MyDocuments`）。
	 */
	static documents(): string;
	/**
	 * 获取 AppData（漫游）路径。
	 * @returns 如 `"C:\Users\Alice\AppData\Roaming"`（`SpecialFolder.ApplicationData`）。
	 */
	static appData(): string;
	/**
	 * 获取 LocalAppData 路径。
	 * @returns 如 `"C:\Users\Alice\AppData\Local"`（`SpecialFolder.LocalApplicationData`）。
	 */
	static localAppData(): string;
	/**
	 * 获取临时目录路径。
	 * @returns 系统 Temp 目录绝对路径，与 `Path.getTempPath()` 等价。
	 */
	static tempPath(): string;
	/**
	 * 获取命令行参数数组。
	 * @returns 进程启动时传入的所有参数（包含可执行文件名作为第一个元素）。
	 */
	static commandLineArgs(): string[];
	/**
	 * 获取当前进程 ID。
	 * @returns 当前进程的操作系统 PID。
	 */
	static processId(): number;
}

/** 进程执行结果，由 {@link Process.run}/{@link Process.exec} 系列方法返回。 */
interface ProcessResult {
	/** 进程退出码；`0` 通常表示成功，非 `0` 表示各种错误（具体含义由程序定义）。 */
	exitCode: number;
	/** 进程标准输出（stdout）的全部内容；未产生输出时为空字符串。 */
	stdout: string;
	/** 进程标准错误（stderr）的全部内容；无错误输出时为空字符串。 */
	stderr: string;
}

/**
 * 进程操作桥接。
 *
 * @example
 * const result = Process.exec("echo hello");
 * print(result.stdout); // "hello\n"
 */
declare class Process {
	/**
	 * 启动外部进程并等待完成。
	 * @param fileName - 可执行文件名或路径。
	 * @param arguments - 命令行参数。
	 */
	static run(fileName: string, arguments?: string): ProcessResult;
	/**
	 * 异步启动外部进程并等待完成。
	 * @param fileName - 可执行文件。
	 * @param arguments - 命令行参数。
	 */
	static runAsync(fileName: string, arguments?: string): Promise<ProcessResult>;
	/**
	 * 执行 shell 命令（Windows: cmd /c, Linux: /bin/sh -c）。
	 * @param command - Shell 命令字符串。
	 */
	static exec(command: string): ProcessResult;
	/**
	 * 异步执行 shell 命令。
	 * @param command - Shell 命令字符串。
	 */
	static execAsync(command: string): Promise<ProcessResult>;
	/**
	 * 启动后台进程（不等待完成），返回进程 ID。
	 * @param fileName - 可执行文件。
	 * @param arguments - 命令行参数。
	 */
	static start(fileName: string, arguments?: string): number;
	/**
	 * 终止指定进程。
	 * @param processId - 进程 ID。
	 */
	static kill(processId: number): void;
	/**
	 * 判断进程是否运行中。
	 * @param processId - 进程 ID。
	 */
	static isRunning(processId: number): boolean;
}

/** 正则匹配结果，由 {@link Regex.match}/{@link Regex.matches} 返回。 */
interface RegexMatch {
	/** 是否存在有效匹配；`false` 时其余字段为默认值，通常不应使用。 */
	success: boolean;
	/** 整个匹配项的文本内容。 */
	value: string;
	/** 匹配项在原始字符串中的起始索引（从 `0` 开始）。 */
	index: number;
	/** 匹配项的字符长度。 */
	length: number;
	/**
	 * 命名/编号捕获组字典。
	 *
	 * 键为组名（命名组 `(?<name>...)` 使用组名，编号组使用 `"1"`、`"2"` 等）。
	 * 值为捕获文本；未参与匹配的可选组为 `null`。
	 */
	groups: Record<string, string | null>;
}

/**
 * .NET 正则表达式桥接。
 *
 * 与 JS 原生正则不同，使用 .NET 引擎，支持命名组、Lookbehind 等高级特性。
 *
 * @example
 * const m = Regex.match("hello world", "(?<word>\\w+)");
 * print(m?.groups?.word); // "hello"
 */
declare class Regex {
	/**
	 * 判断是否匹配。
	 * @param input      - 待检测字符串。
	 * @param pattern    - .NET 正则表达式模式。
	 * @param ignoreCase - `true` 忽略大小写；默认 `false`。
	 * @returns 有任意匹配返回 `true`，否则返回 `false`。
	 */
	static isMatch(input: string, pattern: string, ignoreCase?: boolean): boolean;
	/**
	 * 返回第一个匹配结果，无匹配返回 `null`。
	 * @param input      - 待搜索字符串。
	 * @param pattern    - .NET 正则表达式模式。
	 * @param ignoreCase - `true` 忽略大小写；默认 `false`。
	 * @returns 匹配结果对象（含 `value`、`index`、`groups`），无匹配时返回 `null`。
	 */
	static match(input: string, pattern: string, ignoreCase?: boolean): RegexMatch | null;
	/**
	 * 返回所有匹配结果。
	 * @param input      - 待搜索字符串。
	 * @param pattern    - .NET 正则表达式模式。
	 * @param ignoreCase - `true` 忽略大小写；默认 `false`。
	 * @returns {@link RegexMatch} 数组；无任何匹配时返回空数组。
	 */
	static matches(input: string, pattern: string, ignoreCase?: boolean): RegexMatch[];
	/**
	 * 正则替换。
	 * @param input       - 原始字符串。
	 * @param pattern     - .NET 正则表达式模式。
	 * @param replacement - 替换文本，支持 `$1`、`${name}` 等反向引用。
	 * @param ignoreCase  - `true` 忽略大小写；默认 `false`。
	 * @returns 替换后的新字符串。
	 */
	static replace(input: string, pattern: string, replacement: string, ignoreCase?: boolean): string;
	/**
	 * 正则分割。
	 * @param input      - 待分割字符串。
	 * @param pattern    - 用作分隔符的 .NET 正则表达式模式。
	 * @param ignoreCase - `true` 忽略大小写；默认 `false`。
	 * @returns 分割后的子字符串数组。
	 */
	static split(input: string, pattern: string, ignoreCase?: boolean): string[];
	/**
	 * 转义正则特殊字符。
	 * @param input - 需要转义的原始字符串（如用户输入）。
	 * @returns 所有正则元字符（`\.+*?^${}[]|()`）均被 `\` 转义后的安全字符串。
	 */
	static escape(input: string): string;
	/**
	 * 反转义（将 `\` 转义序列还原为原始字符）。
	 * @param input - 由 `escape` 转义过的字符串。
	 * @returns 还原后的原始字符串。
	 */
	static unescape(input: string): string;
}

/**
 * 类型转换与日期时间桥接。
 *
 * @example
 * const ts = Convert.unixTimestamp();
 * const hex = Convert.toHex(255); // "FF"
 */
declare class Convert {
	/**
	 * 转换为 32 位整数（`int`）。
	 * @param value - 任意值；字符串将被解析，浮点数将截断小数部分。
	 * @returns 32 位有符号整数；转换失败时行为由宿主决定（通常返回 `0`）。
	 */
	static toInt(value: any): number;
	/**
	 * 转换为 64 位整数（`long`）。
	 * @param value - 任意值。
	 * @returns 64 位有符号整数。
	 */
	static toLong(value: any): number;
	/**
	 * 转换为浮点数（`double`）。
	 * @param value - 任意值；字符串将被解析为浮点数。
	 * @returns 双精度浮点数；转换失败时通常返回 `0`。
	 */
	static toDouble(value: any): number;
	/**
	 * 转换为布尔值。
	 * @param value - 任意值；字符串 `"true"/"false"` 不区分大小写均可识别。
	 * @returns 对应的布尔值。
	 */
	static toBool(value: any): boolean;
	/**
	 * 转换为字符串。
	 * @param value - 任意值，等同于 `.ToString()`。
	 * @returns 字符串表示。
	 */
	static toString(value: any): string;
	/**
	 * 整数转十六进制字符串（大写，无前缀）。
	 * @param value - 整数值。
	 * @returns 大写十六进制字符串，如 `255` → `"FF"`。
	 */
	static toHex(value: number): string;
	/**
	 * 十六进制字符串转整数。
	 * @param hex - 十六进制字符串，可含或不含 `0x` 前缀，大小写均可。
	 * @returns 解析后的整数值。
	 */
	static fromHex(hex: string): number;
	/**
	 * 整数转二进制字符串（无前缀）.
	 * @param value - 整数值。
	 * @returns 二进制字符串，如 `10` → `"1010"`。
	 */
	static toBinary(value: number): string;
	/**
	 * 二进制字符串转整数。
	 * @param binary - 二进制字符串（仅含 `0` 和 `1`）。
	 * @returns 解析后的整数值。
	 */
	static fromBinary(binary: string): number;
	/**
	 * 整数转八进制字符串（无前缀）。
	 * @param value - 整数值。
	 * @returns 八进制字符串，如 `8` → `"10"`。
	 */
	static toOctal(value: number): string;
	/**
	 * 八进制字符串转整数。
	 * @param octal - 八进制字符串（仅含 `0-7`）。
	 * @returns 解析后的整数值。
	 */
	static fromOctal(octal: string): number;
	/**
	 * 获取当前 UTC 时间 ISO 8601 字符串。
	 * @returns 如 `"2026-03-10T08:30:00.000Z"` 的 UTC 时间字符串。
	 */
	static nowUtc(): string;
	/**
	 * 获取当前本地时间 ISO 8601 字符串。
	 * @returns 含时区偏移的本地时间字符串，如 `"2026-03-10T16:30:00.000+08:00"`。
	 */
	static now(): string;
	/**
	 * 获取 Unix 时间戳（秒）。
	 * @returns 自 1970-01-01 00:00:00 UTC 起经过的秒数（整数）。
	 */
	static unixTimestamp(): number;
	/**
	 * 获取 Unix 时间戳（毫秒）。
	 * @returns 自 1970-01-01 00:00:00 UTC 起经过的毫秒数。
	 */
	static unixTimestampMs(): number;
	/**
	 * ISO 8601 字符串转 Unix 时间戳（秒）。
	 * @param dateString - ISO 8601 格式的日期时间字符串。
	 * @returns 对应的 Unix 秒级时间戳；解析失败时抛出异常。
	 */
	static parseToUnix(dateString: string): number;
	/**
	 * Unix 时间戳（秒）转 ISO 8601 字符串。
	 * @param unixSeconds - Unix 秒级时间戳。
	 * @returns UTC 时间的 ISO 8601 字符串。
	 */
	static fromUnix(unixSeconds: number): string;
	/**
	 * 格式化日期字符串。
	 * @param dateString - ISO 8601 格式的日期时间字符串。
	 * @param format     - .NET 日期格式字符串，如 `"yyyy-MM-dd HH:mm:ss"`。
	 * @returns 按指定格式输出的日期字符串。
	 */
	static formatDate(dateString: string, format: string): string;
	/**
	 * 安全解析整数（失败返回默认值而非抛出）。
	 * @param value        - 待解析字符串。
	 * @param defaultValue - 解析失败时的默认值，默认为 `0`。
	 * @returns 解析结果或 `defaultValue`。
	 */
	static tryParseInt(value: string, defaultValue?: number): number;
	/**
	 * 安全解析浮点数（失败返回默认值而非抛出）。
	 * @param value        - 待解析字符串。
	 * @param defaultValue - 解析失败时的默认值，默认为 `0`。
	 * @returns 解析结果或 `defaultValue`。
	 */
	static tryParseDouble(value: string, defaultValue?: number): number;
	/**
	 * 安全解析布尔值（失败返回默认值而非抛出）。
	 * @param value        - 待解析字符串。
	 * @param defaultValue - 解析失败时的默认值，默认为 `false`。
	 * @returns 解析结果或 `defaultValue`。
	 */
	static tryParseBool(value: string, defaultValue?: boolean): boolean;
}

/** Stopwatch 计时器句柄。 */
interface Stopwatch {}

/**
 * 计时器与延时桥接。
 *
 * @example
 * const sw = Timer.startNew();
 * // ... do work ...
 * Timer.stop(sw);
 * print(`耗时 ${Timer.elapsedMs(sw)} ms`);
 */
declare class Timer {
	/**
	 * 创建并启动计时器。
	 * @returns 已启动的 {@link Stopwatch} 句柄，可传入其他 `Timer.*` 方法使用。
	 */
	static startNew(): Stopwatch;
	/**
	 * 获取已用时间（毫秒）。
	 * @param sw - 由 `startNew` 创建的计时器句柄。
	 * @returns 自启动或上次 `restart` 起经过的毫秒数（浮点精度）。
	 */
	static elapsedMs(sw: Stopwatch): number;
	/**
	 * 获取已用时间（秒）。
	 * @param sw - 计时器句柄。
	 * @returns 自启动或上次 `restart` 起经过的秒数（浮点精度）。
	 */
	static elapsedSeconds(sw: Stopwatch): number;
	/**
	 * 停止计时。
	 * @param sw - 计时器句柄。已停止的计时器再次调用无副作用。
	 */
	static stop(sw: Stopwatch): void;
	/**
	 * 重置并重新启动。
	 * @param sw - 计时器句柄。将已用时间清零并立即重新计时。
	 */
	static restart(sw: Stopwatch): void;
	/**
	 * 重置计时器（不重新启动）。
	 * @param sw - 计时器句柄。将已用时间清零，计时器进入已停止状态。
	 */
	static reset(sw: Stopwatch): void;
	/**
	 * 获取高精度时间戳（`Stopwatch.GetTimestamp()` 原始刻度值）。
	 *
	 * 适合两次调用做差后计算精确耗时，精度高于 `Date.now()`。
	 * @returns 当前高精度计时器的刻度值；需结合 `Stopwatch.Frequency` 换算为秒。
	 */
	static getTimestamp(): number;
	/**
	 * 异步延时指定毫秒。
	 * @param milliseconds - 延时毫秒数（正整数）。
	 * @returns Promise，延时结束后 resolve。
	 * @example
	 * await Timer.delay(500); // 等待 0.5 秒
	 */
	static delay(milliseconds: number): Promise<void>;
	/**
	 * 同步阻塞延时（慎用）。
	 *
	 * 会阻塞当前线程，在异步场景中应优先使用 `delay`。
	 * @param milliseconds - 阻塞毫秒数。
	 */
	static sleep(milliseconds: number): void;
	/**
	 * 获取当前高精度时间（毫秒），用于 diff 计算。
	 * @returns 基于高精度计时器的当前毫秒数，适合 `const t0 = Timer.nowMs(); ... elapsed = Timer.nowMs() - t0` 模式。
	 */
	static nowMs(): number;
}

// ── 文件 I/O 桥接 ────────────────────────────────────

/**
 * 文件流句柄（底层 FileStream）。
 *
 * 通过 {@link FileIO.openStream} 创建，使用完毕须调用 {@link DrxFileStream.close}。
 */
declare class DrxFileStream {
	/** 流当前位置（字节偏移）。 */
	position: number;
	/** 流总长度（字节）。 */
	readonly length: number;
	/** 是否可读。 */
	readonly canRead: boolean;
	/** 是否可写。 */
	readonly canWrite: boolean;
	/** 是否可定位（Seek）。 */
	readonly canSeek: boolean;

	/**
	 * 读取字节，返回 Base64 字符串；到达末尾时返回空字符串。
	 * @param count - 最多读取的字节数，默认 4096。
	 */
	readBytes(count?: number): string;

	/**
	 * 将 Base64 字节写入流。
	 * @param base64 - Base64 编码的字节数据。
	 */
	writeBytes(base64: string): void;

	/**
	 * 定位流到指定位置。
	 * @param offset - 字节偏移量。
	 * @param origin - 基准位置：`"begin"`（默认）/ `"current"` / `"end"`。
	 * @returns 新位置（字节偏移）。
	 */
	seek(offset: number, origin?: "begin" | "current" | "end"): number;

	/** 刷新缓冲区到磁盘。 */
	flush(): void;

	/**
	 * 截断或扩展流到指定长度。
	 * @param length - 目标字节长度。
	 */
	setLength(length: number): void;

	/**
	 * 异步读取字节，返回 Base64 字符串。
	 * @param count - 最多读取的字节数，默认 4096。
	 */
	readBytesAsync(count?: number): Promise<string>;

	/**
	 * 异步写入 Base64 字节。
	 * @param base64 - Base64 编码的字节数据。
	 */
	writeBytesAsync(base64: string): Promise<void>;

	/** 关闭并释放流资源。 */
	close(): void;
}

/**
 * 文本读取器句柄（底层 StreamReader）。
 *
 * 通过 {@link FileIO.openReader} 创建，使用完毕须调用 {@link DrxStreamReader.close}。
 *
 * @example
 * const reader = FileIO.openReader("data.txt");
 * while (!reader.endOfStream) {
 *   const line = reader.readLine();
 *   print(line);
 * }
 * reader.close();
 */
declare class DrxStreamReader {
	/** 是否已到流末尾。 */
	readonly endOfStream: boolean;

	/**
	 * 读取一行文本，到达末尾返回 `null`。
	 * @returns 一行文本或 `null`。
	 */
	readLine(): string | null;

	/**
	 * 读取所有剩余文本。
	 * @returns 剩余全部文本字符串。
	 */
	readToEnd(): string;

	/**
	 * 异步读取一行文本。
	 * @returns Promise，完成后返回一行文本或 `null`。
	 */
	readLineAsync(): Promise<string | null>;

	/**
	 * 异步读取所有剩余文本。
	 * @returns Promise，完成后返回所有剩余文本。
	 */
	readToEndAsync(): Promise<string>;

	/** 关闭读取器。 */
	close(): void;
}

/**
 * 文本写入器句柄（底层 StreamWriter）。
 *
 * 通过 {@link FileIO.openWriter} 创建，使用完毕须调用 {@link DrxStreamWriter.close}。
 *
 * @example
 * const writer = FileIO.openWriter("log.txt", true); // 追加模式
 * writer.writeLine("hello world");
 * writer.close();
 */
declare class DrxStreamWriter {
	/**
	 * 是否自动刷新（每次写入后立即 flush）。
	 * 默认 `false`；高频写入场景关闭以提升性能。
	 */
	autoFlush: boolean;

	/**
	 * 写入文本（不换行）。
	 * @param text - 要写入的文本。
	 */
	write(text: string): void;

	/**
	 * 写入文本并附加换行符。
	 * @param text - 要写入的文本，省略则只写入换行。
	 */
	writeLine(text?: string): void;

	/**
	 * 异步写入文本。
	 * @param text - 要写入的文本。
	 */
	writeAsync(text: string): Promise<void>;

	/**
	 * 异步写入文本并换行。
	 * @param text - 要写入的文本，省略则只写换行。
	 */
	writeLineAsync(text?: string): Promise<void>;

	/** 刷新写入器缓冲区。 */
	flush(): void;

	/** 关闭写入器。 */
	close(): void;
}

/**
 * 文件操作桥接。
 *
 * 提供文件读写、复制、移动、信息查询以及流打开等静态方法。
 * 字节操作统一使用 Base64 编码传递，避免二进制数据跨桥接层丢失。
 *
 * @example
 * // 简单读写
 * const text = FileIO.readAllText("config.json");
 * FileIO.writeAllText("output.txt", "Hello");
 *
 * // 逐行读取（流式）
 * const reader = FileIO.openReader("big.log");
 * while (!reader.endOfStream) print(reader.readLine());
 * reader.close();
 *
 * // 二进制写入
 * const stream = FileIO.openStream("data.bin", "write");
 * stream.writeBytes(someBase64);
 * stream.close();
 */
declare class FileIO {
	// ── 存在性 ────────────────────────────────────────

	/**
	 * 判断文件是否存在。
	 * @param path - 文件路径（相对或绝对）。
	 * @returns 文件存在返回 `true`，否则返回 `false`。
	 */
	static exists(path: string): boolean;

	// ── 读取 ──────────────────────────────────────────

	/**
	 * 读取文件的全部文本内容。
	 * @param path     - 文件路径。
	 * @param encoding - 编码名称，默认 `"utf-8"`；支持 `"ascii"` / `"unicode"` / `"utf-32"` 等。
	 * @returns 文件的全部文本。
	 *
	 * @example
	 * const content = File.readAllText("readme.md");
	 */
	static readAllText(path: string, encoding?: string | null): string;

	/**
	 * 读取文件的所有行，返回字符串数组。
	 * @param path     - 文件路径。
	 * @param encoding - 编码名称，默认 `"utf-8"`。
	 * @returns 每行文本组成的数组。
	 *
	 * @example
	 * const lines = File.readAllLines("data.csv");
	 */
	static readAllLines(path: string, encoding?: string | null): string[];

	/**
	 * 读取文件的全部字节，以 Base64 字符串返回。
	 * @param path - 文件路径。
	 * @returns Base64 编码的文件字节数据。
	 */
	static readAllBytesBase64(path: string): string;

	// ── 写入 ──────────────────────────────────────────

	/**
	 * 将文本写入文件（覆盖现有内容，不存在则创建）。
	 * @param path     - 文件路径。
	 * @param content  - 要写入的文本。
	 * @param encoding - 编码名称，默认 `"utf-8"`（无 BOM）。
	 *
	 * @example
	 * File.writeAllText("out.txt", "Hello, World!");
	 */
	static writeAllText(path: string, content: string, encoding?: string | null): void;

	/**
	 * 将字符串数组写入文件（每个元素占一行，覆盖）。
	 * @param path     - 文件路径。
	 * @param lines    - 要写入的行数组。
	 * @param encoding - 编码名称，默认 `"utf-8"`。
	 */
	static writeAllLines(path: string, lines: string[], encoding?: string | null): void;

	/**
	 * 将 Base64 字节数据写入文件（覆盖）。
	 * @param path   - 文件路径。
	 * @param base64 - Base64 编码的字节数据。
	 */
	static writeAllBytesBase64(path: string, base64: string): void;

	// ── 追加 ──────────────────────────────────────────

	/**
	 * 向文件末尾追加文本（不存在则创建）。
	 * @param path     - 文件路径。
	 * @param content  - 要追加的文本。
	 * @param encoding - 编码名称，默认 `"utf-8"`。
	 *
	 * @example
	 * File.appendAllText("log.txt", `[${new Date().toISOString()}] Event\n`);
	 */
	static appendAllText(path: string, content: string, encoding?: string | null): void;

	/**
	 * 向文件末尾追加多行（不存在则创建）。
	 * @param path     - 文件路径。
	 * @param lines    - 要追加的行数组。
	 * @param encoding - 编码名称，默认 `"utf-8"`。
	 */
	static appendAllLines(path: string, lines: string[], encoding?: string | null): void;

	// ── 异步读写 ──────────────────────────────────────

	/**
	 * 异步读取文件的全部文本内容。
	 * @param path     - 文件路径。
	 * @param encoding - 编码名称，默认 `"utf-8"`。
	 * @returns Promise，完成后返回文件全部文本。
	 */
	static readAllTextAsync(path: string, encoding?: string | null): Promise<string>;

	/**
	 * 异步将文本写入文件（覆盖）。
	 * @param path     - 文件路径。
	 * @param content  - 要写入的文本。
	 * @param encoding - 编码名称，默认 `"utf-8"`。
	 * @returns Promise，写入完成后 resolve。
	 */
	static writeAllTextAsync(path: string, content: string, encoding?: string | null): Promise<void>;

	/**
	 * 异步向文件末尾追加文本。
	 * @param path     - 文件路径。
	 * @param content  - 要追加的文本。
	 * @param encoding - 编码名称，默认 `"utf-8"`。
	 * @returns Promise，追加完成后 resolve。
	 */
	static appendAllTextAsync(path: string, content: string, encoding?: string | null): Promise<void>;

	// ── 管理 ──────────────────────────────────────────

	/**
	 * 复制文件。
	 * @param sourceFileName - 源文件路径。
	 * @param destFileName   - 目标文件路径。
	 * @param overwrite      - 目标存在时是否覆盖，默认 `false`。
	 *
	 * @example
	 * File.copy("src/app.ts", "backup/app.ts", true);
	 */
	static copy(sourceFileName: string, destFileName: string, overwrite?: boolean): void;

	/**
	 * 移动（重命名）文件。
	 * @param sourceFileName - 源文件路径。
	 * @param destFileName   - 目标文件路径。
	 * @param overwrite      - 目标存在时是否覆盖，默认 `false`。
	 */
	static move(sourceFileName: string, destFileName: string, overwrite?: boolean): void;

	/**
	 * 删除文件（文件不存在时不报错）。
	 * @param path - 要删除的文件路径。
	 */
	static delete(path: string): void;

	// ── 信息 ──────────────────────────────────────────

	/**
	 * 获取文件大小（字节）。
	 * @param path - 文件路径。
	 * @returns 文件字节数。
	 */
	static getSize(path: string): number;

	/**
	 * 获取文件创建时间（ISO 8601 本地时间字符串）。
	 * @param path - 文件路径。
	 * @returns 如 `"2026-03-10T16:00:00.0000000+08:00"`。
	 */
	static getCreationTime(path: string): string;

	/**
	 * 获取文件最后修改时间（ISO 8601 本地时间字符串）。
	 * @param path - 文件路径。
	 */
	static getLastWriteTime(path: string): string;

	/**
	 * 获取文件最后访问时间（ISO 8601 本地时间字符串）。
	 * @param path - 文件路径。
	 */
	static getLastAccessTime(path: string): string;

	// ── 流 ───────────────────────────────────────────

	/**
	 * 打开文件流（二进制）。
	 *
	 * @param path - 文件路径。
	 * @param mode - 打开模式：
	 *   - `"read"`（默认）— 只读，文件须存在。
	 *   - `"write"` — 只写，覆盖创建。
	 *   - `"append"` — 追加写，不存在则创建。
	 *   - `"readWrite"` — 读写，不存在则创建。
	 * @returns 文件流句柄 {@link DrxFileStream}。
	 *
	 * @example
	 * const s = FileIO.openStream("data.bin", "write");
	 * s.writeBytes(base64Data);
	 * s.close();
	 */
	static openStream(path: string, mode?: "read" | "write" | "append" | "readWrite"): DrxFileStream;

	/**
	 * 打开文本读取器（StreamReader）。
	 *
	 * @param path     - 文件路径（须存在）。
	 * @param encoding - 编码名称，默认 `"utf-8"`。
	 * @returns 文本读取器句柄 {@link DrxStreamReader}。
	 *
	 * @example
	 * const reader = FileIO.openReader("big.log");
	 * while (!reader.endOfStream) print(reader.readLine());
	 * reader.close();
	 */
	static openReader(path: string, encoding?: string | null): DrxStreamReader;

	/**
	 * 打开文本写入器（StreamWriter）。
	 *
	 * @param path     - 文件路径。
	 * @param append   - `true` 追加模式；`false`（默认）覆盖创建。
	 * @param encoding - 编码名称，默认 `"utf-8"`（无 BOM）。
	 * @returns 文本写入器句柄 {@link DrxStreamWriter}。
	 *
	 * @example
	 * const writer = FileIO.openWriter("output.log", true);
	 * writer.writeLine("started");
	 * writer.close();
	 */
	static openWriter(path: string, append?: boolean, encoding?: string | null): DrxStreamWriter;
}

/**
 * 全局日志工具类。
 *
 * 提供分级日志输出（debug/info/warn/error）及日志级别控制。
 * 调用 `Logger.setLevel("error")` 可静默所有非错误日志，大幅提升服务器吞吐性能。
 *
 * @example
 * Logger.setLevel("error"); // 在创建服务器前调用，隐藏初始化日志
 * Logger.info("this will be hidden");
 * Logger.error("this will still show");
 */
declare class Logger {
	/** 输出 INFO 级别日志。 */
	static info(message: any): void;
	/** 输出 WARN 级别日志。 */
	static warn(message: any): void;
	/** 输出 ERROR 级别日志。 */
	static error(message: any): void;
	/** 输出 DEBUG 级别日志。 */
	static debug(message: any): void;
	/** 输出 INFO 级别日志（log 为 info 的别名）。 */
	static log(message: any): void;

	/** 异步输出 INFO 级别日志。 */
	static infoAsync(message: any): Promise<void>;
	/** 异步输出 WARN 级别日志。 */
	static warnAsync(message: any): Promise<void>;
	/** 异步输出 ERROR 级别日志。 */
	static errorAsync(message: any): Promise<void>;
	/** 异步输出 DEBUG 级别日志。 */
	static debugAsync(message: any): Promise<void>;
	/** 异步输出 INFO 级别日志。 */
	static logAsync(message: any): Promise<void>;

	/**
	 * 设置全局日志最低级别。低于此级别的日志将被静默丢弃。
	 *
	 * 支持值：`"debug"` | `"info"` | `"warn"` | `"error"` | `"fatal"` | `"none"`
	 *
	 * @param level - 日志级别字符串。设为 `"error"` 可仅保留错误日志。
	 *
	 * @example
	 * Logger.setLevel("error"); // 仅输出 error/fatal
	 * Logger.setLevel("debug"); // 恢复全部输出
	 */
	static setLevel(level: "debug" | "info" | "warn" | "error" | "fatal" | "none"): void;
}
