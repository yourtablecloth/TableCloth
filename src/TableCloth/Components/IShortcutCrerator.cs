using System.Threading.Tasks;
using System.Threading;
using TableCloth.ViewModels;

namespace TableCloth.Components;

public interface IShortcutCrerator
{
    Task<string?> CreateShortcutAsync(ITableClothViewModel viewModel, CancellationToken cancellationToken = default);
}