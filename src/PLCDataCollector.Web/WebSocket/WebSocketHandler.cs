// ============================================================================
// TAG: thread-safe — 2026-05-20 — replaced HashSet<WebSocket> with
//   ConcurrentDictionary<WebSocket, byte> to prevent corruption under
//   concurrent subscribe/unsubscribe/broadcast.
// TAG: json-switch — replaced Newtonsoft.Json with System.Text.Json
// ============================================================================

using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using PLCDataCollector.Core.Models;

namespace PLCDataCollector.Web.WebSocket;

public class WebSocketHandler
{
    // deviceId → set of subscribed sockets (ConcurrentDictionary as thread-safe set)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<System.Net.WebSockets.WebSocket, byte>> _subscriptions = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task HandleAsync(System.Net.WebSockets.WebSocket ws, CancellationToken ct)
    {
        var buffer = new byte[4096];

        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
            }
            catch
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
                break;

            var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
            SubscribeMessage? msg;
            try
            {
                msg = JsonSerializer.Deserialize<SubscribeMessage>(json, JsonOpts);
            }
            catch
            {
                continue;
            }

            if (msg == null) continue;

            var key = msg.DeviceId;
            if (msg.Type == "subscribe")
            {
                var set = _subscriptions.GetOrAdd(key, _ => new ConcurrentDictionary<System.Net.WebSockets.WebSocket, byte>());
                set.TryAdd(ws, 0);
            }
            else if (msg.Type == "unsubscribe")
            {
                if (_subscriptions.TryGetValue(key, out var set))
                    set.TryRemove(ws, out _);
            }
        }

        // cleanup: remove closed sockets from all subscriptions
        foreach (var (key, sockets) in _subscriptions)
        {
            foreach (var s in sockets.Keys)
            {
                if (s.State != WebSocketState.Open)
                    sockets.TryRemove(s, out _);
            }
            if (sockets.IsEmpty)
                _subscriptions.TryRemove(key, out _);
        }
    }

    public async Task BroadcastPoint(PointValue pv)
    {
        var payload = new
        {
            type = "data",
            deviceId = pv.DeviceId,
            pointId = pv.PointId,
            value = pv.Value,
            ts = pv.Timestamp,
            quality = (int)pv.Quality
        };
        var json = JsonSerializer.Serialize(payload, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);

        await BroadcastToDevice(pv.DeviceId, bytes);
    }

    public async Task BroadcastStatus(string deviceId, bool online)
    {
        var payload = new
        {
            type = "status",
            deviceId,
            online
        };
        var json = JsonSerializer.Serialize(payload, JsonOpts);
        var bytes = Encoding.UTF8.GetBytes(json);

        await BroadcastToDevice(deviceId, bytes);
    }

    private async Task BroadcastToDevice(string deviceId, byte[] data)
    {
        if (!_subscriptions.TryGetValue(deviceId, out var sockets)) return;

        var segment = new ArraySegment<byte>(data);
        // snapshot keys to avoid mutation during iteration
        foreach (var ws in sockets.Keys.ToArray())
        {
            if (ws.State == WebSocketState.Open)
            {
                try
                {
                    await ws.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch
                {
                    sockets.TryRemove(ws, out _);
                }
            }
        }

        if (sockets.IsEmpty)
            _subscriptions.TryRemove(deviceId, out _);
    }
}

public class SubscribeMessage
{
    public string Type { get; set; } = "";
    public string DeviceId { get; set; } = "";
    public List<string>? PointIds { get; set; }
}
