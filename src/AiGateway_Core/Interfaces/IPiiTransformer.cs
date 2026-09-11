using AiGateway_Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Interfaces
{
    public interface IPiiTransformer
    {
        Task<PiiCheckResult> PiiTransformAsync(string prompt, IReadOnlyList<PiiMatch> matches, CancellationToken cancellationToken = default);
    }
}
