using System;
using System.Collections.Generic;

namespace Web.KaxServer.Models.Domain
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public UserPermissionType UserPermission { get; set; }
        public decimal Coins { get; set; }
        public List<UserAsset> OwnedAssets { get; set; } = new();
        public List<McaCode> McaCodes { get; set; } = new();
        public List<ClientToken> ClientTokens { get; set; } = new();
        public List<int> PublishedAssetIds { get; set; } = new List<int>();
        public string AvatarUrl { get; set; }
        public string SessionId { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<ClientOnline> ClientOnline { get; set; } = new();
        public bool Banned { get; set; } = false;
        public DateTime BanEndTime { get; set; } = DateTime.MinValue;

        public bool IsBanned()
        {
            if (Banned)
            {
                if (BanEndTime > DateTime.Now)
                {
                    return true;
                }
                else
                {
                    Banned = false;
                    BanEndTime = DateTime.MinValue;
                }
            }
            return false;
        }
    }

    public class UserAsset
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int AssetId { get; set; }
        public DateTime PurchaseDate { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class McaCode
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int AssetId { get; set; }
        public string CodeValue { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class ClientToken
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int AssetId { get; set; }
        public string Token { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class ClientOnline
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int AssetId { get; set; }
        public bool IsOnline { get; set; } = false;
        public DateTime LastOnlineTime { get; set; } = DateTime.MinValue;
    }
} 