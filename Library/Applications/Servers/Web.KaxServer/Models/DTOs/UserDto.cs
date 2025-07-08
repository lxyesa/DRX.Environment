using Drx.Sdk.Network.DataBase;
using Drx.Sdk.Network.Sqlite;
using System;
using System.Collections.Generic;

namespace Web.KaxServer.Models.DTOs
{
    public class UserDto : IDataBase
    {
        public int UserId { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public UserPermissionType UserPermission { get; set; }
        public decimal Coins { get; set; }

        [SqliteRelation("UserAssets", "UserId")]
        public List<UserAssetDto> OwnedAssets { get; set; } = new();

        [SqliteRelation("McaCodes", "UserId")]
        public List<McaCodeDto> McaCodes { get; set; } = new();

        [SqliteRelation("ClientTokens", "UserId")]
        public List<ClientTokenDto> ClientTokens { get; set; } = new();

        [SqliteRelation("PublishedAssets", "UserId")]
        public List<int> PublishedAssetIds { get; set; } = new List<int>();

        public string AvatarUrl { get; set; }

        [SqliteIgnore]
        public string SessionId { get; set; }
        public DateTime CreatedAt { get; set; }

        [SqliteRelation("ClientOnline", "UserId")]
        public List<ClientOnlineDto> ClientOnline { get; set; } = new();

        public bool Banned { get; set; } = false;
        public DateTime BanEndTime { get; set; } = DateTime.MinValue;

        int IDataBase.Id { get => UserId; set => UserId = value; }
    }

    public class UserAssetDto : IDataBase
    {
        public int UserId { get; set; }
        public int AssetId { get; set; }
        public DateTime PurchaseDate { get; set; }
        public bool IsActive { get; set; } = true;
        public int Id { get; set; }
    }

    public class McaCodeDto : IDataBase
    {
        public int UserId { get; set; }
        public int AssetId { get; set; }
        public string CodeValue { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int Id { get; set; }
    }

    public class ClientTokenDto : IDataBase
    {
        public int UserId { get; set; }
        public int AssetId { get; set; }
        public string Token { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public int Id { get; set; }
    }

    public class ClientOnlineDto : IDataBase
    {
        public int UserId { get; set; }
        public int AssetId { get; set; }
        public bool IsOnline { get; set; } = false;
        public DateTime LastOnlineTime { get; set; } = DateTime.MinValue;
        public int Id { get; set; }
    }
} 