using System.Threading;
using System.Threading.Tasks;

namespace TableCloth.Components;

public interface ILicenseDescriptor
{
    Task<string> GetLicenseDescriptionsAsync(CancellationToken cancellationToken = default);
}