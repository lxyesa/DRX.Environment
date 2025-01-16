using DRX.Framework.Common.Args;
using DRX.Framework.Common.Components;
using DRX.Framework.Common.Enums;
using DRX.Framework.Common.Interface;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DRX.Framework.Common.Base.Command
{
    public abstract class BaseCommand : ICommand
    {

        public virtual uint PermissionGroup => (uint)PermissionGroupType.Client;
        protected BaseCommand()
        {
            
        }

        public virtual object? Execute(object[] args, object executer)
        {
            return null;
        }

        protected virtual DRXSocket? GetExecuterSocket(object executer)
        {
            return executer as DRXSocket;
        }

        protected virtual ClientComponent? GetExecuterClientComponent(object executer)
        {
            return GetExecuterSocket(executer)?.GetComponent<ClientComponent>();
        }

        protected virtual uint GetExecuterPermissionLevel(object executer)
        {
            var owner = executer;
            if (owner is DRXSocket socket)
            {
                if (socket.HasComponent<PermissionGroup>())
                {
                    var permissionGroup = socket.GetComponent<PermissionGroup>();
                    if (permissionGroup != null)
                    {
                        return (uint)permissionGroup.GetPermissionGroup();
                    }
                }
            }
            return 0;
        }

        /// <summary>
        /// 验证参数，返回验证结果
        /// </summary>
        /// <param name="args">参数数组</param>
        /// <returns>参数验证结果</returns>
        protected virtual CommandResult VerifyParams(object[] args)
        {
            Type[]? expectedTypes = GetParamsType();
            string header = "BaseCommand";

            var result = new CommandResult();

            if (expectedTypes == null)
            {
                // 如果 GetParamsType 返回 null，则表示该命令无参数
                if (args.Length != 0)
                {
                    result.Message = "该命令不接受任何参数。";
                    result.IsSuccess = false;
                    return result;
                }
                result.IsSuccess = true;
                return result;
            }

            int expectedCount = expectedTypes.Length;

            if (args == null)
            {
                if (expectedCount != 0)
                {
                    result.Message = "参数不能为空。";
                    result.IsSuccess = false;
                    return result;
                }
                result.IsSuccess = true;
                return result;
            }

            if (args.Length != expectedCount)
            {
                result.Message = $"参数数量不匹配。预期参数数量为 {expectedCount}，但收到 {args.Length} 个参数。";
                result.IsSuccess = false;
                return result;
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == null)
                {
                    // 如果预期类型是值类型，null 不合法
                    if (expectedTypes[i].IsValueType)
                    {
                        result.Message = $"参数 {i} 不能为 null。";
                        result.IsSuccess = false;
                        return result;
                    }
                    continue;
                }

                if (!expectedTypes[i].IsAssignableFrom(args[i].GetType()))
                {
                    result.Message = $"参数 {i} 的类型不正确。预期类型为 {expectedTypes[i].Name}，但收到 {args[i].GetType().Name}。";
                    result.IsSuccess = false;
                    return result;
                }
            }

            result.IsSuccess = true;
            return result;
        }

        protected virtual bool VerifyPermissionLevel(uint permissionLevel)
        {
            string header = "BaseCommand";
            if (permissionLevel < GetPermissionGroup())
            {
                Logger.Log(LogLevel.Warning, header, $"权限级别不足。需要: {GetPermissionGroup()}, 当前: {permissionLevel}");
                return false;
            }
            return true;
        }

        protected virtual uint GetPermissionGroup()
        {
            return PermissionGroup;
        }

        protected virtual int GetParamCount()
        {
            return 0;
        }

        protected virtual Type[]? GetParamsType()
        {
            return null;
        }

        public virtual object? Info()
        {
            return null;
        }
    }

    public enum VerifyParamsResult
    {
        /// <summary>
        /// 验证成功
        /// </summary>
        Success,

        /// <summary>
        /// 不需要任何参数但传入了参数
        /// </summary>
        NoParametersExpected,

        /// <summary>
        /// 参数数量不匹配
        /// </summary>
        ParamCountMismatch,

        /// <summary>
        /// 参数为空
        /// </summary>
        NullArgument,

        /// <summary>
        /// 参数类型不匹配
        /// </summary>
        TypeMismatch
    }
}
