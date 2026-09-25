using AiGateway_Core.IncomingLlmPayload;
using System.Text.Json;

namespace AiGateway_Proxy.Parsers
{
    public static class RequestParser
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public static async Task<RequestPromptContext> ParseAsync(HttpRequest request, CancellationToken cancellationToken)
        {
            if (request.HasFormContentType)
            {
                var form = await request.ReadFormAsync(cancellationToken);

                string prompt = form["prompt"].ToString();
                if (string.IsNullOrWhiteSpace(prompt))
                {
                    throw new JsonException("The 'prompt' form field is required.");
                }

                var gatewayRequest = new RequestPromptPayload
                {
                    Prompt = prompt,
                    Model = form["model"].FirstOrDefault()
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


            request.Body.Position = 0;
            var jsonRequest = await JsonSerializer.DeserializeAsync<RequestPromptPayload>(request.Body, JsonOptions, cancellationToken);

            if (jsonRequest is null || string.IsNullOrWhiteSpace(jsonRequest.Prompt))
            {
                throw new JsonException("Invalid JSON payload or missing 'Prompt' property.");
            }

            request.Body.Position = 0;

            return new RequestPromptContext
            {
                PromptRequest = jsonRequest,
                Files = Array.Empty<InputFile>()
            };
        }
    }
}
