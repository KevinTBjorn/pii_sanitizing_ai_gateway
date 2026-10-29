    using AiGateway_Core.LlmPayload;
using AiGateway_Core.Orchestration;
using AiGateway_Core.RequestParser;
using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Infrastructure.LlmPayload
{
    public class OpenAiPayloadMapper : ILlmProviderPayloadMapper
    {
        public object MapToProviderPayload(RequestPromptPayload request, IReadOnlyList<TextEntry> sanitizedEntries)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(sanitizedEntries);

            var messages = new List<object>();

            var docContents = sanitizedEntries
                .Where(e => e.Source == TextSourceType.Document && !string.IsNullOrWhiteSpace(e.Content))
                .Select(e => e.Content)
                .ToArray();

            string? userPrompt = sanitizedEntries
                .FirstOrDefault(e => e.Source == TextSourceType.UserPrompt)?.Content;

            if (docContents.Length > 0)
            {
                var contextBuilder = new StringBuilder();
                contextBuilder.Append("<context>\n");
                for (int i = 0; i < docContents.Length; i++)
                {
                    contextBuilder.Append($"  <document index=\"{i + 1}\">\n");
                    contextBuilder.Append(docContents[i]);
                    contextBuilder.Append("\n  </document>\n");
                }
                contextBuilder.AppendLine("</context>");

                messages.Add(new
                {
                    role = "system",
                    content = contextBuilder.ToString()
                });
            }

            if (!string.IsNullOrWhiteSpace(userPrompt))
            {
                messages.Add(new
                {
                    role = "user",
                    content = userPrompt
                });
            }

            return new
            {
                model = request.Model,
                messages = messages,
                temperature = request.Temperature,
                max_tokens = request.Max_Tokens
            };
        }
    }
}
