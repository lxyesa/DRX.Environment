using NetworkCoreStandard.Common.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworkCoreStandard.Common.Systems
{
    public class CommandSystem
    {
        private readonly Dictionary<string, ICommand> _commands = new();
        private object _owner;

        public CommandSystem(object owner)
        {
            _owner = owner;
        }

        public void RegisterCommand(string commandName, ICommand command)
        {
            if (_commands.ContainsKey(commandName))
            {
                throw new InvalidOperationException($"Command {commandName} is already registered");
            }
            _commands[commandName] = command;
        }

        public void UnregisterCommand(string commandName)
        {
            if (_commands.ContainsKey(commandName))
            {
                _commands.Remove(commandName);
            }
        }

        public object ExecuteCommand(string commandName, object[] args, object executer)
        {
            if (_commands.TryGetValue(commandName, out var command))
            {
                return command.Execute(args, executer);
            }
            else
            {
                throw new InvalidOperationException($"Command {commandName} is not registered");
            }
        }

        public bool HasCommand(string commandName)
        {
            return _commands.ContainsKey(commandName);
        }
    }
}

