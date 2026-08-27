using AiGateway_Core.Interfaces;
using AiGateway_Infrastructure.Services;
using AiGateway_Proxy.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 1. Registrer PiiScannerService under IPiiScanner grænsefladen
builder.Services.AddSingleton<IPiiScanner, PiiScannerService>();

// 2. Registrer YARP Reverse Proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.MapReverseProxy();

app.Run();
