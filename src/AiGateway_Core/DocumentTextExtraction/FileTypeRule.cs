using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.DocumentTextExtraction
{
    public record FileTypeRule(
        string Extension,
        byte[]? MagicBytes = null,
        string? ContainerType = null,
        bool IsText = false
    );
}
