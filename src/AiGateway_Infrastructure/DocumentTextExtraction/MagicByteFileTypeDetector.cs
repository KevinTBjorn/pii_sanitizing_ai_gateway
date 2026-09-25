using AiGateway_Core.DocumentTextExtraction;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Infrastructure.DocumentTextExtraction
{
    public class MagicByteFileTypeDetector : IFileTypeDetector
    {
        // Known container formats (e.g., ZIP containers used by DOCX, XLSX, PPTX)
        private static readonly Dictionary<string, byte[]> ContainerSignatures = new()
        {
            { "zip", new byte[] { 0x50, 0x4B, 0x03, 0x04 } }
        };

        private readonly List<FileTypeRule> _rules;

        /// <summary>
        /// Simply add or remove rules here to update supported formats across your application.
        /// </summary>
        private static List<FileTypeRule> GetDefaultRules() => new()
    {
        // --- Binary Formats (Direct Magic Bytes) ---
        new(Extension: "pdf",  MagicBytes: new byte[] { 0x25, 0x50, 0x44, 0x46 }),

        // --- Container Formats (Matches Container Signature + Fallback Extension) ---
        new(Extension: "docx", ContainerType: "zip"),

        // --- Text Formats (UTF-8 + No Null Bytes) ---
        new(Extension: "txt",  IsText: true),
        //new(Extension: "csv",  IsText: true)
    };

        public MagicByteFileTypeDetector(IEnumerable<FileTypeRule>? customRules = null)
        {
            _rules = customRules?.ToList() ?? GetDefaultRules();
        }

        public async Task<string> DetectTypeAsync(Stream fileStream, string fallbackExtension = "", CancellationToken cancellationToken = default)
        {
            if (!fileStream.CanRead)
                throw new ArgumentException("Stream must be readable.", nameof(fileStream));

            var cleanFallback = fallbackExtension.TrimStart('.').ToLowerInvariant();

            byte[] buffer = new byte[512];
            long initialPosition = fileStream.CanSeek ? fileStream.Position : 0;

            int bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);

            if (fileStream.CanSeek)
            {
                fileStream.Position = initialPosition;
            }

            if (bytesRead == 0)
            {
                throw new InvalidOperationException("Cannot detect file type of an empty stream.");
            }

            // TIER 1: Direct Magic Bytes Match
            foreach (var rule in _rules)
            {
                if (rule.MagicBytes is { } magicBytes && bytesRead >= magicBytes.Length)
                {
                    if (buffer.AsSpan(0, magicBytes.Length).SequenceEqual(magicBytes))
                    {
                        return rule.Extension;
                    }
                }
            }

            // TIER 2: Container Disambiguation (e.g., DOCX inside ZIP)
            foreach (var (container, signature) in ContainerSignatures)
            {
                if (bytesRead >= signature.Length && buffer.Take(signature.Length).SequenceEqual(signature))
                {
                    var matchingRule = _rules.FirstOrDefault(r =>
                        r.ContainerType == container &&
                        r.Extension.Equals(cleanFallback, StringComparison.OrdinalIgnoreCase));

                    if (matchingRule != null)
                    {
                        return matchingRule.Extension;
                    }
                }
            }

            // TIER 3: Text Validation
            if (IsValidTextStream(buffer, bytesRead))
            {
                var textRule = _rules.FirstOrDefault(r =>
                    r.IsText &&
                    r.Extension.Equals(cleanFallback, StringComparison.OrdinalIgnoreCase));

                if (textRule != null)
                {
                    return textRule.Extension;
                }

                // Fallback default for text files if extension wasn't explicitly matched
                if (_rules.Any(r => r.IsText && r.Extension == "txt"))
                {
                    return "txt";
                }
            }

            throw new NotSupportedException($"File content is unsupported or claimed extension '{fallbackExtension}' was rejected.");
        }

        private static bool IsValidTextStream(byte[] buffer, int length)
        {
            for (int i = 0; i < length; i++)
            {
                if (buffer[i] == 0x00) return false;
            }

            try
            {
                _ = Encoding.UTF8.GetString(buffer, 0, length);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
