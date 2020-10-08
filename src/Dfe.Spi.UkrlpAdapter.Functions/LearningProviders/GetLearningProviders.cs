using System;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Http.Server;
using Dfe.Spi.Common.Http.Server.Definitions;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.Common.Models;
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
    public class GetLearningProviders : FunctionsBase<GetLearningProvidersRequest>
    {
        private const string FunctionName = nameof(GetLearningProviders);

        private readonly ILearningProviderManager _learningProviderManager;
        private readonly ILoggerWrapper _logger;

        public GetLearningProviders(
            ILearningProviderManager learningProviderManager,
            IHttpSpiExecutionContextManager httpSpiExecutionContextManager,
            ILoggerWrapper logger)
            : base(httpSpiExecutionContextManager, logger)
        {
            _learningProviderManager = learningProviderManager;
            _logger = logger;
        }

        [FunctionName(FunctionName)]
        public async Task<IActionResult> RunAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "learning-providers")]
            HttpRequest req,
            CancellationToken cancellationToken)
        {
            return await ValidateAndRunAsync(req, null, cancellationToken);
        }

        protected override HttpErrorBodyResult GetMalformedErrorResponse(FunctionRunContext runContext)
        {
            return new HttpErrorBodyResult(
                HttpStatusCode.BadRequest,
                Errors.GetLearningProvidersMalformedRequest.Code,
                Errors.GetLearningProvidersMalformedRequest.Message);
        }

        protected override HttpErrorBodyResult GetSchemaValidationResponse(JsonSchemaValidationException validationException, FunctionRunContext runContext)
        {
            return new HttpSchemaValidationErrorBodyResult(Errors.GetLearningProvidersSchemaValidation.Code, validationException);
        }

        protected override async Task<IActionResult> ProcessWellFormedRequestAsync(GetLearningProvidersRequest request, FunctionRunContext runContext,
            CancellationToken cancellationToken)
        {
            try
            {
                var providers = await _learningProviderManager.GetLearningProvidersAsync(request.Identifiers, request.Fields, request.Live, request.PointInTime,
                    cancellationToken);

                return new FormattedJsonResult(providers);
            }
            catch (ArgumentException ex)
            {
                _logger.Info($"{FunctionName} returning bad request: {ex.Message}");

                return new HttpErrorBodyResult(
                    new HttpErrorBody
                    {
                        StatusCode = HttpStatusCode.BadRequest,
                        ErrorIdentifier = "SPI-UKRLP-PROV01",
                        Message = ex.Message
                    });
            }
        }
    }

    public class GetLearningProvidersRequest : RequestResponseBase
    {
        public string[] Identifiers { get; set; }
        public string[] Fields { get; set; }
        public bool Live { get; set; }
        public DateTime? PointInTime { get; set; }
    }
}