using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Forwarding
{
    public interface ILlmForwardingService
    {
        Task ForwardRequestAsync(HttpContext httpContext, CancellationToken cancellationToken);
    }
}
