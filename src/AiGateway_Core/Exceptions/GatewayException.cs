using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Exceptions
{
    public class GatewayException(string message, int statusCode) : Exception(message)
    {
        public int StatusCode { get; } = statusCode;
    }
}