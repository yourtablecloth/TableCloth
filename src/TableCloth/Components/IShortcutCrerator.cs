using System.Threading;
using System.Threading.Tasks;
using TableCloth.ViewModels;

namespace TableCloth.Components;

public interface IShortcutCrerator
{
    Task<string?> CreateShortcutAsync(DetailPageViewModel viewModel, CancellationToken cancellationToken = default);

    Task<string?> CreateResponseFileAsync(DetailPageViewModel viewModel, CancellationToken cancellationToken = default);
}