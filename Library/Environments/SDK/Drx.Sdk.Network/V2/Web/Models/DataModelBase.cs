using Drx.Sdk.Network.DataBase.Sqlite;

namespace Drx.Sdk.Network.V2.Web.Models;

public abstract class DataModelBase : IDataBase
{
    public int Id { get; set; }
}