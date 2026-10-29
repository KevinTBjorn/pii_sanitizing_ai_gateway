using AiGateway_Core.Exceptions;
using AiGateway_Core.RequestParser;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AiGateway_Infrastructure.RequestParser
{
    public class RequestParser : IRequestParser
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            AllowTrailingCommas = true
        };

        public async Task<RequestPromptContext> ParseAsync(HttpRequest request, CancellationToken cancellationToken)
        {
            string? contentType = request.ContentType;

            if (string.IsNullOrWhiteSpace(contentType))
            {
                throw new GatewayException("Missing 'Content-Type' header. Expected 'application/json' or 'multipart/form-data'.", 415);
            }

            if (request.HasFormContentType)
            {
                var form = await request.ReadFormAsync(cancellationToken);

                string model = form["model"].ToString();
                if (string.IsNullOrWhiteSpace(model))
                {
                    throw new GatewayException("The 'model' form field is required for request routing.", 400);
                }

                string rawMessagesJson = form["messages"].ToString();
                if (string.IsNullOrWhiteSpace(rawMessagesJson))
                {
                    throw new GatewayException("The 'messages' form field is required and must contain a valid JSON array of chat messages.", 400);
                }

                IReadOnlyList<ChatMessagePayload> messages;
                try
                {
                    messages = JsonSerializer.Deserialize<IReadOnlyList<ChatMessagePayload>>(rawMessagesJson, JsonOptions)
                               ?? Array.Empty<ChatMessagePayload>();
                }
                catch (JsonException)
                {
                    throw new GatewayException("The 'messages' form field contains malformed JSON.", 400);
                }

                if (messages.Count == 0)
                {
                    throw new GatewayException("The 'messages' array must contain at least one message.", 400);
                }

                double? temperature = double.TryParse(form["temperature"], out var temp) ? temp : null;
                double? topP = double.TryParse(form["top_p"], out var tp) ? tp : null;
                int? maxTokens = int.TryParse(form["max_tokens"], out var tokens) ? tokens : null;

                var files = form.Files
                    .Where(f => f.Length > 0)
                    .Select(f => new InputFile(f.OpenReadStream(), Path.GetExtension(f.FileName) ?? string.Empty))
                    .ToList();

                return new RequestPromptContext
                {
                    PromptRequest = new RequestPromptPayload
                    {
                        Model = model,
                        Messages = messages,
                        Temperature = temperature,
                        TopP = topP,
                        MaxTokens = maxTokens
                    },
                    Files = files
                };
            }


            if (contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase))
            {
                if (request.Body.CanSeek)
                {
                    request.Body.Position = 0;
                }

                RequestPromptPayload? jsonRequest;
                try
                {
                    jsonRequest = await JsonSerializer.DeserializeAsync<RequestPromptPayload>(request.Body, JsonOptions, cancellationToken);
                }
                catch (JsonException)
                {
                    throw new GatewayException("Malformed JSON payload in request body.", 400);
                }

                if (jsonRequest is null)
                {
                    throw new GatewayException("Request body cannot be null.", 400);
                }

                if (jsonRequest.Messages is null || jsonRequest.Messages.Count == 0)
                {
                    throw new GatewayException("The 'messages' property is required and must contain at least one message.", 400);
                }

                if (string.IsNullOrWhiteSpace(jsonRequest.Model))
                {
                    throw new GatewayException("The 'model' property is required in JSON payload for request routing.", 400);
                }

                return new RequestPromptContext
                {
                    PromptRequest = jsonRequest,
                    Files = Array.Empty<InputFile>()
                };
            }

            throw new GatewayException($"Unsupported Content-Type '{contentType}'. Expected 'application/json' or 'multipart/form-data'.", 415);
        }
    }
}
