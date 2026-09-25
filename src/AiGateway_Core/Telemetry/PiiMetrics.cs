using System.Diagnostics.Metrics;

namespace AiGateway_Core.Telemetry
{
    public static class PiiMetrics
    {
        public const string MeterName = "AiGateway.Pii";
        public const string PiiCounterName = "aigateway_pii_occurrences_total";

        private static readonly Meter Meter = new(MeterName, "1.0.0");

        private static readonly Counter<long> PiiCounter = Meter.CreateCounter<long>(
            name: PiiCounterName,
            unit: "{items}",
            description: "Antal fundne PII-forekomster opdelt på type og handling"
        );

        public static void RecordPii(string piiType, PiiAction action, int count = 1)
        {
            PiiCounter.Add(count,
                new KeyValuePair<string, object?>("pii_type", piiType),
                new KeyValuePair<string, object?>("action", action)
            );
        }
    }
}