using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Models
{
    public record PiiCheckResult(
        string CleanedPrompt,
        bool HasPii
    );
}
