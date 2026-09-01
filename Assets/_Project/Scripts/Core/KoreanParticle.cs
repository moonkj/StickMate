namespace StickMate.Core
{
    /// <summary>
    /// ★ 한국어 조사 자동 선택 — 받침 유무로 은/는·이/가·을/를·과/와·으로/로를 고른다.
    ///
    /// ============================================================================
    /// 왜 필요한가
    /// ============================================================================
    /// 로그와 화면 문구에 <c>{이름}은(는)</c> 같은 표기가 남아 있다
    /// (예: "같은 카테고리의 베레모은(는) 자동 해제"). 괄호 표기는 <b>둘 다 틀린 문장</b>을 보여
    /// 주는 타협이고, 이름이 사용자 눈에 그대로 보이는 이 앱에서는 특히 눈에 띈다.
    /// 이 클래스는 그 선택을 <b>순수 함수</b>로 만든다 — 상태도, 로케일도, 할당도 없다.
    ///
    /// ============================================================================
    /// 판정 규칙 (정의를 여기 한 곳에만 둔다)
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>한글 음절</b>(U+AC00~U+D7A3): 종성 = <c>(code - 0xAC00) % 28</c>.
    ///         0이면 받침 없음, 8이면 ㄹ 받침, 그 외는 그 밖의 받침.</item>
    ///   <item><b>숫자</b>: 한국어로 읽은 소리의 종성을 쓴다 —
    ///         0 영(ㅇ) / 1 일(ㄹ) / 2 이(없음) / 3 삼(ㅁ) / 4 사(없음) / 5 오(없음) /
    ///         6 육(ㄱ) / 7 칠(ㄹ) / 8 팔(ㄹ) / 9 구(없음).
    ///         장비 이름 뒤에 <c>LV.9</c> 같은 꼬리표가 붙을 수 있어서 반드시 정의해야 한다.</item>
    ///   <item><b>영문자</b>: 알파벳 이름을 한국어로 읽은 소리의 종성.
    ///         받침이 있는 글자는 <b>L(엘) M(엠) N(엔) R(알)</b> 넷뿐이고, 그중
    ///         <b>L·R은 ㄹ 받침</b>이다. 나머지 22자는 전부 받침이 없다
    ///         (A 에이 / B 비 / C 씨 / D 디 / E 이 / F 에프 / G 지 / H 에이치 / I 아이 /
    ///          J 제이 / K 케이 / O 오 / P 피 / Q 큐 / S 에스 / T 티 / U 유 / V 브이 /
    ///          W 더블유 / X 엑스 / Y 와이 / Z 제트). 대소문자를 가리지 않는다.</item>
    ///   <item><b>그 밖의 문자</b>(공백·괄호·마침표·기호·한자·가나 등): <b>건너뛰고</b> 그 앞을 본다.
    ///         "펜던트 목걸이(신규)"처럼 꾸밈이 붙어도 실제로 읽는 마지막 소리로 판정하기 위해서다.
    ///         끝까지 판정할 글자가 없으면(빈 문자열, 기호만 있는 이름) <b>받침 없음</b>으로 본다 —
    ///         읽을 수 없는 것에 "은/이/을/과/으로"를 붙이면 더 어색하다.</item>
    /// </list>
    ///
    /// <para><b>'로/으로'만 규칙이 다르다</b>: 받침이 없을 때뿐 아니라 <b>ㄹ 받침일 때도 '로'</b>다
    /// (고글로, 물방울로, 포니테일로). 그래서 이 클래스는 "받침이 있나?"(bool)가 아니라
    /// <see cref="Ending"/> <b>3분류</b>를 1차 결과로 둔다 — bool로 두면 ㄹ 예외를 표현할 수 없다.</para>
    ///
    /// <para><b>할당</b>: <see cref="Of"/>는 리터럴을 그대로 돌려주므로 할당이 0이다.
    /// 문자열을 붙이는 <see cref="Attach"/>만 새 문자열을 만든다 — <c>Update()</c> 안에서 쓰지 마라.</para>
    ///
    /// <para><b>플랫폼</b>: 완전한 플랫폼 중립(순수 문자열 계산). Windows/macOS/iOS 동일.</para>
    /// </summary>
    public static class KoreanParticle
    {
        /// <summary>말의 마지막 소리 3분류. bool이 아닌 이유는 위 '로/으로' 문단 참고.</summary>
        public enum Ending
        {
            /// <summary>받침 없음(모음으로 끝남) — 는/가/를/와/로.</summary>
            Vowel = 0,

            /// <summary>ㄹ 받침 — 은/이/을/과이지만 <b>'로'</b>다.</summary>
            Rieul = 1,

            /// <summary>ㄹ이 아닌 받침 — 은/이/을/과/으로.</summary>
            Consonant = 2,
        }

        /// <summary>고를 조사 쌍.</summary>
        public enum Josa
        {
            /// <summary>은 / 는 (주제).</summary>
            Topic = 0,

            /// <summary>이 / 가 (주격).</summary>
            Subject = 1,

            /// <summary>을 / 를 (목적격).</summary>
            Objective = 2,

            /// <summary>과 / 와 (접속·공동).</summary>
            Comitative = 3,

            /// <summary>으로 / 로 (도구·방향). <b>ㄹ 받침은 '로'</b>다.</summary>
            Instrumental = 4,
        }

        private const int HangulBase = 0xAC00;
        private const int HangulLast = 0xD7A3;
        private const int JongseongCount = 28;

        /// <summary>ㄹ 종성의 인덱스. <c>(code - 0xAC00) % 28</c>이 이 값이면 ㄹ 받침이다.</summary>
        private const int RieulJongseong = 8;

        /// <summary><paramref name="word"/>의 마지막 <b>읽히는</b> 소리를 3분류한다.
        /// 판정할 글자가 하나도 없으면 <see cref="Ending.Vowel"/>.</summary>
        public static Ending ResolveEnding(string word)
        {
            if (string.IsNullOrEmpty(word)) return Ending.Vowel;

            for (int i = word.Length - 1; i >= 0; i--)
            {
                char c = word[i];

                if (c >= HangulBase && c <= HangulLast)
                {
                    int jong = (c - HangulBase) % JongseongCount;
                    if (jong == 0) return Ending.Vowel;
                    return jong == RieulJongseong ? Ending.Rieul : Ending.Consonant;
                }

                if (c >= '0' && c <= '9') return DigitEnding(c - '0');

                if (c >= 'a' && c <= 'z') return LetterEnding((char)(c - ('a' - 'A')));
                if (c >= 'A' && c <= 'Z') return LetterEnding(c);

                // 읽지 않는 문자(공백/기호/괄호/한자 등)는 건너뛰고 그 앞을 본다.
            }
            return Ending.Vowel;
        }

        /// <summary>받침이 하나라도 있는가. '로/으로'는 이 값으로 고르면 <b>틀린다</b>
        /// (ㄹ 예외) — 그 경우엔 <see cref="ResolveEnding"/>를 써라.</summary>
        public static bool HasFinalConsonant(string word) => ResolveEnding(word) != Ending.Vowel;

        /// <summary>이름 뒤에 붙일 조사만 돌려준다(문자열을 새로 만들지 않는다).</summary>
        public static string Of(string word, Josa josa) => For(ResolveEnding(word), josa);

        /// <summary>이미 분류가 끝난 경우의 진입점 — 같은 이름으로 여러 조사를 고를 때 재계산을 막는다.</summary>
        public static string For(Ending ending, Josa josa)
        {
            bool hasFinal = ending != Ending.Vowel;
            switch (josa)
            {
                case Josa.Topic:      return hasFinal ? "은" : "는";
                case Josa.Subject:    return hasFinal ? "이" : "가";
                case Josa.Objective:  return hasFinal ? "을" : "를";
                case Josa.Comitative: return hasFinal ? "과" : "와";
                // ★ 유일한 예외 — ㄹ 받침은 받침이 있는데도 '로'다.
                case Josa.Instrumental: return ending == Ending.Consonant ? "으로" : "로";
                default: return string.Empty;
            }
        }

        /// <summary>"이름 + 조사" 한 덩어리. 새 문자열을 만드므로 매 프레임 경로에서 쓰지 마라.</summary>
        public static string Attach(string word, Josa josa) => word + Of(word, josa);

        // 자주 쓰는 쌍의 짧은 이름 — 호출부가 enum을 몰라도 읽히게 한다.
        public static string Topic(string word) => Of(word, Josa.Topic);
        public static string Subject(string word) => Of(word, Josa.Subject);
        public static string Objective(string word) => Of(word, Josa.Objective);
        public static string Comitative(string word) => Of(word, Josa.Comitative);
        public static string Instrumental(string word) => Of(word, Josa.Instrumental);

        /// <summary>숫자 한 자리를 한국어로 읽었을 때의 종성 분류(위 클래스 문서의 표).</summary>
        private static Ending DigitEnding(int digit)
        {
            switch (digit)
            {
                case 0: return Ending.Consonant; // 영(ㅇ)
                case 1: return Ending.Rieul;     // 일(ㄹ)
                case 3: return Ending.Consonant; // 삼(ㅁ)
                case 6: return Ending.Consonant; // 육(ㄱ)
                case 7: return Ending.Rieul;     // 칠(ㄹ)
                case 8: return Ending.Rieul;     // 팔(ㄹ)
                default: return Ending.Vowel;    // 2 이 / 4 사 / 5 오 / 9 구
            }
        }

        /// <summary>영문자 이름을 한국어로 읽었을 때의 종성 분류. 받침이 있는 글자는 L M N R 넷뿐이다.</summary>
        private static Ending LetterEnding(char upper)
        {
            switch (upper)
            {
                case 'L': return Ending.Rieul;      // 엘
                case 'R': return Ending.Rieul;      // 알
                case 'M': return Ending.Consonant;  // 엠
                case 'N': return Ending.Consonant;  // 엔
                default: return Ending.Vowel;
            }
        }
    }
}
