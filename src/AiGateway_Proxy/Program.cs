using AiGateway_Core.DocumentTextExtraction;
using AiGateway_Core.Interfaces;
using AiGateway_Core.LanguageDetection;
using AiGateway_Core.Options;
using AiGateway_Core.PiiScanners;
using AiGateway_Core.TextMasking;
using AiGateway_Infrastructure.Coordinators;
using AiGateway_Infrastructure.DocumentTextExtraction;
using AiGateway_Infrastructure.LanguageDetection;
using AiGateway_Infrastructure.Payloads;
using AiGateway_Infrastructure.PiiScanners;
using AiGateway_Core.Telemetry;
using AiGateway_Infrastructure.Transformers;
using AiGateway_Proxy.Middleware;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddOptions<PiiGatewayOptions>()
    .Bind(builder.Configuration.GetSection(PiiGatewayOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(opts => opts.IgnoredKeys != null && opts.IgnoredKeys.Count > 0, "PiiOptions:IgnoredKeys cannot be null or empty.")
    .ValidateOnStart();

builder.Services.AddOptions<LanguageDetectionOptions>()
    .Bind(builder.Configuration.GetSection(LanguageDetectionOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(opts => opts.SupportedLanguages != null && opts.SupportedLanguages.Count > 0,
        "LanguageDetection:SupportedLanguages cannot be null or empty.")
    .ValidateOnStart();


builder.Services.AddSingleton<ILanguageDetector, LinguaLanguageDetector>();
builder.Services.AddSingleton<IPiiTransformer, MaskingTransformer>();
builder.Services.AddSingleton<IFileTypeDetector, MagicByteFileTypeDetector>();

builder.Services.AddScoped<IPiiGatewayCoordinator, PiiGatewayCoordinator>();
builder.Services.AddScoped<IPiiPayloadSanitizer, JsonPiiPayloadSanitizer>();

builder.Services.AddTransient<IDocumentTextExtractor, PdfTextExtractor>();
builder.Services.AddTransient<IDocumentTextExtractor, TxtTextExtractor>();

builder.Services.AddTransient<IDocumentExtractionService, DocumentExtractionService>();


builder.Services.AddHttpClient<IPiiScanner, PresidioPiiScanner>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<PiiGatewayOptions>>().Value;

    client.BaseAddress = new Uri(options.presidioURL);
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));


builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddMeter(PiiMetrics.MeterName);
        metrics.AddPrometheusExporter();
    });


var app = builder.Build();

app.UseMiddleware<PiiSanitizationMiddleware>();
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.MapReverseProxy();

app.Run();