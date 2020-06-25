using System;

namespace Dfe.Spi.UkrlpAdapter.Domain.Cache
{
    public class StagingBatchQueueItem
    {
        public long[] Identifiers { get; set; }
        public DateTime PointInTime { get; set; }
    }
}