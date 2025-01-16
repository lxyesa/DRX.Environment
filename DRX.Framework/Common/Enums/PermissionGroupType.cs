using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DRX.Framework.Common.Enums
{
    public enum PermissionGroupType
    {
        None = 0,
        /* 客户端 */
        Client = 1,
        /* 管理员 */
        Admin = 2,
        /* 终端管理员 */
        TerminalAdmin = 3,
        /* 终端 */
        Terminal = 4,
    }
}
