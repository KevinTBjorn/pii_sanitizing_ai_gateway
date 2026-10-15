using AiGateway_Core.Exceptions;
using AiGateway_Core.Orchestration;
using AiGateway_Core.RequestParser;
using AiGateway_Core.Routing;
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

        public object FormatPayload(LlmPayloadFormat format, RequestPromptPayload request, IReadOnlyList<TextEntry> sanitizedEntries)
        {
            if (!Enum.IsDefined(format))
            {
                throw new ArgumentOutOfRangeException(nameof(format), format, $"Invalid payload format: {format}");
            }

            var mapper = _serviceProvider.GetKeyedService<ILlmProviderPayloadMapper>(format);

            if (mapper == null)
            {
                throw new GatewayException($"No payload mapper registered for LLM format '{format}'.", 415);
            }

            return mapper.MapToProviderPayload(request, sanitizedEntries);
        }
    }
}
