(function() {
  const exports = {};
  const module = { exports: exports };
  const require = function(id) {
    if (typeof globalThis.__drxRequireNative === 'function') {
      return globalThis.__drxRequireNative(id);
    }
    throw new Error('[CJS] require() is not available in this context: ' + id);
  };
// ─────────────────────────────────────────────────────────────────────────────
// Paperclip HTTP 
// KaxHub
// ─────────────────────────────────────────────────────────────────────────────
async function main() {
    // 创建服务器实例（直接 new + 单前缀）
    const server = new HttpServer("http://localhost:8462/");
    print(getdir() + "\n");
    // 链式配置
    server
        .debugMode(true)
        .setFileRoot(getdir())
        .setViewRoot(getdir() + "/views/html")
        .setRateLimit(120, 1, "minutes");
    server.get("/index", (req) => {
        return HttpResponse.file("index.html");
    });
    // 启动服务器
    try {
        await server.startAsync();
        print("Server stopped.\n");
    }
    catch (err) {
        console.error(`Server error: ${err}`);
        server.disposeAsync().catch(() => { });
    }
}

  return module.exports;
})();
//# sourceURL=D:/Code/demo/main.ts