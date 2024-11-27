using System;

public class LoginResponseBody : INetworkPacketBody
{
    public string? Token { get; set; }
    public string? Message { get; set; }
    public bool Success { get; set; }
    public string? Username { get; set; }
    public ResponseCode ResponseCode { get; set; }

    public LoginResponseBody(string? token, string? message, bool success, string? username, ResponseCode responseCode)
    {
        Token = token;
        Message = message;
        Success = success;
        Username = username;
        ResponseCode = responseCode;
    }
}
