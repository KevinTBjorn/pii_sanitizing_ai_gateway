using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Orchestration
{
    public interface IPiiGatewayOrchestrator
    {
        Task<PiiCheckResultTemp> OrchestrateSanitizeProcessAsync(string prompt, CancellationToken cancellationToken = default);
    }
}
