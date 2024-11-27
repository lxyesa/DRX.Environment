using System;
using System.Net;

public class UserInstance
{
    public string Username { get; set; } = string.Empty;    // 用户名
    public string Password { get; set; } = string.Empty;    // 密码
    public string? LastLoginIp { get; set; }    // 改用字符串存储IP地址
    public DateTime LastLoginTime { get; set; }             // 上次登录时间
    public UserGroup UserGroup { get; set; } = UserGroup.Guest;     // 用户组
    public Guid UserGuid { get; set; } = Guid.NewGuid();        // 用户唯一标识
    public string MachineCode { get; set; } = string.Empty;    // 机械码
    public bool IsOnline { get; set; } = false;
    public string? CurrentToken { get; set; }    // 添加当前token属性

    // Web端会话相关属性
    public string? WebSessionToken { get; set; }
    public DateTime? WebSessionExpiry { get; set; }

    public string Email { get; set; } = string.Empty;    // 邮箱
    public string Salt { get; set; } = string.Empty;     // 密码盐
    public DateTime RegisterTime { get; set; }           // 注册时间
    public bool IsEmailVerified { get; set; } = false;   // 邮箱是否验证

    public UserInstance() 
    {
        LastLoginTime = DateTime.Now;
        RegisterTime = DateTime.Now;
    }

    public UserInstance(string username, string password, UserGroup group = UserGroup.Normal)
    {
        Username = username;
        Password = password;
        UserGroup = group;
        LastLoginTime = DateTime.Now;
    }

    public void UpdateLoginInfo(IPAddress ipAddress, string machineCode)
    {
        LastLoginIp = ipAddress.ToString();
        LastLoginTime = DateTime.Now;
        MachineCode = machineCode;
        IsOnline = true;
    }

    public bool ValidatePassword(string inputPassword)
    {
        return Password == inputPassword;
    }

    public UserLoginResult ValidateLogin(string inputPassword, IPAddress ipAddress, string machineCode)
    {
        if (!ValidatePassword(inputPassword))
            return UserLoginResult.WrongPassword;
            
        UpdateLoginInfo(ipAddress, machineCode);
        IsOnline = true;
        return UserLoginResult.Success;
    }
}

public enum UserLoginResult
{
    Success,
    UserNotFound,
    WrongPassword,
    AlreadyOnline
}