﻿using System;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Http.Client;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.Models.Entities;
using Dfe.Spi.UkrlpAdapter.Domain.Configuration;
using Dfe.Spi.UkrlpAdapter.Domain.Events;
using Newtonsoft.Json;
using RestSharp;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.SpiMiddleware
{
    public class MiddlewareEventPublisher : IEventPublisher
    {
        private readonly IRestClient _restClient;
        private readonly ILoggerWrapper _logger;

        public MiddlewareEventPublisher(
            AuthenticationConfiguration authenticationConfiguration,
            MiddlewareConfiguration configuration,
            IRestClient restClient,
            ILoggerWrapper logger)
        {
            _restClient = restClient;
            _restClient.BaseUrl = new Uri(configuration.BaseUrl, UriKind.Absolute);
            if (!string.IsNullOrEmpty(configuration.SubscriptionKey))
            {
                _restClient.DefaultParameters.Add(new Parameter("Ocp-Apim-Subscription-Key", configuration.SubscriptionKey,
                    ParameterType.HttpHeader));
            }

            _restClient.Authenticator = new OAuth2ClientCredentialsAuthenticator(
                authenticationConfiguration.TokenEndpoint,
                authenticationConfiguration.ClientId,
                authenticationConfiguration.ClientSecret,
                authenticationConfiguration.Resource);

            _logger = logger;
        }

        public async Task PublishLearningProviderCreatedAsync(LearningProvider learningProvider, DateTime pointInTime, CancellationToken cancellationToken)
        {
            var @event = new PointInTimeMiddlewareEvent<LearningProvider>
            {
                Details = learningProvider,
                PointInTime = pointInTime,
            };
            await SendEventToMiddleware("learning-provider-created", @event, cancellationToken);
            _logger.Debug($"Published learning provider created: {JsonConvert.SerializeObject(learningProvider)}");
        }

        public async Task PublishLearningProviderUpdatedAsync(LearningProvider learningProvider, DateTime pointInTime, CancellationToken cancellationToken)
        {
            var @event = new PointInTimeMiddlewareEvent<LearningProvider>
            {
                Details = learningProvider,
                PointInTime = pointInTime,
            };
            await SendEventToMiddleware("learning-provider-updated", @event, cancellationToken);
            _logger.Debug($"Published learning provider updated: {JsonConvert.SerializeObject(learningProvider)}");
        }

        private async Task SendEventToMiddleware(string eventType, object details, CancellationToken cancellationToken)
        {
            var request = new RestRequest(eventType, Method.POST, DataFormat.Json);
            request.AddParameter(string.Empty, JsonConvert.SerializeObject(details), ParameterType.RequestBody);
            
            var response = await _restClient.ExecuteTaskAsync(request, cancellationToken);
            if (!response.IsSuccessful)
            {
                throw new MiddlewareException(eventType, response.StatusCode, response.Content);
            }
        }
    }
}