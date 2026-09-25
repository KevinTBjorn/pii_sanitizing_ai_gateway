using AiGateway_Core.Orchestration;

namespace AiGateway_Core.Interfaces
{
    public interface IPiiPayloadSanitizer
    {
        Task<PiiCheckResultTemp> SanitizePayloadAsync(string rawBody, CancellationToken cancellationToken = default);
    }
}
