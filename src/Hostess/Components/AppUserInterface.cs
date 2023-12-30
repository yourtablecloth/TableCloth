using Hostess.Dialogs;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hostess.Components
{
    public sealed class AppUserInterface
    {
        public AppUserInterface(
            IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private readonly IServiceProvider _serviceProvider;

        public AboutWindow CreateAboutWindow()
            => _serviceProvider.GetRequiredService<AboutWindow>();

        public PrecautionsWindow CreatePrecautionsWindow()
            => _serviceProvider.GetRequiredService<PrecautionsWindow>();
    }
}
