using TableCloth.Bootstrap;

namespace TableCloth.Test;

[TestClass]
public sealed class LicenseGateTests
{
    [TestMethod]
    public void IsAgreementAccepted_OwnerlessDialogWithAcceptedState_ReturnsTrue()
    {
        var result = LicenseGate.IsAgreementAccepted(dialogResult: null, licenseAccepted: true);

        Assert.IsTrue(result);
    }

    [TestMethod]
    [DataRow(null, false, false)]
    [DataRow(false, false, false)]
    [DataRow(true, false, true)]
    [DataRow(false, true, true)]
    public void IsAgreementAccepted_CombinesDialogResultAndWindowState(
        bool? dialogResult,
        bool licenseAccepted,
        bool expected)
    {
        var result = LicenseGate.IsAgreementAccepted(dialogResult, licenseAccepted);

        Assert.AreEqual(expected, result);
    }
}
