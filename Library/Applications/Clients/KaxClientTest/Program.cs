using System.Collections.Specialized;
using System.Text.Json.Nodes;
using Drx.Sdk.Network.V2.Web;
using Drx.Sdk.Shared;

var client = new DrxHttpClient();
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

var token = JsonNode.Parse(loginResponse.Body)!["login_token"]!.ToString();
for (int i = 0; i < 50; i++)
{
    var sayHelloResponse = await client.SendAsync(new HttpRequest()
    {
        Method = "GET",
        Url = $"http://127.0.0.1:8462/api/hello",
        Headers =
        {
            { HttpHeaders.Authorization, $"Bearer {token}" }
        }
    });

    Logger.Info($"问候响应状态码: {sayHelloResponse.StatusCode}");
    Logger.Info($"问候响应内容: {sayHelloResponse.Body}");
}

Logger.Info($"注册响应状态码: {registerResponse.StatusCode}");
Logger.Info($"注册响应内容: {registerResponse.Body}");

Console.WriteLine("==================================================");

Logger.Info($"登录响应状态码: {loginResponse.StatusCode}");
Logger.Info($"登录响应内容: {JsonNode.Parse(loginResponse.Body)!["message"]}");
Logger.Info($"登录令牌: {JsonNode.Parse(loginResponse.Body)!["login_token"]}");

Console.WriteLine("==================================================");
