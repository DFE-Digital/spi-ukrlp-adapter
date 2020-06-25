using System;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.SpiMiddleware
{
    public class PointInTimeMiddlewareEvent<T>
    {
        public T Details { get; set; }
        public DateTime PointInTime { get; set; }
    }
}