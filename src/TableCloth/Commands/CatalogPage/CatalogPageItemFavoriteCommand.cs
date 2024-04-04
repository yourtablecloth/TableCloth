using AsyncAwaitBestPractices;
using AsyncAwaitBestPractices.MVVM;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TableCloth.Components;
using TableCloth.Models.Catalog;

namespace TableCloth.Commands.CatalogPage;

public class CatalogPageItemFavoriteCommand(
    IPreferencesManager preferencesManager) : CommandBase, IAsyncCommand
{
    public override void Execute(object? obj)
        => ExecuteAsync(obj as CatalogInternetService).SafeFireAndForget();

    public async Task ExecuteAsync(CatalogInternetService? service)
    {
        var settings = await preferencesManager.LoadPreferencesAsync();

        settings!.Favorites ??= new List<string>();
        if (service!.IsFavorite)
            settings.Favorites.Add(service.Id);
        else if(settings.Favorites.Contains(service.Id))
            settings.Favorites.Remove(service.Id);

        await preferencesManager.SavePreferencesAsync(settings);
    }

    [Obsolete("unused path.",false)]
    public Task ExecuteAsync() => throw new NotImplementedException();
}