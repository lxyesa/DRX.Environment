using System;
using NetworkCoreStandard.EventArgs;

namespace NetworkCoreStandard.User.EventArgs;

public class LoginEventArgs : BaseEventArgs
{
    public string? Username { get; set; } = string.Empty;
    public string? Password { get; set; } = string.Empty;
    
    public NetworkEventArgs SetArgs(string username, string password)
    {
        Username = username;
        Password = password;
        return (NetworkEventArgs)owner!;
    }
}
