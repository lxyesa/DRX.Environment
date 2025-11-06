using Drx.Sdk.Network.DataBase.Sqlite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KaxSocket
{
    public class DLTBModPackerGlobal
    {
        private readonly SqliteUnified<ModInfo> modInfoDb;
        private readonly static DLTBModPackerGlobal inst = new DLTBModPackerGlobal();
        public readonly static DLTBModPackerGlobal Instance = inst;

        public readonly string DLTB_DatabaseFile = "dltb_database.db";
        public List<ModInfo> ModInfos = new List<ModInfo>();

        private DLTBModPackerGlobal()
        {
            modInfoDb = new SqliteUnified<ModInfo>(DLTB_DatabaseFile);
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
