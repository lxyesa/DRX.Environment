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
// Paperclip HTTP Server Demo
// 使用实例方法 + 直接函数引用的简洁 API
// ─────────────────────────────────────────────────────────────────────────────
async function main() {
    // 创建服务器实例（直接 new + 单前缀）
    const server = new HttpServer("http://localhost:8080/");
    // 链式配置
    server
        .debugMode(true)
        .setFileRoot("d:/Code/demo")
        .setViewRoot("d:/Code/demo/views")
        .setRateLimit(120, 1, "minutes");
    server.get("/index", (req) => {
        return HttpResponse.file("html/index.html");
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