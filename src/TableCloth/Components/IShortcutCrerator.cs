using TableCloth.ViewModels;

namespace TableCloth.Components;

public interface IShortcutCrerator
{
    void CreateShortcut(ITableClothViewModel viewModel);
}