using System;
using System.Net;

namespace Dfe.Spi.UkrlpAdapter.Infrastructure.SpiMiddleware
{
    public class MiddlewareException : Exception
    {
        public MiddlewareException(string resource, HttpStatusCode statusCode, string details)
            : base($"Error calling {resource} on middleware. Status {(int)statusCode} - {details}")
        {
            StatusCode = statusCode;
            Details = details;
        }

        public HttpStatusCode StatusCode { get; }
        public string Details { get; }
    }
}