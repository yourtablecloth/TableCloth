using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Spork.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Events;
using TableCloth.Resources;

namespace Spork.ViewModels
{
    public partial class PrecautionsWindowViewModelForDesigner : PrecautionsWindowViewModel { }

    public partial class PrecautionsWindowViewModel : ObservableObject
    {
        protected PrecautionsWindowViewModel() { }

        [ActivatorUtilitiesConstructor]
        public PrecautionsWindowViewModel(
            IResourceCacheManager resourceCacheManager,
            ICommandLineArguments commandLineArguments,
            TaskFactory taskFactory)
        {
            _resourceCacheManager = resourceCacheManager;
            _commandLineArguments = commandLineArguments;
            _taskFactory = taskFactory;
        }

        private readonly IResourceCacheManager _resourceCacheManager;
        private readonly ICommandLineArguments _commandLineArguments;
        private readonly TaskFactory _taskFactory;

        /// <summary>
        /// 호출 측이 명시적으로 주의 사항을 표시할 사이트 ID 목록을 지정하기 위해 사용합니다.
        /// 카탈로그 UI 진입 흐름에서는 명령줄 인자가 비어 있으므로 반드시 이 속성으로 전달해야 합니다.
        /// 비어 있으면 기존 동작(<see cref="ICommandLineArguments"/>의 SelectedServices 폴백)을 따릅니다.
        /// </summary>
        public IEnumerable<string> TargetServiceIds { get; set; }

        [RelayCommand]
        private void PrecautionsWindowLoaded()
        {
            var catalog = _resourceCacheManager.CatalogDocument;
            var targets = TargetServiceIds ?? _commandLineArguments.GetCurrent().SelectedServices;
            var targetSet = new HashSet<string>(targets ?? Enumerable.Empty<string>(), StringComparer.Ordinal);

            var buffer = new StringBuilder();

            foreach (var eachItem in catalog.Services.Where(x => targetSet.Contains(x.Id)))
            {
                buffer.AppendLine($"[{eachItem.DisplayName} {UIStringResources.Spork_Warning_Title}]");
                buffer.AppendLine();
                buffer.AppendLine(eachItem.CompatibilityNotes);
                buffer.AppendLine();
            }

            CautionContent = buffer.ToString();
        }

        [RelayCommand]
        private Task PrecautionsWindowClose()
        {
            return _taskFactory.StartNew(
                () => CloseRequested?.Invoke(this, new DialogRequestEventArgs(true)),
                default(CancellationToken));
        }

        public event EventHandler<DialogRequestEventArgs> CloseRequested;

        [ObservableProperty]
        private string _cautionContent;
    }
}
