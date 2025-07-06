using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DRX.Framework.Common.Enums.Packet
{
    public class PacketBodyKey
    {
        //----------------------------------------------------------------- 以下为命令类型的数据包键值
        public static readonly string Command = "command";
        public static readonly string CommandArgs = "command_args";
        public static readonly string CommandResponse = "command_result";

        //----------------------------------------------------------------- 以下为其他类型的数据包键值
        public static readonly string Message = "message";
        public static readonly string Response = "response";
    }
}
