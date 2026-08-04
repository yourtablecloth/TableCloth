using Microsoft.VisualStudio.TestTools.UnitTesting;
using TableCloth;

namespace TableCloth.Test
{
    /// <summary>
    /// 받침 판정과 조사 선택에 대한 회귀 방지 테스트.
    /// </summary>
    [TestClass]
    public sealed class KoreanParticleTests
    {
        [TestMethod]
        [DataRow("우리은행 개인뱅킹", "을")]   // 킹 - 받침 ㅇ
        [DataRow("국민은행", "을")]            // 행 - 받침 ㅇ
        [DataRow("카카오뱅크", "를")]          // 크 - 받침 없음
        [DataRow("토스", "를")]                // 스 - 받침 없음
        [DataRow("하나", "를")]                // 나 - 받침 없음
        [DataRow("신한", "을")]                // 한 - 받침 ㄴ
        public void EulReul_HangulEnding_ShouldFollowJongseong(string word, string expected)
            => Assert.AreEqual(expected, KoreanParticle.EulReul(word));

        [TestMethod]
        [DataRow("우리은행 개인뱅킹", "이")]
        [DataRow("카카오뱅크", "가")]
        public void IGa_HangulEnding_ShouldFollowJongseong(string word, string expected)
            => Assert.AreEqual(expected, KoreanParticle.IGa(word));

        [TestMethod]
        [DataRow("우리은행 개인뱅킹", "은")]
        [DataRow("카카오뱅크", "는")]
        public void EunNeun_HangulEnding_ShouldFollowJongseong(string word, string expected)
            => Assert.AreEqual(expected, KoreanParticle.EunNeun(word));

        /// <summary>숫자는 읽는 음가로 판정한다(1=일, 2=이 …).</summary>
        [TestMethod]
        [DataRow("메가박스1", "을")]   // 일 - 받침 ㄹ
        [DataRow("채널2", "를")]       // 이 - 받침 없음
        [DataRow("서비스3", "을")]     // 삼 - 받침 ㅁ
        [DataRow("포털5", "를")]       // 오 - 받침 없음
        [DataRow("사이트6", "을")]     // 육 - 받침 ㄱ
        [DataRow("버전9", "를")]       // 구 - 받침 없음
        [DataRow("코드0", "을")]       // 영 - 받침 ㅇ
        public void EulReul_DigitEnding_ShouldFollowReading(string word, string expected)
            => Assert.AreEqual(expected, KoreanParticle.EulReul(word));

        /// <summary>
        /// 끝이 영문 등 판정 불가한 문자면, 틀린 조사를 단정하지 않고 병기 형태로 떨어진다.
        /// </summary>
        [TestMethod]
        [DataRow("Internet Banking")]
        [DataRow("KB")]
        [DataRow("")]
        [DataRow(null)]
        public void EulReul_Undeterminable_ShouldFallBackToBothForms(string? word)
            => Assert.AreEqual("을(를)", KoreanParticle.EulReul(word));

        /// <summary>공백·괄호·따옴표로 끝나면 그 앞의 판정 가능한 글자로 판정한다.</summary>
        [TestMethod]
        [DataRow("우리은행 (개인)", "을")]   // 인 - 받침 ㄴ
        [DataRow("카카오뱅크 ", "를")]       // 크 - 받침 없음
        public void EulReul_TrailingPunctuation_ShouldUsePrecedingCharacter(string word, string expected)
            => Assert.AreEqual(expected, KoreanParticle.EulReul(word));

        [TestMethod]
        public void HasJongseong_ShouldReportNullWhenUndeterminable()
        {
            Assert.IsTrue(KoreanParticle.HasJongseong("뱅킹"));
            Assert.IsFalse(KoreanParticle.HasJongseong("뱅크"));
            Assert.IsNull(KoreanParticle.HasJongseong("Bank"));
        }
    }
}
