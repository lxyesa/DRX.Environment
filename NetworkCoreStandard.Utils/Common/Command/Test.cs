using NetworkCoreStandard.Common.Args;
using NetworkCoreStandard.Common.Interface;
using NetworkCoreStandard.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworkCoreStandard.Common.Command
{
    public class Test : ICommand
    {
        public uint PermissionGroup => 0;

        public object Execute(object[] args, object executer)
        {
            if (args.Length > 0)
            {
                var message = $"TestCommand 只接收 0 个参数，但是传入了 {args.Length} 个参数";
                Logger.Log("Command", message);

                return message;
            }
            else
            {
                return null;
            }
        }
    }
}
