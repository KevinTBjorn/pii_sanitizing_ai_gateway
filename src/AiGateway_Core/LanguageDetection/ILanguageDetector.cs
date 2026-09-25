namespace AiGateway_Core.LanguageDetection
{
    public interface ILanguageDetector
    {
        string DetectLanguage(string text);
        string DetectLanguage(string text, string fallback);
    }
}
