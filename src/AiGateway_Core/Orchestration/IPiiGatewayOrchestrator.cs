using AiGateway_Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Orchestration
{
    public interface IPiiGatewayOrchestrator
    {
        Task<PiiCheckResult> SanitizeTextAsync(string prompt, CancellationToken cancellationToken = default);
    }
}
