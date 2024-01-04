using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models;

namespace Hostess.Components
{
    public interface IAppStartup : IDisposable
    {
        Task<ApplicationStartupResultModel> HasRequirementsMetAsync(IList<string> warnings);
        Task<ApplicationStartupResultModel> InitializeAsync(IList<string> warnings, CancellationToken cancellationToken = default);
    }
}