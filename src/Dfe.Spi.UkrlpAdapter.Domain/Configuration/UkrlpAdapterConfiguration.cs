namespace Dfe.Spi.UkrlpAdapter.Domain.Configuration
{
    public class UkrlpAdapterConfiguration
    {
        public UkrlpApiConfiguration UkrlpApi { get; set; }
        public CacheConfiguration Cache { get; set; }
        public MiddlewareConfiguration Middleware { get; set; }
        public TranslatorConfiguration Translator { get; set; }
    }
}