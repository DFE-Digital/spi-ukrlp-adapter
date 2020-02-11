using System;

namespace Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi
{
    public class Provider
    {
        public long UnitedKingdomProviderReferenceNumber { get; set; }
        public string ProviderName { get; set; }
        public string AccessibleProviderName { get; set; }
        public ProviderContact[] ProviderContacts { get; set; }
        public DateTime? ProviderVerificationDate { get; set; }
        public DateTime? ExpiryDate { get; set; }
        public string ProviderStatus { get; set; }
        public VerificationDetails[] Verifications { get; set; }


        public string Postcode { get; set; }
    }

    public class ProviderContact
    {
        public string ContactType { get; set; }
        public AddressStructure ContactAddress { get; set; }
        public PersonNameStructure ContactPersonalDetails { get; set; }
        public string ContactRole { get; set; }
        public string ContactTelephone1 { get; set; }
        public string ContactTelephone2 { get; set; }
        public string ContactFax { get; set; }
        public string ContactWebsiteAddress { get; set; }
        public string ContactEmail { get; set; }
        public DateTime? LastUpdated { get; set; }
    }

    public class AddressStructure
    {
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string Address3 { get; set; }
        public string Address4 { get; set; }
        public string Town { get; set; }
        public string County { get; set; }
        public string PostCode { get; set; }
    }

    public class PersonNameStructure
    {
        public string PersonNameTitle { get; set; }
        public string PersonGivenName { get; set; }
        public string PersonFamilyName { get; set; }
        public string PersonNameSuffix { get; set; }
        public string PersonRequestedName { get; set; }
    }

    public class VerificationDetails
    {
        public string Authority { get; set; }
        public string Id { get; set; }
    }
}