namespace Drx.Sdk.Shared.ConsoleCommand
{
    public interface ICommandHandler
    {
        public Exception Execute(string[] args);
    }
}