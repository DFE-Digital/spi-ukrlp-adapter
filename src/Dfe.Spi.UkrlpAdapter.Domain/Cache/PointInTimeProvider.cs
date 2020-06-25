using System;
using Dfe.Spi.UkrlpAdapter.Domain.UkrlpApi;

namespace Dfe.Spi.UkrlpAdapter.Domain.Cache
{
    public class PointInTimeProvider : Provider
    {
        public DateTime PointInTime { get; set; }
        public bool IsCurrent { get; set; }
    }
}