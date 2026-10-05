using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text;

namespace AiGateway_Infrastructure.DocumentTextExtraction
{
    public static class SupportedTextExtensions
    {
        public static readonly FrozenSet<string> Extensions = new[]
        {
        ".txt", ".csv", ".md", ".log"
        }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);
    }
}
