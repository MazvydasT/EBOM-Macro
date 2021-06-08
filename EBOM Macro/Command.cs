using System;
using System.Windows.Input;

namespace EBOM_Macro
{
    public class Command : ICommand
    {
        readonly Action<object> execute;
        readonly Predicate<object> canExecute;

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public Command(Action<object> execute) : this(execute, null) { }
        public Command(Action<object> execute, Predicate<object> canExecute)
        {
            this.execute = execute;
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameters) => canExecute?.Invoke(parameters) ?? true;
        public void Execute(object parameters) => execute?.Invoke(parameters);
    }
}
