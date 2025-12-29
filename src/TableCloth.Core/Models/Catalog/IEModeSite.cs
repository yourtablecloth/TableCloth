namespace TableCloth.Models.Catalog
{
    /// <summary>
    /// Internet Explorer 모드로 표시할 개별 사이트의 정보를 담는 XML를 나타냅니다.
    /// </summary>
    public sealed class IEModeSite
    {
        /// <summary>
        /// 도메인
        /// </summary>
        public string Domain { get; set; } = null;

        /// <summary>
        /// 동작 모드
        /// </summary>
        public string Mode { get; set; } = null;

        /// <summary>
        /// 브라우저 실행 방법
        /// </summary>
        public string OpenIn { get; set; } = null;
    }
}
