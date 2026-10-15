using AiGateway_Core.Exceptions;
using AiGateway_Core.RequestParser;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace AiGateway_Infrastructure.RequestParser
{
    public class RequestParser : IRequestParser
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
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

                string prompt = form["prompt"].ToString();
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    throw new GatewayException("The 'prompt' form field is required.", 400);
                }

                string model = form["model"].ToString();
                if (string.IsNullOrWhiteSpace(model))
                {
                    throw new GatewayException("The 'model' form field is required for request routing.", 400);
                }

                var gatewayRequest = new RequestPromptPayload
                {
                    Prompt = prompt,
                    Model = model
                };

                if (double.TryParse(form["temperature"], out var temp))
                {
                    gatewayRequest = gatewayRequest with { Temperature = temp };
                }

                var files = form.Files
                    .Where(f => f.Length > 0)
                    .Select(f => new InputFile(f.OpenReadStream(), Path.GetExtension(f.FileName)))
                    .ToList();

                return new RequestPromptContext
                {
                    PromptRequest = gatewayRequest,
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

                if (string.IsNullOrWhiteSpace(jsonRequest.Prompt))
                {
                    throw new GatewayException("The 'prompt' property is required in JSON payload.", 400);
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
