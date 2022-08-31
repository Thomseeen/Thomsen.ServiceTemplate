using Hardcodet.Wpf.TaskbarNotification;

using System.IO;
using System.Runtime;
using System.Windows;
using System.Windows.Threading;

using Thomsen.ServiceTemplate.Observer.Models;
using Thomsen.ServiceTemplate.Observer.ViewModels;
using Thomsen.ServiceTemplate.Observer.Views;

namespace Thomsen.ServiceTemplate.Observer;

/// <summary>
/// Interaction logic for ObserverApp.xaml
/// </summary>
public partial class ObserverApp : Application {
    /// <summary>
    /// Params:
    /// -n, --name: "ServiceName"
    /// -e, --executable: "/path/to/executable.exe -a -f"
    /// -l, --log: "/path/to/log.log"
    /// -a, --admin: Admin Mode
    /// -m, --minimized: Minimized to tray
    /// </summary>
    /// <param name="e"></param>

    private const string CONFIG_FILE_NAME = "Services.xml";

    private ServiceObserverSettings[]? _settings;
    private NotifyIconView? _notifyIconView;
    private NotifyIconViewModel? _notifyIconViewModel;

    private MainWindowView? _mainWindowView;
    private MainWindowViewModel? _mainWindowViewModel;
    internal bool IsMainWindowShown { get; set; } = false;

    internal void SetupAndShowMainWindow(ServiceObserverSettings? settings = null) {
        ArgumentNullException.ThrowIfNull(_mainWindowViewModel);

        if (settings is not null) {
            _mainWindowViewModel.SelectedSettings = settings;
        }

        _mainWindowView = new MainWindowView {
            DataContext = _mainWindowViewModel
        };

        _mainWindowView.Loaded += async (sender, e) => {
            await _mainWindowViewModel.LoadAsync();
            IsMainWindowShown = true;
        };

        _mainWindowView.Closing += (sender, e) => {
            IsMainWindowShown = false;
        };

        MainWindow = _mainWindowView;

        MainWindow.Show();
        MainWindow.Focus();
    }

    internal void CloseMainWindow() {
        MainWindow.Close();
        MainWindow = null;
    }

    protected override async void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);

        SetupUnhandeledExceptionHandler();

        //ServiceObserverSettings.GenerateTestDefaultConfigFile(CONFIG_FILE_NAME);
        _settings = LoadSettings(e);

        _notifyIconViewModel = new NotifyIconViewModel(_settings);
        _mainWindowViewModel = new MainWindowViewModel(_settings);

        await SetupNotifyIconAsync();

        if (e.Args.Contains("-a") || e.Args.Contains("--admin")) {
            // #TODO: Stat in Admin Mode
        }

        if (!(e.Args.Contains("-m") || e.Args.Contains("--minimized"))) {
            SetupAndShowMainWindow();
        }
    }

    private async Task SetupNotifyIconAsync() {
        ArgumentNullException.ThrowIfNull(_notifyIconViewModel);

        await _notifyIconViewModel.LoadAsync();

        _notifyIconView = new NotifyIconView() {
            DataContext = _notifyIconViewModel
        };
    }

    protected override void OnExit(ExitEventArgs e) {
        _mainWindowViewModel?.Dispose();

        base.OnExit(e);
    }

    private static ServiceObserverSettings[] LoadSettings(StartupEventArgs e) {
        return File.Exists(CONFIG_FILE_NAME)
            ? ServiceObserverSettings.FromConfigFile(CONFIG_FILE_NAME)
            : new[] { ServiceObserverSettings.FromArgs(e.Args) };
    }

    private void SetupUnhandeledExceptionHandler() {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;
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
