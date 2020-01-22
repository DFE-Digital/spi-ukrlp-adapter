using Microsoft.Azure.Cosmos.Table;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.AzureStorage.Cache
{
    internal class ProviderEntity : TableEntity
    {
        public long UnitedKingdomProviderReferenceNumber { get; set; }
        public string ProviderName { get; set; }
        public string Postcode { get; set; }
    }
}