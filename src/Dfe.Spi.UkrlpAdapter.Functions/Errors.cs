namespace Dfe.Spi.UkrlpAdapter.Functions
{
    public static class Errors
    {
        public static readonly ErrorDetails GetLearningProvidersMalformedRequest = new ErrorDetails($"{CodePrefix}-PROVIDERS01", MalformedRequestMessage);
        public static readonly ErrorDetails GetLearningProvidersSchemaValidation = new ErrorDetails($"{CodePrefix}-PROVIDERS02", null);
        
        
        private const string CodePrefix = "SPI-UKRLP";
        private const string MalformedRequestMessage = "The supplied body was either empty, or not well-formed JSON.";
    }

    public class ErrorDetails
    {
        public ErrorDetails(string code, string message)
        {
            Code = code;
            Message = message;
        }
        public string Code { get; }
        public string Message { get; }
    }
}