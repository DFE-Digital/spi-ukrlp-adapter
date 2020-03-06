using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Dfe.Spi.Common.Http.Server;
using Dfe.Spi.UkrlpAdapter.Domain.Cache;
using Dfe.Spi.UkrlpAdapter.Domain.Configuration;
using Dfe.Spi.UkrlpAdapter.Domain.Mapping;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;
using Dfe.Spi.UkrlpAdapter.Infrastructure.AzureStorage.Cache;
using Dfe.Spi.UkrlpAdapter.Infrastructure.InProcMapping.PocoMapping;
using Dfe.Spi.UkrlpAdapter.Infrastructure.SpiTranslator;
using Dfe.Spi.UkrlpAdapter.Infrastructure.UkrlpSoapApi;
using RestSharp;

namespace SeedCacheFromListOfUkprns
{
    class Program
    {
        private static Logger _logger;
        private static HttpSpiExecutionContextManager _httpSpiExecutionContextManager;
        private static IUkrlpApiClient _ukrlpApiClient;
        private static IProviderRepository _providerRepository;

        static async Task Run(CommandLineOptions options, CancellationToken cancellationToken = default)
        {
            Init(options);

            var ukprns = await ReadListOfUkprns(options);
            _logger.Info($"Found {ukprns.Length} UKPRNs");

            await ProcessUkprns(ukprns, cancellationToken);
        }

        static void Init(CommandLineOptions options)
        {
            _ukrlpApiClient = new UkrlpSoapApiClient(
                new RestClient(),
                new UkrlpApiConfiguration
                {
                    Url = options.UkrlpUrl,
                    StakeholderId = options.UkrlpStakeholderId,
                });

            _httpSpiExecutionContextManager = new HttpSpiExecutionContextManager();
            _httpSpiExecutionContextManager.SetInternalRequestId(Guid.NewGuid());

            _providerRepository = new TableProviderRepository(
                new CacheConfiguration
                {
                    TableStorageConnectionString = options.TableStorageConnectionString,
                    ProviderTableName = options.ProviderTableName,
                    StateTableName = options.StateTableName,
                }, 
                _logger);
        }

        static async Task<long[]> ReadListOfUkprns(CommandLineOptions options)
        {
            var ukprns = new List<long>();

            _logger.Info($"Readling list of UKPRNs from {options.Path}");
            using (var stream = new FileStream(options.Path, FileMode.Open, FileAccess.Read))
            using (var reader = new StreamReader(stream))
            {
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (!string.IsNullOrEmpty(line?.Trim()))
                    {
                        ukprns.Add(long.Parse(line.Trim()));
                    }
                }
            }

            return ukprns.ToArray();
        }

        static async Task ProcessUkprns(long[] ukprns, CancellationToken cancellationToken)
        {
            const int batchSize = 100;
            var position = 0;
            
            while (position <= ukprns.Length)
            {
                var batch = ukprns.Skip(position).Take(batchSize).ToArray();
                _logger.Debug($"Processing {position} to {position + batch.Length} of {ukprns.Length} ukprns");
                
                var providers = await _ukrlpApiClient.GetProvidersAsync(batch, cancellationToken);
                foreach (var provider in providers)
                {
                    await _providerRepository.StoreAsync(provider, cancellationToken);
                    _logger.Debug($"Stored {provider.UnitedKingdomProviderReferenceNumber} in repository");
                }

                position += batchSize;
            }
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