using System;
using System.Diagnostics;
using TableCloth.ViewModels;

namespace TableCloth.Commands.DetailPage;

public sealed class DetailPageOpenHomepageLinkCommand : ViewModelCommandBase<DetailPageViewModel>
{
    public override void Execute(DetailPageViewModel viewModel)
    {
        if (!Uri.TryCreate(viewModel.Url, UriKind.Absolute, out var parsedUri) || parsedUri == null)
            return;

        Process.Start(new ProcessStartInfo(parsedUri.ToString())
        {
            UseShellExecute = true,
        });
    }
}