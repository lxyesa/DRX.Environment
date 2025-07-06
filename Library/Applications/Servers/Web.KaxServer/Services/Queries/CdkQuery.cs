using System.Collections.Generic;
using Web.KaxServer.Models;

namespace Web.KaxServer.Services.Queries
{
    public class CdkQueryResult
    {
        public List<Cdk> Cdks { get; set; } = new List<Cdk>();
        public int TotalCount { get; set; }
    }

    public class CdkQueryParameters
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public string? SortBy { get; set; }
        public string? SearchTerm { get; set; }
    }
} 