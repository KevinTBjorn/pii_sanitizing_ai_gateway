namespace AiGateway_Core.Interfaces
{
    public interface ILanguageDetector
    {
        string DetectLanguage(string text);
        string DetectLanguage(string text, string fallback);
    }
}
