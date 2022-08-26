namespace Thomsen.ServiceTemplate.Observer {
    internal record class ServiceObserverSettings {
        public string ServiceName { get; set; } = "Thomsen ServiceTemplate";

        public string ServiceExecutablePath { get; set; } = "./Service.exe";

        public string ServiceLogPath { get; set; } = "./Log/Service.log";

        internal string[] ToArgs() {
            return new string[] {
                "-n",
                $"\"{ServiceName}\"",
                "-e",
                $"\"{ServiceExecutablePath}\"",
                "-l",
                $"\"{ServiceLogPath}\"",
            };
        }

        internal static ServiceObserverSettings FromArgs(string[] args) {
            ServiceObserverSettings settings = new();

            if (TryGetParamIdx(args, "-n", "--name", out string name)) {
                settings.ServiceName = name;
            }

            if (TryGetParamIdx(args, "-e", "--executable", out string path)) {
                settings.ServiceExecutablePath = path;
            }

            if (TryGetParamIdx(args, "-l", "--log", out string log)) {
                settings.ServiceLogPath = log;
            }

            return settings;
        }

        private static bool TryGetParamIdx(string[] args, string shortFlag, string longFlag, out string value) {
            value = "";

            int idx = Array.IndexOf(args, shortFlag);
            idx = idx == -1 ? Array.IndexOf(args, longFlag) : idx;

            if (args.Length > idx + 1) {
                value = args[idx + 1];
                return true;
            }

            return false;
        }
    }
}