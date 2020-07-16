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

        public async Task StoreAsync(PointInTimeProvider provider, CancellationToken cancellationToken)
        {
            await _table.CreateIfNotExistsAsync(cancellationToken);

            var operation = TableOperation.InsertOrReplace(ModelToEntity(provider));
            await _table.ExecuteAsync(operation, cancellationToken);
        }

        public async Task StoreAsync(PointInTimeProvider[] providers, CancellationToken cancellationToken)
        {
            const int batchSize = 100;

            await _table.CreateIfNotExistsAsync(cancellationToken);

            var entities = new List<ProviderEntity>();
            foreach (var provider in providers)
            {
                if (provider.IsCurrent)
                {
                    entities.Add(ModelToEntity("current", provider));
                }
                entities.Add(ModelToEntity(provider));
            }
            
            var partitionedEntities = entities
                .GroupBy(entity => entity.PartitionKey)
                .ToDictionary(g => g.Key, g => g.ToArray());
            foreach (var partition in partitionedEntities.Values)
            {
                var position = 0;
                while (position < partition.Length)
                {
                    var batchOfEntities = partition.Skip(position).Take(batchSize).ToArray();
                    var batch = new TableBatchOperation();

                    foreach (var entity in batchOfEntities)
                    {
                        batch.InsertOrReplace(entity);
                    }

                    _logger.Debug(
                        $"Inserting {position} to {partition.Length} for partition {batchOfEntities.First().PartitionKey}");
                    await _table.ExecuteBatchAsync(batch, cancellationToken);

                    position += batchSize;
                }
            }
        }

        public async Task StoreInStagingAsync(PointInTimeProvider[] providers, CancellationToken cancellationToken)
        {
            const int batchSize = 100;

            await _table.CreateIfNotExistsAsync(cancellationToken);

            var partitionedEntities = providers
                .Select(ModelToEntityForStaging)
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

        public async Task<PointInTimeProvider> GetProviderAsync(long ukprn, CancellationToken cancellationToken)
        {
            return await RetrieveAsync(ukprn.ToString(), "current", cancellationToken);
        }

        public async Task<PointInTimeProvider> GetProviderAsync(long ukprn, DateTime? pointInTime, CancellationToken cancellationToken)
        {
            if (!pointInTime.HasValue)
            {
                return await RetrieveAsync(ukprn.ToString(), "current", cancellationToken);
            }
            
            var query = new TableQuery<ProviderEntity>()
                .Where(TableQuery.CombineFilters(
                    TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, ukprn.ToString()),
                    TableOperators.And,
                    TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.LessThanOrEqual, pointInTime.Value.ToString("yyyyMMdd"))))
                .OrderByDesc("RowKey")
                .Take(1);
            var results = await QueryAsync(query, cancellationToken);

            return results.SingleOrDefault();
        }

        public async Task<PointInTimeProvider> GetProviderFromStagingAsync(long ukprn, DateTime pointInTime, CancellationToken cancellationToken)
        {
            var operation = TableOperation.Retrieve<ProviderEntity>(GetStagingPartitionKey(pointInTime), ukprn.ToString());
            var operationResult = await _table.ExecuteAsync(operation, cancellationToken);
            var entity = (ProviderEntity) operationResult.Result;

            if (entity == null)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<PointInTimeProvider>(entity.ProviderJson);
        }

        public async Task<Provider[]> GetProvidersAsync(long[] ukprns, CancellationToken cancellationToken)
        {
            var ukprnFilters = ukprns
                .Select(ukprn => $"PartitionKey eq '{ukprn}'")
                .Aggregate((x, y) => $"{x} or {y}");
            var filter = $"RowKey eq 'current' and ({ukprnFilters})";
            var query = new TableQuery<ProviderEntity>()
                .Where(filter);
            return await QueryAsync(query, cancellationToken);
        }

        public async Task<Provider[]> GetProvidersAsync(long[] ukprns, DateTime? pointInTime, CancellationToken cancellationToken)
        {
            var ukprnFilters = ukprns
                .Select(ukprn => $"PartitionKey eq '{ukprn}'")
                .Aggregate((x, y) => $"{x} or {y}");
            var timeFilter = pointInTime.HasValue
                ? $"RowKey le '{pointInTime.Value:yyyyMMdd}'"
                : "RowKey eq 'current'";
            var filter = $"{timeFilter} and ({ukprnFilters})";
            var query = new TableQuery<ProviderEntity>()
                .Where(filter);
            var results = await QueryAsync(query, cancellationToken);

            return results
                .GroupBy(p => p.UnitedKingdomProviderReferenceNumber)
                .Select(g => (Provider)g.OrderByDescending(p => p.PointInTime).First())
                .ToArray();
        }

        public async Task<Provider[]> GetProvidersAsync(CancellationToken cancellationToken)
        {
            var query = new TableQuery<ProviderEntity>()
                .Where(TableQuery.GenerateFilterCondition("RowKey", QueryComparisons.Equal, "current"));
            return await QueryAsync(query, cancellationToken);
        }


        private ProviderEntity ModelToEntity(PointInTimeProvider provider)
        {
            return ModelToEntity(provider.PointInTime.ToString("yyyyMMdd"), provider);
        }
        private ProviderEntity ModelToEntity(string rowKey, PointInTimeProvider provider)
        {
            return ModelToEntity(provider.UnitedKingdomProviderReferenceNumber.ToString(), rowKey, provider);
        }

        private ProviderEntity ModelToEntityForStaging(PointInTimeProvider provider)
        {
            return ModelToEntity(GetStagingPartitionKey(provider.PointInTime),
                provider.UnitedKingdomProviderReferenceNumber.ToString(), provider);
        }

        private ProviderEntity ModelToEntity(string partitionKey, string rowKey, PointInTimeProvider provider)
        {
            return new ProviderEntity
            {
                PartitionKey = partitionKey,
                RowKey = rowKey,
                ProviderJson = JsonConvert.SerializeObject(provider),
                PointInTime = provider.PointInTime,
                IsCurrent = provider.IsCurrent,
            };
        }

        private string GetStagingPartitionKey(DateTime pointInTime)
        {
            return $"staging{pointInTime:yyyyMMdd}";
        }

        private async Task<PointInTimeProvider> RetrieveAsync(string partitionKey, string rowKey, CancellationToken cancellationToken)
        {
            var operation = TableOperation.Retrieve<ProviderEntity>(partitionKey, rowKey);
            var operationResult = await _table.ExecuteAsync(operation, cancellationToken);
            var entity = (ProviderEntity) operationResult.Result;
            if (entity == null)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<PointInTimeProvider>(entity.ProviderJson);
        }
        private async Task<PointInTimeProvider[]> QueryAsync(TableQuery<ProviderEntity> query, CancellationToken cancellationToken)
        {
            var nextQuery = query;
            var continuationToken = default(TableContinuationToken);
            var results = new List<ProviderEntity>();

            do
            {
                var result =
                    await _table.ExecuteQuerySegmentedAsync(nextQuery, continuationToken, cancellationToken);
                
                results.AddRange(result.Results);

                continuationToken = result.ContinuationToken;
            } while (continuationToken != null && !cancellationToken.IsCancellationRequested);

            return results
                .Where(entity => !string.IsNullOrEmpty(entity.ProviderJson))
                .Select(entity => JsonConvert.DeserializeObject<PointInTimeProvider>(entity.ProviderJson))
                .ToArray();
        }
    }
}