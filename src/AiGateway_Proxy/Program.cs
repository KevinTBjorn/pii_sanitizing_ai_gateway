using AiGateway_Core.Interfaces;
using AiGateway_Infrastructure.Services;
using AiGateway_Proxy.Middleware;
using AiGateway_Infrastructure.Telemetry;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

// 1. Registrer PiiScannerService under IPiiScanner grænsefladen
builder.Services.AddSingleton<IPiiScanner, RegexPiiScanner>();

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