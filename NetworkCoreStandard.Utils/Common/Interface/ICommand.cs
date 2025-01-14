using NetworkCoreStandard.Common.Args;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NetworkCoreStandard.Common.Interface
{
    public interface ICommand
    {
        uint PermissionGroup { get; }
        object Execute(object[] args, object executer);
    }
}

