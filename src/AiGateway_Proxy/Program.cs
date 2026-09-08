using AiGateway_Core.Interfaces;
using AiGateway_Infrastructure.LanguageDetection;
using AiGateway_Infrastructure.Scanners;
using AiGateway_Infrastructure.Services;
using AiGateway_Infrastructure.Telemetry;
using AiGateway_Proxy.Middleware;
using OpenTelemetry.Metrics;

var builder = WebApplication.CreateBuilder(args);

//builder.Services.AddSingleton<IPiiScanner, RegexPiiScanner>();

var supportedLanguages = builder.Configuration
    .GetSection("LanguageDetection:SupportedLanguages")
    .Get<string[]>() ?? new[] { "da", "en" };

builder.Services.AddSingleton<ILanguageDetector>(new LinguaLanguageDetector(supportedLanguages));

builder.Services.AddSingleton<RegexPiiScanner>();
builder.Services.AddHttpClient<PresidioPiiScanner>(client =>
{
    // Hvis proxyen kører i Docker Compose: "http://presidio_analyzer:5001"
    // Hvis du tester lokalt fra din maskine: "http://localhost:5001"
    var presidioUrl = builder.Configuration["Presidio:BaseUrl"] ?? "http://127.0.0.1:5001/";
    client.BaseAddress = new Uri(presidioUrl);

    client.Timeout = TimeSpan.FromSeconds(3);
});

// Registrer Composite Scanneren som IPiiScanner til Gatewayen
builder.Services.AddSingleton<IPiiScanner>(sp => new ConditionalPiiScanner(new IPiiScanner[]
{
    sp.GetRequiredService<RegexPiiScanner>(),     // Kører først (Hurtig & deterministisk)
    sp.GetRequiredService<PresidioPiiScanner>()   // Kører bagefter (NLP for navne/steder)
}));

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