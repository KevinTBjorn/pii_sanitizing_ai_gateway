using System;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Core.PiiScanners
{
    public record PiiMatch(
        int Start,
        int Length,
        string EntityType,
        double Score,
        string RawValue
    );
}
