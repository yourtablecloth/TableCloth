using TableCloth.Components.Implementations;
using TableCloth.Models.Configuration;

namespace TableCloth.Test;

[TestClass]
public sealed class AppUpdateManagerTests
{
    [TestMethod]
    [DataRow("x64", ReleaseChannel.Retail, "TableCloth_1.21.1.0_Release_x64.exe")]
    [DataRow("arm64", ReleaseChannel.Retail, "TableCloth_1.21.1.0_Release_arm64.exe")]
    [DataRow("x64", ReleaseChannel.Preview, "TableCloth-Preview_1.22.0.0_Release_x64.exe")]
    [DataRow("arm64", ReleaseChannel.Preview, "TableCloth-Preview_1.22.0.0_Release_arm64.exe")]
    public void FindInstallerAsset_MixedRelease_SelectsRequestedProductArchitectureAndChannel(
        string arch,
        ReleaseChannel channel,
        string expectedName)
    {
        var assets = CreateAssets(
            "SporkBootstrap_1.21.1.0_Release_x64.exe",
            "Spork_1.21.1.0_Release_x64.exe",
            "SporkBootstrap_1.21.1.0_Release_arm64.exe",
            "Spork_1.21.1.0_Release_arm64.exe",
            "TableCloth-Preview_1.22.0.0_Release_arm64.exe",
            "TableCloth-Preview_1.22.0.0_Release_x64.exe",
            "TableCloth_1.21.1.0_Release_arm64.exe",
            "TableCloth_1.21.1.0_Release_x64.exe");

        var asset = AppUpdateManager.FindInstallerAsset(assets, arch, channel);

        Assert.IsNotNull(asset);
        Assert.AreEqual(expectedName, asset.Name);
    }

    [TestMethod]
    [DataRow("x64", ReleaseChannel.Retail)]
    [DataRow("arm64", ReleaseChannel.Retail)]
    [DataRow("x64", ReleaseChannel.Preview)]
    [DataRow("arm64", ReleaseChannel.Preview)]
    public void FindInstallerAsset_NoMatchingInstaller_DoesNotFallBackToAnotherExecutable(
        string arch,
        ReleaseChannel channel)
    {
        var otherArch = arch == "x64" ? "arm64" : "x64";
        var otherProduct = channel == ReleaseChannel.Retail ? "TableCloth-Preview" : "TableCloth";
        var assets = CreateAssets(
            $"SporkBootstrap_1.21.1.0_Release_{arch}.exe",
            $"Spork_1.21.1.0_Release_{arch}.exe",
            $"TableCloth_1.21.1.0_Release_{otherArch}.exe",
            $"TableCloth-Preview_1.22.0.0_Release_{otherArch}.exe",
            $"{otherProduct}_1.22.0.0_Release_{arch}.exe");

        var asset = AppUpdateManager.FindInstallerAsset(assets, arch, channel);

        Assert.IsNull(asset);
    }

    [TestMethod]
    public void FindInstallerAsset_RejectsUnrelatedNamesAndNonInstallerAssets()
    {
        var assets = CreateAssets(
            null,
            "setup.exe",
            "OtherTableCloth_1.21.1.0_Release_x64.exe",
            "TableClothBootstrap_1.21.1.0_Release_x64.exe",
            "TableCloth_1.21.1.0_Release_x64_Portable.zip",
            "TableCloth_1.21.1.0_Release_x64_symbols.exe",
            "TableCloth_1.21.1.0_Release_x64.exe.bak");

        var asset = AppUpdateManager.FindInstallerAsset(assets, "x64", ReleaseChannel.Retail);

        Assert.IsNull(asset);
    }

    [TestMethod]
    public void FindInstallerAsset_EmptyRelease_ReturnsNull()
    {
        var asset = AppUpdateManager.FindInstallerAsset([], "x64", ReleaseChannel.Retail);

        Assert.IsNull(asset);
    }

    [TestMethod]
    public void FindInstallerAsset_NameComparison_IsCaseInsensitive()
    {
        var assets = CreateAssets("tablecloth_1.21.1.0_release_X64.EXE");

        var asset = AppUpdateManager.FindInstallerAsset(assets, "x64", ReleaseChannel.Retail);

        Assert.AreSame(assets[0], asset);
    }

    private static AppUpdateManager.GitHubAssetInfo[] CreateAssets(params string?[] names)
        => names.Select(name => new AppUpdateManager.GitHubAssetInfo { Name = name }).ToArray();
}
