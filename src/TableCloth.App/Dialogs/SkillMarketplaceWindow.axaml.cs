using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.Threading.Tasks;
using TableCloth.ManagedAi;
using TableCloth.ViewModels;

namespace TableCloth.Dialogs;

public enum SkillMarketplaceTab { Catalog, Installed }

public partial class SkillMarketplaceWindow : Window
{
    public SkillMarketplaceWindow() => InitializeComponent();

    public SkillMarketplaceWindow(SkillMarketplaceWindowViewModel viewModel,
        SkillMarketplaceTab initialTab = SkillMarketplaceTab.Catalog) : this()
    {
        DataContext = viewModel;
        this.FindControl<TabControl>("MarketplaceTabs")!.SelectedIndex = initialTab == SkillMarketplaceTab.Installed ? 1 : 0;
        viewModel.PickFolderAsync = PickFolderAsync;
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private async void OnOpened(object? sender, EventArgs e)
    {
        if (DataContext is SkillMarketplaceWindowViewModel viewModel) await viewModel.LoadAsync();
    }

    private async Task<string?> PickFolderAsync()
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = ManagedAiText.Select("SKILL.md가 들어 있는 스킬 폴더 선택", "Select a skill folder containing SKILL.md"),
            AllowMultiple = false
        });
        return folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        Opened -= OnOpened;
        Closed -= OnClosed;
        if (DataContext is SkillMarketplaceWindowViewModel viewModel) viewModel.Dispose();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e) => Close();
}
