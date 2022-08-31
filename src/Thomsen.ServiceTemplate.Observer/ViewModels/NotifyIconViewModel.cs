using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

    private readonly ObservableCollection<ServiceStateEntry> _serviceStates;

    private ICommand? _showMainWindowCmd;
    private ICommand? _hideMainWindowCmd;
    private ICommand? _exitCmd;

    public ICommand ShowMainWindowCmd => _showMainWindowCmd ??=
        new CommandHandler(param => (Application.Current as ObserverApp)!.SetupAndShowMainWindow((param as ServiceStateEntry)?.Settings), () => !(Application.Current as ObserverApp)!.IsMainWindowShown);

    public ICommand HideMainWindowCmd => _hideMainWindowCmd ??=
        new CommandHandler(param => (Application.Current as ObserverApp)!.CloseMainWindow(), () => (Application.Current as ObserverApp)!.IsMainWindowShown);

    public ICommand ExitCmd => _exitCmd ??=
        new CommandHandler(param => Application.Current.Shutdown(), () => true);

    public ObservableCollection<ServiceStateEntry> ServiceStates => _serviceStates;

    public NotifyIconViewModel(ServiceObserverSettings[] settings) {
        _settings = settings;

        _timer = new System.Timers.Timer(5000);
        _timer.Elapsed += Timer_Elapsed;

        _timer.Start();

        _serviceStates = new ObservableCollection<ServiceStateEntry>();
    }

    public async Task LoadAsync() {
        await InitServiceStatesAsync();
    }

    private async void Timer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e) {
        _timer.Stop();

        await RefreshServiceStatesAsync();

        _timer.Start();
    }

    private async Task InitServiceStatesAsync() {
        foreach (ServiceObserverSettings setting in _settings) {
            if (setting.ServiceName is null) {
                continue;
            }

            ServiceState state = await WindowsServiceManager.GetServiceStateAsync(setting.ServiceName);
            ServiceStates.Add(new ServiceStateEntry() { Settings = setting, State = state });
        }
    }

    private async Task RefreshServiceStatesAsync() {
        foreach (ServiceStateEntry serviceState in ServiceStates) {
            if (serviceState.Settings.ServiceName is null) {
                continue;
            }

            ServiceState state = await WindowsServiceManager.GetServiceStateAsync(serviceState.Settings.ServiceName!);
            if (serviceState.State != state) {
                serviceState.State = state;
            }
        }
    }
}
