using System.Threading;
using System.Threading.Tasks;

namespace Dfe.Spi.UkrlpAdapter.Domain.Translation
{
    public interface ITranslator
    {
        Task<string> TranslateEnumValue(string enumName, string sourceValue, CancellationToken cancellationToken);
    }
}