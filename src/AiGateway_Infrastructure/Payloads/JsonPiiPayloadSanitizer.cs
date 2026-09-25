using AiGateway_Core.Interfaces;
using AiGateway_Core.Options;
using AiGateway_Core.Orchestration;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AiGateway_Infrastructure.Payloads
{
    public class JsonPiiPayloadSanitizer : IPiiPayloadSanitizer
    {
        private readonly IPiiGatewayOrchestrator _coordinator;
        private readonly HashSet<string> _ignoredKeys;

        private static readonly JsonSerializerOptions UnescapedJsonOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public JsonPiiPayloadSanitizer(IPiiGatewayOrchestrator coordinator, IOptions<PiiGatewayOptions> options)
        {
            ArgumentNullException.ThrowIfNull(coordinator);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(options.Value);

            _coordinator = coordinator;
            _ignoredKeys = options.Value.IgnoredKeys;
        }

        public async Task<PiiCheckResultTemp> SanitizePayloadAsync(string rawBody, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(rawBody))
                return new PiiCheckResultTemp(rawBody, false);

            var jsonNode = JsonNode.Parse(rawBody);
            if (jsonNode == null)
                return new PiiCheckResultTemp(rawBody, false);

            bool hasPii = false;

            async Task InspectNodeAsync(JsonNode? node, string? parentKey = null)
            {
                if (node == null) return;

                if (node is JsonValue val && val.GetValueKind() == JsonValueKind.String)
                {
                    if (parentKey != null && _ignoredKeys.Contains(parentKey))
                    {
                        return;
                    }

                    string originalText = val.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(originalText))
                    {
                        var result = await _coordinator.OrchestrateSanitizeProcessAsync(originalText, cancellationToken);

                        if (result.HasPii)
                        {
                            val.ReplaceWith(JsonValue.Create(result.CleanedPrompt));
                            hasPii = true;
                        }
                    }
                }
                else if (node is JsonObject obj)
                {
                    foreach (var property in obj)
                    {
                        await InspectNodeAsync(property.Value, property.Key);
                    }
                }
                else if (node is JsonArray array)
                {
                    for (int i = 0; i < array.Count; i++)
                    {
                        await InspectNodeAsync(array[i], parentKey);
                    }
                }
            }

            await InspectNodeAsync(jsonNode);

            return new PiiCheckResultTemp(hasPii ? jsonNode.ToJsonString(UnescapedJsonOptions) : rawBody, hasPii);
        }
    }
}
