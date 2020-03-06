using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Dfe.Spi.Common.Http.Server;
using Dfe.Spi.Models.Entities;
using Dfe.Spi.UkrlpAdapter.Domain.Cache;
using Dfe.Spi.UkrlpAdapter.Domain.Configuration;
using Dfe.Spi.UkrlpAdapter.Domain.Mapping;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;
using Dfe.Spi.UkrlpAdapter.Infrastructure.AzureStorage.Cache;
using Dfe.Spi.UkrlpAdapter.Infrastructure.InProcMapping.PocoMapping;
using Dfe.Spi.UkrlpAdapter.Infrastructure.SpiTranslator;
using Newtonsoft.Json;
using RestSharp;

namespace ExportCacheToLearningProviderFile
{
    class Program
    {
        private static Logger _logger;
        private static HttpSpiExecutionContextManager _httpSpiExecutionContextManager;
        private static IProviderRepository _providerRepository;
        private static IMapper _mapper;

        static async Task Run(CommandLineOptions options, CancellationToken cancellationToken = default)
        {
            Init(options);

            var providers = await ReadCurrentProvidersFromCache(cancellationToken);
            _logger.Info($"Read {providers.Length} providers from cache");

            var learningProviders = await MapProvidersToLearningProviders(providers, cancellationToken);
            await WriteOutput(learningProviders, options.OutputPath);
        }

        static void Init(CommandLineOptions options)
        {
            _httpSpiExecutionContextManager = new HttpSpiExecutionContextManager();
            _httpSpiExecutionContextManager.SetInternalRequestId(Guid.NewGuid());

            _providerRepository = new TableProviderRepository(
                new CacheConfiguration
                {
                    TableStorageConnectionString = options.TableStorageConnectionString,
                    ProviderTableName = options.ProviderTableName,
                }, 
                _logger);
            
            var translator = new TranslatorApiClient(
                new AuthenticationConfiguration()
                {
                    ClientId = options.ClientId,
                    ClientSecret = options.ClientSecret,
                    Resource = options.Resource,
                    TokenEndpoint = options.TokenEndpoint,
                },
                new RestClient(),
                new TranslatorConfiguration
                {
                    BaseUrl = options.TranslatorBaseUrl,
                    SubscriptionKey = options.TranslatorSubscriptionKey,
                }, 
                _logger,
                _httpSpiExecutionContextManager);
            _mapper = new PocoMapper(translator);
        }

        static async Task<Provider[]> ReadCurrentProvidersFromCache(CancellationToken cancellationToken)
        {
            _logger.Debug("Reading providers from cache");
            return await _providerRepository.GetProvidersAsync(cancellationToken);
        }

        static async Task<LearningProvider[]> MapProvidersToLearningProviders(Provider[] providers, CancellationToken cancellationToken)
        {
            var learningProviders = new LearningProvider[providers.Length];
            
            for (var i = 0; i < providers.Length; i++)
            {
                _logger.Info($"Starting to map establishment {i} of {providers.Length}");

                learningProviders[i] = await _mapper.MapAsync<LearningProvider>(providers[i], cancellationToken);
            }
            
            _logger.Info($"Mapped {learningProviders.Length} learning providers");
            return learningProviders;
        }

        static async Task WriteOutput(LearningProvider[] learningProviders, string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir))
            {
                _logger.Info($"Creating directory {dir}");
                Directory.CreateDirectory(dir);
            }
            
            var json = JsonConvert.SerializeObject(learningProviders);
            using(var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new StreamWriter(stream))
            {
                await writer.WriteAsync(json);
                await writer.FlushAsync();
            }
            
            _logger.Info($"Written {learningProviders.Length} learning providers to {path}");
        }
        
        static void Main(string[] args)
        {
            _logger = new Logger();

            CommandLineOptions options = null;
            Parser.Default.ParseArguments<CommandLineOptions>(args).WithParsed((parsed) => options = parsed);
            if (options != null)
            {
                try
                {
                    Run(options).Wait();
                }
                catch (Exception ex)
                {
                    _logger.Error(ex);
                }

                _logger.Info("Done. Press any key to exit...");
                Console.ReadKey();
            }
        }
    }
}