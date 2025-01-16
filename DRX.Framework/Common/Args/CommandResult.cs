using DRX.Framework.Common.Base;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DRX.Framework.Common.Args
{
    public class CommandResult : BaseArgs<CommandResult>
    { 
        public object? Result { get; set; }
        public string? Message { get; set; }
        public bool IsSuccess { get; set; }

        public CommandResult()
        {
            Result = new();
            Message = string.Empty;
            IsSuccess = false;
        }
    }
}
