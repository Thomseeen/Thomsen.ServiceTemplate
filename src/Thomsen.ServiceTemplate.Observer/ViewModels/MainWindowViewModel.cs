using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Windows;
using System.Windows.Input;

using Thomsen.ServiceTemplate.Observer.Models;
using Thomsen.ServiceTemplate.Observer.Mvvm;

namespace Thomsen.ServiceTemplate.Observer.ViewModels;

internal class MainWindowViewModel : BaseViewModel, IDisposable {
    private bool _isAdminMode = false;
    private readonly ServiceObserverSettings[] _availableSettingsSets;

    private ServiceState _serviceState;
    private ServiceObserverSettings _loadedSettings;
    private ServiceObserverSettings _selectedSettings;

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
        new CommandHandler(async param => await StartServiceAsync(), () =>
            IsAdminMode &&
            IsServiceInstalled &&
            !IsServiceRunning &&
            !IsStandaloneRunning &&
            LoadedSettings.ServiceName is not null);

    public ICommand StopServiceCmd => _stopServiceCmd ??=
        new CommandHandler(async param => await StopServiceAsync(), () =>
            IsAdminMode &&
            IsServiceInstalled &&
            IsServiceRunning &&
            LoadedSettings.ServiceName is not null);

    public ICommand InstallServiceCmd => _installServiceCmd ??=
        new CommandHandler(async param => await InstallServiceAsync(), () =>
            IsAdminMode &&
            !IsServiceInstalled &&
            !IsServiceRunning &&
            LoadedSettings.ServiceName is not null &&
            LoadedSettings.ServiceExecutablePath is not null);

    public ICommand UninstallServiceCmd => _uninstallServiceCmd ??=
        new CommandHandler(async param => await UninstallServiceAsync(), () =>
            IsAdminMode &&
            IsServiceInstalled &&
            !IsServiceRunning &&
            LoadedSettings.ServiceName is not null);

    public ICommand StartStandaloneCmd => _startStandaloneCmd ??=
        new CommandHandler(param => StartStandalone(), () =>
            !IsServiceRunning &&
            !IsStandaloneRunning &&
            LoadedSettings.ServiceExecutablePath is not null);

    public ICommand StopStandaloneCmd => _stopStandaloneCmd ??=
        new CommandHandler(async param => await StopStandaloneAsync(), () => IsStandaloneRunning);

    public ICommand StartObservingLogCmd => _startObservingLogCmd ??=
        new CommandHandler(param => StartObservingLog(), () => !IsObservingLog && LoadedSettings.ServiceLogPath is not null);

    public ICommand StopObservingLogCmd => _stopObservingLogCmd ??=
        new CommandHandler(param => StopObservingLog(), () => IsObservingLog);

    public ICommand ClearLogCmd => _clearLogCmd ??=
        new CommandHandler(param => LogText.Clear(), () => LogText.Count > 0);

    public ICommand RestartAsAdminCmd => _restartAsAdminCmd ??=
        new CommandHandler(async param => await RestartAsAdminAsync(), () => !IsAdminMode);

    public ICommand ForceRefreshCmd => _forceRefreshCmd ??=
        new CommandHandler(async param => await RunActionAndHandleExceptionAsync(async () => await RefreshServiceStateAsync()), () => LoadedSettings.ServiceName is not null);

    public ServiceObserverSettings[] AvailableSettingsSets { get => _availableSettingsSets; }

    public ServiceObserverSettings LoadedSettings { get => _loadedSettings; set => SetProperty(ref _loadedSettings, value); }

    public ServiceObserverSettings SelectedSettings { get => _selectedSettings; set => SetProperty(ref _selectedSettings, value); }

    public string WindowTitle => $"{nameof(Observer)}: {LoadedSettings.ServiceName ?? Path.GetFileName(LoadedSettings.ServiceLogPath)}{(IsAdminMode ? " (Administrator)" : "")}";

    public bool IsAdminMode { get => _isAdminMode; set => SetProperty(ref _isAdminMode, value); }

    public ServiceState ServiceState { get => _serviceState; set => SetProperty(ref _serviceState, value); }

    public bool IsServiceInstalled { get => _isServiceInstalled; set => SetProperty(ref _isServiceInstalled, value); }

    public bool IsServiceRunning { get => _isServiceRunning; set => SetProperty(ref _isServiceRunning, value); }

    public bool IsStandaloneRunning { get => _isStandaloneRunning; set => SetProperty(ref _isStandaloneRunning, value); }

    public bool IsObservingLog { get => _isObservingLog; set => SetProperty(ref _isObservingLog, value); }

    public bool IsLogScrollingEnabled { get => _isLogScrollingEnabled; set => SetProperty(ref _isLogScrollingEnabled, value); }

    public long ObservingLogUnchangedSeconds { get => _observingLogUnchangedSeconds; set => SetProperty(ref _observingLogUnchangedSeconds, value); }

    public ObservableCollection<LogLine> LogText { get => _logText; set => SetProperty(ref _logText, value); }

    public MainWindowViewModel(params ServiceObserverSettings[] settings) {
        if (settings.Length == 0) {
            throw new ArgumentException("Is empty", nameof(settings));
        }

        WindowsPrincipal wp = new(WindowsIdentity.GetCurrent());
        IsAdminMode = wp.IsInRole(WindowsBuiltInRole.Administrator);

        _availableSettingsSets = settings;
        _selectedSettings = _availableSettingsSets.First();
        _loadedSettings = _selectedSettings;

        PropertyChanged += MainWindowViewModel_PropertyChanged;
    }

    public async Task LoadAsync(ServiceObserverSettings? settings = null) {
        Unload();

        if (settings is not null) {
            LoadedSettings = settings;
        }

        await RefreshServiceStateAsync();

        StartObservingLog();
    }

    public void Unload() {
        ForceStopStandalone();

        StopObservingLog();

        LogText.Clear();
    }

    private async void MainWindowViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e) {
        if (e.PropertyName == nameof(SelectedSettings)) {
            await LoadAsync(SelectedSettings);
        }
    }

    private void StartObservingLog() {
        if (!File.Exists(LoadedSettings.ServiceLogPath)) {
            return;
        }

        if (IsObservingLog || LoadedSettings.ServiceLogPath is null) {
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
        ArgumentNullException.ThrowIfNull(LoadedSettings.ServiceLogPath);

        IsObservingLog = true;

        try {
            // #TODO: Use file observer or more low level logic to track changes based on changed bytes

            using FileStream stream = File.Open(LoadedSettings.ServiceLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

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
            ArgumentNullException.ThrowIfNull(LoadedSettings.ServiceName);

            await WindowsServiceManager.StartServiceAsync(LoadedSettings.ServiceName);

            await Task.Delay(3000);

            await RefreshServiceStateAsync();

            StartObservingLog();
        });
    }

    private async Task StopServiceAsync() {
        await RunActionAndHandleExceptionAsync(async () => {
            ArgumentNullException.ThrowIfNull(LoadedSettings.ServiceName);

            await WindowsServiceManager.StopServiceAsync(LoadedSettings.ServiceName);

            await Task.Delay(3000);

            await RefreshServiceStateAsync();

            StopObservingLog();
        });
    }

    private async Task InstallServiceAsync() {
        await RunActionAndHandleExceptionAsync(async () => {
            ArgumentNullException.ThrowIfNull(LoadedSettings.ServiceExecutablePath);
            ArgumentNullException.ThrowIfNull(LoadedSettings.ServiceName);

            string executableFullPath = Path.Combine(Directory.GetCurrentDirectory(), LoadedSettings.ServiceExecutablePath);
            await WindowsServiceManager.InstallServiceAsync(LoadedSettings.ServiceName, executableFullPath);
            await RefreshServiceStateAsync();
        });
    }

    private async Task UninstallServiceAsync() {
        await RunActionAndHandleExceptionAsync(async () => {
            ArgumentNullException.ThrowIfNull(LoadedSettings.ServiceName);

            await WindowsServiceManager.UninstallServiceAsync(LoadedSettings.ServiceName);
            await RefreshServiceStateAsync();
        });
    }

    private void StartStandalone() {
        RunActionAndHandleException(() => {
            ArgumentNullException.ThrowIfNull(LoadedSettings.ServiceExecutablePath);

            _standaloneProcess = Process.Start(LoadedSettings.ServiceExecutablePath, LoadedSettings.ServiceExecutableStandaloneArgs ?? "");

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

    private void ForceStopStandalone() {
        if (_standaloneProcess is not null) {
            _standaloneProcess.Kill();
            _standaloneProcess.Dispose();
            _standaloneProcess = null;

            IsStandaloneRunning = false;
        }
    }

    private async Task RestartAsAdminAsync() {
        await RunActionAndHandleExceptionAsync(async () => {
            await StopStandaloneAsync();

            Process.Start(new ProcessStartInfo() {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = Environment.ProcessPath!,
                Arguments = string.Join(" ", LoadedSettings.ToArgs()),
                Verb = "runas",
            });

            Environment.Exit(0);
        });
    }

    private async Task RefreshServiceStateAsync() {
        if (LoadedSettings.ServiceName is null) {
            return;
        }

        ServiceState = await WindowsServiceManager.GetServiceStateAsync(LoadedSettings.ServiceName);

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
            case ServiceState.NotInstalled:
                IsServiceInstalled = false;
                IsServiceRunning = false;
                break;
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
        ForceStopStandalone();
    }
}
