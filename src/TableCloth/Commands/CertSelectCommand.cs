﻿using System;
using TableCloth.Components;
using TableCloth.Contracts;

namespace TableCloth.Commands
{
    public sealed class CertSelectCommand : CommandBase
    {
        public CertSelectCommand(
            AppUserInterface appUserInterface)
        {
            _appUserInterface = appUserInterface;
        }

        private readonly AppUserInterface _appUserInterface;

        public AppUserInterface AppUserInterface
            => _appUserInterface;

        public override void Execute(object? parameter)
        {
            var viewModel = parameter as ICertSelect;

            if (viewModel == null)
                throw new ArgumentException(nameof(parameter));

            var certSelectWindow = _appUserInterface.CreateWindow<CertSelectWindow>();
            var response = certSelectWindow.ShowDialog();

            if (!response.HasValue || !response.Value)
                return;

            if (certSelectWindow.ViewModel.SelectedCertPair != null)
                viewModel.SelectedCertFile = certSelectWindow.ViewModel.SelectedCertPair;
        }
    }
}