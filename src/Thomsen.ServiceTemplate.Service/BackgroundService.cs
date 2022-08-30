using Thomsen.ServiceTemplate.Service.Logger;

namespace Thomsen.ServiceTemplate.Service;

public class BackgroundService : Microsoft.Extensions.Hosting.BackgroundService {
    private readonly ILogger<BackgroundService> _logger;
    private readonly IConfiguration _conf;

    private readonly PeriodicTimer _timer;

    public BackgroundService(ILogger<BackgroundService> logger, IConfiguration conf) {
        _logger = logger;
        _conf = conf;

        int interval = _conf.GetSection("General").GetValue("IntervalMilliseconds", 1000);
        _timer = new PeriodicTimer(TimeSpan.FromMilliseconds(interval));
    }

    public override async Task StartAsync(CancellationToken cancellationToken) {
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken) {
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        _logger.LogInformation("--- Started ---");

        try {
            while (!stoppingToken.IsCancellationRequested) {
                _logger.LogInformation($"Running at {DateTime.UtcNow}");

                await _timer.WaitForNextTickAsync(stoppingToken);
            }
        } catch (OperationCanceledException) {
            _logger.LogInformation($"Gracefully stopping after {nameof(OperationCanceledException)}");
        } catch (Exception ex) {
            _logger.LogError($"{ex.GetAllMessages()}", ex);
            Environment.Exit(1);
        }

        _logger.LogInformation("--- Stopped ---");
    }
}

