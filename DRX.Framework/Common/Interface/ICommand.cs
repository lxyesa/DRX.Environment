namespace DRX.Framework.Common.Interface
{
    public interface ICommand
    {
        uint PermissionGroup { get; }
        object? Execute(object[] args, object executer);
        object? Info();
    }
}

