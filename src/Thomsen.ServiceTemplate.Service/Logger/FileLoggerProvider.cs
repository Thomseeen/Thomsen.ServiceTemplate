using Microsoft.Extensions.Options;

using System.Runtime.Versioning;

namespace Thomsen.ServiceTemplate.Service.Logger {
    [UnsupportedOSPlatform("browser")]
    [ProviderAlias("FileLog")]
    public sealed class FileLoggerProvider : ILoggerProvider {
        private readonly IDisposable _onChangeToken;
        private FileLoggerConfiguration _currentConfig;

        public FileLoggerProvider(IOptionsMonitor<FileLoggerConfiguration> config) {
            _currentConfig = config.CurrentValue;
            _onChangeToken = config.OnChange(updatedConfig => _currentConfig = updatedConfig);
        }

        public ILogger CreateLogger(string categoryName) => new FileLogger(categoryName, GetCurrentConfig);

        private FileLoggerConfiguration GetCurrentConfig() => _currentConfig;

        public void Dispose() {
            _onChangeToken.Dispose();
        }
    }
}
