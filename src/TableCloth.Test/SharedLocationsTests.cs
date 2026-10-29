using System.Diagnostics;
using TableCloth.Components.Implementations;

namespace TableCloth.Test;

[TestClass]
public sealed class SharedLocationsTests
{
    [TestMethod]
    public void UsesApplicationPayloadRatherThanHostingProcessForBundledResources()
    {
        var applicationExecutable = Path.Combine(AppContext.BaseDirectory, "TableCloth.exe");
        var imagesZip = Path.Combine(AppContext.BaseDirectory, "Images.zip");
        Assert.IsTrue(File.Exists(applicationExecutable));
        Assert.IsTrue(File.Exists(imagesZip));

        var locations = new SharedLocations();
        Assert.AreEqual(applicationExecutable, locations.ExecutableFilePath);
        Assert.AreEqual(Path.GetDirectoryName(applicationExecutable), locations.ExecutableDirectoryPath);
        Assert.AreEqual(imagesZip, locations.ImagesZipFilePath);
        Assert.AreNotEqual(Process.GetCurrentProcess().MainModule?.FileName, locations.ExecutableFilePath);
    }
}
