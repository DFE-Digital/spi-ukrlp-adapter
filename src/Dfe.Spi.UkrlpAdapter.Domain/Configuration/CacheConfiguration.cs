namespace Dfe.Spi.UkrlpAdapter.Domain.Configuration
{
    public class CacheConfiguration
    {
        public string TableStorageConnectionString { get; set; }
        public string ProviderTableName { get; set; }
        public string StateTableName { get; set; }
        
        public string ProviderProcessingQueueConnectionString { get; set; }
    }
}