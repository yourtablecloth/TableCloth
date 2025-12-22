using TableCloth.Models.Configuration;

namespace TableCloth.Test
{
    [TestClass]
    public sealed class PreferenceSettingsTests
    {
        #region 기본값 테스트

        [TestMethod]
        public void UseAudioRedirection_DefaultValue_ShouldBeFalse()
        {
            var settings = new PreferenceSettings();
            Assert.IsFalse(settings.UseAudioRedirection);
        }

        [TestMethod]
        public void UseVideoRedirection_DefaultValue_ShouldBeFalse()
        {
            var settings = new PreferenceSettings();
            Assert.IsFalse(settings.UseVideoRedirection);
        }

        [TestMethod]
        public void UsePrinterRedirection_DefaultValue_ShouldBeFalse()
        {
            var settings = new PreferenceSettings();
            Assert.IsFalse(settings.UsePrinterRedirection);
        }

        [TestMethod]
        public void InstallEveryonesPrinter_DefaultValue_ShouldBeTrue()
        {
            var settings = new PreferenceSettings();
            Assert.IsTrue(settings.InstallEveryonesPrinter);
        }

        [TestMethod]
        public void InstallAdobeReader_DefaultValue_ShouldBeTrue()
        {
            var settings = new PreferenceSettings();
            Assert.IsTrue(settings.InstallAdobeReader);
        }

        [TestMethod]
        public void InstallHancomOfficeViewer_DefaultValue_ShouldBeTrue()
        {
            var settings = new PreferenceSettings();
            Assert.IsTrue(settings.InstallHancomOfficeViewer);
        }

        [TestMethod]
        public void InstallRaiDrive_DefaultValue_ShouldBeTrue()
        {
            var settings = new PreferenceSettings();
            Assert.IsTrue(settings.InstallRaiDrive);
        }

        [TestMethod]
        public void UseLogCollection_DefaultValue_ShouldBeTrue()
        {
            var settings = new PreferenceSettings();
            Assert.IsTrue(settings.UseLogCollection);
        }

        [TestMethod]
        public void LastDisclaimerAgreedTime_DefaultValue_ShouldBeNull()
        {
            var settings = new PreferenceSettings();
            Assert.IsNull(settings.LastDisclaimerAgreedTime);
        }

        [TestMethod]
        public void ShowFavoritesOnly_DefaultValue_ShouldBeFalse()
        {
            var settings = new PreferenceSettings();
            Assert.IsFalse(settings.ShowFavoritesOnly);
        }

        [TestMethod]
        public void Favorites_DefaultValue_ShouldBeEmptyList()
        {
            var settings = new PreferenceSettings();
            Assert.IsNotNull(settings.Favorites);
            Assert.IsEmpty(settings.Favorites);
        }

        [TestMethod]
        public void LastUsedCertHash_DefaultValue_ShouldBeNull()
        {
            var settings = new PreferenceSettings();
            Assert.IsNull(settings.LastUsedCertHash);
        }

        [TestMethod]
        public void LicenseAgreedTime_DefaultValue_ShouldBeNull()
        {
            var settings = new PreferenceSettings();
            Assert.IsNull(settings.LicenseAgreedTime);
        }

        [TestMethod]
        public void LicenseAgreedVersion_DefaultValue_ShouldBeNull()
        {
            var settings = new PreferenceSettings();
            Assert.IsNull(settings.LicenseAgreedVersion);
        }

        #endregion

        #region 속성 설정 테스트

        [TestMethod]
        public void AllProperties_CanBeSet()
        {
            var now = DateTime.UtcNow;
            var settings = new PreferenceSettings
            {
                UseAudioRedirection = true,
                UseVideoRedirection = true,
                UsePrinterRedirection = true,
                InstallEveryonesPrinter = false,
                InstallAdobeReader = false,
                InstallHancomOfficeViewer = false,
                InstallRaiDrive = false,
                UseLogCollection = false,
                LastDisclaimerAgreedTime = now,
                ShowFavoritesOnly = true,
                LastUsedCertHash = "ABC123",
                LicenseAgreedTime = now,
                LicenseAgreedVersion = "1.0.0"
            };

            Assert.IsTrue(settings.UseAudioRedirection);
            Assert.IsTrue(settings.UseVideoRedirection);
            Assert.IsTrue(settings.UsePrinterRedirection);
            Assert.IsFalse(settings.InstallEveryonesPrinter);
            Assert.IsFalse(settings.InstallAdobeReader);
            Assert.IsFalse(settings.InstallHancomOfficeViewer);
            Assert.IsFalse(settings.InstallRaiDrive);
            Assert.IsFalse(settings.UseLogCollection);
            Assert.AreEqual(now, settings.LastDisclaimerAgreedTime);
            Assert.IsTrue(settings.ShowFavoritesOnly);
            Assert.AreEqual("ABC123", settings.LastUsedCertHash);
            Assert.AreEqual(now, settings.LicenseAgreedTime);
            Assert.AreEqual("1.0.0", settings.LicenseAgreedVersion);
        }

        [TestMethod]
        public void Favorites_CanAddItems()
        {
            var settings = new PreferenceSettings();

            settings.Favorites.Add("service1");
            settings.Favorites.Add("service2");

            Assert.HasCount(2, settings.Favorites);
            Assert.AreEqual("service1", settings.Favorites[0]);
            Assert.AreEqual("service2", settings.Favorites[1]);
        }

        #endregion
    }
}
