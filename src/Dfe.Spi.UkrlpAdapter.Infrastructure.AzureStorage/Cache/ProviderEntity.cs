using System;
using Microsoft.Azure.Cosmos.Table;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.AzureStorage.Cache
{
    internal class ProviderEntity : TableEntity
    {
        public string ProviderJson { get; set; }
        public DateTime PointInTime { get; set; }
        public bool IsCurrent { get; set; }
    }
}