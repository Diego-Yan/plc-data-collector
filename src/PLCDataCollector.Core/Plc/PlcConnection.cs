// TAG: fixed — 2026-05-20 — replaced sync Open/Read/Close with async overloads, removed dead using
using System;
using System.Threading.Tasks;
using S7.Net;

namespace PLCDataCollector.Core.Plc;

public class PlcConnection : IDisposable
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
        try { await _plc.CloseAsync(); }
        catch { }
        ConnectionStateChanged?.Invoke(this, false);
    }

    public async Task<object?> ReadAsync(string address)
    {
        try
        {
            return await _plc.ReadAsync(address);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _plc?.Close();
            _plc?.Dispose();
            _disposed = true;
        }
    }
}
