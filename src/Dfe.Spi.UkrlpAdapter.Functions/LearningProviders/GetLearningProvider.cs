using System;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.UkrlpAdapter.Application.LearningProviders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Dfe.Spi.Common.Http.Server.Definitions;

namespace Dfe.Spi.UkrlpAdapter.Functions.LearningProviders
{
    public class GetLearningProvider
    {
        private const string FunctionName = nameof(GetLearningProvider);

        private readonly ILearningProviderManager _learningProviderManager;
        private readonly IHttpSpiExecutionContextManager _httpSpiExecutionContextManager;
        private readonly ILoggerWrapper _logger;

        public GetLearningProvider(ILearningProviderManager learningProviderManager, IHttpSpiExecutionContextManager httpSpiExecutionContextManager, ILoggerWrapper logger)
        {
            _learningProviderManager = learningProviderManager;
            _httpSpiExecutionContextManager = httpSpiExecutionContextManager;
            _logger = logger;
        }
        
        [FunctionName(FunctionName)]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "learning-providers/{id}")]
            HttpRequest req,
            string id,
            CancellationToken cancellationToken)
        {
            _httpSpiExecutionContextManager.SetContext(req.Headers);
            _logger.Info($"{FunctionName} triggered at {DateTime.Now} with id {id}");

            try
            {
                var learningProvider = await _learningProviderManager.GetLearningProviderAsync(id, cancellationToken);

                if (learningProvider == null)
                {
                    _logger.Info($"{FunctionName} found no learning provider with id {id}. Returning not found");
                    return new NotFoundResult();
                }

                _logger.Info($"{FunctionName} found learning provider with id {id}. Returning ok");
                return new OkObjectResult(learningProvider);
            }
            catch (ArgumentException ex)
            {
                _logger.Info($"{FunctionName} returning bad request: {ex.Message}");
                return new BadRequestObjectResult(ex.Message);
            }
        }
    }
}