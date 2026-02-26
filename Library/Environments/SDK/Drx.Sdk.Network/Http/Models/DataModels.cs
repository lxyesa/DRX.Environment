namespace Drx.Sdk.Network.Http.Models;

public class DrxUserDataModel : DataModelBase
{
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public string Email { get; set; }
}