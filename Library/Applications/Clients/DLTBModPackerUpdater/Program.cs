using System.Diagnostics;
using System.Text.Json.Nodes;
using Drx.Sdk.Network.V2.Web.Core;
using Drx.Sdk.Shared;
using Newtonsoft.Json.Linq;

string baseUrl = "http://127.0.0.1:8462/api/dltbmodpacker";

// TODO: 检测DLTBModPacker是否已运行，若已运行则提示用户关闭后再试
Process[] processes = Process.GetProcessesByName("DLTBModPacker");
if (processes.Length > 0)
{
    Logger.Error("检测到 DLTBModPacker 正在运行。请关闭 DLTBModPacker 后重试。");
    Environment.Exit(1);
}

Logger.Info("正在请求最新版本...");
await using var client = new Drx.Sdk.Network.V2.Web.DrxHttpClient();

var request = new HttpRequest()
{
    Method = "GET",
    Url = $"{baseUrl}/version/check",
};

var response = await client.SendAsync(request);
if (response.StatusCode == 200)
{
    var bodyJson = JsonNode.Parse(response.Body);
    var version = bodyJson?["version"]?.ToString();

    Logger.Info($"当前最新版本: {version}");

    // 下载新版本程序
}
else
{
    Logger.Error($"服务器连接错误: {response.StatusCode}");
}

Console.ResetColor();
