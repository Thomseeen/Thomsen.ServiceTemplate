using System.Diagnostics;

namespace Thomsen.ServiceTemplate.Service.Logger {
    public sealed class FileLogger : ILogger {
        private readonly Func<FileLoggerConfiguration> _getCurrentConfig;
        private readonly string _name;

        private static readonly object _logFileLock = new();

        public FileLogger(string name, Func<FileLoggerConfiguration> getCurrentConfig) => (_name, _getCurrentConfig) = (name, getCurrentConfig);

        public IDisposable BeginScope<TState>(TState state) => default!;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) {
            if (!IsEnabled(logLevel)) return;

            string message = formatter(state, exception);

            string line = $"{DateTime.Now} - {logLevel} - {_name}: {message}";

            lock (_logFileLock) {
                string currentFilePath = RefreshPath(_getCurrentConfig());

                using StreamWriter writer = GetStreamWriter(currentFilePath);

                writer.WriteLine(line);
            }
        }

        private static StreamWriter GetStreamWriter(string filePath) {
            return File.Exists(filePath)
                 ? File.AppendText(filePath)
                 : File.CreateText(filePath);
        }


        private static string RefreshPath(FileLoggerConfiguration conf) {
            Directory.CreateDirectory(conf.LogFileDirectory);

            string fileName = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule!.ModuleName!);
            string currentLogFilePath = Path.Combine(conf.LogFileDirectory, $"{fileName}.log");

            // No log file yet, nothing to be done
            if (!File.Exists(currentLogFilePath)) {
                return currentLogFilePath;
            }

            // Log file smaller than configred max. size, nothing to be done
            FileInfo info = new(currentLogFilePath);
            if (info.Length < conf.MaxLogFileSizeKB * 1000) {
                return currentLogFilePath;
            }

            // Get all present log file so we can possible rotate them and delete some if there are too many
            Dictionary<int, string> oldLogFiles = GetOldLogFilesByIdx(conf.LogFileDirectory);

            // Delete as long as there are more files than configured as max.
            while (oldLogFiles.Count > conf.MaxOldLogFilesCnt - 1) {
                File.Delete(Path.Combine(conf.LogFileDirectory, $"{oldLogFiles.First().Value}_{oldLogFiles.First().Key}.log"));

                oldLogFiles = GetOldLogFilesByIdx(conf.LogFileDirectory);
            }

            // Rotate old log files by appended index
            foreach (KeyValuePair<int, string> logFileParts in oldLogFiles) {
                File.Move(
                    sourceFileName: Path.Combine(conf.LogFileDirectory, $"{logFileParts.Value}_{logFileParts.Key}.log"),
                    destFileName: Path.Combine(conf.LogFileDirectory, $"{logFileParts.Value}_{logFileParts.Key + 1}.log"));
            }

            // Rotate newest log file to contain an index
            File.Move(
                sourceFileName: currentLogFilePath,
                destFileName: Path.Combine(conf.LogFileDirectory, $"{Path.GetFileNameWithoutExtension(currentLogFilePath)}_1.log"));

            return currentLogFilePath;
        }

        private static Dictionary<int, string> GetOldLogFilesByIdx(string logFileDirectory) {
            return Directory.EnumerateFiles(logFileDirectory)
                .Select(file => Path.GetFileNameWithoutExtension(file).Split('_'))
                .Where(fileParts => int.TryParse(fileParts.Last(), out _))
                .OrderByDescending(fileParts => int.Parse(fileParts.Last()))
                .ToDictionary(
                    fileParts => int.Parse(fileParts.Last()),
                    fileParts => string.Join("_", fileParts.Take(fileParts.Length - 1)));
        }
    }
}
