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
}
