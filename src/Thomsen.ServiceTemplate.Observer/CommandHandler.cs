using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Thomsen.ServiceTemplate.Observer;

internal class CommandHandler : ICommand {
    private readonly Action<object?> _action;
    private readonly Func<bool> _canExecute;

    public event EventHandler? CanExecuteChanged {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public CommandHandler(Action<object?> action, Func<bool> canExecute) {
        _action = action;
        _canExecute = canExecute;
    }

    public bool CanExecute(object? parameter) => _canExecute.Invoke();

    public void Execute(object? parameter) => _action(parameter);
}
