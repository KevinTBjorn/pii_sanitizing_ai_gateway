namespace AiGateway_Core.Models
{
    public record PiiScanResult(
        bool HasSensitiveData,
        string CleanedPrompt,
        IReadOnlyList<string> DetectedTypes
    );
}
