from pathlib import Path
path = Path('Library/Applications/Servers/KaxSocket/Handlers/KaxHttp.cs')
text = path.read_text(encoding='utf-8')
start = text.index('// TODO: /api/user/verify/asset/{assetId}')
end = text.index('// --------------------------------------------------', start)
replacement = """
    [HttpHandle("/api/user/verify/asset/{assetId}", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_VerifyUserAsset(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null)
        {
            return new JsonResult(new { message = "未授权" }, 401);
        }

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
        {
            return new JsonResult(new { message = "未授权" }, 401);
        }

        if (await KaxGlobal.IsUserBanned(userName))
        {
            return new JsonResult(new { message = "账号被封禁" }, 403);
        }

        if (!request.PathParameters.TryGetValue("assetId", out var assetIdValue) ||
            !int.TryParse(assetIdValue, out var assetId) || assetId <= 0)
        {
            return new JsonResult(new { message = "assetId 参数必须是大于 0 的整数" }, 400);
        }

        var hasAsset = await KaxGlobal.VerifyUserHasActiveAsset(userName, assetId);
        return new JsonResult(new { hasAsset });
    }

"""
new_text = text[:start] + replacement + text[end:]
path.write_text(new_text, encoding='utf-8')
