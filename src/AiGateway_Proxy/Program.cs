using AiGateway_Core.DocumentTextExtraction;
using AiGateway_Core.LanguageDetection;
using AiGateway_Core.LlmPayload;
using AiGateway_Core.Options;
using AiGateway_Core.Orchestration;
using AiGateway_Core.PiiScanners;
using AiGateway_Core.Telemetry;
using AiGateway_Core.TextMasking;
using AiGateway_Infrastructure.DocumentTextExtraction;
using AiGateway_Infrastructure.LanguageDetection;
using AiGateway_Infrastructure.LlmPayload;
using AiGateway_Infrastructure.PiiScanners;
using AiGateway_Infrastructure.RequestLlmPayload;
using AiGateway_Infrastructure.TextMasking;
using AiGateway_Proxy.Middleware;
using AiGateway_Proxy.Parsers;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddOptions<PiiGatewayOptions>()
    .Bind(builder.Configuration.GetSection(PiiGatewayOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<LanguageDetectionOptions>()
    .Bind(builder.Configuration.GetSection(LanguageDetectionOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(opts => opts.SupportedLanguages != null && opts.SupportedLanguages.Count > 0,
        "LanguageDetection:SupportedLanguages cannot be null or empty.")
    .ValidateOnStart();


builder.Services.AddSingleton<ILanguageDetector, LinguaLanguageDetector>();
builder.Services.AddSingleton<IFileTypeDetector, MagicByteFileTypeDetector>();
builder.Services.AddSingleton<IPiiTransformer, MaskingTransformer>();

builder.Services.AddScoped<IPiiGatewayOrchestrator, PiiGatewayOrchestrator>();
builder.Services.AddScoped<IRequestParser, RequestParser>();
builder.Services.AddScoped<ILlmPayloadFormatter, LlmPayloadFormatter>();

builder.Services.AddTransient<IFileFormatHandler, PdfTextExtractor>();
builder.Services.AddTransient<IFileFormatHandler, TxtTextExtractor>();

builder.Services.AddTransient<IDocumentExtractionService, DocumentExtractionService>();

builder.Services.AddKeyedSingleton<ILlmProviderPayloadMapper, OpenAiPayloadMapper>("openai");

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

app.UseWhen(
    context => context.Request.Path.StartsWithSegments("/v1"),
    appBuilder => appBuilder.UseMiddleware<PiiSanitizationMiddleware>()
);
app.UseOpenTelemetryPrometheusScrapingEndpoint();
app.MapReverseProxy();

app.Run();