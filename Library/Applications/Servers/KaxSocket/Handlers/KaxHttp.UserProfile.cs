using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Drx.Sdk.Network.Http.Configs;
using Drx.Sdk.Network.Http;
using Drx.Sdk.Network.Http.Protocol;
using Drx.Sdk.Network.Http.Results;
using Drx.Sdk.Shared;
using Drx.Sdk.Shared.Utility;
using KaxSocket;
using KaxSocket.Cache;

namespace KaxSocket.Handlers;

/// <summary>
/// 用户资料管理模块 - 处理用户资料查询、更新、头像管理等功能
/// </summary>
public partial class KaxHttp
{
    #region 用户资料管理 (User Profile Management)

    // GET  /api/user/profile   -> 返回当前登录用户的资料
    // POST /api/user/profile   -> 更新当前登录用户的资料（displayName, email, bio）
    [HttpHandle("/api/user/profile", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_UserProfile(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "未授权" }, 401);

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName)) return new JsonResult(new { message = "未授权" }, 401);

        if (await KaxGlobal.IsUserBanned(userName)) return new JsonResult(new { message = "账号被封禁" }, 403);

        var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null) return new JsonResult(new { message = "用户不存在" }, 404);

        var respDisplayName = string.IsNullOrEmpty(user.DisplayName) ? user.UserName : user.DisplayName;
        var respEmail = user.Email ?? string.Empty;
        var respBio = string.Empty;
        var bioProp = user.GetType().GetProperty("Bio");
        if (bioProp != null) respBio = (bioProp.GetValue(user) as string) ?? string.Empty;
        var respSignature = string.Empty;
        var signatureProp = user.GetType().GetProperty("Signature");
        if (signatureProp != null) respSignature = (signatureProp.GetValue(user) as string) ?? string.Empty;
        var respRegisteredAt = user.RegisteredAt;
        var respLastLoginAt = user.LastLoginAt;

        // 若存在服务器头像文件，则提供可访问的头像 URL（前端将直接使用该 URL）
        string avatarUrl = string.Empty;
        try
        {
            var avatarPath = KaxGlobal.GetUserAvatarPathById(user.Id);
            if (!string.IsNullOrEmpty(avatarPath))
            {
                var stamp = respLastLoginAt > 0 ? respLastLoginAt : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                avatarUrl = $"/api/user/avatar/{user.Id}?v={stamp}";
            }
        }
        catch { }

        // 额外动态字段：resourceCount / gold / recentActivity / cdkCount
        var resourceCount = 0; var gold = 0; var recentActivity = 0; var cdkCount = 0;
        try { resourceCount = user.ResourceCount; gold = user.Gold; recentActivity = user.RecentActivity; } catch { }
        try { cdkCount = await KaxGlobal.GetUserCdkCountAsync(user.UserName); } catch { }

        return new JsonResult(new
        {
            id = user.Id,
            user = user.UserName,
            displayName = respDisplayName,
            email = respEmail,
            bio = respBio,
            signature = respSignature,
            registeredAt = respRegisteredAt,
            lastLoginAt = respLastLoginAt,
            permissionGroup = (int)user.PermissionGroup,
            // 保留旧字段以兼容前端
            isBanned = user.Status != null && user.Status.IsBanned,
            bannedAt = user.Status != null ? user.Status.BannedAt : 0,
            banExpiresAt = user.Status != null ? user.Status.BanExpiresAt : 0,
            banReason = user.Status != null ? (user.Status.BanReason ?? string.Empty) : string.Empty,
            avatarUrl = avatarUrl,
            resourceCount = resourceCount,
            gold = gold,
            recentActivity = recentActivity,
            cdkCount = cdkCount
        });
    }

    // GET /api/user/profile/{uid} -> 返回指定用户的资料（公开信息）
    [HttpHandle("/api/user/profile/{uid}", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_UserProfileByUid(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "未授权" }, 401);

        var currentUserName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(currentUserName)) return new JsonResult(new { message = "未授权" }, 401);

        if (await KaxGlobal.IsUserBanned(currentUserName)) return new JsonResult(new { message = "账号被封禁" }, 403);

        // 从路径中提取 uid
        var uidStr = request.Path.Split('/').LastOrDefault();
        if (!long.TryParse(uidStr, out var uid))
            return new JsonResult(new { message = "无效的用户 ID" }, 400);

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("Id", uid)).FirstOrDefault();
            if (user == null) return new JsonResult(new { message = "用户不存在" }, 404);

            // 检查目标用户是否被封禁
            if (user.Status != null && user.Status.IsBanned)
                return new JsonResult(new { message = "该用户已被封禁" }, 403);

            var respDisplayName = string.IsNullOrEmpty(user.DisplayName) ? user.UserName : user.DisplayName;
            var respEmail = user.Email ?? string.Empty;
            var respBio = string.Empty;
            var bioProp = user.GetType().GetProperty("Bio");
            if (bioProp != null) respBio = (bioProp.GetValue(user) as string) ?? string.Empty;
            var respSignature = string.Empty;
            var signatureProp = user.GetType().GetProperty("Signature");
            if (signatureProp != null) respSignature = (signatureProp.GetValue(user) as string) ?? string.Empty;
            var respRegisteredAt = user.RegisteredAt;
            var respLastLoginAt = user.LastLoginAt;

            // 若存在服务器头像文件，则提供可访问的头像 URL
            string avatarUrl = string.Empty;
            try
            {
                var avatarPath = KaxGlobal.GetUserAvatarPathById(user.Id);
                if (!string.IsNullOrEmpty(avatarPath))
                {
                    var stamp = respLastLoginAt > 0 ? respLastLoginAt : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    avatarUrl = $"/api/user/avatar/{user.Id}?v={stamp}";
                }
            }
            catch { }

            // 额外动态字段
            var resourceCount = 0; var gold = 0; var recentActivity = 0; var cdkCount = 0;
            try { resourceCount = user.ResourceCount; gold = user.Gold; recentActivity = user.RecentActivity; } catch { }
            try { cdkCount = await KaxGlobal.GetUserCdkCountAsync(user.UserName); } catch { }

            return new JsonResult(new
            {
                id = user.Id,
                user = user.UserName,
                displayName = respDisplayName,
                email = respEmail,
                bio = respBio,
                signature = respSignature,
                registeredAt = respRegisteredAt,
                lastLoginAt = respLastLoginAt,
                permissionGroup = (int)user.PermissionGroup,
                isBanned = user.Status != null && user.Status.IsBanned,
                bannedAt = user.Status != null ? user.Status.BannedAt : 0,
                banExpiresAt = user.Status != null ? user.Status.BanExpiresAt : 0,
                banReason = user.Status != null ? (user.Status.BanReason ?? string.Empty) : string.Empty,
                avatarUrl = avatarUrl,
                resourceCount = resourceCount,
                gold = gold,
                recentActivity = recentActivity,
                cdkCount = cdkCount
            });
        }
        catch (Exception ex)
        {
            Logger.Error("获取用户资料时出错: " + ex.Message);
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    [HttpHandle("/api/user/profile", "POST", RateLimitMaxRequests = 10, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_UpdateUserProfile(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "未授权" }, 401);

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName)) return new JsonResult(new { message = "未授权" }, 401);

        if (await KaxGlobal.IsUserBanned(userName)) return new JsonResult(new { message = "账号被封禁" }, 403);

        if (string.IsNullOrEmpty(request.Body)) return new JsonResult(new { message = "请求体不能为空" }, 400);

        var body = JsonNode.Parse(request.Body);
        if (body == null) return new JsonResult(new { message = "无效的 JSON" }, 400);

        var displayName = body["displayName"]?.ToString()?.Trim() ?? string.Empty;
        var email = body["email"]?.ToString()?.Trim() ?? string.Empty;
        var bio = body["bio"]?.ToString() ?? string.Empty;
        var signature = body["signature"]?.ToString() ?? string.Empty;
        var targetUidStr = body["targetUid"]?.ToString() ?? string.Empty;

        // 基础校验
        if (!string.IsNullOrEmpty(email) && !CommonUtility.IsValidEmail(email))
            return new JsonResult(new { message = "无效的电子邮箱地址" }, 400);

        // 验证 targetUid 参数
        if (string.IsNullOrEmpty(targetUidStr))
            return new JsonResult(new { message = "targetUid 参数缺失" }, 400);

        if (!long.TryParse(targetUidStr, out var targetUid) || targetUid <= 0)
            return new JsonResult(new { message = "targetUid 参数无效" }, 400);

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
            if (user == null) return new JsonResult(new { message = "用户不存在" }, 404);

            // 权限验证：确保只能更新自己的资料（当前用户 ID 必须与 targetUid 一致）
            if (user.Id != targetUid)
            {
                Logger.Warn($"用户 {userName}(ID:{user.Id}) 尝试修改他人资料（目标 UID: {targetUid}），请求被拒绝");
                return new JsonResult(new { message = "无权修改他人资料" }, 403);
            }

            // 如果邮箱发生变化，检查唯一性
            if (!string.IsNullOrEmpty(email) && !string.Equals(user.Email ?? string.Empty, email, StringComparison.OrdinalIgnoreCase))
            {
                var byEmail = (await KaxGlobal.UserDatabase.SelectWhereAsync("Email", email)).FirstOrDefault();
                if (byEmail != null && !string.Equals(byEmail.UserName, user.UserName, StringComparison.OrdinalIgnoreCase))
                {
                    return new JsonResult(new { message = "该邮箱已被占用" }, 409);
                }
                user.Email = email;
            }

            if (!string.IsNullOrEmpty(displayName)) user.DisplayName = displayName;

            // 如果模型支持 Bio 字段则保存（向后兼容）
            var prop = user.GetType().GetProperty("Bio");
            if (prop != null) prop.SetValue(user, bio ?? string.Empty);

            // 如果模型支持 Signature 字段则保存
            var signatureProp = user.GetType().GetProperty("Signature");
            if (signatureProp != null) signatureProp.SetValue(user, signature ?? string.Empty);

            await KaxGlobal.UserDatabase.UpdateAsync(user);

            Logger.Info($"用户 {userName} 成功更新了自己的资料");
            return new JsonResult(new { message = "资料已更新" });
        }
        catch (Exception ex)
        {
            Logger.Error("更新用户资料时出错: " + ex.Message);
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    // POST /api/user/password -> 修改当前登录用户的密码（需提供旧密码、新密码、确认新密码）
    [HttpHandle("/api/user/password", "POST", RateLimitMaxRequests = 6, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_ChangePassword(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "未授权" }, 401);

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName)) return new JsonResult(new { message = "未授权" }, 401);

        if (await KaxGlobal.IsUserBanned(userName)) return new JsonResult(new { message = "账号被封禁" }, 403);

        if (string.IsNullOrEmpty(request.Body)) return new JsonResult(new { message = "请求体不能为空" }, 400);
        var body = JsonNode.Parse(request.Body);
        if (body == null) return new JsonResult(new { message = "无效的 JSON" }, 400);

        var oldPassword = body["oldPassword"]?.ToString() ?? string.Empty;
        var newPassword = body["newPassword"]?.ToString() ?? string.Empty;
        var confirmPassword = body["confirmPassword"]?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(oldPassword) || string.IsNullOrEmpty(newPassword) || string.IsNullOrEmpty(confirmPassword))
            return new JsonResult(new { message = "旧密码/新密码/确认密码均为必填" }, 400);

        if (newPassword.Length < 8) return new JsonResult(new { message = "新密码长度至少 8 位" }, 400);
        if (newPassword != confirmPassword) return new JsonResult(new { message = "两次新密码不匹配" }, 400);

        try
        {
            var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
            if (user == null) return new JsonResult(new { message = "用户不存在" }, 404);

            // 安全验证：确保只能修改自己的密码
            // 这是一个防御性检查，防止通过 API 直接修改他人密码
            if (!string.Equals(user.UserName, userName, StringComparison.OrdinalIgnoreCase))
            {
                Logger.Warn($"用户 {userName} 尝试修改他人密码（目标用户: {user.UserName}），请求被拒绝");
                return new JsonResult(new { message = "无权修改他人密码" }, 403);
            }

            var oldHash = CommonUtility.ComputeSHA256Hash(oldPassword);
            if (!string.Equals(user.PasswordHash ?? string.Empty, oldHash, StringComparison.Ordinal))
            {
                Logger.Warn($"用户 {userName} 修改密码失败：旧密码不正确。");
                return new JsonResult(new { message = "旧密码不正确" }, 401);
            }

            user.PasswordHash = CommonUtility.ComputeSHA256Hash(newPassword);
            await KaxGlobal.UserDatabase.UpdateAsync(user);

            Logger.Info($"用户 {userName} 已更新密码。");
            return new JsonResult(new { message = "密码已更新" });
        }
        catch (Exception ex)
        {
            Logger.Error("修改密码时出错: " + ex.Message);
            return new JsonResult(new { message = "服务器错误" }, 500);
        }
    }

    // GET /api/user/avatar/{userId} -> 返回指定用户的头像文件（若存在）
    [HttpHandle("/api/user/avatar/{userId}", "GET", RateLimitMaxRequests = 120, RateLimitWindowSeconds = 60)]
    public static async Task<IActionResult> Get_UserAvatar(HttpRequest request)
    {
        if (!request.PathParameters.TryGetValue("userId", out var idStr) || !int.TryParse(idStr, out var userId) || userId <= 0)
            return new JsonResult(new { message = "userId 参数无效" }, 400);

        // 尝试从缓存获取头像
        if (_avatarCache.TryGetAvatar(userId, out var cachedImageData, out var cachedContentType))
        {
            return new BytesResult(cachedImageData, cachedContentType ?? "image/png");
        }

        var path = KaxGlobal.GetUserAvatarPathById(userId);
        if (string.IsNullOrEmpty(path)) return new JsonResult(new { message = "未找到头像" }, 404);

        try
        {
            var imageData = await File.ReadAllBytesAsync(path);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var contentType = ext == ".png" ? "image/png" : "image/jpeg";

            // 将读取的数据存入缓存
            _avatarCache.SetAvatar(userId, imageData, contentType);

            return new BytesResult(imageData, contentType);
        }
        catch (Exception ex)
        {
            Logger.Error($"读取用户头像失败 (userId: {userId}): {ex.Message}");
            return new JsonResult(new { message = "读取头像失败" }, 500);
        }
    }

    // GET /api/user/stats -> 返回当前登录用户的统计信息（resourceCount / cdkCount / recentActivity / gold）
    [HttpHandle("/api/user/stats", "GET", RateLimitMaxRequests = 60, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Get_UserStats(HttpRequest request)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "未授权" }, 401);

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName)) return new JsonResult(new { message = "未授权" }, 401);
        if (await KaxGlobal.IsUserBanned(userName)) return new JsonResult(new { message = "账号被封禁" }, 403);

        var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null) return new JsonResult(new { message = "用户不存在" }, 404);

        var resourceCount = user.ResourceCount;
        var gold = user.Gold;
        var recentActivity = user.RecentActivity;
        var cdkCount = await KaxGlobal.GetUserCdkCountAsync(userName);

        return new JsonResult(new
        {
            user = userName,
            resourceCount = resourceCount,
            cdkCount = cdkCount,
            recentActivity = recentActivity,
            gold = gold
        });
    }

    // POST /api/user/avatar -> 上传当前登录用户的头像（multipart/form-data, field name 可任意，使用第一个文件）
    [HttpHandle("/api/user/avatar", "POST", RateLimitMaxRequests = 10, RateLimitWindowSeconds = 60, RateLimitCallbackMethodName = nameof(RateLimitCallback))]
    public static async Task<IActionResult> Post_UploadUserAvatar(HttpRequest request, DrxHttpServer server)
    {
        var token = request.Headers[HttpHeaders.Authorization]?.Replace("Bearer ", "");
        var principal = KaxGlobal.ValidateToken(token!);
        if (principal == null) return new JsonResult(new { message = "未授权" }, 401);

        var userName = principal.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName)) return new JsonResult(new { message = "未授权" }, 401);
        if (await KaxGlobal.IsUserBanned(userName)) return new JsonResult(new { message = "账号被封禁" }, 403);

        if (request.UploadFile == null || request.UploadFile.Stream == null)
            return new JsonResult(new { message = "缺少上传的文件（multipart/form-data）" }, 400);

        var upload = request.UploadFile;
        var fileExt = Path.GetExtension(upload.FileName ?? string.Empty).ToLowerInvariant();
        if (fileExt == ".jpeg") fileExt = ".jpg";
        if (fileExt != ".png" && fileExt != ".jpg")
        {
            return new JsonResult(new { message = "仅支持 PNG / JPG 格式（文件扩展名应为 .png/.jpg）" }, 400);
        }

        if (upload.Stream.Length > 2 * 1024 * 1024) return new JsonResult(new { message = "文件过大，最大 2MB" }, 413);

        var user = (await KaxGlobal.UserDatabase.SelectWhereAsync("UserName", userName)).FirstOrDefault();
        if (user == null) return new JsonResult(new { message = "用户不存在" }, 404);

        // 安全验证：确保只能上传自己的头像
        // 这是一个防御性检查，防止通过 API 直接修改他人头像
        if (!string.Equals(user.UserName, userName, StringComparison.OrdinalIgnoreCase))
        {
            Logger.Warn($"用户 {userName} 尝试修改他人头像（目标用户: {user.UserName}），请求被拒绝");
            return new JsonResult(new { message = "无权修改他人头像" }, 403);
        }

        var ext = fileExt;

        var iconsDir = Path.Combine(AppContext.BaseDirectory, "resources", "user", "icon");
        Directory.CreateDirectory(iconsDir);
        var finalPngPath = Path.Combine(iconsDir, $"{user.Id}.png");

        // 统一将上传的图片转为 PNG 并保存为 {uid}.png；若存在旧的 jpg 文件则删除
        upload.Stream.Position = 0;
        try
        {
            using var img = Image.FromStream(upload.Stream, useEmbeddedColorManagement: true, validateImageData: true);
            // 使用高质量重采样以保证输出稳定
            using var bmp = new Bitmap(img.Width, img.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
                g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                g.DrawImage(img, 0, 0, img.Width, img.Height);
            }

            // 覆盖保存为 PNG
            bmp.Save(finalPngPath, ImageFormat.Png);

            // 删除可能残留的 JPG（避免同一用户存在多种扩展名）
            var legacyJpg = Path.Combine(iconsDir, $"{user.Id}.jpg");
            if (File.Exists(legacyJpg))
            {
                try { File.Delete(legacyJpg); } catch { /* 忽略删除失败 */ }
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"保存用户头像并转换为 PNG 失败: {ex.Message}");
            return new JsonResult(new { message = "保存头像失败（无效的图片或服务器错误）" }, 500);
        }

        // 清除该用户的头像缓存，使下次请求重新加载
        _avatarCache.InvalidateAvatar(user.Id);

        var url = $"/api/user/avatar/{user.Id}?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
        Logger.Info($"用户 {userName} 成功上传了头像");
        return new JsonResult(new { message = "头像已上传", url = url });
    }

    #endregion
}
