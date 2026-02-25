using System;

namespace Drx.Sdk.Network.V2.Web.Configs
{
    /// <summary>
    /// Cookie选项类
    /// </summary>
    public class CookieOptions
    {
        public bool HttpOnly { get; set; } = true;
        public bool Secure { get; set; } = false;
        public string? SameSite { get; set; } = "Lax";
        public string? Path { get; set; } = "/";
        public string? Domain { get; set; }
        public DateTime? Expires { get; set; }
        public TimeSpan? MaxAge { get; set; }
    }
}
