using System;
using Microsoft.Azure.Cosmos.Table;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.AzureStorage.Cache
{
    public class LastReadEntity : TableEntity
    {
        public DateTime LastRead { get; set; }
    }
}