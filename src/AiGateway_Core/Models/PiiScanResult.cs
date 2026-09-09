namespace AiGateway_Core.Models
{
    public record PiiScanResult(
        bool HasSensitiveData,
        string CleanedPrompt,
        IReadOnlyList<string> DetectedTypes
    )
    {
        public static PiiScanResult NoPiiFound(string text) =>
            new(false, text, Array.Empty<string>());
    }
}
