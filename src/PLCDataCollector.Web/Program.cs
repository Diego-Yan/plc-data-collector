// TAG: review-fix-2 — 2026-06-22 — NLog configuration, add TimeSeriesService to DI
//      disposal via IHostApplicationLifetime, DeviceManager.Flush on shutdown.
using PLCDataCollector.Core.Cache;
using PLCDataCollector.Core.Storage;
using PLCDataCollector.Web.Services;
using PLCDataCollector.Web.WebSocket;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseWindowsService(options => options.ServiceName = "PLCDataCollector");

builder.Services.AddControllers();

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

builder.Services.AddHostedService<CollectorService>();
builder.Services.AddHostedService<ForwardService>();

var app = builder.Build();

app.UseWebSockets();
app.UseStaticFiles();
app.MapControllers();

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

var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
lifetime.ApplicationStopping.Register(() =>
{
    app.Services.GetRequiredService<DeviceManager>().Flush();
});

app.Run();
