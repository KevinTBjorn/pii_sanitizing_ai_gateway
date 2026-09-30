using AiGateway_Core.Orchestration;
using AiGateway_Core.PiiScanners;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.TextMasking
{
    public interface IPiiTransformer
    {
        Task<IReadOnlyList<TextEntry>> PiiTransformAsync(IReadOnlyList<TextEntry> entries, CancellationToken cancellationToken = default);
    }
}
