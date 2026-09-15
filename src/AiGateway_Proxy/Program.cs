using AiGateway_Core.Interfaces;
using AiGateway_Core.Models;
using AiGateway_Infrastructure.Coordinators;
using AiGateway_Infrastructure.LanguageDetection;
using AiGateway_Infrastructure.Scanners;
using AiGateway_Infrastructure.Services;
using AiGateway_Infrastructure.Telemetry;
using AiGateway_Infrastructure.Transformers;
using AiGateway_Proxy.Middleware;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<PiiGatewayOptions>(
    builder.Configuration.GetSection(PiiGatewayOptions.SectionName));

var supportedLanguages = builder.Configuration
    .GetSection("LanguageDetection:SupportedLanguages")
    .Get<string[]>() ?? new[] { "da", "en" };

builder.Services.AddSingleton<ILanguageDetector>(new LinguaLanguageDetector(supportedLanguages));

// Resolve options cleanly inside AddHttpClient
builder.Services.AddHttpClient<IPiiScanner, PresidioPiiScanner>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<PiiGatewayOptions>>().Value;

    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

builder.Services.AddSingleton<IPiiTransformer, MaskingTransformer>();

builder.Services.AddTransient<PiiGatewayCoordinator>();

// Registrer YARP Reverse Proxy
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

app.UseMiddleware<PiiSanitizationMiddleware>();
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.MapReverseProxy();

app.Run();