using System.Diagnostics.Metrics;

namespace AiGateway_Infrastructure.Telemetry;

public static class PiiMetrics
{
    public const string MeterName = "AiGateway.Infrastructure";

    private static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> PiiMaskedCounter = Meter.CreateCounter<long>(
        name: "aigateway_pii_masked_total",
        unit: "{items}",
        description: "Antal maskerede PII-forekomster opdelt på type"
    );

    public static void RecordMaskedItem(string piiType, int count = 1)
    {
        PiiMaskedCounter.Add(count, new KeyValuePair<string, object?>("pii_type", piiType));
    }
}