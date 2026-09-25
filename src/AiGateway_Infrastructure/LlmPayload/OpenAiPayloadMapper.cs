using AiGateway_Core.IncomingLlmPayload;
using AiGateway_Core.LlmPayload;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Infrastructure.LlmPayload
{
    public class OpenAiPayloadMapper : IOutgoingLlmPayloadMapper
    {
        public bool CanHandle(string providerName) => providerName.Equals("OpenAI", StringComparison.OrdinalIgnoreCase);

        public object MapToProviderPayload(RequestPromptPayload request, string anonymizedPrompt)
        {
            return new
            {
                model = request.Model,
                temperature = request.Temperature,
                messages = new object[]
                {
                new { role = "user", content = anonymizedPrompt }
                }
            };
        }
    }
}
