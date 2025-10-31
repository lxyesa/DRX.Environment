using Drx.Sdk.Network.DataBase.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KaxSocket
{
    public class Global
    {
        private readonly SqliteUnified<ModInfo> modInfoDb;
        private readonly static Global inst = new Global();
        public readonly static Global Instance = inst;

        public readonly string DatabaseFile = "KaxSocketData.db";
        public List<ModInfo> ModInfos = new List<ModInfo>();

        private Global()
        {
            modInfoDb = new SqliteUnified<ModInfo>(DatabaseFile);
            Init();
        }

        private void Init()
        {
            // 从数据库加载 ModInfos
            ModInfos.Clear();
            foreach (var mod in modInfoDb.QueryAll())
            {
                ModInfos.Add(mod);
            }
        }

        public SqliteUnified<ModInfo> GetModInfoDataBase()
        {
            return modInfoDb;
        }
    }
}
