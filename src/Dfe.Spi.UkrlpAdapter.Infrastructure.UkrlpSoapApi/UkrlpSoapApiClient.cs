using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Dfe.Spi.UkrlpAdapter.Domain.Configuration;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;
using RestSharp;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.UkrlpSoapApi
{
    public class UkrlpSoapApiClient : IUkrlpApiClient
    {
        private readonly IRestClient _restClient;
        private readonly IUkrlpSoapMessageBuilder _messageBuilder;

        internal UkrlpSoapApiClient(IRestClient restClient, IUkrlpSoapMessageBuilder messageBuilder,
            UkrlpApiConfiguration configuration)
        {
            _restClient = restClient;
            _restClient.BaseUrl = new Uri(configuration.Url, UriKind.Absolute);
            _messageBuilder = messageBuilder;
        }

        public UkrlpSoapApiClient(IRestClient restClient, UkrlpApiConfiguration configuration)
            : this(restClient, new UkrlpSoapMessageBuilder(configuration.StakeholderId), configuration)
        {
        }

        public async Task<Provider> GetProviderAsync(long ukprn, CancellationToken cancellationToken)
        {
            var message = _messageBuilder.BuildMessageToGetSpecificUkprn(ukprn);

            var request = new RestRequest(Method.POST);
            request.AddParameter("text/xml", message, ParameterType.RequestBody);
            request.AddHeader("SOAPAction", "retrieveAllProviders");

            var response = await _restClient.ExecuteTaskAsync(request, cancellationToken);
            var result = EnsureSuccessResponseAndExtractResult(response);

            var provider = MapProvidersFromSoapResult(result);
            return provider.FirstOrDefault();
        }

        public async Task<Provider[]> GetProvidersUpdatedSinceAsync(DateTime updatedSince, CancellationToken cancellationToken)
        {
            var message = _messageBuilder.BuildMessageToGetUpdatesSince(updatedSince);

            var request = new RestRequest(Method.POST);
            request.AddParameter("text/xml", message, ParameterType.RequestBody);
            request.AddHeader("SOAPAction", "retrieveAllProviders");

            var response = await _restClient.ExecuteTaskAsync(request, cancellationToken);
            var result = EnsureSuccessResponseAndExtractResult(response);

            return MapProvidersFromSoapResult(result);
        }

        
        
        private static XElement EnsureSuccessResponseAndExtractResult(IRestResponse response)
        {
            XDocument document;
            try
            {
                document = XDocument.Parse(response.Content);
            }
            catch (Exception ex)
            {
                throw new UkrlpSoapApiException($"Error deserializing SOAP response: {ex.Message} (response: {response.Content})", ex);
            }
            
            var envelope = document.Elements().Single();
            var body = envelope.GetElementByLocalName("Body");

            if (!response.IsSuccessful)
            {
                var fault = body.Elements().Single();
                var faultCode = fault.GetElementByLocalName("faultcode");
                var faultString = fault.GetElementByLocalName("faultstring");
                throw new SoapException(faultCode.Value, faultString.Value);
            }

            return body.Elements().First();
        }

        private static Provider[] MapProvidersFromSoapResult(XElement result)
        {
            var matches = result.GetElementsByLocalName("MatchingProviderRecords");
            var providers = new Provider[matches.Length];

            for (var i = 0; i < matches.Length; i++)
            {
                var match = matches[i];
                
                providers[i] = new Provider
                {
                    UnitedKingdomProviderReferenceNumber = long.Parse(match.GetElementByLocalName("UnitedKingdomProviderReferenceNumber").Value),
                    ProviderName = match.GetElementByLocalName("ProviderName").Value,
                    AccessibleProviderName = match.GetElementByLocalName("AccessibleProviderName")?.Value,
                    ProviderStatus = match.GetElementByLocalName("ProviderStatus")?.Value,
                    ProviderVerificationDate = ReadNullableDateTime(match.GetElementByLocalName("ProviderVerificationDate")),
                    ExpiryDate = ReadNullableDateTime(match.GetElementByLocalName("ExpiryDate")),
                    ProviderContacts = MapProviderContactsFromSoapProvider(match),
                };

                var legalContactElement = match.GetElementsByLocalName("ProviderContact")
                    .FirstOrDefault(contactElement => contactElement.GetElementByLocalName("ContactType")?.Value == "L");
                if (legalContactElement != null)
                {
                    providers[i].Postcode = legalContactElement.GetElementByLocalName("ContactAddress")
                        ?.GetElementByLocalName("PostCode")?.Value;
                }

                var verifications = new List<VerificationDetails>();
                var verificationElements = match.GetElementsByLocalName("VerificationDetails");
                foreach (var verificationElement in verificationElements)
                {
                    verifications.Add(new VerificationDetails
                    {
                        Authority = verificationElement.GetElementByLocalName("VerificationAuthority").Value,
                        Id = verificationElement.GetElementByLocalName("VerificationID").Value,
                    });
                }
                providers[i].Verifications = verifications.ToArray();
            }

            return providers;
        }

        private static ProviderContact[] MapProviderContactsFromSoapProvider(XElement providerElement)
        {
            var contactElements = providerElement.GetElementsByLocalName("ProviderContact");
            if (contactElements == null || contactElements.Length == 0)
            {
                return new ProviderContact[0];
            }

            return contactElements.Select(contactElement => new ProviderContact
            {
                ContactType = contactElement.GetElementByLocalName("ContactType")?.Value,
                ContactRole = contactElement.GetElementByLocalName("ContactRole")?.Value,
                ContactTelephone1 = contactElement.GetElementByLocalName("ContactTelephone1")?.Value,
                ContactTelephone2 = contactElement.GetElementByLocalName("ContactTelephone2")?.Value,
                ContactFax = contactElement.GetElementByLocalName("ContactFax")?.Value,
                ContactWebsiteAddress = contactElement.GetElementByLocalName("ContactWebsiteAddress")?.Value,
                ContactEmail = contactElement.GetElementByLocalName("ContactEmail")?.Value,
                LastUpdated = ReadNullableDateTime(contactElement.GetElementByLocalName("LastUpdated")),
                ContactAddress = MapContactAddressFromSoapContactElement(contactElement),
                ContactPersonalDetails = MapPersonNameStructureFromSoapContactElement(contactElement),
            }).ToArray();
        }

        private static AddressStructure MapContactAddressFromSoapContactElement(XElement contactElement)
        {
            var addressElement = contactElement.GetElementByLocalName("ContactAddress");
            return addressElement == null
                ? null
                : new AddressStructure
                {
                    Address1 = addressElement.GetElementByLocalName("Address1")?.Value,
                    Address2 = addressElement.GetElementByLocalName("Address2")?.Value,
                    Address3 = addressElement.GetElementByLocalName("Address3")?.Value,
                    Address4 = addressElement.GetElementByLocalName("Address4")?.Value,
                    Town = addressElement.GetElementByLocalName("Town")?.Value,
                    County = addressElement.GetElementByLocalName("County")?.Value,
                    PostCode = addressElement.GetElementByLocalName("PostCode")?.Value,
                };
        }
        private static PersonNameStructure MapPersonNameStructureFromSoapContactElement(XElement contactElement)
        {
            var personalDetailsElement = contactElement.GetElementByLocalName("ContactPersonalDetails");
            return personalDetailsElement == null
                ? null
                : new PersonNameStructure
                {
                    PersonNameTitle = personalDetailsElement.GetElementByLocalName("PersonNameTitle")?.Value,
                    PersonGivenName = personalDetailsElement.GetElementByLocalName("PersonGivenName")?.Value,
                    PersonFamilyName = personalDetailsElement.GetElementByLocalName("PersonFamilyName")?.Value,
                    PersonNameSuffix = personalDetailsElement.GetElementByLocalName("PersonNameSuffix")?.Value,
                    PersonRequestedName = personalDetailsElement.GetElementByLocalName("PersonRequestedName")?.Value,
                };
        }
        private static DateTime? ReadNullableDateTime(XElement element)
        {
            if (element == null)
            {
                return null;
            }

            return DateTime.Parse(element.Value);
        }
    }
}