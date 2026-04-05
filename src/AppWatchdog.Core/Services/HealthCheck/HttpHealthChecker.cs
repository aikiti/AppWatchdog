using Serilog;

namespace AppWatchdog.Core.Services.HealthCheck;

public class HttpHealthChecker : IHealthChecker
{
    private readonly string _url;
    private static readonly HttpClient SharedClient = new() { Timeout = TimeSpan.FromSeconds(10) };

    public string Description => $"HTTP {_url}";

    public HttpHealthChecker(string url)
    {
        _url = url;
    }

    public async Task<bool> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            var response = await SharedClient.GetAsync(_url, ct);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Log.Debug("HTTP health check failed for {Url}: {Msg}", _url, ex.Message);
            return false;
        }
    }
}
