using System;
using Drx.Sdk.Network.Session;

namespace Web.KaxServer.Models
{
    public class EmailVerificationSession : BaseSession
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }

        public EmailVerificationSession(string username, string email, string password) 
            : base("EmailVerification", 600) // 10分钟过期
        {
            Username = username;
            Email = email;
            Password = password;
        }
    }
} 