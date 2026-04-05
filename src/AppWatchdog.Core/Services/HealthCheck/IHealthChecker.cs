namespace AppWatchdog.Core.Services.HealthCheck;

public interface IHealthChecker
{
    Task<bool> CheckAsync(CancellationToken ct = default);
    string Description { get; }
}
