using AiGateway_Core.IncomingLlmPayload;
using AiGateway_Core.LlmPayload;
using AiGateway_Core.Orchestration;
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

            var messages = new List<object>();

            var docContents = sanitizedEntries
                .Where(e => e.Source == TextSourceType.Document && !string.IsNullOrWhiteSpace(e.Content))
                .Select(e => e.Content);

            string userPrompt = sanitizedEntries
                .FirstOrDefault(e => e.Source == TextSourceType.UserPrompt)?.Content
                ?? request.Prompt
                ?? string.Empty;

            if (docContents.Any())
            {
                messages.Add(new
                {
                    role = "system",
                    content = $"Context Documents:\n{string.Join("\n---\n", docContents)}"
                });
            }

            messages.Add(new
            {
                role = "user",
                content = userPrompt
            });

            return new
            {
                model = request.Model,
                messages = messages,
                temperature = request.Temperature
            };
        }
    }
}
