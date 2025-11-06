using System.Text.Json.Nodes;
using Drx.Sdk.Network.V2.Web;
using Drx.Sdk.Shared;

await using var client = new DrxHttpClient();
var registerResponse = await client.SendAsync(new HttpRequest()
{
    Method = "POST",
    Url = "http://127.0.0.1:8462/api/user/register",
    Body = @"{
        ""username"": ""testuser"",
        ""password"": ""securepassword"",
        ""email"": ""testuser@example.com""
    }",
});

var loginResponse = await client.SendAsync(new HttpRequest()
{
    Method = "POST",
    Url = "http://127.0.0.1:8462/api/user/login",
    Body = @"{
        ""username"": ""testuser"",
        ""password"": ""securepassword""
    }",
});

var sayHelloResponse = await client.SendAsync(new HttpRequest()
{
    Method = "GET",
    Url = $"http://127.0.0.1:8462/api/hello/{JsonNode.Parse(loginResponse.Body)!["login_token"]!.ToString()}",
});

Logger.Info($"注册响应状态码: {registerResponse.StatusCode}");
Logger.Info($"注册响应内容: {registerResponse.Body}");

Console.WriteLine("==================================================");

Logger.Info($"登录响应状态码: {loginResponse.StatusCode}");
Logger.Info($"登录响应内容: {JsonNode.Parse(loginResponse.Body)!["message"]}");
Logger.Info($"登录令牌: {JsonNode.Parse(loginResponse.Body)!["login_token"]}");

Console.WriteLine("==================================================");

Logger.Info($"问候响应状态码: {sayHelloResponse.StatusCode}");
Logger.Info($"问候响应内容: {sayHelloResponse.Body}");