using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

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
        new CommandHandler(async param => await StartServiceAsync(), () => IsAdminMode && IsServiceInstalled && !IsServiceRunning && !IsStandaloneRunning);

    public ICommand StopServiceCmd => _stopServiceCmd ??=
        new CommandHandler(async param => await StopServiceAsync(), () => IsAdminMode && IsServiceInstalled && IsServiceRunning);

    public ICommand InstallServiceCmd => _installServiceCmd ??=
        new CommandHandler(async param => await InstallServiceAsync(), () => IsAdminMode && !IsServiceInstalled && !IsServiceRunning && !IsStandaloneRunning);

    public ICommand UninstallServiceCmd => _uninstallServiceCmd ??=
        new CommandHandler(async param => await UninstallServiceAsync(), () => IsAdminMode && IsServiceInstalled && !IsServiceRunning && !IsStandaloneRunning);

    public ICommand StartStandaloneCmd => _startStandaloneCmd ??=
        new CommandHandler(param => StartStandalone(), () => !IsServiceRunning && !IsStandaloneRunning);

    public ICommand StopStandaloneCmd => _stopStandaloneCmd ??=
        new CommandHandler(async param => await StopStandaloneAsync(), () => IsStandaloneRunning);

    public ICommand StartObservingLogCmd => _startObservingLogCmd ??=
        new CommandHandler(param => StartObservingLog(), () => !IsObservingLog);

    public ICommand StopObservingLogCmd => _stopObservingLogCmd ??=
        new CommandHandler(param => StopObservingLog(), () => IsObservingLog);

    public ICommand ClearLogCmd => _clearLogCmd ??=
        new CommandHandler(param => LogText.Clear(), () => LogText.Count > 0);

    public ICommand RestartAsAdminCmd => _restartAsAdminCmd ??=
        new CommandHandler(async param => await RestartAsAdminAsync(), () => !IsAdminMode);

    public ICommand ForceRefreshCmd => _forceRefreshCmd ??=
        new CommandHandler(async param => await RunActionAndHandleExceptionAsync(async () => await RefreshServiceStateAsync()), () => true);

    public ServiceObserverSettings Settings { get; }

    public string WindowTitle => $"{nameof(Observer)}: {Settings.ServiceName}{(IsAdminMode ? " (Administrator)" : "")}";

    public bool IsAdminMode { get => _isAdminMode; set => SetProperty(ref _isAdminMode, value); }

    public ServiceState ServiceState { get => _serviceState; set => SetProperty(ref _serviceState, value); }

    public bool IsServiceInstalled { get => _isServiceInstalled; set => SetProperty(ref _isServiceInstalled, value); }

    public bool IsServiceRunning { get => _isServiceRunning; set => SetProperty(ref _isServiceRunning, value); }

    public bool IsStandaloneRunning { get => _isStandaloneRunning; set => SetProperty(ref _isStandaloneRunning, value); }

    public bool IsObservingLog { get => _isObservingLog; set => SetProperty(ref _isObservingLog, value); }

    public bool IsLogScrollingEnabled { get => _isLogScrollingEnabled; set => SetProperty(ref _isLogScrollingEnabled, value); }

    public long ObservingLogUnchangedSeconds { get => _observingLogUnchangedSeconds; set => SetProperty(ref _observingLogUnchangedSeconds, value); }

    public ObservableCollection<LogLine> LogText { get => _logText; set => SetProperty(ref _logText, value); }

    public string Test => Process.GetCurrentProcess().MainModule!.ModuleName!;

    public string Test2 => AppContext.BaseDirectory;

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

        if (IsObservingLog) {
            return;
        }

        _observingLogFileCts = new CancellationTokenSource();

        _observingLogFileThread = new(async () => {
            await ReadLogFileStreamAsync(_observingLogFileCts.Token);
        });

        _observingLogFileThread.Start();

        IsObservingLog = true;
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

        IsObservingLog = false;
    }

    private async Task ReadLogFileStreamAsync(CancellationToken cancellationToken) {
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
            await WindowsServiceManager.StartServiceAsync(Settings.ServiceName);
            await RefreshServiceStateAsync();

            StartObservingLog();
        });
    }

    private async Task StopServiceAsync() {
        await RunActionAndHandleExceptionAsync(async () => {
            await WindowsServiceManager.StopServiceAsync(Settings.ServiceName);
            await RefreshServiceStateAsync();

            StopObservingLog();
        });
    }

    private async Task InstallServiceAsync() {
        await RunActionAndHandleExceptionAsync(async () => {
            string executableFullPath = Path.Combine(Directory.GetCurrentDirectory(), Settings.ServiceExecutablePath);
            await WindowsServiceManager.InstallServiceAsync(Settings.ServiceName, executableFullPath);
            await RefreshServiceStateAsync();
        });
    }

    private async Task UninstallServiceAsync() {
        await RunActionAndHandleExceptionAsync(async () => {
            await WindowsServiceManager.UninstallServiceAsync(Settings.ServiceName);
            await RefreshServiceStateAsync();
        });
    }

    private void StartStandalone() {
        RunActionAndHandleException(() => {
            _standaloneProcess = Process.Start(Settings.ServiceExecutablePath);

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
                FileName = Process.GetCurrentProcess().MainModule!.FileName!,
                Arguments = string.Join(" ", Settings.ToArgs()),
                Verb = "runas",
            });

            Environment.Exit(0);
        });
    }

    private async Task RefreshServiceStateAsync() {
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
        } catch (InvalidDataException) {
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
