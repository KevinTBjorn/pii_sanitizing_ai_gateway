using System.Text.RegularExpressions;
using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;

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
            if (!EmailRegex().IsMatch(text)) return text;
            types.Add("Email");
            return EmailRegex().Replace(text, "[MASKED_EMAIL]");
        }

        private static string MaskCprs(string text, List<string> types)
        {
            if (!CprRegex().IsMatch(text)) return text;
            types.Add("Danish_CPR");
            return CprRegex().Replace(text, "[MASKED_CPR]");
        }

        private static string MaskApiKeys(string text, List<string> types)
        {
            if (OpenAiKeyRegex().IsMatch(text))
            {
                types.Add("OpenAI_API_Key");
                text = OpenAiKeyRegex().Replace(text, "[MASKED_OPENAI_KEY]");
            }

            if (GenericKeyAssignmentRegex().IsMatch(text))
            {
                types.Add("Generic_Secret");
                text = GenericKeyAssignmentRegex().Replace(text, "[MASKED_SECRET]");
            }

            return text;
        }
    }
}
