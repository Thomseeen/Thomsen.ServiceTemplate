namespace Thomsen.ServiceTemplate.Service.Logger {
    public class FileLoggerConfiguration {
        public string LogFileDirectory { get; set; } = "";

        public int MaxLogFileSizeKB { get; set; } = 2000;

        public int MaxOldLogFilesCnt { get; set; } = 10;
    }
}
