using System.Collections.Generic;
using Web.KaxServer.Models;
using Web.KaxServer.Services.Queries;

namespace Web.KaxServer.Services
{
    /// <summary>
    /// CDK服务接口，定义CDK相关业务逻辑
    /// </summary>
    public interface ICdkService
    {
        /// <summary>
        /// 创建一批CDK
        /// </summary>
        /// <param name="quantity">数量</param>
        /// <param name="type">类型</param>
        /// <param name="assetId">资产ID（对于资产类型）</param>
        /// <param name="coinAmount">金币数量（对于金币类型）</param>
        /// <param name="durationValue">持续时间值（对于资产类型）</param>
        /// <param name="durationUnit">持续时间单位（对于资产类型）</param>
        /// <returns>创建的CDK列表</returns>
        List<Cdk> CreateCdks(int quantity, CdkType type, int? assetId, decimal? coinAmount, int? durationValue = null, DurationUnit? durationUnit = null);
        
        /// <summary>
        /// 查询CDK
        /// </summary>
        /// <param name="parameters">查询参数</param>
        /// <returns>查询结果</returns>
        CdkQueryResult QueryCdks(CdkQueryParameters parameters);
        
        /// <summary>
        /// 通过代码获取CDK
        /// </summary>
        /// <param name="code">CDK代码</param>
        /// <returns>CDK对象</returns>
        Cdk? GetCdkByCode(string code);
        
        /// <summary>
        /// 验证CDK是否有效
        /// </summary>
        /// <param name="code">CDK代码</param>
        /// <returns>CDK对象（如果有效）</returns>
        Cdk? VerifyCdk(string code);
        
        /// <summary>
        /// 激活CDK
        /// </summary>
        /// <param name="code">CDK代码</param>
        /// <param name="userSession">用户会话</param>
        /// <returns>激活的CDK对象</returns>
        Cdk? ActivateCdk(string code, UserSession userSession);
        
        /// <summary>
        /// 删除批次CDK
        /// </summary>
        /// <param name="batchId">批次ID</param>
        void DeleteCdksByBatchId(string batchId);
        
        /// <summary>
        /// 从CDK代码获取批次ID
        /// </summary>
        /// <param name="code">CDK代码</param>
        /// <returns>批次ID</returns>
        string? GetBatchIdFromCdkCode(string code);
    }
} 