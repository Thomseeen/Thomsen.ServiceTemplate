using System.IO;
using System.Xml.Serialization;

namespace Thomsen.ServiceTemplate.Observer.Models;

public record class ServiceObserverSettings {
    [XmlAttribute]
    public string? ServiceName { get; set; }

    [XmlAttribute]
    public string? ServiceExecutablePath { get; set; }

    [XmlAttribute]
    public string? ServiceExecutableStandaloneArgs { get; set; }

    [XmlAttribute]
    public string? ServiceLogPath { get; set; }

    public override string? ToString() {
        return ServiceName ?? Path.GetFileName(ServiceLogPath) ?? base.ToString();
    }

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

    internal static ServiceObserverSettings[] FromConfigFile(string filePath) {
        XmlSerializer serializer = new(typeof(ServiceObserverSettings[]));
        using FileStream stream = File.OpenRead(filePath);

        return (ServiceObserverSettings[])(serializer.Deserialize(stream) ?? throw new InvalidOperationException("Can't deserialize"));
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

    public static void GenerateTestDefaultConfigFile(string filePath) {
        ServiceObserverSettings[] settingsSets = new ServiceObserverSettings[] {
            new ServiceObserverSettings() {
                ServiceName = "Thomsen ServiceTemplate",
                ServiceExecutablePath = "./Service.exe",
                ServiceLogPath = "./Log/Service.log"
            },
            new ServiceObserverSettings() {
                ServiceLogPath = "C:/MahaPA/MPAS/Bin_Test/Log/MPASPorscheReportLib.log"
            }
        };

        XmlSerializer serializer = new(typeof(ServiceObserverSettings[]));
        using FileStream stream = File.OpenWrite(filePath);

        serializer.Serialize(stream, settingsSets);
    }
}