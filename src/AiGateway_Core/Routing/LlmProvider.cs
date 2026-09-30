using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json.Serialization;

namespace AiGateway_Core.Routing
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum LlmProvider
    {
        OpenAi,
        Anthropic
    }
}
