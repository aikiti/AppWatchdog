using System.Net.Sockets;
using Serilog;

namespace AppWatchdog.Core.Services.HealthCheck;

public class TcpHealthChecker : IHealthChecker
{
    private readonly string _host;
    private readonly int _port;

    public string Description => $"TCP {_host}:{_port}";

    public TcpHealthChecker(string host, int port)
    {
        _host = host;
        _port = port;
    }

    public async Task<bool> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            await client.ConnectAsync(_host, _port, cts.Token);
            return true;
        }
        catch (Exception ex)
        {
            Log.Debug("TCP health check failed for {Host}:{Port}: {Msg}", _host, _port, ex.Message);
            return false;
        }
    }
}
