using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AiGateway_Core.DocumentTextExtraction
{
    public static class DocumentTextNormalizer
    {
        private static readonly Regex MultipleSpacesRegex = new(@" {2,}", RegexOptions.Compiled);
        private static readonly Regex MultipleNewlinesRegex = new(@"\n{3,}", RegexOptions.Compiled);

        public static string NormalizeForLlm(string? rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return string.Empty;

            string text = rawText.Replace("\r\n", "\n").Replace("\r", "\n");

            text = MultipleSpacesRegex.Replace(text, " ");

            text = MultipleNewlinesRegex.Replace(text, "\n\n");

            return text.Trim();
        }
    }
}
