using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;

using Thomsen.ServiceTemplate.Service.Logger;

namespace Thomsen.ServiceTemplate.Service;

public class Program {
    public static async Task Main(string[] args) {
        using IHost host = Host.CreateDefaultBuilder(args)
            .UseWindowsService(options => {
                options.ServiceName = "Thomsen ServiceTemplate";
            })
            .ConfigureServices(services => {
                LoggerProviderOptions.RegisterProviderOptions<EventLogSettings, EventLogLoggerProvider>(services);

                services.AddHostedService<WindowsBackgroundService>();
            })
            .ConfigureLogging((context, logging) => {
                logging.ClearProviders();

                // Custom file logging
                logging.AddFileLogger(conf => {
                    conf.LogFileDirectory = Path.Combine(AppContext.BaseDirectory, "Log");
                });

                // Workaround for bug in logging to event log, see: https://github.com/dotnet/runtime/issues/47303
                logging.AddConfiguration(context.Configuration.GetSection("Logging"));
            })
            .Build();

        await host.RunAsync();
    }
}