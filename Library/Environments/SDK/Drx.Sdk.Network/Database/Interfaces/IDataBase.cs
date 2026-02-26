using System;

namespace Drx.Sdk.Network.DataBase
{
    /// <summary>
    /// 表示数据库主表实体的基础接口
    /// </summary>
    public interface IDataBase
    {
        /// <summary>
        /// 主键ID
        /// </summary>
        int Id { get; set; }

        /// <summary>
        /// 获取表名。如果返回空或null，将使用类名作为表名。
        /// </summary>
        string TableName => null;
    }
}
