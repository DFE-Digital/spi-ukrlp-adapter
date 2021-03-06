using System.Linq;
using System.Xml.Linq;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.UkrlpSoapApi
{
    internal static class XElementExtensions
    {
        internal static XElement GetElementByLocalName(this XElement containerElement, string localName)
        {
            return containerElement.Elements().SingleOrDefault(e => e.Name.LocalName == localName);
        }
        internal static XElement[] GetElementsByLocalName(this XElement containerElement, string localName)
        {
            return containerElement.Elements().Where(e => e.Name.LocalName == localName).ToArray();
        }
    }
}