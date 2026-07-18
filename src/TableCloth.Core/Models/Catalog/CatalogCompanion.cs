using System;

namespace TableCloth.Models.Catalog
{
    /// <summary>
    /// 서비스에 관계없이 공용으로 사용할 수 있는 소프트웨어에 대한 정보를 담는 XML 요소를 나타냅니다.
    /// </summary>
    public sealed class CatalogCompanion
    {
        private static readonly char[] FilterTextSeparators = new char[] { ',', };

        /// <summary>
        /// 고유 아이디 값
        /// </summary>
        public string Id { get; set; } = null;

        /// <summary>
        /// 사용자에게 표시될 이름
        /// </summary>
        public string DisplayName { get; set; } = null;

        /// <summary>
        /// 소프트웨어를 다운로드할 수 있는 URL
        /// </summary>
        public string Url { get; set; } = null;

        /// <summary>
        /// 설치 프로그램 실행 시 전달할 매개 변수
        /// </summary>
        public string Arguments { get; set; } = null;

        /// <summary>
        /// 보조 프로그램 목록의 키워드 필터에 사용됩니다. <see cref="CatalogInternetService.IsMatchedItem(object, string)"/>
        /// 와 동일하게 쉼표로 구분된 키워드 중 하나라도 걸리면 표시합니다(OR 매칭, 대소문자 무시).
        /// 아이콘/카테고리가 없는 단순 항목이므로 표시 이름·URL·아이디·설치 인자를 대상으로 검색합니다.
        /// </summary>
        public static bool IsMatchedItem(object item, string filterText)
        {
            var actualItem = item as CatalogCompanion;

            if (actualItem == null)
                return false;

            if (string.IsNullOrWhiteSpace(filterText))
                return true;

            var result = false;
            var splittedFilterText = filterText.Split(FilterTextSeparators, StringSplitOptions.RemoveEmptyEntries);

            foreach (var eachFilterText in splittedFilterText)
            {
                var keyword = eachFilterText.Trim();

                if (keyword.Length < 1)
                    continue;

                result |= ContainsKeyword(actualItem.DisplayName, keyword)
                    || ContainsKeyword(actualItem.Url, keyword)
                    || ContainsKeyword(actualItem.Id, keyword)
                    || ContainsKeyword(actualItem.Arguments, keyword);
            }

            return result;
        }

        // CatalogInternetService 와 달리 각 필드가 null 일 수 있어(기본값 null) null 안전하게 비교한다.
        private static bool ContainsKeyword(string source, string keyword)
            => !string.IsNullOrEmpty(source) && source.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) > (-1);
    }
}
