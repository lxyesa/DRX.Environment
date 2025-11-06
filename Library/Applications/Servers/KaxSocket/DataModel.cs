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
        public string LoginToken { get; set; }  // 通过时间戳、随机数、Base64 以及Hash生成一个登录令牌
    }
}
