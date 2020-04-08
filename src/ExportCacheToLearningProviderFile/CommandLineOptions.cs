using CommandLine;

namespace ExportCacheToLearningProviderFile
{
    public class CommandLineOptions
    {

        [Option('o', "output", Required = true, HelpText = "Path to output file to")]
        public string OutputPath { get; set; }
        
        [Option("connection-string", Required = true, HelpText = "Connection string for cache storage")]
        public string TableStorageConnectionString { get; set; }
        
        [Option('p', "provider-table-name", Required = true, HelpText = "Providers table name")]
        public string ProviderTableName { get; set; }
        
        
        [Option('e', "token-endpoint", Required = true, HelpText = "An OAuth token endpoint.")]
        public string TokenEndpoint { get; set; }

        [Option('c', "client-id", Required = true, HelpText = "An OAuth client id.")]
        public string ClientId { get; set; }

        [Option('s', "client-secret", Required = true, HelpText = "An OAuth client secret.")]
        public string ClientSecret { get; set; }

        [Option('r', "resource", Required = true, HelpText = "An OAuth resource.")]
        public string Resource { get; set; }
        
        [Option('t', "translator-url", Required = true, HelpText = "Base URL of translator API")]
        public string TranslatorBaseUrl { get; set; }
        
        [Option('k', "translator-subscription-key", Required = true, HelpText = "Subscription key of Translator API")]
        public string TranslatorSubscriptionKey { get; set; }
    }
}