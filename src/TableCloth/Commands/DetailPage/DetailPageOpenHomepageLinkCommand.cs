using System;
using System.Diagnostics;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DetailPage;

public sealed class DetailPageOpenHomepageLinkCommand : CommandBase
{
    public override void Execute(object? parameter)
    {
        if (parameter is not DetailPageViewModel viewModel)
            throw new ArgumentException("Selected parameter is not a supported type.", nameof(parameter));

        if (!Uri.TryCreate(viewModel.Url, UriKind.Absolute, out var parsedUri) || parsedUri == null)
            return;

        Process.Start(new ProcessStartInfo(parsedUri.ToString())
        {
            UseShellExecute = true,
        });
    }
}