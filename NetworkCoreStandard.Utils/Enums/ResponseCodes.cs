namespace NetworkCoreStandard.Enums;

public enum ResponseCodes
{
    Success = 0,
    BadRequest = -1,
}

public enum HeaderType
{
    None = 0, // 无/未知
    Request = 1, // 请求
    Response = 2, // 响应
    Heartbeat = 3, // 心跳
    Notification = 4, // 通知
    Command = 5, // 命令
    Error = 6, // 错误
    Warning = 7, // 警告
    Info = 8, // 信息
    Debug = 9, // 调试
    Custom = 10, // 自定义
}

public enum RequestIdentifier
{
    None = 0, // 无/未知
    Login = 1, // 登录
    Register = 2, // 注册
}

// 错误原因
public enum ErrorCauses
{
    // 用户相关 - 登录 (1 - 10)
    UserNotFound = 1, // 用户不存在
    PasswordError = 2, // 密码错误
    
    // 用户相关 - 注册 (11 - 20)
    UserAlreadyExists = 11, // 用户已存在
    BadUserName = 12, // 用户名不合法
    BadPassword = 13, // 密码不合法
    BadEmail = 14, // 邮箱不合法
    BadKey = 15, // 验证码不合法

    // 验证码相关 (21 - 30)
    KeyNotFound = 21, // 验证码不存在
    KeyExpired = 22, // 验证码已过期

    // 卡密相关 (31 - 40)
    CardNotFound = 31, // 卡密不存在
    CardUsed = 32, // 卡密已使用
    CardExpired = 33, // 卡密已过期
    CardInvalid = 34, // 卡密无效(一般是格式错误、不合法等)

    // 服务器相关 (41 - 100)
    
    
    // 断开连接相关 (101 - 150)
    Disconnected = 101, // 连接断开
    ConnectionLost = 102, // 连接丢失
    ConnectionClosed = 103, // 连接关闭(一般是服务器主动关闭)
    ConnectionRefused = 104, // 连接被拒绝(一般是服务器拒绝连接)
    KickOut = 105, // 被踢出(一般是服务器主动踢出)
    Banned = 106, // 被封禁(一般是服务器主动封禁)
    Timeout = 107, // 连接超时

    // 连接失败相关 (151 - 200)
    ConnectionFailed = 151, // 连接失败
    BannedConnection = 152, // 连接被封禁(服务器将连接列入黑名单)
    ConnectionRefusedByServer = 153, // 服务器拒绝连接
}
