using NetworkCoreStandard.Common.Args;
using NetworkCoreStandard.Common.Base.Command;
using NetworkCoreStandard.Common.Utility;
using NetworkCoreStandard.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NDVServerLib.Command
{
    public class NDVRegister : BaseRegister
    {
        protected override object? Register(object executer, object[] args)
        {
            string assd = "assd[adada]";

            var par = new RegexParam()
            {
                Input = assd,
                Param1 = "[",
                Param2 = "]",
                ReturnMode = NetworkCoreStandard.Common.Utility.DRXRegex.ReturnMode.ReturnMatchedStrings,
            };

            Logger.Log("abc",DRXRegex.Execute(par).ToString());

            return null;
        }

        protected override int GetParamCount()
        {
            return 2;
        }

        protected override uint GetPermissionGroup()
        {
            return 0;
        }

        protected override Type[]? GetParamsType()
        {
            return new Type[] { typeof(string) };
        }
    }
}
