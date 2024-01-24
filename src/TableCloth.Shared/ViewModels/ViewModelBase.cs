using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace TableCloth.ViewModels
{
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        private readonly TaskFactory _taskFactory =
            new TaskFactory(TaskScheduler.FromCurrentSynchronizationContext());

        protected TaskFactory TaskFactory => _taskFactory;

        public event PropertyChangedEventHandler
#if !NETFX
            ?
#endif
            PropertyChanged;

        protected void NotifyPropertyChanged(
            [CallerMemberName] string
#if !NETFX
            ?
#endif
            propertyName = default)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        protected void NotifyMultiplePropertiesChanged(
            string[] propertiesToNotify)
        {
            if (propertiesToNotify == null)
                return;

            foreach (var eachPropertyName in propertiesToNotify)
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(eachPropertyName ?? string.Empty));
        }

        protected virtual bool SetProperty<T>(
            ref T member, T value,
            [CallerMemberName] string
#if !NETFX
            ?
#endif
            propertyName = default)
        {
            if (EqualityComparer<T>.Default.Equals(member, value))
                return false;

            member = value;
            NotifyPropertyChanged(propertyName);
            return true;
        }

        protected virtual bool SetProperty<T>(
            ref T member, T value,
            string[] propertiesToNotify)
        {
            if (EqualityComparer<T>.Default.Equals(member, value))
                return false;

            member = value;
            NotifyMultiplePropertiesChanged(propertiesToNotify);
            return true;
        }
    }
}
