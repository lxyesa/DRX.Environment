// const server = new HttpServer("http://localhost:8080/");

// server.setFileRoot("./public");

// server.get("/", (req) => {
//     return HttpResponse.file("./public/index.html");
// });

// server.get("/api/hello", (req) => {
//     return HttpResponse.json({ message: "Hello from Paperclip HTTP Server!" });
// });

// server.get("/api/time", (req) => {
//     return HttpResponse.json({ time: new Date().toISOString() });
// });

// server.post("/api/echo", (req) => {
//     return HttpResponse.json({ echo: req.Body });
// });

// async function main(): Promise<void> {
//     console.log("Starting HTTP server on http://localhost:8080/");
//     await server.startAsync();
// }

// 将以上代码取消注释，你将得到一个最小的 HTTP 服务器模板，支持静态文件和简单 API 路由。你可以根据需要扩展路由和功能。