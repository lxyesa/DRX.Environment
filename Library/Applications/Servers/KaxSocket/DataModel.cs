using Drx.Sdk.Network.DataBase.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KaxSocket
{
    public abstract class DataModel : IDataBase
    {
        public int Id { get; set; }
    }

    public class ModInfo : DataModel
    {
        public string ModId { get; set; }
        public string ModName { get; set; }
        public string ModVersion { get; set; }
        public string ModAuthor { get; set; }
        public string ModDescription { get; set; }
        public long LastUpdatedAt { get; set; }
    }

    public class UserData : DataModel
    {
        public string UserName { get; set; }
        public string PasswordHash { get; set; }
        public string Email { get; set; }
        public long RegisteredAt { get; set; }
        public long LastLoginAt { get; set; }
        public string LoginToken { get; set; }
        public string DisplayName { get; set; }
        public UserStatus Status { get; set; } = new UserStatus();
    }

    public class UserStatus : IDataTable
    {
        public int ParentId { get; set; }
        public bool IsBanned { get; set; }
        public long BannedAt { get; set; }
        public long BanExpiresAt { get; set; }
        public string BanReason { get; set; }   // 封禁原因
        public string TableName => nameof(UserStatus);
        public int Id { get; set; }
    }
}
