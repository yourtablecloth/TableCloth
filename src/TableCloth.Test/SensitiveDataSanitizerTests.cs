using TableCloth.ManagedAi;
using TableCloth.ManagedAi.OpenAi;

namespace TableCloth.Test;

[TestClass]
public sealed class SensitiveDataSanitizerTests
{
    [TestMethod]
    public void MasksKoreanIdentifiersAndAddressesWithoutKeepingDigits()
    {
        const string original = "주민등록번호 900101-1234567, 휴대전화 010-1234-5678, 집전화 02-1234-5678, 해외 +82 10 1234 5678\n" +
            "도로명 서울특별시 강남구 테헤란로 123 101동 202호\n" +
            "지번 경기도 성남시 분당구 정자동 12-3\n주소: 테헤란로 456 3층";
        var sanitized = SensitiveDataSanitizer.Sanitize(original);

        Assert.DoesNotContain("900101", sanitized);
        Assert.DoesNotContain("1234567", sanitized);
        Assert.DoesNotContain("010-1234-5678", sanitized);
        Assert.DoesNotContain("02-1234-5678", sanitized);
        Assert.DoesNotContain("+82 10 1234 5678", sanitized);
        Assert.DoesNotContain("테헤란로", sanitized);
        Assert.DoesNotContain("정자동", sanitized);
        Assert.Contains("[주민등록번호]", sanitized);
        Assert.Contains("[전화번호]", sanitized);
        Assert.Contains("[주소]", sanitized);
        Assert.AreEqual(sanitized, SensitiveDataSanitizer.Sanitize(sanitized));
    }

    [TestMethod]
    public void PreservesOrdinaryDatesAndUnrelatedText()
    {
        const string ordinary = "2026-10-01에 인증서가 몇 개 만료되나요? 주민등록등본 발급 방법도 알려 주세요.";
        Assert.AreEqual(ordinary, SensitiveDataSanitizer.Sanitize(ordinary));
        Assert.Contains("[주민등록번호]", SensitiveDataSanitizer.Sanitize("주민번호: 990231-1234567"));
        Assert.AreEqual("[주민등록번호]", SensitiveDataSanitizer.Sanitize("9001011234567"));
        Assert.AreEqual("[전화번호]", SensitiveDataSanitizer.Sanitize("01012345678"));
        Assert.AreEqual("[주소]", SensitiveDataSanitizer.Sanitize("서울시 강남구 역삼동 123-45"));
    }

    [TestMethod]
    public void PromptSanitizesDirectRequestsAndHistoryAfterCertificateGuard()
    {
        var prompt = OpenAiChatPromptFactory.Create(new("연락처 010-1234-5678",
            [new(AiChatRole.User, "주소: 테헤란로 123")], null));
        Assert.DoesNotContain("010-1234-5678", prompt);
        Assert.DoesNotContain("테헤란로 123", prompt);
        Assert.Contains("[전화번호]", prompt);
        Assert.Contains("[주소]", prompt);

        var error = Assert.ThrowsExactly<ManagedAiException>(() =>
            OpenAiChatPromptFactory.Create(new("인증서 주체명: 홍길동, 연락처 010-1234-5678", [], null)));
        Assert.AreEqual(AiFailureCode.InvalidQuery, error.Code);
    }
}
