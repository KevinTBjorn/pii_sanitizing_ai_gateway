using AiGateway_Core.IncomingLlmPayload;
using AiGateway_Core.Orchestration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.LlmPayload
{
    public  class LlmPayloadFormatter : ILlmPayloadFormatter
    {
        private readonly IServiceProvider _serviceProvider;

        public LlmPayloadFormatter(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public object FormatPayload(string providerName, RequestPromptPayload request, IReadOnlyList<TextEntry> sanitizedEntries)
        {
            if (string.IsNullOrWhiteSpace(providerName))
            {
                throw new ArgumentException("Provider name cannot be null or empty.", nameof(providerName));
            }

            var mapper = _serviceProvider.GetKeyedService<ILlmProviderPayloadMapper>(providerName.Trim().ToLowerInvariant());

            if (mapper == null)
            {
                throw new NotSupportedException($"No payload mapper registered for LLM provider '{providerName}'.");
            }

            return mapper.MapToProviderPayload(request, sanitizedEntries);
        }
    }
}
