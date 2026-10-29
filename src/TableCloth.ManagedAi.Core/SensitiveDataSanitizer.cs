using System.Text.RegularExpressions;

namespace TableCloth.ManagedAi;

public static class SensitiveDataSanitizer
{
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.IgnoreCase;
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly Regex ResidentNumber = new(
        @"(?<!\d)(?<birth>\d{6})[\s-]?(?<rest>[1-8]\d{6})(?!\d)", Options, MatchTimeout);
    private static readonly Regex LabeledResidentNumber = new(
        @"(?i)(?:주민등록번호|주민번호|RRN)\s*[:：]?\s*\d{6}[\s-]?\d{7}(?!\d)", Options, MatchTimeout);
    private static readonly Regex PhoneNumber = new(
        @"(?<!\d)(?:\+82[\s.-]?(?:0)?(?:10|11|16|17|18|19)|0(?:10|11|16|17|18|19|2|3[1-3]|4[1-4]|5[1-5]|6[1-4]|70))[\s.-]?\d{3,4}[\s.-]?\d{4}(?!\d)",
        Options, MatchTimeout);
    private const string Region = @"(?:[가-힣]{2,8}(?:특별시|광역시|특별자치시|특별자치도|도)|서울시|부산시|대구시|인천시|광주시|대전시|울산시|세종시|서울|부산|대구|인천|광주|대전|울산|세종|경기|강원|충북|충남|전북|전남|경북|경남|제주)";
    private const string District = @"(?:[가-힣]{1,12}(?:시|군|구)\s+){0,2}";
    private const string Detail = @"(?:\s*,?\s*[가-힣0-9]{1,20}(?:동|층|호)){0,3}";
    private static readonly Regex RoadAddress = new(
        @"(?<![가-힣0-9])" + Region + @"\s+" + District + @"[가-힣0-9·-]{2,30}(?:대로|로|길)\s+\d{1,5}(?:-\d{1,5})?" + Detail,
        Options, MatchTimeout);
    private static readonly Regex LotAddress = new(
        @"(?<![가-힣0-9])" + Region + @"\s+" + District + @"[가-힣0-9-]{1,20}(?:동|읍|면|리)\s+\d{1,5}(?:-\d{1,5})?" + Detail,
        Options, MatchTimeout);
    private static readonly Regex LabeledAddress = new(
        @"(?:주소|address)\s*[:：]\s*(?<value>[^\r\n,;]{5,120})", Options, MatchTimeout);
    private static readonly Regex RoadOrLotFragment = new(
        @"[가-힣0-9·-]{2,30}(?:대로|로|길)\s+\d{1,5}|[가-힣0-9-]{1,20}(?:동|읍|면|리)\s+\d{1,5}",
        Options, MatchTimeout);

    public static string Sanitize(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        try
        {
            var sanitized = RoadAddress.Replace(text, "[주소]");
            sanitized = LotAddress.Replace(sanitized, "[주소]");
            sanitized = LabeledAddress.Replace(sanitized, match =>
                RoadOrLotFragment.IsMatch(match.Groups["value"].Value) ? "주소: [주소]" : match.Value);
            sanitized = LabeledResidentNumber.Replace(sanitized, "[주민등록번호]");
            sanitized = ResidentNumber.Replace(sanitized, match =>
                IsPlausibleBirthDate(match) ? "[주민등록번호]" : match.Value);
            return PhoneNumber.Replace(sanitized, "[전화번호]");
        }
        catch (RegexMatchTimeoutException) { return "[민감정보]"; }
    }

    private static bool IsPlausibleBirthDate(Match match)
    {
        var birth = match.Groups["birth"].Value;
        var century = match.Groups["rest"].Value[0] is '3' or '4' or '7' or '8' ? 2000 : 1900;
        var year = century + int.Parse(birth[..2]);
        var month = int.Parse(birth.Substring(2, 2));
        var day = int.Parse(birth.Substring(4, 2));
        return month is >= 1 and <= 12 && day >= 1 && day <= DateTime.DaysInMonth(year, month);
    }
}
