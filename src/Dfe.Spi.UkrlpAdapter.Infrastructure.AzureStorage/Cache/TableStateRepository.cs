using System;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.UkrlpAdapter.Domain.Cache;
using Dfe.Spi.UkrlpAdapter.Domain.Configuration;
using Microsoft.Azure.Cosmos.Table;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.AzureStorage.Cache
{
    public class TableStateRepository : IStateRepository
    {
        private readonly ILoggerWrapper _logger;
        private readonly CloudTable _table;

        public TableStateRepository(CacheConfiguration configuration, ILoggerWrapper logger)
        {
            _logger = logger;

            var storageAccount = CloudStorageAccount.Parse(configuration.TableStorageConnectionString);
            var tableClient = storageAccount.CreateCloudTableClient();
            _table = tableClient.GetTableReference(configuration.StateTableName);
        }

        public async Task<DateTime> GetLastProviderReadTimeAsync(CancellationToken cancellationToken)
        {
            return await GetLastDateTimeStateAsync("learning-provider", "last-read", DateTime.Now.Date.AddDays(-14), cancellationToken);
        }

        public async Task SetLastProviderReadTimeAsync(DateTime lastRead, CancellationToken cancellationToken)
        {
            await SetLastDateTimeStateAsync("learning-provider", "last-read", lastRead, cancellationToken);
        }

        public async Task<DateTime> GetLastStagingDateClearedAsync(CancellationToken cancellationToken)
        {
            return await GetLastDateTimeStateAsync("provider-staging", "last-cleared", new DateTime(2020, 6, 1), cancellationToken);
        }

        public async Task SetLastStagingDateClearedAsync(DateTime lastRead, CancellationToken cancellationToken)
        {
            await SetLastDateTimeStateAsync("provider-staging", "last-cleared", lastRead, cancellationToken);
        }


        private async Task<DateTime> GetLastDateTimeStateAsync(
            string partitionKey,
            string rowKey,
            DateTime defaultValue,
            CancellationToken cancellationToken)
        {
            await _table.CreateIfNotExistsAsync(cancellationToken);

            var operation = TableOperation.Retrieve<LastDateTimeEntity>(partitionKey, rowKey);
            var operationResult = await _table.ExecuteAsync(operation, cancellationToken);
            var entity = (LastDateTimeEntity) operationResult.Result;

            if (entity == null)
            {
                return defaultValue;
            }

            return entity.LastRead;
        }

        private async Task SetLastDateTimeStateAsync(
            string partitionKey,
            string rowKey,
            DateTime lastRead,
            CancellationToken cancellationToken)
        {
            await _table.CreateIfNotExistsAsync(cancellationToken);

            var operation = TableOperation.InsertOrReplace(new LastDateTimeEntity
            {
                PartitionKey = partitionKey,
                RowKey = rowKey,
                LastRead = lastRead,
            });
            await _table.ExecuteAsync(operation, cancellationToken);
        }
    }
}