using System;

namespace TableCloth
{
    /// <summary>
    /// 앞 단어의 받침 유무에 따라 한국어 조사를 고른다.
    /// </summary>
    /// <remarks>
    /// <para>
    /// 한글 음절(U+AC00~U+D7A3)은 <c>(초성 × 21 × 28) + (중성 × 28) + 종성</c> 순으로 규칙적으로
    /// 배열되어 있다. 따라서 <c>(코드 - 0xAC00) % 28</c> 이 0 이면 받침이 없다.
    /// </para>
    /// <para>
    /// 끝 글자가 한글이 아니면(영문·기호 등) 읽는 음가를 알 수 없으므로 <b>병기 형태</b>
    /// (예: <c>을(를)</c>)로 돌려준다 — 틀린 조사를 단정하는 것보다 낫다. 숫자는 읽는 음가가
    /// 정해져 있어 예외적으로 판정한다(1=일, 3=삼, 6=육, 7=칠, 8=팔, 0=영 → 받침 있음).
    /// </para>
    /// </remarks>
    public static class KoreanParticle
    {
        private const char HangulSyllableFirst = '가';
        private const char HangulSyllableLast = '힣';
        private const int JongseongCount = 28;

        /// <summary>목적격 조사: 받침 있으면 을, 없으면 를.</summary>
        public static string EulReul(string? word)
            => Select(word, "을", "를");

        /// <summary>주격 조사: 받침 있으면 이, 없으면 가.</summary>
        public static string IGa(string? word)
            => Select(word, "이", "가");

        /// <summary>보조사: 받침 있으면 은, 없으면 는.</summary>
        public static string EunNeun(string? word)
            => Select(word, "은", "는");

        /// <summary>
        /// 받침 유무에 따라 <paramref name="withJongseong"/> / <paramref name="withoutJongseong"/> 중 하나를
        /// 고른다. 판정할 수 없으면 <c>"을(를)"</c> 같은 병기 형태를 돌려준다.
        /// </summary>
        public static string Select(string? word, string withJongseong, string withoutJongseong)
        {
            var hasJongseong = HasJongseong(word);

            if (!hasJongseong.HasValue)
                return $"{withJongseong}({withoutJongseong})";

            return hasJongseong.Value ? withJongseong : withoutJongseong;
        }

        /// <summary>
        /// 마지막 글자에 받침이 있는지. 판정할 수 없으면 <see langword="null"/>.
        /// </summary>
        public static bool? HasJongseong(string? word)
        {
            if (string.IsNullOrEmpty(word))
                return null;

            // 괄호나 공백으로 끝나는 표기(예: "우리은행 (개인)")를 감안해 뒤에서부터 판정 가능한
            // 글자를 찾는다. 끝까지 못 찾으면 판정 불가로 둔다.
            for (var i = word!.Length - 1; i >= 0; i--)
            {
                var ch = word[i];

                if (ch >= HangulSyllableFirst && ch <= HangulSyllableLast)
                    return (ch - HangulSyllableFirst) % JongseongCount != 0;

                if (ch >= '0' && ch <= '9')
                    return DigitHasJongseong(ch);

                // 공백·괄호·따옴표 등은 건너뛰고 그 앞 글자로 판정한다.
                if (char.IsWhiteSpace(ch) || char.IsPunctuation(ch) || char.IsSymbol(ch))
                    continue;

                // 영문 등 그 밖의 문자는 읽는 음가가 관례에 따라 갈리므로 단정하지 않는다.
                return null;
            }

            return null;
        }

        /// <summary>숫자를 읽었을 때 받침이 있는지. 일·삼·육·칠·팔·영은 받침이 있다.</summary>
        private static bool DigitHasJongseong(char digit)
            => digit switch
            {
                '0' => true,   // 영
                '1' => true,   // 일
                '3' => true,   // 삼
                '6' => true,   // 육
                '7' => true,   // 칠
                '8' => true,   // 팔
                _ => false,    // 이·사·오·구
            };
    }
}
