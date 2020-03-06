using CommandLine;

namespace SeedCacheFromListOfUkprns
{
    public class CommandLineOptions
    {
        [Option('p', "path", Required = true, HelpText = "Path to file with list of UKPRNs")]
        public string Path { get; set; }
        
        [Option('u', "ukrlp-url", Required = true, HelpText = "Url of UKRLP soap endpoint")]
        public string UkrlpUrl { get; set; }
        
        [Option('i', "ukrlp-stakeholder-id", Required = true, HelpText = "Stakeholder id for UKRLP API")]
        public string UkrlpStakeholderId { get; set; }
        
        
        
        [Option('c', "connection-string", Required = true, HelpText = "Connection string for cache storage")]
        public string TableStorageConnectionString { get; set; }
        
        [Option('t', "provider-table-name", Required = true, HelpText = "Providers table name")]
        public string ProviderTableName { get; set; }
        
        [Option('s', "state-table-name", Required = true, HelpText = "State table name")]
        public string StateTableName { get; set; }
    }
}