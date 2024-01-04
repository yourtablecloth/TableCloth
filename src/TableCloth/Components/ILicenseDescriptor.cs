using System.Threading.Tasks;

namespace TableCloth.Components;

public interface ILicenseDescriptor
{
    Task<string> GetLicenseDescriptions();
}