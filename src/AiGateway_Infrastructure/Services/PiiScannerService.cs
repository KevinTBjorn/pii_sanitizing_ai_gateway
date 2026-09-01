using AiGateway_Core.Constants;
using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;
using AiGateway_Infrastructure.Telemetry;
using System.Text.RegularExpressions;

namespace AiGateway_Infrastructure.Services
{
    public partial class PiiScannerService : IPiiScanner
    {
        [GeneratedRegex(@"(?<![\p{L}0-9._%+-])[\p{L}0-9._%+-]+@[\p{L}0-9.-]+\.[\p{L}]{2,}(?![\p{L}])", RegexOptions.IgnoreCase)]
        private static partial Regex EmailRegex();

        [GeneratedRegex(@"(?<!\d)(0[1-9]|[12][0-9]|3[01])(0[1-9]|1[0-2])\d{2}-?\d{4}(?!\d)")]
        private static partial Regex CprRegex();

        // OpenAI (legacy sk-, project keys, admin keys)
        [GeneratedRegex(@"sk-(proj-|admin-)?[a-zA-Z0-9_-]{32,}(?![a-zA-Z0-9_-])")]
        private static partial Regex OpenAiKeyRegex();

        //Generic Prefixed Tokens (e.g., ak_123..., key-abc..., token_xyz...)
        [GeneratedRegex(@"(?:api[_-]?key|access[_-]?token|secret[_-]?key|auth[_-]?token)\s*[:=]\s*[""']?([a-zA-Z0-9_\-\.]{16,})[""']?", RegexOptions.IgnoreCase)]
        private static partial Regex GenericKeyAssignmentRegex();

        public PiiScanResult ScanAndMask(string prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                return new PiiScanResult(false, prompt, Array.Empty<string>());

            var detectedTypes = new List<string>();
            string currentPrompt = prompt;

            currentPrompt = MaskEmails(currentPrompt, detectedTypes);
            currentPrompt = MaskCprs(currentPrompt, detectedTypes);
            currentPrompt = MaskApiKeys(currentPrompt, detectedTypes);

            return new PiiScanResult(
                HasSensitiveData: detectedTypes.Count > 0,
                CleanedPrompt: currentPrompt,
                DetectedTypes: detectedTypes.AsReadOnly()
            );
        }

        private static string MaskEmails(string text, List<string> types)
        {
            int matchCount = EmailRegex().Matches(text).Count;
            if (matchCount == 0) return text;
            types.Add("Email");
            PiiMetrics.RecordMaskedItem("masked_email", matchCount);
            return EmailRegex().Replace(text, PiiTokens.Email);
        }

        private static string MaskCprs(string text, List<string> types)
        {
            int matchCount = CprRegex().Matches(text).Count;
            if (matchCount == 0) return text;
            types.Add("Danish_CPR");
            PiiMetrics.RecordMaskedItem("masked_cpr", matchCount);
            return CprRegex().Replace(text, PiiTokens.Cpr);
        }

        private static string MaskApiKeys(string text, List<string> types)
        {
            int openAiMatchCount = OpenAiKeyRegex().Matches(text).Count;
            if (openAiMatchCount > 0)
            {
                types.Add("OpenAI_API_Key");
                PiiMetrics.RecordMaskedItem("masked_openai_key", openAiMatchCount);
                text = OpenAiKeyRegex().Replace(text, PiiTokens.ApiKey);
            }

            int genericMatchCount = GenericKeyAssignmentRegex().Matches(text).Count;
            if (genericMatchCount > 0)
            {
                types.Add("Generic_Secret");
                PiiMetrics.RecordMaskedItem("masked_generic_key", genericMatchCount);
                text = GenericKeyAssignmentRegex().Replace(text, PiiTokens.ApiKey);
            }

            return text;
        }
    }
}
