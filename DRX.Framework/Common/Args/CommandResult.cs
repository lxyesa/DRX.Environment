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
        public object? Result { get; set; } = new();
        public object? Message { get; set; } = string.Empty;
        public bool IsSuccess { get; set; } = false;
    }
}
