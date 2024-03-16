using System;
using System.Windows.Input;

namespace Spork.Commands
{
    public abstract class CommandBase : ICommand
    {
        private bool _canExecute = true;

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void RaiseCanExecuteChanged()
        {
            _canExecute = this.EvaluateCanExecute();
            CommandManager.InvalidateRequerySuggested();
        }

        protected virtual bool EvaluateCanExecute()
            => true;

        public bool CanExecute(object parameter)
            => _canExecute;

        public abstract void Execute(object parameter);
    }
}
