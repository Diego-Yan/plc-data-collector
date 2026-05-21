// ============================================================================
// TAG: single-process-merge — 2026-05-20
// Merged Windows Service + ASP.NET Kestrel into one process.
// Was two separate executables (Service + Web) with no shared memory.
// Now: single exe that runs Windows Service, hosts Kestrel, WebSocket, and
// all BackgroundServices (Collector, Forward) in one process.
// ============================================================================

using PLCDataCollector.Core.Cache;
using PLCDataCollector.Core.Storage;
using PLCDataCollector.Web.Services;
using PLCDataCollector.Web.WebSocket;

var builder = WebApplication.CreateBuilder(args);

// ---- Windows Service ----
builder.Host.UseWindowsService(options => options.ServiceName = "PLCDataCollector");

// ---- MVC + Controllers ----
builder.Services.AddControllers();

// ---- Core services (singletons — shared across web requests + background services) ----
builder.Services.AddSingleton<MemoryCacheService>();
builder.Services.AddSingleton(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var connStr = config.GetConnectionString("TimeSeriesDb")
        ?? "Host=127.0.0.1;Database=plc_data;Username=postgres";
    return new TimeSeriesService(connStr);
});
builder.Services.AddSingleton<DeviceManager>();
builder.Services.AddSingleton<WebSocketHandler>();

// ---- Background services (IHostedService) ----
builder.Services.AddHostedService<CollectorService>();
builder.Services.AddHostedService<ForwardService>();

// ---- Build ----
var app = builder.Build();

app.UseWebSockets();
app.UseStaticFiles();
app.MapControllers();

// ---- WebSocket endpoint ----
app.Map("/ws", async (HttpContext ctx, WebSocketHandler handler) =>
{
    if (ctx.WebSockets.IsWebSocketRequest)
    {
        var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        await handler.HandleAsync(ws, ctx.RequestAborted);
    }
    else
    {
        ctx.Response.StatusCode = 400;
    }
});

app.MapFallbackToFile("index.html");

app.Run();
