using System;

namespace Drx.Sdk.Network.DataBase
{
    /// <summary>
    /// 数据库子表的基础接口 - V2 版本
    /// 相比 V1 增加了 String 类型的 ID、时间戳等字段，支持更灵活的关系管理
    /// </summary>
    public interface IDataTableV2
    {
        /// <summary>
        /// 子表唯一标识（String 类型，GUID 格式）
        /// 用于防止父表 ID 重复时的冲突
        /// </summary>
        string Id { get; set; }

        /// <summary>
        /// 父表主键ID
        /// </summary>
        int ParentId { get; set; }

        /// <summary>
        /// 创建时间（Unix 时间戳，毫秒）
        /// </summary>
        long CreatedAt { get; set; }

        /// <summary>
        /// 最后更新时间（Unix 时间戳，毫秒）
        /// </summary>
        long UpdatedAt { get; set; }

        /// <summary>
        /// 子表表名。如果返回空或 null，将使用类名作为表名
        /// </summary>
        string TableName { get; }
    }
}
