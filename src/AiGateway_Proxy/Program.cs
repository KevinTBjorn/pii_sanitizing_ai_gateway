using AiGateway_Core.Interfaces;
using AiGateway_Infrastructure.Language;
using AiGateway_Infrastructure.Scanners;
using AiGateway_Infrastructure.Services;
using AiGateway_Infrastructure.Telemetry;
using AiGateway_Proxy.Middleware;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// 1. Registrer PiiScannerService under IPiiScanner grænsefladen
builder.Services.AddSingleton<IPiiScanner, RegexPiiScanner>();
builder.Services.AddSingleton<ILanguageDetector, LinguaLanguageDetector>();

// Registrer PresidioPiiScanner med en konfigureret HttpClient
builder.Services.AddHttpClient<PresidioPiiScanner>(client =>
{
    // Hvis proxyen kører i Docker Compose: "http://presidio_analyzer:5001"
    // Hvis du tester lokalt fra din maskine: "http://localhost:5001"
    var presidioUrl = builder.Configuration["Presidio:BaseUrl"] ?? "http://localhost:5001";
    client.BaseAddress = new Uri(presidioUrl);

    // Sæt en fornuftig timeout så Presidio ikke blokerer proxyen for længe
    client.Timeout = TimeSpan.FromSeconds(3);
});

// 2. Registrer YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation(); // Indsamler CPU, RAM, HTTP request-tider osv.
        metrics.AddMeter(PiiMetrics.MeterName);
        metrics.AddPrometheusExporter();        // Formaterer metrics til Prometheus
    });

var app = builder.Build();

app.UseMiddleware<PiiMaskingMiddleware>();

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.MapReverseProxy();

app.Run();