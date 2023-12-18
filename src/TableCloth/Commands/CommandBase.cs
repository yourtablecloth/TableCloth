using System;
using System.Windows.Input;

namespace TableCloth.Commands
{
    public abstract class CommandBase : ICommand
    {
        private bool canExecute = true;

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public void RaiseCanExecuteChanged()
        {
            this.canExecute = this.EvaluateCanExecute();
            CommandManager.InvalidateRequerySuggested();
        }

        protected virtual bool EvaluateCanExecute()
            => true;

        public bool CanExecute(object parameter)
            => canExecute;

        public abstract void Execute(object parameter);
    }
}
