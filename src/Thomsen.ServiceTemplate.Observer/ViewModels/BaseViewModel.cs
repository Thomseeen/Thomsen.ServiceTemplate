using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Thomsen.ServiceTemplate.Observer.ViewModels;

internal class BaseViewModel : INotifyPropertyChanged {
    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = "") {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected void SetProperty<T>(ref T backingFieldRef, T value, [CallerMemberName] string propertyName = "") {
        backingFieldRef = value;
        OnPropertyChanged(propertyName);
    }

    protected void SetProperty<T>(ref T backingFieldRef, T value, [CallerMemberName] string propertyName = "", params string[] otherProperties) {
        backingFieldRef = value;
        OnPropertyChanged(propertyName);

        foreach (string property in otherProperties) {
            OnPropertyChanged(property);
        }
    }
}
