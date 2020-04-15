using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Http.Server.Definitions;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.UkrlpAdapter.Application.LearningProviders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Dfe.Spi.UkrlpAdapter.Functions.LearningProviders
{
    public class GetLearningProviders
    {
        private const string FunctionName = nameof(GetLearningProviders);

        private readonly ILearningProviderManager _learningProviderManager;
        private readonly IHttpSpiExecutionContextManager _httpSpiExecutionContextManager;
        private readonly ILoggerWrapper _logger;

        public GetLearningProviders(
            ILearningProviderManager learningProviderManager, 
            IHttpSpiExecutionContextManager httpSpiExecutionContextManager, 
            ILoggerWrapper logger)
        {
            _learningProviderManager = learningProviderManager;
            _httpSpiExecutionContextManager = httpSpiExecutionContextManager;
            _logger = logger;
        }
        
        [FunctionName(FunctionName)]
        public async Task<IActionResult> RunAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "learning-providers")]
            HttpRequest req,
            CancellationToken cancellationToken)
        {
            _httpSpiExecutionContextManager.SetContext(req.Headers);
            _logger.Info($"{FunctionName} triggered at {DateTime.Now}");

            // Read request
            string json;
            using (var reader = new StreamReader(req.Body))
            {
                json = await reader.ReadToEndAsync();
            }
            _logger.Debug($"{FunctionName} read json {json} from body");
            
            // Deserialize it
            var request = JsonConvert.DeserializeObject<GetLearningProvidersRequest>(json);
            _logger.Debug($"Deserialized request to {JsonConvert.SerializeObject(request)}");
            
            // Get the results
            var providers = await _learningProviderManager.GetLearningProvidersAsync(request.Identifiers, request.Fields, cancellationToken);
            
            // Return
            if (JsonConvert.DefaultSettings != null)
            {
                return new JsonResult(
                    providers,
                    JsonConvert.DefaultSettings());
            }
            else
            {
                return new JsonResult(providers);
            }
        }
    }

    public class GetLearningProvidersRequest
    {
        public string[] Identifiers { get; set; }
        public string[] Fields { get; set; }
    }
}