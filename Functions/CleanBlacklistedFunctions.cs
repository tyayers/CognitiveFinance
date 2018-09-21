
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;

namespace CogStockFunctions.Functions
{
    public static class CleanBlacklistedFunctions
    {
        [FunctionName("CleanBlacklisted")]
        public static IActionResult Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequest req, TraceWriter log)
        {
            log.Info("C# CleanBlacklisted HTTP trigger function processed a request.");

            int deletedRows = Utils.DataRepository.CleanBlacklistedCompanies(log);

            log.Info($"Deleted {deletedRows} rows.");

            return new OkObjectResult($"Deleted {deletedRows} rows.");
        }
    }
}
