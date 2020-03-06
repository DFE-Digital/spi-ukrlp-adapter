using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Context.Definitions;
using Dfe.Spi.Common.Context.Models;
using Dfe.Spi.Common.Http.Client;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.Common.WellKnownIdentifiers;
using Dfe.Spi.UkrlpAdapter.Domain.Configuration;
using Dfe.Spi.UkrlpAdapter.Domain.Translation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RestSharp;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.SpiTranslator
{
    public class TranslatorApiClient : ITranslator
    {
        private readonly IRestClient _restClient;
        private readonly ILoggerWrapper _logger;
        private readonly OAuth2ClientCredentialsAuthenticator _oAuth2ClientCredentialsAuthenticator;
        private readonly ISpiExecutionContextManager _spiExecutionContextManager;
        private readonly Dictionary<string, Dictionary<string, string[]>> _cache;

        public TranslatorApiClient(
            AuthenticationConfiguration authenticationConfiguration,
            IRestClient restClient,
            TranslatorConfiguration configuration,
            ILoggerWrapper logger,
            ISpiExecutionContextManager spiExecutionContextManager)
        {
            _restClient = restClient;
            _restClient.BaseUrl = new Uri(configuration.BaseUrl);
            if (!string.IsNullOrEmpty(configuration.SubscriptionKey))
            {
                _restClient.DefaultParameters.Add(
                    new Parameter("Ocp-Apim-Subscription-Key", configuration.SubscriptionKey, ParameterType.HttpHeader));
            }
            
            _logger = logger;

            _oAuth2ClientCredentialsAuthenticator = new OAuth2ClientCredentialsAuthenticator(
                authenticationConfiguration.TokenEndpoint,
                authenticationConfiguration.ClientId,
                authenticationConfiguration.ClientSecret,
                authenticationConfiguration.Resource);

            _spiExecutionContextManager = spiExecutionContextManager;

            _cache = new Dictionary<string, Dictionary<string, string[]>>();
        }

        public async Task<string> TranslateEnumValue(string enumName, string sourceValue,
            CancellationToken cancellationToken)
        {
            var mappings = await GetMappings(enumName, sourceValue, cancellationToken);
            var mapping = mappings.FirstOrDefault(kvp =>
                kvp.Value.Any(v => v.Equals(sourceValue, StringComparison.InvariantCultureIgnoreCase))).Key;
            if (string.IsNullOrEmpty(mapping))
            {
                _logger.Info($"No enum mapping found for GIAS for {enumName} with value {sourceValue}");
                return null;
            }
            
            _logger.Debug($"Found mapping of {mapping} for {enumName} with value {sourceValue}");
            return mapping;
        }

        private async Task<Dictionary<string, string[]>> GetMappings(string enumName, string sourceValue,
            CancellationToken cancellationToken)
        {
            var cacheKey = $"{enumName}:{sourceValue}";
            if (_cache.ContainsKey(cacheKey))
            {
                return _cache[cacheKey];
            }

            var mappings = await GetMappingsFromApi(enumName, sourceValue, cancellationToken);
            _cache.Add(cacheKey, mappings);
            return mappings;
        }

        private async Task<Dictionary<string, string[]>> GetMappingsFromApi(string enumName, string sourceValue,
            CancellationToken cancellationToken)
        {
            var resource = $"enumerations/{enumName}/{SourceSystemNames.UkRegisterOfLearningProviders}";
            _logger.Info($"Calling {resource} on translator api");
            var request = new RestRequest(resource, Method.GET);

            SpiExecutionContext spiExecutionContext =
                _spiExecutionContextManager.SpiExecutionContext;

            request.AppendContext(spiExecutionContext);

            // Do we have an OAuth token?
            // Or is this a server process?
            string identityToken = spiExecutionContext.IdentityToken;

            if (string.IsNullOrEmpty(identityToken))
            {
                _logger.Debug(
                    $"No OAuth token present in the " +
                    $"{nameof(SpiExecutionContext)}. The " +
                    $"{nameof(OAuth2ClientCredentialsAuthenticator)} will " +
                    $"be used, and a new token generated.");

                // The fact we don't have a token means this is probably a
                // server process.
                // We need to generate one...
                _restClient.Authenticator = _oAuth2ClientCredentialsAuthenticator;
            }
            else
            {
                _logger.Debug(
                    $"OAuth token present in the " +
                    $"{nameof(SpiExecutionContext)}. This will be used in " +
                    $"calling the Translator.");
            }

            var response = await _restClient.ExecuteTaskAsync(request, cancellationToken);
            if (!response.IsSuccessful)
            {
                throw new TranslatorApiException(
                    resource,
                    response.StatusCode,
                    response.Content,
                    response.ErrorException);
            }

            _logger.Info($"Received {response.Content}");
            var translationResponse = JsonConvert.DeserializeObject<TranslationResponse>(response.Content);
            return translationResponse.MappingsResult.Mappings;
        }
    }

    internal class TranslationResponse
    {
        public TranslationMappingsResult MappingsResult { get; set; }
    }

    internal class TranslationMappingsResult
    {
        public Dictionary<string, string[]> Mappings { get; set; }
    }
}