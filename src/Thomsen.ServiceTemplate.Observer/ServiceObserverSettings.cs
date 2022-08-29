namespace Thomsen.ServiceTemplate.Observer;

internal record class ServiceObserverSettings {
    public string? ServiceName { get; set; }

    public string? ServiceExecutablePath { get; set; }

    public string? ServiceExecutableStandaloneArgs { get; set; }

    public string? ServiceLogPath { get; set; }

    internal string[] ToArgs() {
        List<string> args = new();

        if (ServiceName is not null) {
            args.Add("-n");
            args.Add($"\"{ServiceName}\"");
        }

        if (ServiceExecutablePath is not null) {
            args.Add("-e");
            args.Add($"\"{ServiceExecutablePath}{(ServiceExecutableStandaloneArgs is not null ? $" {ServiceExecutableStandaloneArgs}" : "")}\"");
        }

        if (ServiceLogPath is not null) {
            args.Add("-l");
            args.Add($"\"{ServiceLogPath}\"");
        }

        return args.ToArray();
    }

    internal static ServiceObserverSettings FromArgs(string[] args) {
        ServiceObserverSettings settings = new();

        if (TryGetParam(args, "-n", "--name", out string name)) {
            settings.ServiceName = name;
        }

        if (TryGetParam(args, "-e", "--executable", out string path)) {
            settings.ServiceExecutablePath = GetExecutablePathAndArgs(path, out string? standaloneArgs);
            settings.ServiceExecutableStandaloneArgs = standaloneArgs;
        }

        if (TryGetParam(args, "-l", "--log", out string log)) {
            settings.ServiceLogPath = log;
        }

        return settings;
    }

    private static string GetExecutablePathAndArgs(string path, out string? args) {
        const char separator = ' ';

        string[] parts = path.Split(separator);

        args = parts.Length > 1
            ? string.Join(separator, parts.Skip(1).ToArray())
            : null;

        return parts[0];
    }

    private static bool TryGetParam(string[] args, string shortFlag, string longFlag, out string value) {
        value = "";

        int idx = Array.IndexOf(args, shortFlag);
        idx = idx == -1 ? Array.IndexOf(args, longFlag) : idx;

        if (idx != -1 && args.Length > idx + 1) {
            value = args[idx + 1];
            return true;
        }

        return false;
    }
}