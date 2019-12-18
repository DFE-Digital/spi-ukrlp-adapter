namespace Dfe.Spi.UkrlpAdapter.Infrastructure.UkrlpSoapApi
{
    public class SoapException : UkrlpSoapApiException
    {
        public string FaultCode { get; }

        public SoapException(string faultCode, string faultString)
            : base(faultString)
        {
            FaultCode = faultCode;
        }
    }
}