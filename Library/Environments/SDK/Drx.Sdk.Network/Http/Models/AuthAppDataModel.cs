using System;

namespace Drx.Sdk.Network.Http.Models;

/// <summary>
/// OpenAuth 客户端应用注册模型。
/// 仅已注册且启用的 Auth App 可参与授权流程。
/// </summary>
public class AuthAppDataModel : DataModelBase
{
    /// <summary>
    /// 客户端唯一标识（client_id）
    /// </summary>
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// 客户端密钥哈希（SHA256，允许为空表示公开客户端）
    /// </summary>
    public string ClientSecretHash { get; set; } = string.Empty;

    /// <summary>
    /// 应用展示名称
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// 应用说明
    /// </summary>
    public string ApplicationDescription { get; set; } = string.Empty;

    /// <summary>
    /// 回调地址（redirect_uri）
    /// </summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>
    /// 默认 scope（空格或逗号分隔）
    /// </summary>
    public string Scopes { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 创建时间（UTC Unix 秒）
    /// </summary>
    public long CreatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    /// <summary>
    /// 最近更新时间（UTC Unix 秒）
    /// </summary>
    public long UpdatedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
}
