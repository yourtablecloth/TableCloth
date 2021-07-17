using System;
using System.ComponentModel;

namespace TableCloth.Models
{
    [Serializable]
    public enum InternetServiceCategory : short
    {
        [Description("기타")]
        Other = 0,

        [Description("인터넷 뱅킹")]
        Banking,

        [Description("금융 서비스")]
        Financing,

        [Description("신용 카드")]
        CreditCard,

        [Description("공공")]
        Government,
    }
}
