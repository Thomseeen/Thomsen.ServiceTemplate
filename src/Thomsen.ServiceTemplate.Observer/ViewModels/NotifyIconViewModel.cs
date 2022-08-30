using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using Thomsen.ServiceTemplate.Observer.Models;
using Thomsen.ServiceTemplate.Observer.Mvvm;

namespace Thomsen.ServiceTemplate.Observer.ViewModels;

internal class NotifyIconViewModel : BaseViewModel {
    private readonly ServiceObserverSettings[] _settings;
    private readonly System.Timers.Timer _timer;

    private ServiceStateEntry[] _serviceStates;

    private ICommand? _showMainWindowCmd;
    private ICommand? _hideMainWindowCmd;
    private ICommand? _exitCmd;

    public ICommand ShowMainWindowCmd => _showMainWindowCmd ??=
        new CommandHandler(param => (Application.Current as ObserverApp)!.SetupAndShowMainWindow((param as ServiceStateEntry)?.Settings), () => !(Application.Current as ObserverApp)!.IsMainWindowShown);

    public ICommand HideMainWindowCmd => _hideMainWindowCmd ??=
        new CommandHandler(param => (Application.Current as ObserverApp)!.CloseMainWindow(), () => (Application.Current as ObserverApp)!.IsMainWindowShown);

    public ICommand ExitCmd => _exitCmd ??=
        new CommandHandler(param => Application.Current.Shutdown(), () => true);

    public ServiceStateEntry[] ServiceStates { get => _serviceStates; set => SetProperty(ref _serviceStates, value); }

    public NotifyIconViewModel(ServiceObserverSettings[] settings) {
        _settings = settings;

        _timer = new System.Timers.Timer(5000);
        _timer.Elapsed += Timer_Elapsed;

        _timer.Start();

        _serviceStates = Array.Empty<ServiceStateEntry>();
    }

    private async void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e) {
        _timer.Stop();

        ServiceStates = await RefreshServiceStatesAsync(_settings);

        _timer.Start();
    }

    private static async Task<ServiceStateEntry[]> RefreshServiceStatesAsync(ServiceObserverSettings[] settings) {
        List<ServiceStateEntry> services = new();

        foreach (ServiceObserverSettings setting in settings) {
            if (setting.ServiceName is null) {
                continue;
            }

            try {
                ServiceState state = await WindowsServiceManager.GetServiceStateAsync(setting.ServiceName);
                services.Add(new ServiceStateEntry() { Settings = setting, State = state });
            } catch (WindowsServiceManagerException ex) when (ex.IsServiceNotInstalledError) { }
        }

        return services.ToArray();
    }
}
