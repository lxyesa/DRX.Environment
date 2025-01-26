using DRX.Framework.Common.Args;
using DRX.Framework.Common.Base.Command;

namespace KaxServer.Commands
{
    public class TemplateCommand : BaseCommand
    {
        public override object? Execute(object[] args, object executer)
        {
            // 检查执行者的权限级别
            if (!VerifyPermissionLevel(GetExecuterPermissionLevel(executer)))
            {
                var result = new CommandResult
                {
                    Result = null,
                    Message = "错误",
                    IsSuccess = false
                };

                return result;
            }

            // 返回命令结果。
            return new object();
        }
    }
}
