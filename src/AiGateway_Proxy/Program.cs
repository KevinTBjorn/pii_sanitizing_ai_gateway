using AiGateway_Core.Attributes;
using AiGateway_Core.DocumentTextExtraction;
using AiGateway_Core.Forwarding;
using AiGateway_Core.LanguageDetection;
using AiGateway_Core.LlmPayload;
using AiGateway_Core.Options;
using AiGateway_Core.Orchestration;
using AiGateway_Core.PiiScanners;
using AiGateway_Core.RequestParser;
using AiGateway_Core.Routing;
using AiGateway_Core.Telemetry;
using AiGateway_Core.TextMasking;
using AiGateway_Infrastructure.DocumentTextExtraction;
using AiGateway_Infrastructure.Forwarding;
using AiGateway_Infrastructure.LanguageDetection;
using AiGateway_Infrastructure.LlmPayload;
using AiGateway_Infrastructure.PiiScanners;
using AiGateway_Infrastructure.RequestParser;
using AiGateway_Infrastructure.Routing;
using AiGateway_Proxy.Middleware;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddOptions<PresidioOptions>()
    .Bind(builder.Configuration.GetSection(PresidioOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddOptions<LanguageDetectionOptions>()
    .Bind(builder.Configuration.GetSection(LanguageDetectionOptions.SectionName))
    .ValidateDataAnnotations()
    .Validate(opts => opts.SupportedLanguages != null && opts.SupportedLanguages.Count > 0,
        "LanguageDetection:SupportedLanguages cannot be null or empty.")
    .ValidateOnStart();

builder.Services.Configure<LlmRoutingOptions>(
    builder.Configuration.GetSection(LlmRoutingOptions.SectionName));


builder.Services.AddSingleton<ILanguageDetector, LinguaLanguageDetector>();
builder.Services.AddSingleton<IFileTypeDetector, FileTypeDetector>();
builder.Services.AddSingleton<IPiiTransformer, MaskingTransformer>();
builder.Services.AddSingleton<IGatewayRouter, GatewayRouter>();

builder.Services.AddScoped<RouteConfig>();
builder.Services.AddScoped<IPiiGatewayOrchestrator, PiiGatewayOrchestrator>();
builder.Services.AddScoped<IRequestParser, RequestParser>();
builder.Services.AddScoped<ILlmPayloadFormatter, LlmPayloadFormatter>();

builder.Services.AddTransient<IFileFormatHandler, PdfTextExtractor>();
builder.Services.AddTransient<IFileFormatHandler, PlainTextExtractor>();
builder.Services.AddTransient<IFileFormatHandler, DocxTextExtractor>();
builder.Services.AddTransient<IDocumentExtractionService, DocumentExtractionService>();

builder.Services.AddKeyedSingleton<ILlmProviderPayloadMapper, OpenAiPayloadMapper>(LlmPayloadFormat.OpenAi);

builder.Services.AddHttpClient<ILlmForwardingService, LlmForwardingService>();
builder.Services.AddHttpClient<IPiiScanner, PresidioPiiScanner>((sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<PresidioOptions>>().Value;

    client.BaseAddress = options.PresidioUrl;
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});

builder.Services.AddOpenTelemetry()
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddMeter(PiiMetrics.MeterName);
        metrics.AddPrometheusExporter();
    });


var app = builder.Build();

app.UseRouting();

app.UseMiddleware<PiiSanitizationMiddleware>();

app.UseOpenTelemetryPrometheusScrapingEndpoint();

app.MapPost("/v1/chat/completions", async (
    HttpContext httpContext,
    [FromServices] ILlmForwardingService forwardingService,
    CancellationToken cancellationToken) =>
{
    await forwardingService.ForwardRequestAsync(httpContext, cancellationToken);
});

app.MapFallback(async (HttpContext httpContext) =>
{
    httpContext.Response.StatusCode = StatusCodes.Status404NotFound;
    httpContext.Response.ContentType = "application/json";

    await httpContext.Response.WriteAsJsonAsync(new
    {
        error = new
        {
            code = 404,
            message = $"The endpoint '{httpContext.Request.Method} {httpContext.Request.Path}' does not exist on this gateway.",
            supportedEndpoints = new[] { "POST /v1/chat/completions" },
            timestamp = DateTime.UtcNow
        }
    });
})
.WithMetadata(new SkipPiiSanitizationAttribute()); ;

app.Run();