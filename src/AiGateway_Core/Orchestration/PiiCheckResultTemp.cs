using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Orchestration
{
    public record PiiCheckResultTemp(
        string CleanedPrompt,
        bool HasPii
    );
}
