using System;
using System.Linq;

namespace Dfe.Spi.UkrlpAdapter.Application
{
    internal static class CloningExtensions
    {
        internal static TDestination Clone<TDestination>(this object source, Func<TDestination> activator = null)
        {
            // TODO: This could be more efficient with some caching of properties
            var sourceProperties = source.GetType().GetProperties();
            var destinationProperties = source.GetType().GetProperties();

            TDestination destination;
            if (activator != null)
            {
                destination = activator();
            }
            else
            {
                destination = Activator.CreateInstance<TDestination>();
            }

            foreach (var destinationProperty in destinationProperties)
            {
                var sourceProperty = sourceProperties.SingleOrDefault(p => p.Name == destinationProperty.Name);
                if (sourceProperty != null)
                {
                    // TODO: This assumes the property types are the same. If this is not true then handling will be required
                    var sourceValue = sourceProperty.GetValue(source);
                    destinationProperty.SetValue(destination, sourceValue);
                }
            }

            return destination;
        }
    }
}