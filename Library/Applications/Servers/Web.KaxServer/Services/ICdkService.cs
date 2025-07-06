using System.Collections.Generic;
using Web.KaxServer.Models;
using Web.KaxServer.Services.Queries;

namespace Web.KaxServer.Services
{
    public interface ICdkService
    {
        List<Cdk> CreateCdks(int quantity, CdkType type, int? assetId, decimal? coinAmount, int? durationValue = null, DurationUnit? durationUnit = null);
        
        CdkQueryResult QueryCdks(CdkQueryParameters parameters);

        Cdk? GetCdkByCode(string code);
        Cdk? VerifyCdk(string code);
        Cdk? ActivateCdk(string code, UserSession userSession);
        void DeleteCdksByBatchId(string batchId);
        string? GetBatchIdFromCdkCode(string code);
    }
} 