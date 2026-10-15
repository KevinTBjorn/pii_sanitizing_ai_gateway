using AiGateway_Core.Orchestration;
using AiGateway_Core.RequestParser;
using AiGateway_Core.Routing;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.LlmPayload
{
    public interface ILlmPayloadFormatter
    {
        object FormatPayload(LlmPayloadFormat format, RequestPromptPayload request, IReadOnlyList<TextEntry> sanitizedEntries);
    }
}
