using AiGateway_Core.IncomingLlmPayload;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Infrastructure.RequestLlmPayload
{
    public interface IRequestParser
    {
        Task<RequestPromptContext> ParseAsync(HttpRequest request, CancellationToken cancellationToken = default);
    }
}
