using AiGateway_Core.IncomingLlmPayload;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.LlmPayload
{
    public interface IOutgoingLlmPayloadMapper
    {
        bool CanHandle(string providerName);
        object MapToProviderPayload(RequestPromptPayload request, string anonymizedPrompt);
    }
}
