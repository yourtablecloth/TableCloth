using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading.Tasks;

namespace TableCloth.ViewModels
{
    /// <summary>
    /// ViewModel의 기본 클래스입니다.
    /// CommunityToolkit.Mvvm의 ObservableObject를 상속하여 INotifyPropertyChanged를 구현합니다.
    /// </summary>
    /// <remarks>
    /// 기존 코드와의 호환성을 위해 NotifyPropertyChanged, NotifyMultiplePropertiesChanged, 
    /// SetProperty 오버로드를 유지합니다. 새로운 코드에서는 ObservableObject의 기능을 직접 사용하거나
    /// [ObservableProperty] 어트리뷰트를 사용하세요.
    /// </remarks>
    public abstract class ViewModelBase : ObservableObject
    {
        private readonly TaskFactory _taskFactory =
            new TaskFactory(TaskScheduler.FromCurrentSynchronizationContext());

        /// <summary>
        /// 현재 동기화 컨텍스트에서 작업을 실행하기 위한 TaskFactory입니다.
        /// </summary>
        protected TaskFactory TaskFactory => _taskFactory;

        /// <summary>
        /// 속성 변경을 알립니다.
        /// </summary>
        /// <param name="propertyName">변경된 속성 이름</param>
        /// <remarks>
        /// 이 메서드는 기존 코드와의 호환성을 위해 유지됩니다.
        /// 새로운 코드에서는 OnPropertyChanged()를 사용하세요.
        /// </remarks>
        protected void NotifyPropertyChanged(
            [System.Runtime.CompilerServices.CallerMemberName] string propertyName = default)
            => OnPropertyChanged(propertyName);

        /// <summary>
        /// 여러 속성의 변경을 알립니다.
        /// </summary>
        /// <param name="propertiesToNotify">변경된 속성 이름 배열</param>
        /// <remarks>
        /// 이 메서드는 기존 코드와의 호환성을 위해 유지됩니다.
        /// </remarks>
        protected void NotifyMultiplePropertiesChanged(string[] propertiesToNotify)
        {
            if (propertiesToNotify == null)
                return;

            foreach (var eachPropertyName in propertiesToNotify)
                OnPropertyChanged(eachPropertyName ?? string.Empty);
        }

        /// <summary>
        /// 속성 값을 설정하고 여러 속성의 변경을 알립니다.
        /// </summary>
        /// <typeparam name="T">속성 타입</typeparam>
        /// <param name="member">backing field 참조</param>
        /// <param name="value">새 값</param>
        /// <param name="propertiesToNotify">변경을 알릴 속성 이름 배열</param>
        /// <returns>값이 변경되었으면 true</returns>
        /// <remarks>
        /// 이 메서드는 기존 코드와의 호환성을 위해 유지됩니다.
        /// 하나의 속성에서 여러 다른 속성의 변경을 알려야 할 때 사용합니다.
        /// </remarks>
        protected virtual bool SetProperty<T>(
            ref T member, T value,
            string[] propertiesToNotify)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(member, value))
                return false;

            member = value;
            NotifyMultiplePropertiesChanged(propertiesToNotify);
            return true;
        }
    }
}
