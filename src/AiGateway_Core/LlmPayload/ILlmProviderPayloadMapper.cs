using AiGateway_Core.IncomingLlmPayload;
using AiGateway_Core.Orchestration;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.LlmPayload
{
    public interface ILlmProviderPayloadMapper
    {
        object MapToProviderPayload(RequestPromptPayload request, IReadOnlyList<TextEntry> sanitizedEntries);
    }
}
