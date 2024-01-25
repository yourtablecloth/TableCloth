using System.Threading;
using System.Threading.Tasks;
using TableCloth.ViewModels;

namespace TableCloth.Components;

public interface IShortcutCrerator
{
    Task<string?> CreateShortcutAsync(ITableClothViewModel viewModel, CancellationToken cancellationToken = default);
}