namespace Drx.Sdk.Network.Socket
{
    /// <summary>
    /// 定义了用于 Socket 通信的状态码。
    /// 范围: 0x20000000 - 0x3FFFFFFF
    /// </summary>
    public enum SocketStatusCode : uint
    {
        // 失败代码 (0x20000000 - 0x20FFFFFF) - 请求被处理，但由于逻辑条件未满足而失败。
        Failure_General = 0x20000000,               // 通用失败。客户端应将此视为一个否定性、非特定的响应。
        Failure_MachineCodeMismatch = 0x20000001,     // 验证失败：提供的机械码与记录不匹配。
        Failure_UserNotFound = 0x20000002,            // 验证失败：用户不存在或未登录。
        Failure_AssetInvalid = 0x20000003,            // 验证失败：用户不拥有指定资产或资产已过期。
        Failure_SessionConflict = 0x20000004,         // 验证失败：找到了多个冲突的用户会话。

        // 成功代码 (0x21000000 - 0x21FFFFFF) - 表示请求已成功处理。
        Success_General = 0x21000000,               // 通用成功。客户端应将此视为一个肯定性、非特定的响应。
        Success_Verified = 0x21000001,                // 验证成功：机械码匹配成功。
        Success_BoundAndVerified = 0x21000002,        // 验证成功：这是一个新的机械码，已成功绑定到资产并放行。
        Success_DataFound = 0x21000003,               // 查询成功：已找到并返回请求的数据（例如，用户邮箱）。

        // 客户端错误代码 (0x22000000 - 0x22FFFFFF) - 表示请求本身存在问题（例如，格式错误或参数无效）。
        Error_UnknownCommand = 0x22000000,          // 错误：客户端发送的命令未知或不受支持。
        Error_MissingArguments = 0x22000001,        // 错误：请求缺少必需的参数。
        Error_InvalidArguments = 0x22000002,        // 错误：请求中的一个或多个参数格式无效。
        
        // 服务器错误代码 (0x23000000 - 0x23FFFFFF) - 表示服务器在处理有效请求时遇到了内部问题。
        Error_InternalServerError = 0x23000000,       // 错误：服务器在处理请求时遇到意外的内部错误。
    }
} 