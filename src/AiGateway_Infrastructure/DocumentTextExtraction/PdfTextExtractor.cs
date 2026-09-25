using AiGateway_Core.DocumentTextExtraction;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.DocumentLayoutAnalysis.WordExtractor;

namespace AiGateway_Infrastructure.DocumentTextExtraction
{
    public class PdfTextExtractor : IDocumentTextExtractor
    {
        public bool CanHandle(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return false;

            return string.Equals(extension.StartsWith('.') ? extension : $".{extension}", ".pdf", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<string> ExtractTextAsync(Stream fileStream, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(fileStream);

            Stream seekableStream = fileStream;
            MemoryStream? tempMemoryStream = null;

            try
            {
                if (!fileStream.CanSeek)
                {
                    tempMemoryStream = new MemoryStream();
                    await fileStream.CopyToAsync(tempMemoryStream, cancellationToken);
                    tempMemoryStream.Position = 0;
                    seekableStream = tempMemoryStream;
                }
                else if (fileStream.Position != 0)
                {
                    fileStream.Position = 0;
                }
                var textBuilder = new StringBuilder();

                using (var pdfDocument = PdfDocument.Open(seekableStream))
                {
                    foreach (var page in pdfDocument.GetPages())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var pageText = GetTextWithNewlines(page);
                        if (!string.IsNullOrWhiteSpace(pageText))
                        {
                            textBuilder.AppendLine(pageText);
                        }
                    }
                }
                return Regex.Replace(textBuilder.ToString(), @"(\b[a-zA-Z]+)-\s*\r?\n\s*([a-zA-Z]+\b)", "$1$2");
            }
            finally
            {
                tempMemoryStream?.Dispose();
            }
        }

        public static string GetTextWithNewlines(Page page)
        {
            var words = page.GetWords();
            if (!words.Any())
            {
                return string.Empty;
            }

            var lines = words
                .GroupBy(w => Math.Round(w.BoundingBox.Bottom / 2.0) * 2.0)
                .OrderByDescending(g => g.Key)
                .Select(lineGroup => string.Join(" ", lineGroup
                    .OrderBy(w => w.BoundingBox.Left)
                    .Select(w => w.Text)));

            return string.Join("\n", lines);
        }
    }
}
