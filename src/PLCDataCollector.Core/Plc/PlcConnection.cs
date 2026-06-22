// TAG: fixed — 2026-05-20 — replaced sync Open/Read/Close with async overloads
// TAG: review-fix — 2026-06-22 — IAsyncDisposable, dispose old Plc on reconnect, ReadAsync timeout
// TAG: review-fix-2 — 2026-06-22 — fixed ReadAsync timeout race (Task.Delay cancelled by CTS),
//      use WaitAsync (.NET 6+) for reliable timeout.
using System;
using System.Threading;
using System.Threading.Tasks;
using S7.Net;

namespace PLCDataCollector.Core.Plc;

public class PlcConnection : IDisposable, IAsyncDisposable
{
    private readonly string _ip;
    private readonly int _port;
    private readonly int _rack;
    private readonly int _slot;
    private Plc? _plc;
    private bool _disposed;

    public string DeviceId { get; }
    public string Ip => _ip;
    public int Port => _port;
    public bool IsConnected => _plc?.IsConnected ?? false;

    public event EventHandler<bool>? ConnectionStateChanged;

    public PlcConnection(string deviceId, string ip, int port = 102, int rack = 0, int slot = 1)
    {
        DeviceId = deviceId;
        _ip = ip;
        _port = port;
        _rack = rack;
        _slot = slot;
    }

    public async Task<bool> ConnectAsync()
    {
        try
        {
            if (_plc != null)
            {
                try { _plc.Dispose(); } catch { }
            }
            _plc = new Plc(CpuType.S71200, _ip, _rack, _slot);
            await _plc.OpenAsync();
            ConnectionStateChanged?.Invoke(this, true);
            return true;
        }
        catch (Exception)
        {
            ConnectionStateChanged?.Invoke(this, false);
            return false;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (_plc != null)
                await _plc.CloseAsync();
        }
        catch { }
        ConnectionStateChanged?.Invoke(this, false);
    }

    public async Task<object?> ReadAsync(string address, int timeoutMs = 3000)
    {
        if (_plc == null) return null;
        try
        {
            return await _plc.ReadAsync(address)
                .WaitAsync(TimeSpan.FromMilliseconds(timeoutMs));
        }
        catch (TimeoutException) { return null; }
        catch { return null; }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            try { _plc?.Close(); } catch { }
            _plc?.Dispose();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            try
            {
                if (_plc != null)
                    await _plc.CloseAsync();
            }
            catch { }
            _plc?.Dispose();
        }
    }
}
