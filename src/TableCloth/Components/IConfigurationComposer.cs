using TableCloth.Models;
using TableCloth.Models.Configuration;
using TableCloth.ViewModels;

namespace TableCloth.Components;

public interface IConfigurationComposer
{
    TableClothConfiguration GetConfigurationFromArgumentModel(CommandLineArgumentModel argumentModel);
    TableClothConfiguration GetConfigurationFromViewModel(DetailPageViewModel viewModel);
}