using AiGateway_Core.RequestParser;
using AiGateway_Core.Routing;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Orchestration
{
    public interface IPiiGatewayOrchestrator
    {
        Task<PiiGatewayExecutionResult> OrchestrateSanitizeProcessAsync(RequestPromptContext context, LlmPayloadFormat format, CancellationToken cancellationToken = default);
    }
}
