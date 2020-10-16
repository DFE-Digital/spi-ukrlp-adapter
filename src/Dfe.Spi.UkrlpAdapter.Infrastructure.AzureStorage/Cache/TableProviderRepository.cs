using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private readonly int _concurrentBatchReadThreads;

        public TableProviderRepository(CacheConfiguration configuration, ILoggerWrapper logger)
        {
            _logger = logger;

            var storageAccount = CloudStorageAccount.Parse(configuration.TableStorageConnectionString);
            var tableClient = storageAccount.CreateCloudTableClient();
            _table = tableClient.GetTableReference(configuration.ProviderTableName);

            _concurrentBatchReadThreads = 10;
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

            // Could be more than 1 result if used a point in time. Take the most recent
            return results
                .OrderByDescending(x => x.PointInTime)
                .FirstOrDefault();
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
            return await GetProvidersAsync(ukprns, null, cancellationToken);
        }

        public async Task<Provider[]> GetProvidersAsync(long[] ukprns, DateTime? pointInTime, CancellationToken cancellationToken)
        {
            var itemsPerThread = (int) Math.Ceiling(ukprns.Length / (float) _concurrentBatchReadThreads);
            var numberOfThreads = (int) Math.Ceiling(ukprns.Length / (float) itemsPerThread);
            var threads = new Task<PointInTimeProvider[]>[numberOfThreads];

            for (var i = 0; i < threads.Length; i++)
            {
                var batchOfUkprns = ukprns
                    .Skip(i * itemsPerThread)
                    .Take(itemsPerThread)
                    .ToArray();
                threads[i] = GetBatchOfProvidersAsync(batchOfUkprns, pointInTime, cancellationToken);
            }
            
            var providers = await Task.WhenAll(threads);
            return providers
                .SelectMany(x => x)
                .Where(x => x != null)
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

        private async Task<PointInTimeProvider[]> GetBatchOfProvidersAsync(long[] batchOfUkprns, DateTime? pointInTime, CancellationToken cancellationToken)
        {
            var providers = new PointInTimeProvider[batchOfUkprns.Length];

            for (var i = 0; i < batchOfUkprns.Length; i++)
            {
                providers[i] = await GetProviderAsync(batchOfUkprns[i], pointInTime, cancellationToken);
            }

            return providers;
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