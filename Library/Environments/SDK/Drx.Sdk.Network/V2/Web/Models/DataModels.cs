namespace Drx.Sdk.Network.V2.Web.Models;

public class DrxUserDataModel : DataModelBase
{
    public string Username { get; set; }
    public string PasswordHash { get; set; }
    public string Email { get; set; }
}