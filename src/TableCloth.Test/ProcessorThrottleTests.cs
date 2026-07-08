using TableCloth;

namespace TableCloth.Test
{
    /// <summary>
    /// 전원 옵션 "이름"이 아니라 실제 CPU 최대 성능 상한(최대 프로세서 상태, %)으로 스로틀 여부를 판정하는
    /// 순수 분류기(<see cref="Helpers.IsProcessorStateThrottled(int)"/>)를 검증한다. 실제 P/Invoke 판독은
    /// 환경 의존적이라 여기서는 다루지 않고, 값 → 판정 로직만 확인한다.
    /// </summary>
    [TestClass]
    public sealed class ProcessorThrottleTests
    {
        [TestMethod]
        public void IsProcessorStateThrottled_FullHundredPercent_ReturnsFalse()
        {
            // AC의 '균형 조정'을 포함해 최대 프로세서 상태가 100%면 제한되지 않은 것으로 본다(경고 안 함).
            Assert.IsFalse(Helpers.IsProcessorStateThrottled(100));
        }

        [TestMethod]
        public void IsProcessorStateThrottled_JustBelowHundred_ReturnsTrue()
        {
            Assert.IsTrue(Helpers.IsProcessorStateThrottled(99));
        }

        [TestMethod]
        public void IsProcessorStateThrottled_Half_ReturnsTrue()
        {
            Assert.IsTrue(Helpers.IsProcessorStateThrottled(50));
        }

        [TestMethod]
        public void IsProcessorStateThrottled_Zero_ReturnsTrue()
        {
            Assert.IsTrue(Helpers.IsProcessorStateThrottled(0));
        }

        [TestMethod]
        public void IsProcessorStateThrottled_AboveHundred_ReturnsFalse()
        {
            // 100을 초과하는 값은 발생하지 않지만, 방어적으로 "제한 아님"으로 처리한다.
            Assert.IsFalse(Helpers.IsProcessorStateThrottled(101));
        }
    }
}
