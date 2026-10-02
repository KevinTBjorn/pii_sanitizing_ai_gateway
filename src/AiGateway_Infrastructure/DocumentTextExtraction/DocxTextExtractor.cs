using AiGateway_Core.DocumentTextExtraction;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text;

namespace AiGateway_Infrastructure.DocumentTextExtraction
{
    public class DocxTextExtractor : IFileFormatHandler
    {
        public bool CanHandle(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
                return false;

            return string.Equals(extension.StartsWith('.') ? extension : $".{extension}", ".docx", StringComparison.OrdinalIgnoreCase);
        }

        public Task<string> ExtractTextAsync(Stream fileStream, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(fileStream);

            using var doc = WordprocessingDocument.Open(fileStream, isEditable: false);
            var body = doc.MainDocumentPart?.Document?.Body;

            if (body == null)
            {
                return Task.FromResult(string.Empty);
            }

            var sb = new StringBuilder();

            // Iterate through all paragraphs in document order
            foreach (var paragraph in body.Descendants<Paragraph>())
            {
                foreach (var element in paragraph.Descendants())
                {
                    if (element is Text textElement)
                    {
                        sb.Append(textElement.Text);
                    }
                    else if (element is Break) // Handles Shift+Enter soft line breaks
                    {
                        sb.AppendLine();
                    }
                }

                // Paragraph boundary = Enter key = new line
                sb.AppendLine();
            }

            return Task.FromResult(sb.ToString().TrimEnd());
        }

    }
}
