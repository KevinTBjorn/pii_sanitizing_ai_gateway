using AiGateway_Core.Constants;
using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;
using AiGateway_Infrastructure.Telemetry;
using System.Text.RegularExpressions;

namespace AiGateway_Infrastructure.PiiScanners
{
    public partial class RegexPiiScanner
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
    }
}
