using DRX.Framework;
using DRX.Framework.Common.Args;
using DRX.Framework.Common.Base.Command;
using DRX.Framework.Common.Utility;

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
                ReturnMode = DRXRegex.ReturnMode.ReturnMatchedStrings,
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
