using Microsoft.Azure.Cosmos.Table;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.AzureStorage.Cache
{
    internal class ProviderEntity : TableEntity
    {
        public string ProviderJson { get; set; }
    }
}