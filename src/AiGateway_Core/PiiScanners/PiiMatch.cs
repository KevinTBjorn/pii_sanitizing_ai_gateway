using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.Models
{
    public record PiiMatch(
        int Start,
        int Length,
        string EntityType,
        double Score,
        string RawValue
    );
}
