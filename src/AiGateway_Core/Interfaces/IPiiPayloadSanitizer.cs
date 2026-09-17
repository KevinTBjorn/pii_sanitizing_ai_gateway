using AiGateway_Core.Models;

namespace AiGateway_Core.Interfaces
{
    public interface IPiiPayloadSanitizer
    {
        Task<PiiCheckResult> SanitizePayloadAsync(string rawBody, CancellationToken cancellationToken = default);
    }
}
