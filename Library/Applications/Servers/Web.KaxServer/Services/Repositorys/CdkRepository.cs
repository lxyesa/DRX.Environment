using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Drx.Sdk.Network.DataBase;
using Web.KaxServer.Models;

namespace Web.KaxServer.Services.Repositorys
{
    /// <summary>
    /// CDK码的仓库类，提供CDK的持久化存储和检索功能
    /// </summary>
    public class CdkRepository
    {
        public static readonly Sqlite<Cdk> CdkSqlite = new(Path.Combine(AppContext.BaseDirectory, "Data", "cdk", "cdk.db"));

        public static Cdk? GetCdk(string code)
        {
            return CdkSqlite.ReadSingle("Code", code);
        }

        public static List<Cdk> GetAllCdks()
        {
            return CdkSqlite.Read();
        }

        public static void SaveCdk(Cdk cdk)
        {
            CdkSqlite.Save(cdk);
        }

        public static void DeleteCdk(Cdk cdk)
        {
            CdkSqlite.Delete(cdk);
        }

        public static void DeleteCdk(string code)
        {
            CdkSqlite.DeleteWhere(new Dictionary<string, object> { { "Code", code } });
        }

        public static void DeleteCdk(int id)
        {
            CdkSqlite.DeleteWhere(new Dictionary<string, object> { { "Id", id } });
        }

        public static void CreateBatchFile(List<Cdk> cdks, string batchFilePath)
        {
            File.WriteAllLines(batchFilePath, cdks.Select(c => c.Code));
        }
    }
} 