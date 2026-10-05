using AiGateway_Core.RequestParser;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Orchestration
{
    public interface IPiiGatewayOrchestrator
    {
        Task<PiiGatewayExecutionResult> OrchestrateSanitizeProcessAsync(RequestPromptContext context, CancellationToken cancellationToken = default);
    }
}
