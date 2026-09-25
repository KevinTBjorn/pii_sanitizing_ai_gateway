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
                    prompt = form["Prompt"].ToString();
                }

                if (string.IsNullOrWhiteSpace(prompt))
                {
                    throw new JsonException("The 'prompt' form field is required.");
                }

                string? model = form.ContainsKey("model") ? form["model"].ToString() : null;

                double temperature = 0.7;
                if (form.ContainsKey("temperature") && double.TryParse(form["temperature"], out var tempParsed))
                {
                    temperature = tempParsed;
                }

                var gatewayRequest = new RequestPromptPayload
                {
                    Prompt = prompt,
                    Model = model,
                    Temperature = temperature
                };

                var files = new List<InputFile>();
                foreach (var file in form.Files)
                {
                    if (file.Length > 0)
                    {
                        files.Add(new InputFile(file.OpenReadStream(), Path.GetExtension(file.FileName)));
                    }
                }

                return new RequestPromptContext
                {
                    PromptRequest = gatewayRequest,
                    Files = files
                };
            }

            // Case B: Pure application/json Request
            request.Body.Position = 0;
            var jsonRequest = await JsonSerializer.DeserializeAsync<RequestPromptPayload>(request.Body, JsonOptions, cancellationToken);

            if (jsonRequest is null || string.IsNullOrWhiteSpace(jsonRequest.Prompt))
            {
                throw new JsonException("Invalid JSON payload or missing 'Prompt' property.");
            }

            request.Body.Position = 0; // Reset for downstream logging if needed

            return new RequestPromptContext
            {
                PromptRequest = jsonRequest,
                Files = Array.Empty<InputFile>()
            };
        }
    }
}
