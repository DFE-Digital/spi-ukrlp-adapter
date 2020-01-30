using System.IO;
using Dfe.Spi.Common.Logging;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.UkrlpAdapter.Application.Cache;
using Dfe.Spi.UkrlpAdapter.Application.LearningProviders;
using Dfe.Spi.UkrlpAdapter.Domain.Cache;
using Dfe.Spi.UkrlpAdapter.Domain.Configuration;
using Dfe.Spi.UkrlpAdapter.Domain.Events;
using Dfe.Spi.UkrlpAdapter.Domain.Mapping;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;
using Dfe.Spi.UkrlpAdapter.Functions;
using Dfe.Spi.UkrlpAdapter.Infrastructure.AzureStorage.Cache;
using Dfe.Spi.UkrlpAdapter.Infrastructure.InProcMapping.PocoMapping;
using Dfe.Spi.UkrlpAdapter.Infrastructure.SpiMiddleware;
using Dfe.Spi.UkrlpAdapter.Infrastructure.UkrlpSoapApi;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RestSharp;

[assembly: FunctionsStartup(typeof(Startup))]
namespace Dfe.Spi.UkrlpAdapter.Functions
{
    public class Startup : FunctionsStartup
    {
        private IConfigurationRoot _rawConfiguration;
        private UkrlpAdapterConfiguration _configuration;

        public override void Configure(IFunctionsHostBuilder builder)
        {
            var services = builder.Services;

            LoadAndAddConfiguration(services);
            AddLogging(services);
            AddHttp(services);
            AddEventPublishing(services);
            AddUkrlpApi(services);
            AddMapping(services);
            AddCache(services);
            AddManagers(services);
        }

        private void LoadAndAddConfiguration(IServiceCollection services)
        {
            _rawConfiguration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("local.settings.json", true)
                .AddEnvironmentVariables(prefix: "SPI_")
                .Build();
            services.AddSingleton(_rawConfiguration);

            _configuration = new UkrlpAdapterConfiguration();
            _rawConfiguration.Bind(_configuration);
            services.AddSingleton(_configuration);
            services.AddSingleton(_configuration.UkrlpApi);
            services.AddSingleton(_configuration.Cache);
            services.AddSingleton(_configuration.Middleware);
        }
        
        private void AddLogging(IServiceCollection services)
        {
            services.AddLogging();
            services.AddScoped(typeof(ILogger<>), typeof(Logger<>));
            services.AddScoped<ILogger>(provider =>
                provider.GetService<ILoggerFactory>().CreateLogger(LogCategories.CreateFunctionUserCategory("Common")));
            services.AddScoped<ILoggerWrapper, LoggerWrapper>();
        }

        private void AddHttp(IServiceCollection services)
        {
            services.AddTransient<IRestClient, RestClient>();
        }

        private void AddEventPublishing(IServiceCollection services)
        {
            services.AddScoped<IEventPublisher, MiddlewareEventPublisher>();
        }

        private void AddUkrlpApi(IServiceCollection services)
        {
            services.AddScoped<IUkrlpApiClient, UkrlpSoapApiClient>();
        }

        private void AddMapping(IServiceCollection services)
        {
            services.AddScoped<IMapper, PocoMapper>();
        }

        private void AddCache(IServiceCollection services)
        {
            services.AddScoped<IStateRepository, TableStateRepository>();
            services.AddScoped<IProviderRepository, TableProviderRepository>();
            services.AddScoped<IProviderProcessingQueue, QueueProviderProcessingQueue>();
        }

        private void AddManagers(IServiceCollection services)
        {
            services.AddScoped<ILearningProviderManager, LearningProviderManager>();
            services.AddScoped<ICacheManager, CacheManager>();
        }
    }
}