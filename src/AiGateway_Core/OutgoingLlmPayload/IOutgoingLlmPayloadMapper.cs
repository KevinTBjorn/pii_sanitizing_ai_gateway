using AiGateway_Core.IncomingLlmPayload;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.OutgoingLlmPayload
{
    public interface IOutgoingLlmPayloadMapper
    {
        bool CanHandle(string providerName);
        object MapToProviderPayload(RequestPromptPayload request, string anonymizedPromptWithDocs);
    }
}
