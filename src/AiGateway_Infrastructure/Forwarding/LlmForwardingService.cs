using AiGateway_Core.Exceptions;
using AiGateway_Core.Forwarding;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Net.Http.Json;
using System.Text;

namespace AiGateway_Infrastructure.Forwarding
{
    public class LlmForwardingService : ILlmForwardingService
    {
        private readonly HttpClient _httpClient;

        public LlmForwardingService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task ForwardRequestAsync(HttpContext httpContext, CancellationToken cancellationToken)
        {
            var feature = httpContext.Features.Get<LlmForwardingFeature>()
                ?? throw new InvalidOperationException(
                $"Cannot forward request: '{nameof(LlmForwardingFeature)}' was not initialized in HttpContext.Features.");

            using var outboundRequest = new HttpRequestMessage(HttpMethod.Post, feature.TargetEndpoint)
            {
                Content = JsonContent.Create(feature.FormattedPayload)
            };

            using var response = await _httpClient.SendAsync(
                outboundRequest,
                HttpCompletionOption.ResponseHeadersRead,
                httpContext.RequestAborted);

            httpContext.Response.StatusCode = (int)response.StatusCode;

            if (response.Content.Headers.ContentType != null)
            {
                httpContext.Response.ContentType = response.Content.Headers.ContentType.ToString();
            }

            await response.Content.CopyToAsync(httpContext.Response.Body, httpContext.RequestAborted);
        }
    }
}
