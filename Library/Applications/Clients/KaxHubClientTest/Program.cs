using System;
using System.Collections.Specialized;
using System.Text;
using System.Text.Json.Nodes;
using Drx.Sdk.Network.Http.Asp;
using Drx.Sdk.Network.Http.Protocol;

var baseUrl = GetArg(args, "baseUrl") ?? "http://127.0.0.1:8462";
var username = GetArg(args, "username") ?? "yx0303026";
var password = GetArg(args, "password") ?? "12345678";
var hid = GetArg(args, "hid") ?? Environment.MachineName;
var headerSafeHid = ToHeaderSafeHid(hid);

Console.WriteLine("=== KaxHub Client Login Test ===");
Console.WriteLine($"BaseUrl  : {baseUrl}");
Console.WriteLine($"Username : {username}");
Console.WriteLine($"HID      : {hid}");
Console.WriteLine($"HID(head): {headerSafeHid}");
Console.WriteLine();

using var client = new DrxHttpAspClient(baseUrl);

var loginRequest = new HttpRequest
{
	Method = "POST",
	Path = "/api/user/login",
	BodyObject = new
	{
		username,
		password
	}
};

ApplyClientHeaders(loginRequest.Headers, headerSafeHid);

var loginResponse = await client.SendAsync(loginRequest);
Console.WriteLine($"[Login] Status: {loginResponse.StatusCode}");
Console.WriteLine($"[Login] Body  : {loginResponse.Body}");
Console.WriteLine();

if (!loginResponse.IsSuccessStatusCode)
{
	Console.WriteLine("登录失败，停止后续测试。");
	return;
}

var loginJson = JsonNode.Parse(loginResponse.Body) as JsonObject;
var clientToken = loginJson?["client_token"]?.ToString() ?? string.Empty;
var webToken = loginJson?["web_token"]?.ToString() ?? string.Empty;
if (string.IsNullOrWhiteSpace(clientToken))
{
	Console.WriteLine("登录成功但未拿到 client_token，请检查服务端返回。");
	return;
}

Console.WriteLine("已获取 client_token，开始验证受保护接口...");

var verifyRequest = new HttpRequest
{
	Method = "POST",
	Path = "/api/user/verify/account"
};
ApplyClientHeaders(verifyRequest.Headers, headerSafeHid);
verifyRequest.Headers["Authorization"] = $"Bearer {clientToken}";

var verifyResponse = await client.SendAsync(verifyRequest);
Console.WriteLine($"[Verify With HID] Status: {verifyResponse.StatusCode}");
Console.WriteLine($"[Verify With HID] Body  : {verifyResponse.Body}");
Console.WriteLine();

Console.WriteLine("开始负例测试：故意不带 hid 调用受保护接口...");
var badVerifyRequest = new HttpRequest
{
	Method = "POST",
	Path = "/api/user/verify/account"
};
badVerifyRequest.Headers["type"] = "client";
badVerifyRequest.Headers["Authorization"] = $"Bearer {clientToken}";
badVerifyRequest.Headers["User-Agent"] = "KaxHubClientTest/1.0";
badVerifyRequest.Headers["Accept"] = "application/json";

var badVerifyResponse = await client.SendAsync(badVerifyRequest);
Console.WriteLine($"[Verify Without HID] Status: {badVerifyResponse.StatusCode}");
Console.WriteLine($"[Verify Without HID] Body  : {badVerifyResponse.Body}");
Console.WriteLine();

Console.WriteLine("开始互换 token 负例测试：web_token 当 client_token...");
if (string.IsNullOrWhiteSpace(webToken))
{
	Console.WriteLine("[Swap web->client] 跳过：登录响应未返回 web_token。");
}
else
{
	var swapWebAsClientRequest = new HttpRequest
	{
		Method = "POST",
		Path = "/api/user/verify/account"
	};
	ApplyClientHeaders(swapWebAsClientRequest.Headers, headerSafeHid);
	swapWebAsClientRequest.Headers["Authorization"] = $"Bearer {webToken}";

	var swapWebAsClientResponse = await client.SendAsync(swapWebAsClientRequest);
	Console.WriteLine($"[Swap web->client] Status: {swapWebAsClientResponse.StatusCode}");
	Console.WriteLine($"[Swap web->client] Body  : {swapWebAsClientResponse.Body}");
}
Console.WriteLine();

Console.WriteLine("开始互换 token 负例测试：client_token 当 web_token...");
var swapClientAsWebRequest = new HttpRequest
{
	Method = "POST",
	Path = "/api/user/verify/account"
};
ApplyWebLikeHeaders(swapClientAsWebRequest.Headers);
swapClientAsWebRequest.Headers["Authorization"] = $"Bearer {clientToken}";

var swapClientAsWebResponse = await client.SendAsync(swapClientAsWebRequest);
Console.WriteLine($"[Swap client->web] Status: {swapClientAsWebResponse.StatusCode}");
Console.WriteLine($"[Swap client->web] Body  : {swapClientAsWebResponse.Body}");
Console.WriteLine();

Console.WriteLine("测试完成。预期：带 hid 成功，不带 hid 失败；互换 token 两项都应失败(401)。");

static void ApplyClientHeaders(NameValueCollection headers, string hid)
{
	headers["type"] = "client";
	headers["hid"] = hid;
	headers["User-Agent"] = "KaxHubClientTest/1.0";
	headers["Accept"] = "application/json";
	headers["Content-Type"] = "application/json";
}

static void ApplyWebLikeHeaders(NameValueCollection headers)
{
	headers["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/126.0.0.0 Safari/537.36";
	headers["Accept"] = "text/html";
	headers["Origin"] = "http://localhost";
	headers["Sec-Fetch-Mode"] = "cors";
}

static string ToHeaderSafeHid(string rawHid)
{
	if (string.IsNullOrWhiteSpace(rawHid)) return "hid-empty";
	if (IsAscii(rawHid)) return rawHid;

	var bytes = Encoding.UTF8.GetBytes(rawHid);
	var base64 = Convert.ToBase64String(bytes)
		.Replace('+', '-')
		.Replace('/', '_')
		.TrimEnd('=');
	return $"u8.{base64}";
}

static bool IsAscii(string value)
{
	foreach (var ch in value)
	{
		if (ch > 127) return false;
	}

	return true;
}

static string? GetArg(string[] args, string key)
{
	foreach (var arg in args)
	{
		if (!arg.StartsWith("--", StringComparison.Ordinal)) continue;
		var idx = arg.IndexOf('=');
		if (idx <= 2) continue;

		var name = arg.Substring(2, idx - 2);
		if (!string.Equals(name, key, StringComparison.OrdinalIgnoreCase)) continue;

		return arg[(idx + 1)..];
	}

	return null;
}
