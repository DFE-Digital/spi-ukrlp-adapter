using System;
using System.Threading;
using System.Threading.Tasks;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.UkrlpAdapter.Domain.Cache;
using Dfe.Spi.UkrlpAdapter.Domain.Configuration;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;
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
            await _table.CreateIfNotExistsAsync(cancellationToken);
            
            var operation = TableOperation.Retrieve<LastReadEntity>("learning-provider", "last-read");
            var operationResult = await _table.ExecuteAsync(operation, cancellationToken);
            var entity = (LastReadEntity) operationResult.Result;

            if (entity == null)
            {
                return DateTime.Now.Date.AddDays(-14);
            }

            return entity.LastRead;
        }

        public async Task SetLastProviderReadTimeAsync(DateTime lastRead, CancellationToken cancellationToken)
        {
            await _table.CreateIfNotExistsAsync(cancellationToken);

            var operation = TableOperation.InsertOrReplace(new LastReadEntity
            {
                PartitionKey = "learning-provider",
                RowKey = "last-read",
                LastRead = lastRead,
            });
            await _table.ExecuteAsync(operation, cancellationToken);
        }
    }
}