using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

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
