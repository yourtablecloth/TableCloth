using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TableCloth.Models;

namespace TableCloth.Components;

public interface IAppStartup : IDisposable
{
    Task<bool> CheckForInternetConnectionAsync(CancellationToken cancellationToken = default);
    Task<ApplicationStartupResultModel> HasRequirementsMetAsync(IList<string> warnings);
    Task<ApplicationStartupResultModel> InitializeAsync(IList<string> warnings, CancellationToken cancellationToken = default);
}