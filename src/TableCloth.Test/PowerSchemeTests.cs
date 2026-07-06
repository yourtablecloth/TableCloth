using System;
using TableCloth;

namespace TableCloth.Test
{
    [TestClass]
    public sealed class PowerSchemeTests
    {
        // Windows built-in "High performance" power scheme GUID.
        private static readonly Guid HighPerformanceGuid =
            new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");

        // Windows built-in "Ultimate Performance" power scheme GUID.
        private static readonly Guid UltimatePerformanceGuid =
            new Guid("e9a42b02-d5df-448d-aa00-03f14749eb61");

        // Windows built-in "Balanced" power scheme GUID.
        private static readonly Guid BalancedGuid =
            new Guid("381b4222-f694-41f0-9685-ff5bb260df2e");

        // Windows built-in "Power saver" power scheme GUID.
        private static readonly Guid PowerSaverGuid =
            new Guid("a1841308-3541-4fab-bc81-f71556f20b4a");

        [TestMethod]
        public void IsHighPerformancePowerScheme_HighPerformance_ReturnsTrue()
        {
            Assert.IsTrue(Helpers.IsHighPerformancePowerScheme(HighPerformanceGuid));
        }

        [TestMethod]
        public void IsHighPerformancePowerScheme_UltimatePerformance_ReturnsTrue()
        {
            Assert.IsTrue(Helpers.IsHighPerformancePowerScheme(UltimatePerformanceGuid));
        }

        [TestMethod]
        public void IsHighPerformancePowerScheme_Balanced_ReturnsFalse()
        {
            Assert.IsFalse(Helpers.IsHighPerformancePowerScheme(BalancedGuid));
        }

        [TestMethod]
        public void IsHighPerformancePowerScheme_PowerSaver_ReturnsFalse()
        {
            Assert.IsFalse(Helpers.IsHighPerformancePowerScheme(PowerSaverGuid));
        }

        [TestMethod]
        public void IsHighPerformancePowerScheme_EmptyGuid_ReturnsFalse()
        {
            Assert.IsFalse(Helpers.IsHighPerformancePowerScheme(Guid.Empty));
        }
    }
}
