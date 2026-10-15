using AiGateway_Core.RequestParser;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Infrastructure.RequestParser
{
    public interface IRequestParser
    {
        Task<RequestPromptContext> ParseAsync(HttpRequest request, CancellationToken cancellationToken = default);
    }
}
