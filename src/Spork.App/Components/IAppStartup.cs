using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models;

namespace Spork.Components
{
    public interface IAppStartup : IDisposable
    {
        Task<ApplicationStartupResultModel> HasRequirementsMetAsync(IList<string> warnings, CancellationToken cancellationToken = default);
        Task<ApplicationStartupResultModel> InitializeAsync(IList<string> warnings, CancellationToken cancellationToken = default);
    }
}