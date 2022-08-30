using System.Diagnostics;
using System.IO;

namespace Thomsen.ServiceTemplate.Observer;

internal enum ServiceState {
    Stopped,
    StopPending,
    StartPending,
    Running
}

internal static class WindowsServiceManager {
    public static async Task InstallServiceAsync(string serviceName, string serviceBinaryPath, bool autoStart = false) {
        // #TODO: Support service settings
        await RunScProcessAsync($"create \"{serviceName}\" binpath= \"{serviceBinaryPath}\"{(autoStart ? " start= auto" : "")}");
    }

    public static async Task UninstallServiceAsync(string serviceName) {
        await RunScProcessAsync($"delete \"{serviceName}\"");
    }

    public static async Task<ServiceState> StartServiceAsync(string serviceName) {
        return await RunScProcessAndParseStateFromStandardOutputAsync($"start \"{serviceName}\"");
    }

    public static async Task<ServiceState> StopServiceAsync(string serviceName) {
        return await RunScProcessAndParseStateFromStandardOutputAsync($"stop \"{serviceName}\"");
    }

    public static async Task<ServiceState> GetServiceStateAsync(string serviceName) {
        return await RunScProcessAndParseStateFromStandardOutputAsync($"query \"{serviceName}\"");
    }

    private static async Task<ServiceState> RunScProcessAndParseStateFromStandardOutputAsync(string arguments) {
        IAsyncEnumerable<string> result = RunProcessAndGetStandardOutputAsync("sc.exe", arguments);

        return await ParseStateAsync(result);
    }

    private static async Task RunScProcessAsync(string arguments) {
        _ = await RunProcessAsync("sc.exe", arguments);
    }

    private static async Task<ServiceState> ParseStateAsync(IAsyncEnumerable<string> result) {
        await foreach (string line in result) {
            if (line.Contains("STATE")) {
                string state = line.Split(' ')
                    .Where(part => !string.IsNullOrWhiteSpace(part))
                    .Last().Trim().ToLower();

                return state switch {
                    "stopped" => ServiceState.Stopped,
                    "stop_pending" => ServiceState.StopPending,
                    "start_pending" => ServiceState.StartPending,
                    "running" => ServiceState.Running,
                    _ => throw new InvalidOperationException()
                };
            }
        }

        throw new InvalidOperationException("Parsing failed unexpected");
    }

    private static async IAsyncEnumerable<string> RunProcessAndGetStandardOutputAsync(string fileName, string arguments) {
        Process? process = await RunProcessAsync(fileName, arguments);

        using StreamReader reader = process.StandardOutput;

        string? line = await reader.ReadLineAsync();
        while (line is not null) {
            yield return line;
            line = await reader.ReadLineAsync();
        }
    }

    private static async Task<Process> RunProcessAsync(string fileName, string arguments) {
        Process? process = Process.Start(new ProcessStartInfo() {
            FileName = fileName,
            Arguments = arguments,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
        })!;

        await process.WaitForExitAsync();

        if (process.ExitCode != 0) {
            using StreamReader reader = process.StandardOutput;

            // #TODO: Handle this differently
            throw new WindowsServiceManagerException(await reader.ReadToEndAsync(), process.ExitCode);
        }

        return process;
    }
}
