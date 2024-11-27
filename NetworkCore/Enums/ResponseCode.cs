public enum ResponseCode
{
    Success = 0x00,    // 成功
    Failure = 0x01,    // 失败
    Invalid = 0x02,    // 无效
    Timeout = 0x03,    // 超时
    Unauthorized = 0x04,    // 未授权
    Forbidden = 0x05,    // 禁止
    NotFound = 0x06,    // 未找到
    Conflict = 0x07,    // 冲突
    ServerError = 0x08,    // 服务器错误
    ServiceUnavailable = 0x09,    // 服务不可用
    GatewayTimeout = 0x0A,    // 网关超时
    BadGateway = 0x0B,    // 错误网关
    Unknown = 0xFF,   // 未知
    Banned = 0x10,    // 被封禁
    Kicked = 0x11,    // 被踢出
}
