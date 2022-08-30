using System.IO;
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
    /// -e, --executable: "/path/to/executable.exe -a -f"
    /// -l, --log: "/path/to/log.log"
    /// </summary>
    /// <param name="e"></param>

    private const string CONFIG_FILE_NAME = "Services.xml";

    private MainWindowView? _view;
    private MainWindowViewModel? _viewModel;

    protected override void OnStartup(StartupEventArgs e) {
        base.OnStartup(e);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        //ServiceObserverSettings.GenerateTestDefaultConfigFile(CONFIG_FILE_NAME);

        if (e.Args.Length > 0) {
            ServiceObserverSettings settings = ServiceObserverSettings.FromArgs(e.Args);
            _viewModel = new MainWindowViewModel(settings);
        }

        if (File.Exists(CONFIG_FILE_NAME)) {
            ServiceObserverSettings[] settingsSets = ServiceObserverSettings.FromConfigFile(CONFIG_FILE_NAME);
            _viewModel = new MainWindowViewModel(settingsSets);
        }

        if (_viewModel is null) {
            MessageBox.Show($"No settings; neither in args nor in {CONFIG_FILE_NAME}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        _view = new MainWindowView {
            DataContext = _viewModel
        };

        _view.Loaded += async (sender, e) => {
            await _viewModel.LoadAsync();
        };

        _view.Closing += (sender, e) => {
            _viewModel.Dispose();
        };

        MainWindow = _view;
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
