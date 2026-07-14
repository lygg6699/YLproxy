using System;
using System.Windows.Input;

namespace YLproxy.GUI;

public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    private readonly Func<bool>? _canExecute = canExecute;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter) => execute();

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

public sealed class RelayCommand<T>(Action<T> execute, Func<T, bool>? canExecute = null) : ICommand
{
    private readonly Func<T, bool>? _canExecute = canExecute;

    public bool CanExecute(object? parameter)
    {
        if (parameter is not T t)
        {
            if (parameter == null && default(T) == null)
                return _canExecute?.Invoke(default!) ?? true;

            return false;
        }

        return _canExecute?.Invoke(t) ?? true;
    }

    public void Execute(object? parameter)
    {
        if (parameter is T t)
            execute(t);
        else if (parameter == null)
            execute(default!);
        else
            throw new InvalidOperationException($"Invalid parameter type. Expected: {typeof(T).FullName}");
    }

    public event EventHandler? CanExecuteChanged;

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

