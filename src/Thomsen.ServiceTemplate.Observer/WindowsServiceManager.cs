using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Thomsen.ServiceTemplate.Observer;

internal enum ServiceState {
    Stopped,
    StopPending,
    StartPending,
    Running
}

internal class WindowsServiceManager {

    public static async Task InstallServiceAsync(string serviceName, string serviceBinaryPath) {
        await RunScProcessAndThrowIfNotSuccessFromStandardOutputAsync($"create \"{serviceName}\" binpath=\"{serviceBinaryPath}\"");
    }

    public static async Task UninstallServiceAsync(string serviceName) {
        await RunScProcessAndThrowIfNotSuccessFromStandardOutputAsync($"delete \"{serviceName}\"");
    }

    public static async Task<ServiceState> StartServiceAsync(string serviceName) {
        return await RunScProcessAndParseStareFromStandardOutputAsync($"start \"{serviceName}\"");
    }

    public static async Task<ServiceState> StopServiceAsync(string serviceName) {
        return await RunScProcessAndParseStareFromStandardOutputAsync($"stop \"{serviceName}\"");
    }

    public static async Task<ServiceState> GetServiceStateAsync(string serviceName) {
        return await RunScProcessAndParseStareFromStandardOutputAsync($"query \"{serviceName}\"");
    }

    private static async Task<ServiceState> RunScProcessAndParseStareFromStandardOutputAsync(string arguments) {
        IAsyncEnumerable<string> result = RunProcessAndGetStandardOutputAsync("sc.exe", arguments);

        return await ParseStateFromStandardOutputAsync(result);
    }

    private static async Task RunScProcessAndThrowIfNotSuccessFromStandardOutputAsync(string arguments) {
        IAsyncEnumerable<string> result = RunProcessAndGetStandardOutputAsync("sc.exe", arguments);

        await ThrowIfNotSuccessFromStandardOutputAsync(result);
    }

    private static async Task ThrowIfNotSuccessFromStandardOutputAsync(IAsyncEnumerable<string> result) {
        List<string> lines = new();

        await foreach (string line in result) {
            if (line.ToLower().Contains("success")) {
                return;
            }

            lines.Add(line);
        }

        // Reconstruct full message for exception when parsing failed
        throw new InvalidDataException(string.Join(Environment.NewLine, lines));
    }

    private static async Task<ServiceState> ParseStateFromStandardOutputAsync(IAsyncEnumerable<string> result) {
        List<string> lines = new();

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
                    _ => throw new InvalidDataException()
                };
            }

            lines.Add(line);
        }

        // Reconstruct full message for exception when parsing failed
        throw new InvalidDataException(string.Join(Environment.NewLine, lines));
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

        return process;
    }
}
