using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Dfe.Spi.Common.Logging.Definitions;
using Dfe.Spi.UkrlpAdapter.Domain.Configuration;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;
using RestSharp;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.UkrlpSoapApi
{
    public class UkrlpSoapApiClient : IUkrlpApiClient
    {
        private static readonly string[] OrderedProviderStatuses = new[] {"A", "PD1", "PD2"};
        
        private readonly IRestClient _restClient;
        private readonly IUkrlpSoapMessageBuilder _messageBuilder;
        private readonly ILoggerWrapper _logger;

        internal UkrlpSoapApiClient(IRestClient restClient, IUkrlpSoapMessageBuilder messageBuilder,
            UkrlpApiConfiguration configuration, ILoggerWrapper logger)
        {
            _restClient = restClient;
            _restClient.BaseUrl = new Uri(configuration.Url, UriKind.Absolute);
            _messageBuilder = messageBuilder;
            _logger = logger;
        }

        public UkrlpSoapApiClient(IRestClient restClient, UkrlpApiConfiguration configuration, ILoggerWrapper logger)
            : this(restClient, new UkrlpSoapMessageBuilder(configuration.StakeholderId), configuration, logger)
        {
        }

        public async Task<Provider> GetProviderAsync(long ukprn, CancellationToken cancellationToken)
        {
            var providers = await GetProvidersAsync(new[] {ukprn}, cancellationToken);
            return providers.FirstOrDefault();
        }

        public async Task<Provider[]> GetProvidersAsync(long[] ukprns, CancellationToken cancellationToken)
        {
            var providers = new List<Provider>();
            var remainingUkprns = ukprns.ToList();

            foreach (var providerStatus in OrderedProviderStatuses)
            {
                var message = _messageBuilder.BuildMessageToGetSpecificUkprns(remainingUkprns.ToArray(), providerStatus);

                var request = new RestRequest(Method.POST);
                request.AddParameter("text/xml", message, ParameterType.RequestBody);
                request.AddHeader("SOAPAction", "retrieveAllProviders");

                var response = await _restClient.ExecuteTaskAsync(request, cancellationToken);
                var result = EnsureSuccessResponseAndExtractResult(response, message);
                
                var providersForStatus = MapProvidersFromSoapResult(result);
                foreach (var provider in providersForStatus)
                {
                    providers.Add(provider);
                    remainingUkprns.Remove(provider.UnitedKingdomProviderReferenceNumber);
                }
                if (providers.Count == ukprns.Length)
                {
                    break;
                }
            }

            return providers.ToArray();
        }

        public async Task<Provider[]> GetProvidersUpdatedSinceAsync(DateTime updatedSince, CancellationToken cancellationToken)
        {
            var providers = new List<Provider>();

            foreach (var status in OrderedProviderStatuses)
            {
                var providersOfStatus = await GetProvidersOfStatusUpdatedSinceAsync(updatedSince, status, cancellationToken);
                providers.AddRange(providersOfStatus);
            }

            return providers.ToArray();
        }

        private async Task<Provider[]> GetProvidersOfStatusUpdatedSinceAsync(DateTime updatedSince, string status, CancellationToken cancellationToken)
        {
            var message = _messageBuilder.BuildMessageToGetUpdatesSince(updatedSince, status);

            var request = new RestRequest(Method.POST);
            request.AddParameter("text/xml", message, ParameterType.RequestBody);
            request.AddHeader("SOAPAction", "retrieveAllProviders");

            _logger.Info($"Fetching providers with body:{message}");
            var response = await _restClient.ExecuteTaskAsync(request, cancellationToken);
            var result = EnsureSuccessResponseAndExtractResult(response, message);
            return MapProvidersFromSoapResult(result);
        }
        private static XElement EnsureSuccessResponseAndExtractResult(IRestResponse response, string message)
        {
            if (!response.IsSuccessful && response.ErrorException != null)
                throw new UkrlpSoapApiException(
                    $"Error calling UKrlp endpoint: {response.ErrorMessage??"N/A"}({response.StatusCode}) with request body: {message} and response: {response.Content}"
                    , response.ErrorException);

            XDocument document;
            try
            {                
                document = XDocument.Parse(response.Content);
            }
            catch (Exception ex)
            {
                throw new UkrlpSoapApiException(
                    $"Error deserializing SOAP response: {ex.Message} (response: request {message} and response: {response.Content} :: response:statusCode:{response.StatusCode} response:errorMessage:{response.ErrorMessage})"
                    ,ex);
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