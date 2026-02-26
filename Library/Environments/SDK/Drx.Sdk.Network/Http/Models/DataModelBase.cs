using Drx.Sdk.Network.DataBase;

namespace Drx.Sdk.Network.Http.Models;

public abstract class DataModelBase : IDataBase
{
    public int Id { get; set; }
}