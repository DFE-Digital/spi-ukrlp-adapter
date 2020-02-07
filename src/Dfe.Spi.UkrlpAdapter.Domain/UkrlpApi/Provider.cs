namespace Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi
{
    public class Provider
    {
        public long UnitedKingdomProviderReferenceNumber { get; set; }
        public string ProviderName { get; set; }
        public string Postcode { get; set; }
        public string ProviderStatus { get; set; }
        public VerificationDetails[] Verifications { get; set; }
    }

    public class VerificationDetails
    {
        public string Authority { get; set; }
        public string Id { get; set; }
    }
}