namespace TableCloth.Models.Catalog
{
    /// <summary>
    /// 카탈로그 상의 인터넷 서비스 분류를 나타냅니다.
    /// </summary>
    public enum CatalogInternetServiceCategory : short
    {
        /// <summary>
        /// 기타
        /// </summary>
        [EnumDisplayOrder(Order = 8)]
        Other = 0,

        /// <summary>
        /// 인터넷 뱅킹
        /// </summary>
        [EnumDisplayOrder(Order = 1)]
        Banking,

        /// <summary>
        /// 금융
        /// </summary>
        [EnumDisplayOrder(Order = 2)]
        Financing,

        /// <summary>
        /// 투자
        /// </summary>
        [EnumDisplayOrder(Order = 3)]
        Security,

        /// <summary>
        /// 보험
        /// </summary>
        [EnumDisplayOrder(Order = 4)]
        Insurance,

        /// <summary>
        /// 신용 카드
        /// </summary>
        [EnumDisplayOrder(Order = 5)]
        CreditCard,

        /// <summary>
        /// 정부, 공공기관
        /// </summary>
        [EnumDisplayOrder(Order = 6)]
        Government,

        /// <summary>
        /// 교육
        /// </summary>
        [EnumDisplayOrder(Order = 7)]
        Education,
    }
}
