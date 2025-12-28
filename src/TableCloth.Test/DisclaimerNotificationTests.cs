using TableCloth.Models.Configuration;

namespace TableCloth.Test
{
    [TestClass]
    public sealed class DisclaimerNotificationTests
    {
        #region Disclaimer 알림 주기 테스트

        [TestMethod]
        public void ShouldNotifyDisclaimer_WhenNeverAgreed_ReturnsTrue()
        {
            // Arrange
            var settings = new PreferenceSettings { LastDisclaimerAgreedTime = null };
            var currentTime = DateTime.UtcNow;

            // Act
            var result = settings.ShouldNotifyDisclaimer(currentTime);

            // Assert
            Assert.IsTrue(result, "처음 사용하는 경우 Disclaimer를 표시해야 합니다.");
        }

        [TestMethod]
        public void ShouldNotifyDisclaimer_WhenAgreedJustNow_ReturnsFalse()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var settings = new PreferenceSettings { LastDisclaimerAgreedTime = currentTime };

            // Act
            var result = settings.ShouldNotifyDisclaimer(currentTime);

            // Assert
            Assert.IsFalse(result, "방금 동의한 경우 Disclaimer를 표시하지 않아야 합니다.");
        }

        [TestMethod]
        public void ShouldNotifyDisclaimer_WhenAgreed1DayAgo_ReturnsFalse()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var settings = new PreferenceSettings { LastDisclaimerAgreedTime = currentTime.AddDays(-1) };

            // Act
            var result = settings.ShouldNotifyDisclaimer(currentTime);

            // Assert
            Assert.IsFalse(result, "1일 전에 동의한 경우 Disclaimer를 표시하지 않아야 합니다.");
        }

        [TestMethod]
        public void ShouldNotifyDisclaimer_WhenAgreed6DaysAgo_ReturnsFalse()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var settings = new PreferenceSettings { LastDisclaimerAgreedTime = currentTime.AddDays(-6) };

            // Act
            var result = settings.ShouldNotifyDisclaimer(currentTime);

            // Assert
            Assert.IsFalse(result, "6일 전에 동의한 경우 Disclaimer를 표시하지 않아야 합니다.");
        }

        [TestMethod]
        public void ShouldNotifyDisclaimer_WhenAgreedExactly7DaysAgo_ReturnsTrue()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var settings = new PreferenceSettings { LastDisclaimerAgreedTime = currentTime.AddDays(-7) };

            // Act
            var result = settings.ShouldNotifyDisclaimer(currentTime);

            // Assert
            Assert.IsTrue(result, "정확히 7일 전에 동의한 경우 Disclaimer를 표시해야 합니다.");
        }

        [TestMethod]
        public void ShouldNotifyDisclaimer_WhenAgreed8DaysAgo_ReturnsTrue()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var settings = new PreferenceSettings { LastDisclaimerAgreedTime = currentTime.AddDays(-8) };

            // Act
            var result = settings.ShouldNotifyDisclaimer(currentTime);

            // Assert
            Assert.IsTrue(result, "8일 전에 동의한 경우 Disclaimer를 표시해야 합니다.");
        }

        [TestMethod]
        public void ShouldNotifyDisclaimer_WhenAgreed30DaysAgo_ReturnsTrue()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var settings = new PreferenceSettings { LastDisclaimerAgreedTime = currentTime.AddDays(-30) };

            // Act
            var result = settings.ShouldNotifyDisclaimer(currentTime);

            // Assert
            Assert.IsTrue(result, "30일 전에 동의한 경우 Disclaimer를 표시해야 합니다.");
        }

        [TestMethod]
        public void ShouldNotifyDisclaimer_BoundaryTest_6Days23Hours_ReturnsFalse()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var settings = new PreferenceSettings { LastDisclaimerAgreedTime = currentTime.AddDays(-7).AddHours(1) }; // 6일 23시간 전

            // Act
            var result = settings.ShouldNotifyDisclaimer(currentTime);

            // Assert
            Assert.IsFalse(result, "6일 23시간 전에 동의한 경우 Disclaimer를 표시하지 않아야 합니다.");
        }

        [TestMethod]
        public void ShouldNotifyDisclaimer_BoundaryTest_7Days1Hour_ReturnsTrue()
        {
            // Arrange
            var currentTime = DateTime.UtcNow;
            var settings = new PreferenceSettings { LastDisclaimerAgreedTime = currentTime.AddDays(-7).AddHours(-1) }; // 7일 1시간 전

            // Act
            var result = settings.ShouldNotifyDisclaimer(currentTime);

            // Assert
            Assert.IsTrue(result, "7일 1시간 전에 동의한 경우 Disclaimer를 표시해야 합니다.");
        }

        #endregion

        #region 파라미터 없는 오버로드 테스트

        [TestMethod]
        public void ShouldNotifyDisclaimer_NoParameter_WhenNeverAgreed_ReturnsTrue()
        {
            // Arrange
            var settings = new PreferenceSettings { LastDisclaimerAgreedTime = null };

            // Act
            var result = settings.ShouldNotifyDisclaimer();

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void ShouldNotifyDisclaimer_NoParameter_WhenRecentlyAgreed_ReturnsFalse()
        {
            // Arrange
            var settings = new PreferenceSettings { LastDisclaimerAgreedTime = DateTime.UtcNow };

            // Act
            var result = settings.ShouldNotifyDisclaimer();

            // Assert
            Assert.IsFalse(result);
        }

        #endregion
    }
}
