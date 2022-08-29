using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Input;

namespace Thomsen.ServiceTemplate.Observer.ViewModels;

internal class MainWindowViewModel : BaseViewModel, IDisposable {
    private bool _isAdminMode = false;

    private ServiceState _serviceState;

    private bool _isServiceInstalled = false;
    private bool _isServiceRunning = false;
    private bool _isStandaloneRunning = false;
    private bool _isObservingLog = false;

    private bool _isLogScrollingEnabled = true;
    private ObservableCollection<LogLine> _logText = new();
    private long _observingLogUnchangedSeconds = 0;

    private Process? _standaloneProcess;
    private Thread? _observingLogFileThread;
    private CancellationTokenSource? _observingLogFileCts;

    private ICommand? _startServiceCmd;
    private ICommand? _stopServiceCmd;
    private ICommand? _installServiceCmd;
    private ICommand? _uninstallServiceCmd;
    private ICommand? _startStandaloneCmd;
    private ICommand? _stopStandaloneCmd;
    private ICommand? _startObservingLogCmd;
    private ICommand? _stopObservingLogCmd;
    private ICommand? _clearLogCmd;
    private ICommand? _restartAsAdminCmd;
    private ICommand? _forceRefreshCmd;

    public ICommand StartServiceCmd => _startServiceCmd ??=
        new CommandHandler(async param => await StartServiceAsync(), () => IsAdminMode && IsServiceInstalled && !IsServiceRunning && !IsStandaloneRunning && Settings.ServiceName is not null);

    public ICommand StopServiceCmd => _stopServiceCmd ??=
        new CommandHandler(async param => await StopServiceAsync(), () => IsAdminMode && IsServiceInstalled && IsServiceRunning && Settings.ServiceName is not null);

    public ICommand InstallServiceCmd => _installServiceCmd ??=
        new CommandHandler(async param => await InstallServiceAsync(), () => IsAdminMode && !IsServiceInstalled && !IsServiceRunning && !IsStandaloneRunning && Settings.ServiceName is not null);

    public ICommand UninstallServiceCmd => _uninstallServiceCmd ??=
        new CommandHandler(async param => await UninstallServiceAsync(), () => IsAdminMode && IsServiceInstalled && !IsServiceRunning && !IsStandaloneRunning && Settings.ServiceName is not null);

    public ICommand StartStandaloneCmd => _startStandaloneCmd ??=
        new CommandHandler(param => StartStandalone(), () => !IsServiceRunning && !IsStandaloneRunning && Settings.ServiceExecutablePath is not null);

    public ICommand StopStandaloneCmd => _stopStandaloneCmd ??=
        new CommandHandler(async param => await StopStandaloneAsync(), () => IsStandaloneRunning);

    public ICommand StartObservingLogCmd => _startObservingLogCmd ??=
        new CommandHandler(param => StartObservingLog(), () => !IsObservingLog && Settings.ServiceLogPath is not null);

    public ICommand StopObservingLogCmd => _stopObservingLogCmd ??=
        new CommandHandler(param => StopObservingLog(), () => IsObservingLog);

    public ICommand ClearLogCmd => _clearLogCmd ??=
        new CommandHandler(param => LogText.Clear(), () => LogText.Count > 0);

    public ICommand RestartAsAdminCmd => _restartAsAdminCmd ??=
        new CommandHandler(async param => await RestartAsAdminAsync(), () => !IsAdminMode);

    public ICommand ForceRefreshCmd => _forceRefreshCmd ??=
        new CommandHandler(async param => await RunActionAndHandleExceptionAsync(async () => await RefreshServiceStateAsync()), () => Settings.ServiceName is not null);

    public ServiceObserverSettings Settings { get; }

    public string WindowTitle => $"{nameof(Observer)}: {Settings.ServiceName ?? Path.GetFileName(Settings.ServiceLogPath)}{(IsAdminMode ? " (Administrator)" : "")}";

    public bool IsAdminMode { get => _isAdminMode; set => SetProperty(ref _isAdminMode, value); }

    public ServiceState ServiceState { get => _serviceState; set => SetProperty(ref _serviceState, value); }

    public bool IsServiceInstalled { get => _isServiceInstalled; set => SetProperty(ref _isServiceInstalled, value); }

    public bool IsServiceRunning { get => _isServiceRunning; set => SetProperty(ref _isServiceRunning, value); }

    public bool IsStandaloneRunning { get => _isStandaloneRunning; set => SetProperty(ref _isStandaloneRunning, value); }

    public bool IsObservingLog { get => _isObservingLog; set => SetProperty(ref _isObservingLog, value); }

    public bool IsLogScrollingEnabled { get => _isLogScrollingEnabled; set => SetProperty(ref _isLogScrollingEnabled, value); }

    public long ObservingLogUnchangedSeconds { get => _observingLogUnchangedSeconds; set => SetProperty(ref _observingLogUnchangedSeconds, value); }

    public ObservableCollection<LogLine> LogText { get => _logText; set => SetProperty(ref _logText, value); }

    public MainWindowViewModel(ServiceObserverSettings settings) {
        WindowsPrincipal wp = new(WindowsIdentity.GetCurrent());
        IsAdminMode = wp.IsInRole(WindowsBuiltInRole.Administrator);

        Settings = settings;
    }

    public async Task LoadAsync() {
        using WaitCursor _ = new();

        await RefreshServiceStateAsync();

        StartObservingLog();
    }

    private void StartObservingLog() {
        if (!File.Exists(Settings.ServiceLogPath)) {
            return;
        }

        if (IsObservingLog || Settings.ServiceLogPath is null) {
            return;
        }

        _observingLogFileCts = new CancellationTokenSource();

        _observingLogFileThread = new(async () => {
            await ReadLogFileStreamAsync(_observingLogFileCts.Token);
        });

        _observingLogFileThread.Start();
    }

    private void StopObservingLog() {
        if (!IsObservingLog) {
            return;
        }

        _observingLogFileCts!.Cancel();
        _observingLogFileThread!.Join();

        _observingLogFileCts.Dispose();
        _observingLogFileCts = null;

        _observingLogFileThread = null;
    }

    private async Task ReadLogFileStreamAsync(CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(Settings.ServiceLogPath);

        IsObservingLog = true;

        try {
            using FileStream stream = File.Open(Settings.ServiceLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

            try {
                stream.Seek(-2048, SeekOrigin.End);
            } catch (IOException) {
                stream.Seek(0, SeekOrigin.Begin);
            }

            using StreamReader reader = new(stream);

            Stopwatch? stopwatch = null;

            while (!cancellationToken.IsCancellationRequested) {
                string? line = await reader.ReadLineAsync();

                while (line is not null) {
                    Application.Current.Dispatcher.Invoke(() => {
                        LogText.Add(new LogLine(line));
                    });

                    if (stopwatch is not null) {
                        stopwatch.Stop();
                        stopwatch = null;
                    }

                    ObservingLogUnchangedSeconds = 0;

                    line = await reader.ReadLineAsync();
                }

                stopwatch ??= Stopwatch.StartNew();

                ObservingLogUnchangedSeconds = stopwatch.ElapsedMilliseconds / 1000;

                await Task.Delay(1000, cancellationToken);
            }
        } catch (OperationCanceledException) { }

        IsObservingLog = false;
    }

    private async Task StartServiceAsync() {
        await RunActionAndHandleExceptionAsync(async () => {
            ArgumentNullException.ThrowIfNull(Settings.ServiceName);

            await WindowsServiceManager.StartServiceAsync(Settings.ServiceName);

            await Task.Delay(3000);

            await RefreshServiceStateAsync();

            StartObservingLog();
        });
    }

    private async Task StopServiceAsync() {
        await RunActionAndHandleExceptionAsync(async () => {
            ArgumentNullException.ThrowIfNull(Settings.ServiceName);

            await WindowsServiceManager.StopServiceAsync(Settings.ServiceName);

            await Task.Delay(3000);

            await RefreshServiceStateAsync();

            StopObservingLog();
        });
    }

    private async Task InstallServiceAsync() {
        await RunActionAndHandleExceptionAsync(async () => {
            ArgumentNullException.ThrowIfNull(Settings.ServiceExecutablePath);
            ArgumentNullException.ThrowIfNull(Settings.ServiceName);

            string executableFullPath = Path.Combine(Directory.GetCurrentDirectory(), Settings.ServiceExecutablePath);
            await WindowsServiceManager.InstallServiceAsync(Settings.ServiceName, executableFullPath);
            await RefreshServiceStateAsync();
        });
    }

    private async Task UninstallServiceAsync() {
        await RunActionAndHandleExceptionAsync(async () => {
            ArgumentNullException.ThrowIfNull(Settings.ServiceName);

            await WindowsServiceManager.UninstallServiceAsync(Settings.ServiceName);
            await RefreshServiceStateAsync();
        });
    }

    private void StartStandalone() {
        RunActionAndHandleException(() => {
            ArgumentNullException.ThrowIfNull(Settings.ServiceExecutablePath);

            _standaloneProcess = Process.Start(Settings.ServiceExecutablePath, Settings.ServiceExecutableStandaloneArgs ?? "");

            IsStandaloneRunning = true;

            StartObservingLog();
        });
    }

    private async Task StopStandaloneAsync() {
        await RunActionAndHandleExceptionAsync(async () => {
            if (_standaloneProcess is not null) {
                _standaloneProcess.Kill();

                await _standaloneProcess.WaitForExitAsync();

                _standaloneProcess.Dispose();
                _standaloneProcess = null;
            }

            IsStandaloneRunning = false;

            StopObservingLog();
        });
    }

    private async Task RestartAsAdminAsync() {
        await RunActionAndHandleExceptionAsync(async () => {
            await StopStandaloneAsync();

            Process.Start(new ProcessStartInfo() {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = Environment.ProcessPath!,
                Arguments = string.Join(" ", Settings.ToArgs()),
                Verb = "runas",
            });

            Environment.Exit(0);
        });
    }

    private async Task RefreshServiceStateAsync() {
        if (Settings.ServiceName is null) {
            return;
        }

        try {
            ServiceState = await WindowsServiceManager.GetServiceStateAsync(Settings.ServiceName);

            switch (ServiceState) {
                case ServiceState.Running:
                case ServiceState.StartPending:
                    IsServiceInstalled = true;
                    IsServiceRunning = true;
                    break;
                case ServiceState.StopPending:
                case ServiceState.Stopped:
                    IsServiceInstalled = true;
                    IsServiceRunning = false;
                    break;
            }
        } catch (InvalidOperationException) {
            IsServiceInstalled = false;
            IsServiceRunning = false;
        }

        // Force refresh on CanExecutes of all commands
        CommandManager.InvalidateRequerySuggested();
    }

    private static async Task RunActionAndHandleExceptionAsync(Func<Task> action, [CallerMemberName] string name = "") {
        using WaitCursor _ = new();

        try {
            await action();
        } catch (Exception ex) {
            MessageBox.Show($"{name}:\n\n{ex.GetAllMessages()}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void RunActionAndHandleException(Action action, [CallerMemberName] string name = "") {
        using WaitCursor _ = new();

        try {
            action();
        } catch (Exception ex) {
            MessageBox.Show($"{name}:\n\n{ex.GetAllMessages()}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public void Dispose() {
        if (_standaloneProcess is not null) {
            _standaloneProcess.Kill();
            _standaloneProcess.Dispose();
        }
    }
}
