using System;
using System.IO;
using System.Threading.Tasks;
using Dfe.Spi.Common.Logging.Definitions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Dfe.Spi.UkrlpAdapter.Functions.LearningProviders
{
    public class GetLearningProvider
    {
        private const string FunctionName = nameof(GetLearningProvider);
        
        private readonly ILoggerWrapper _logger;

        public GetLearningProvider(ILoggerWrapper logger)
        {
            _logger = logger;
        }
        
        [FunctionName(FunctionName)]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "learning-providers/{id}")]
            HttpRequest req,
            string id)
        {
            _logger.SetContext(req.Headers);
            _logger.Info($"{FunctionName} triggered at {DateTime.Now} with id {id}");
            
            return new OkResult();
        }
    }
}