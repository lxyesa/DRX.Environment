using System;
using Drx.Sdk.Network.DataBase;

namespace KaxSocket.Model;

public class CdkModel : IDataBase
{
    public int Id { get; set; }         // DataBase 内部 ID，无需在生成 CDK 时填写，由系统自动生成
    public string Code { get; set; }    // CDK 码，唯一且不区分大小写，生成时由系统自动生成
    public string Description { get; set; } // CDK 描述信息，可以选择性的在生成 CDK 时填写，用于区分不同用途的 CDK
    public bool IsUsed { get; set; }    // 是否已被使用
    public long CreatedAt { get; set; } // 生成时间，Unix 时间戳（秒）
    public long UsedAt { get; set; }    // 使用时间，生成时不需要填写，由系统自动填写
    public string UsedBy { get; set; }  // 使用者用户名，不要在生成时填写，使用时由系统自动填写
    public string CreatedBy { get; set; } = string.Empty; // 创建者用户名，保存时由系统自动填写，用于溯源
    public int GoldValue { get; set; }  // 生成时需填写(可以为0)，表示使用该 CDK 后用户获得的金币数量
    public long ExpiresInSeconds { get; set; } // 生成时可填写(可以为0)，表示 CDK 兑换有效期秒数，0表示永久有效
}
