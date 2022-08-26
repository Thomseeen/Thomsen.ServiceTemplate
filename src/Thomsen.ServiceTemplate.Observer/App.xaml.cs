using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using Thomsen.ServiceTemplate.Observer.ViewModels;
using Thomsen.ServiceTemplate.Observer.Views;

namespace Thomsen.ServiceTemplate.Observer;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application {
    /// <summary>
    /// Params:
    /// -n, --name: "ServiceName"
    /// -e, --executable: "/path/to/executable.exe"
    /// -l, --log: "/path/to/log.log"
    /// </summary>
    /// <param name="e"></param>


    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        ServiceObserverSettings settings = ServiceObserverSettings.FromArgs(e.Args);

        MainWindow = new MainWindowView {
            DataContext = new MainWindowViewModel(settings)
        };

        MainWindow.Loaded += async (sender, e) => {
            MainWindowViewModel? vm = MainWindow.DataContext as MainWindowViewModel;
            if (vm is not null) {
                await vm.LoadAsync();
            }
        };

        MainWindow.Closing += (sender, e) => {
            MainWindowViewModel? vm = MainWindow.DataContext as MainWindowViewModel;
            if (vm is not null) {
                vm.Dispose();
            }
        };

        MainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e) {
        base.OnExit(e);
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e) {
        WriteCrashDumpFile("App", e.Exception);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e) {
        WriteCrashDumpFile("CurrentDomain", (Exception)e.ExceptionObject);
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e) {
        WriteCrashDumpFile("TaskScheduler", e.Exception);
    }

    private static void WriteCrashDumpFile(string facility, Exception ex) {
        string path = $"{facility}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.dmp";

        using StreamWriter writer = new(path);

        writer.WriteLine($"--- Fatal error in {facility} ---");
        writer.WriteLine($"Time local: {DateTime.Now}");
        writer.WriteLine($"Time UTC: {DateTime.UtcNow}\r\n");
        writer.WriteLine($"Message: {ex.GetAllMessages()}");
        writer.WriteLine($"Source: {ex.Source}");
        writer.WriteLine($"TargetSite: {ex.TargetSite}");
        writer.WriteLine($"Stack Trace:");
        writer.WriteLine($"---");
        writer.Write($"{ex.StackTrace}");
        writer.WriteLine($"---\r\n");
        writer.WriteLine($"Full Exception:");
        writer.WriteLine($"---");
        writer.WriteLine($"{ex}");
        writer.WriteLine($"---");

        writer.Flush();
        writer.Close();
    }
}
