using System;
using Drx.Sdk.Network.DataBase.Sqlite;

namespace KaxSocket.Model;

public class AssetModel : IDataBase
{
    public int Id { get; set; }
    public string Name { get; set; }    // 资源名称，例如 ModName
    public string Version { get; set; } // 资源版本，例如 ModVersion
    public string Author { get; set; }  // 资源作者，例如 ModAuthor
    public string Description { get; set; } // 资源描述，例如 ModDescription
    public long LastUpdatedAt { get; set; } // 资源最后更新时间，Unix 时间戳
    // 软删除相关字段
    public bool IsDeleted { get; set; }
    public long DeletedAt { get; set; }
}
