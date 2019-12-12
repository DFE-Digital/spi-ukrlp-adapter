using System.IO;
using Dfe.Spi.Common.Logging;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.UkrlpAdapter.Domain.Configuration;
using Dfe.Spi.UkrlpAdapter.Functions;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Azure.WebJobs.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        }
        
        private void AddLogging(IServiceCollection services)
        {
            services.AddLogging();
            services.AddScoped(typeof(ILogger<>), typeof(Logger<>));
            services.AddScoped<ILogger>(provider =>
                provider.GetService<ILoggerFactory>().CreateLogger(LogCategories.CreateFunctionUserCategory("Common")));
            services.AddScoped<ILoggerWrapper, LoggerWrapper>();
        }
    }
}