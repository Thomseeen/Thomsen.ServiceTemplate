using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Configuration;

namespace Thomsen.ServiceTemplate.Service.Logger {
    internal static class FileLoggerExtensions {
        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, Action<FileLoggerConfiguration> configure) {
            builder.AddFileLogger();
            builder.Services.Configure(configure);

            return builder;
        }

        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder) {
            builder.AddConfiguration();

            builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<ILoggerProvider, FileLoggerProvider>());

            LoggerProviderOptions.RegisterProviderOptions<FileLoggerConfiguration, FileLoggerProvider>(builder.Services);

            return builder;
        }
    }
}
