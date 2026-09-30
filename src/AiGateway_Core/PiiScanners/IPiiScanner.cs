using AiGateway_Core.Orchestration;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.PiiScanners
{
    public interface IPiiScanner
    {
        Task<IReadOnlyList<TextEntry>> DetectPiiAsync(IReadOnlyList<TextEntry> entries, CancellationToken cancellationToken = default);
    }
}
