using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DetailPage;

public class DetailPageItemFavoriteCommand(
    IPreferencesManager preferencesManager) : CommandBase, IAsyncCommand
{
    public override void Execute(object? obj)
        => ExecuteAsync(obj as DetailPageViewModel).SafeFireAndForget();

    public async Task ExecuteAsync(DetailPageViewModel? viewModel)
    {
        var settings = await preferencesManager.LoadPreferencesAsync();
        var currentId = viewModel?.Id;

        if (!string.IsNullOrWhiteSpace(currentId))
        {
            settings!.Favorites ??= new List<string>();
            if (viewModel!.IsFavorite)
                settings.Favorites.Add(currentId);
            else if (settings.Favorites.Contains(currentId))
                settings.Favorites.Remove(currentId);

            await preferencesManager.SavePreferencesAsync(settings);
        }
    }

    [Obsolete("unused path.", false)]
    public Task ExecuteAsync() => throw new NotImplementedException();
}