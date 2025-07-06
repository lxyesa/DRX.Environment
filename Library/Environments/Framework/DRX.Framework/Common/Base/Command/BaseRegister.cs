
using DRX.Framework.Common.Args;
using DRX.Framework.Common.Utility;

namespace DRX.Framework.Common.Base.Command
{
    public abstract class BaseRegister : BaseCommand
    {
        public override object? Execute(object[] args, object executer)
        {
            // 检查执行者的权限级别
            if (!VerifyPermissionLevel(GetExecuterPermissionLevel(executer)))
            {
                return VerifyPermissionField(executer);
            }

            // 验证参数
            if (!VerifyParams(args).IsSuccess)
            {
                int argsCount = args?.Length ?? 0;
                return VerifyArgsField(executer, argsCount);
            }

            // 执行注册逻辑
            return Register(executer, args);
        }

        /// <summary>
        /// 验证权限字段失败
        /// </summary>
        /// <param name="executer">执行者</param>
        /// <returns></returns>
        protected virtual object? VerifyPermissionField(object executer)
        {
            var message = "permission_not_enough_msg".GetGroup("register");

            var result = new CommandResult
            {
                Result = null,
                Message = message,
                IsSuccess = false
            };

            return result;
        }

        /// <summary>
        /// 验证参数字段失败
        /// </summary>
        /// <param name="executer">执行者</param>
        /// <param name="argsCount">传入参数的数量</param>
        /// <returns></returns>
        protected virtual object? VerifyArgsField(object executer, int argsCount)
        {
            var result = new CommandResult
            {
                Result = null,
                Message = "message",
                IsSuccess = false
            };
            return result;
        }

        /// <summary>
        /// 执行注册逻辑的抽象方法
        /// </summary>
        /// <param name="executer">执行者</param>
        /// <param name="args">参数数组</param>
        /// <returns></returns>
        protected abstract object? Register(object executer, object[] args);
    }
}
