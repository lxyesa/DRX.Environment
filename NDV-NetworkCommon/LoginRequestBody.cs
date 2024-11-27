using System;
public class LoginRequestBody : INetworkPacketBody
{
    public string Username { get; set; }
    public string Password { get; set; }

    public LoginRequestBody(string username, string password)
    {
        Username = username;
        Password = password;
    }
}
