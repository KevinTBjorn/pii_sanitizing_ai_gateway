using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.PiiScanners
{
    public interface IPiiScanner
    {
        Task<IReadOnlyList<PiiMatch>> DetectPiiAsync(string prompt, CancellationToken cancellationToken = default);
    }
}
