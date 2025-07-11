using System;

namespace Drx.Sdk.Network.Sqlite
{
    /// <summary>
    /// 表示数据库关联子表的基础接口
    /// </summary>
    public interface IDataTable
    {
        /// <summary>
        /// 父表主键ID
        /// </summary>
        int ParentId { get; set; }

        /// <summary>
        /// 子表表名。如果返回空或null，将使用类名作为表名。
        /// </summary>
        string TableName { get; }
    }
}
