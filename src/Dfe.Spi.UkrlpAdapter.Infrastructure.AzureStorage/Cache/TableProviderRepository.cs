using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.UkrlpAdapter.Domain.Cache;
using Dfe.Spi.UkrlpAdapter.Domain.Configuration;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;
using Microsoft.Azure.Cosmos.Table;
using Newtonsoft.Json;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.AzureStorage.Cache
{
    public class TableProviderRepository : IProviderRepository
    {
        private readonly ILoggerWrapper _logger;
        private readonly CloudTable _table;

        public TableProviderRepository(CacheConfiguration configuration, ILoggerWrapper logger)
        {
            _logger = logger;

            var storageAccount = CloudStorageAccount.Parse(configuration.TableStorageConnectionString);
            var tableClient = storageAccount.CreateCloudTableClient();
            _table = tableClient.GetTableReference(configuration.ProviderTableName);
        }

        public async Task StoreAsync(Provider provider, CancellationToken cancellationToken)
        {
            await _table.CreateIfNotExistsAsync(cancellationToken);

            var operation = TableOperation.InsertOrReplace(ModelToCurrent(provider));
            await _table.ExecuteAsync(operation, cancellationToken);
        }

        public async Task StoreInStagingAsync(Provider[] providers, CancellationToken cancellationToken)
        {
            const int batchSize = 100;

            await _table.CreateIfNotExistsAsync(cancellationToken);

            var partitionedEntities = providers
                .Select(ModelToStaging)
                .GroupBy(entity => entity.PartitionKey)
                .ToDictionary(g => g.Key, g => g.ToArray());
            foreach (var partition in partitionedEntities.Values)
            {
                var position = 0;
                while (position < partition.Length)
                {
                    var entities = partition.Skip(position).Take(batchSize).ToArray();
                    var batch = new TableBatchOperation();

                    foreach (var entity in entities)
                    {
                        batch.InsertOrReplace(entity);
                    }

                    _logger.Debug(
                        $"Inserting {position} to {partition.Length} for partition {entities.First().PartitionKey}");
                    await _table.ExecuteBatchAsync(batch, cancellationToken);

                    position += batchSize;
                }
            }
        }

        public async Task<Provider> GetProviderAsync(long ukprn, CancellationToken cancellationToken)
        {
            var operation = TableOperation.Retrieve<ProviderEntity>(ukprn.ToString(), "current");
            var operationResult = await _table.ExecuteAsync(operation, cancellationToken);
            var entity = (ProviderEntity) operationResult.Result;

            if (entity == null)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<Provider>(entity.ProviderJson);
        }

        public async Task<Provider> GetProviderFromStagingAsync(long ukprn, CancellationToken cancellationToken)
        {
            var operation = TableOperation.Retrieve<ProviderEntity>(GetStagingPartitionKey(ukprn), ukprn.ToString());
            var operationResult = await _table.ExecuteAsync(operation, cancellationToken);
            var entity = (ProviderEntity) operationResult.Result;

            if (entity == null)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<Provider>(entity.ProviderJson);
        }

        public async Task<Provider[]> GetProvidersAsync(long[] ukprns, CancellationToken cancellationToken)
        {
            var ukprnFilters = ukprns
                .Select(ukprn => $"PartitionKey eq '{ukprn}'")
                .Aggregate((x, y) => $"{x} or {y}");
            var filter = $"RowKey eq 'current' and ({ukprnFilters})";
            var query = new TableQuery<ProviderEntity>()
                .Where(filter);
            var providers = await ExecuteQueryAsync(query, cancellationToken);
            
            return providers
                .Where(entity => !string.IsNullOrEmpty(entity.ProviderJson))
                .Select(entity => JsonConvert.DeserializeObject<Provider>(entity.ProviderJson)).ToArray();
        }

        public async Task<Provider[]> GetProvidersAsync(CancellationToken cancellationToken)
        {
            var query = new TableQuery<ProviderEntity>()
                .Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, "current"));
            var providers = await ExecuteQueryAsync(query, cancellationToken);
            
            return providers
                .Where(entity => !string.IsNullOrEmpty(entity.ProviderJson))
                .Select(entity => JsonConvert.DeserializeObject<Provider>(entity.ProviderJson)).ToArray();
        }


        private ProviderEntity ModelToCurrent(Provider provider)
        {
            return ModelToEntity(provider.UnitedKingdomProviderReferenceNumber.ToString(), "current", provider);
        }

        private ProviderEntity ModelToStaging(Provider provider)
        {
            return ModelToEntity(GetStagingPartitionKey(provider.UnitedKingdomProviderReferenceNumber),
                provider.UnitedKingdomProviderReferenceNumber.ToString(), provider);
        }

        private ProviderEntity ModelToEntity(string partitionKey, string rowKey, Provider provider)
        {
            return new ProviderEntity
            {
                PartitionKey = partitionKey,
                RowKey = rowKey,
                ProviderJson = JsonConvert.SerializeObject(provider),
            };
        }

        private string GetStagingPartitionKey(long urn)
        {
            return $"staging{Math.Floor(urn / 5000d) * 5000}";
        }

        private async Task<T[]> ExecuteQueryAsync<T>(TableQuery<T> query, CancellationToken cancellationToken)
            where T : TableEntity, new()
        {
            var nextQuery = query;
            var continuationToken = default(TableContinuationToken);
            var results = new List<T>();

            do
            {
                var result =
                    await _table.ExecuteQuerySegmentedAsync<T>(nextQuery, continuationToken, cancellationToken);
                
                results.AddRange(result.Results);

                continuationToken = result.ContinuationToken;
            } while (continuationToken != null && !cancellationToken.IsCancellationRequested);

            return results.ToArray();
        }
    }
}