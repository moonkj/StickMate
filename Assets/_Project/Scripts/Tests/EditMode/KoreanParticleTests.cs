using System.Collections.Generic;
using NUnit.Framework;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 한국어 조사 자동 선택(<see cref="KoreanParticle"/>) 검사 — 2026-09-01.
    ///
    /// ============================================================================
    /// 왜 "실제 장비 이름"으로 검사하는가
    /// ============================================================================
    /// 이 유틸이 존재하는 이유는 <c>베레모은(는) 자동 해제</c> 같은 문구 때문이다. 즉 <b>입력이
    /// 장비 이름</b>이라는 것이 이 기능의 전제다. 그래서 예시를 지어내지 않고
    /// <see cref="ItemCatalog"/>(= <c>Resources/Items</c>의 실제 에셋)에서 이름을 읽어 온다.
    /// 아이템 이름이 바뀌면 <see cref="표에_적은_이름이_실제로_카탈로그에_있다"/>가 먼저 빨개져
    /// "표를 갱신하라"고 말한다 — 표가 낡은 채 조용히 통과하는 경로를 막는다.
    ///
    /// ============================================================================
    /// 공허한 통과 방지
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><see cref="카탈로그에_세_가지_받침_유형이_모두_들어_있다"/> — 실제 이름 표본이
    ///         받침없음/ㄹ받침/그밖의받침 <b>세 갈래를 모두</b> 지나가는지 본다. 한 갈래만 있으면
    ///         아래 검사들은 분기 하나만 확인하고 초록이 된다.</item>
    ///   <item><see cref="네거티브_컨트롤_판정이_이름에_따라_실제로_갈린다"/> — 상수를 돌려주는
    ///         구현으로 바꿔도 통과하지 않는지 직접 확인한다.</item>
    /// </list>
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립(순수 문자열 계산).</para>
    /// </summary>
    public sealed class KoreanParticleTests
    {
        /// <summary>실제 장비 이름 하나에 대한 기대값 한 줄. 사람이 소리 내어 읽어 채운 표다.</summary>
        private readonly struct Row
        {
            public readonly string Name;
            public readonly string Topic;        // 은 / 는
            public readonly string Subject;      // 이 / 가
            public readonly string Objective;    // 을 / 를
            public readonly string Comitative;   // 과 / 와
            public readonly string Instrumental; // 으로 / 로

            public Row(string name, string topic, string subject, string objective,
                string comitative, string instrumental)
            {
                Name = name; Topic = topic; Subject = subject; Objective = objective;
                Comitative = comitative; Instrumental = instrumental;
            }
        }

        /// <summary>
        /// <b>실제 장비/외형 아이템 이름</b>(Resources/Items의 displayName)만 골라 담는다.
        /// ㄹ 받침(고글/물방울/포니테일)을 일부러 여러 개 넣었다 — '로/으로'의 예외가 이 유틸에서
        /// 가장 틀리기 쉬운 한 줄이기 때문이다.
        /// </summary>
        private static readonly Row[] RealItemNames =
        {
            //      이름            은/는   이/가   을/를   과/와   으로/로
            new Row("베레모",        "는",  "가",  "를",  "와",  "로"),    // 모 — 받침 없음
            new Row("천모자",        "는",  "가",  "를",  "와",  "로"),    // 자 — 받침 없음
            new Row("리틀스틱메이트", "는",  "가",  "를",  "와",  "로"),    // 트 — 받침 없음
            new Row("나비넥타이",    "는",  "가",  "를",  "와",  "로"),    // 이 — 받침 없음
            new Row("왕관",          "은",  "이",  "을",  "과",  "으로"),  // 관 — ㄴ 받침
            new Row("배낭",          "은",  "이",  "을",  "과",  "으로"),  // 낭 — ㅇ 받침
            new Row("발자국",        "은",  "이",  "을",  "과",  "으로"),  // 국 — ㄱ 받침
            new Row("나뭇잎",        "은",  "이",  "을",  "과",  "으로"),  // 잎 — ㅍ 받침
            new Row("고글",          "은",  "이",  "을",  "과",  "로"),    // 글 — ★ ㄹ 받침 = '로'
            new Row("물방울",        "은",  "이",  "을",  "과",  "로"),    // 울 — ★ ㄹ 받침
            new Row("포니테일",      "은",  "이",  "을",  "과",  "로"),    // 일 — ★ ㄹ 받침
            new Row("펜던트 목걸이", "는",  "가",  "를",  "와",  "로"),    // 공백이 있는 이름
        };

        // ====================================================================
        // ① 실제 장비 이름
        // ====================================================================

        [Test]
        public void 표에_적은_이름이_실제로_카탈로그에_있다()
        {
            var catalogNames = new HashSet<string>();
            IReadOnlyList<ItemCatalogEntry> entries = ItemCatalog.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] == null) continue;
                catalogNames.Add(entries[i].DisplayName);
            }

            Assert.Greater(catalogNames.Count, 0,
                "카탈로그에서 이름을 하나도 읽지 못했습니다 — 이 파일의 '실제 이름으로 검증한다'는 전제가 " +
                "성립하지 않습니다(Resources/Items 로드 실패).");

            for (int i = 0; i < RealItemNames.Length; i++)
            {
                string name = RealItemNames[i].Name;
                Assert.IsTrue(catalogNames.Contains(name),
                    $"표의 '{name}'이 카탈로그에 없습니다 — 아이템 이름이 바뀌었거나 삭제됐습니다. " +
                    "이 표는 '지어낸 예시'가 아니라 실제 이름이어야 의미가 있으므로 표를 갱신하세요.");
            }
        }

        [Test]
        public void 실제_장비_이름의_조사가_전부_맞는다()
        {
            for (int i = 0; i < RealItemNames.Length; i++)
            {
                Row r = RealItemNames[i];
                Assert.AreEqual(r.Topic, KoreanParticle.Topic(r.Name), $"'{r.Name}' 은/는");
                Assert.AreEqual(r.Subject, KoreanParticle.Subject(r.Name), $"'{r.Name}' 이/가");
                Assert.AreEqual(r.Objective, KoreanParticle.Objective(r.Name), $"'{r.Name}' 을/를");
                Assert.AreEqual(r.Comitative, KoreanParticle.Comitative(r.Name), $"'{r.Name}' 과/와");
                Assert.AreEqual(r.Instrumental, KoreanParticle.Instrumental(r.Name), $"'{r.Name}' 으로/로");
            }
        }

        [Test]
        public void 신고된_문구가_실제로_고쳐진다()
        {
            // 리더가 인용한 원문: "같은 카테고리의 베레모은(는) 자동 해제"
            Assert.AreEqual("베레모는", KoreanParticle.Attach("베레모", KoreanParticle.Josa.Topic));
            Assert.AreEqual("왕관은", KoreanParticle.Attach("왕관", KoreanParticle.Josa.Topic));
        }

        [Test]
        public void 카탈로그에_세_가지_받침_유형이_모두_들어_있다()
        {
            bool vowel = false, rieul = false, consonant = false;
            IReadOnlyList<ItemCatalogEntry> entries = ItemCatalog.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] == null) continue;
                switch (KoreanParticle.ResolveEnding(entries[i].DisplayName))
                {
                    case KoreanParticle.Ending.Vowel: vowel = true; break;
                    case KoreanParticle.Ending.Rieul: rieul = true; break;
                    case KoreanParticle.Ending.Consonant: consonant = true; break;
                }
            }

            Assert.IsTrue(vowel && rieul && consonant,
                $"실제 이름 표본이 세 갈래를 모두 지나지 않습니다(받침없음={vowel}, ㄹ={rieul}, " +
                $"그밖={consonant}) — 그러면 이 파일의 검사는 분기 하나만 확인하고 초록이 됩니다.");
        }

        [Test]
        public void 카탈로그의_모든_이름이_빈_조사를_내지_않는다()
        {
            IReadOnlyList<ItemCatalogEntry> entries = ItemCatalog.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] == null) continue;
                string name = entries[i].DisplayName;
                Assert.IsNotEmpty(KoreanParticle.Topic(name), $"'{name}'의 은/는이 비었습니다.");
                Assert.IsNotEmpty(KoreanParticle.Instrumental(name), $"'{name}'의 으로/로가 비었습니다.");
            }
        }

        // ====================================================================
        // ② 영문·숫자로 끝나는 이름 (장비 이름 뒤에 LV.9 같은 꼬리표가 붙는다)
        // ====================================================================

        [TestCase("LV.0", "은", "으로")]   // 영(ㅇ)
        [TestCase("LV.1", "은", "로")]     // 일(ㄹ) — ★ 예외
        [TestCase("LV.2", "는", "로")]     // 이
        [TestCase("LV.3", "은", "으로")]   // 삼(ㅁ)
        [TestCase("LV.4", "는", "로")]     // 사
        [TestCase("LV.5", "는", "로")]     // 오
        [TestCase("LV.6", "은", "으로")]   // 육(ㄱ)
        [TestCase("LV.7", "은", "로")]     // 칠(ㄹ) — ★ 예외
        [TestCase("LV.8", "은", "로")]     // 팔(ㄹ) — ★ 예외
        [TestCase("LV.9", "는", "로")]     // 구
        public void 숫자로_끝나면_읽는_소리로_판정한다(string name, string topic, string instrumental)
        {
            Assert.AreEqual(topic, KoreanParticle.Topic(name), $"'{name}' 은/는");
            Assert.AreEqual(instrumental, KoreanParticle.Instrumental(name), $"'{name}' 으로/로");
        }

        [TestCase("베레모 L", "은", "로")]     // 엘(ㄹ)
        [TestCase("베레모 R", "은", "로")]     // 알(ㄹ)
        [TestCase("베레모 M", "은", "으로")]   // 엠(ㅁ)
        [TestCase("베레모 N", "은", "으로")]   // 엔(ㄴ)
        [TestCase("베레모 A", "는", "로")]     // 에이
        [TestCase("베레모 Z", "는", "로")]     // 제트
        [TestCase("베레모 l", "은", "로")]     // 소문자도 같은 소리
        public void 영문자로_끝나면_알파벳_이름의_소리로_판정한다(string name, string topic, string instrumental)
        {
            Assert.AreEqual(topic, KoreanParticle.Topic(name), $"'{name}' 은/는");
            Assert.AreEqual(instrumental, KoreanParticle.Instrumental(name), $"'{name}' 으로/로");
        }

        // ====================================================================
        // ③ 읽지 않는 글자 / 빈 입력
        // ====================================================================

        [TestCase("펜던트 목걸이(신규)", "는")]  // 괄호를 건너뛰면 마지막 소리는 '규'(받침 없음)
        [TestCase("왕관!", "은")]
        [TestCase("왕관   ", "은")]
        [TestCase("베레모…", "는")]
        public void 읽지_않는_글자는_건너뛰고_그_앞을_본다(string name, string topic)
        {
            Assert.AreEqual(topic, KoreanParticle.Topic(name), $"'{name}' 은/는");
        }

        [Test]
        public void 판정할_글자가_없으면_받침_없음으로_본다()
        {
            Assert.AreEqual(KoreanParticle.Ending.Vowel, KoreanParticle.ResolveEnding(null));
            Assert.AreEqual(KoreanParticle.Ending.Vowel, KoreanParticle.ResolveEnding(string.Empty));
            Assert.AreEqual(KoreanParticle.Ending.Vowel, KoreanParticle.ResolveEnding("!!!"));
            Assert.AreEqual("는", KoreanParticle.Topic("!!!"));
            Assert.AreEqual("로", KoreanParticle.Instrumental("!!!"));
        }

        [Test]
        public void HasFinalConsonant는_ResolveEnding과_모순되지_않는다()
        {
            foreach (string name in new[] { "베레모", "왕관", "고글", "LV.9", "" })
            {
                bool expected = KoreanParticle.ResolveEnding(name) != KoreanParticle.Ending.Vowel;
                Assert.AreEqual(expected, KoreanParticle.HasFinalConsonant(name), $"'{name}'");
            }
        }

        // ====================================================================
        // ④ 네거티브 컨트롤
        // ====================================================================

        /// <summary>
        /// 위 검사들이 "항상 참인 단언"이 아님을 증명한다: 판정이 <b>이름에 따라 실제로 갈리고</b>,
        /// 특히 <b>ㄹ 받침만 '로'</b>가 되는 예외 분기가 살아 있는지 직접 본다. 구현을 상수 반환으로
        /// 바꾸면 이 검사가 가장 먼저 깨진다.
        /// </summary>
        [Test]
        public void 네거티브_컨트롤_판정이_이름에_따라_실제로_갈린다()
        {
            Assert.AreNotEqual(KoreanParticle.Topic("베레모"), KoreanParticle.Topic("왕관"),
                "받침 없는 이름과 있는 이름의 은/는이 같습니다 — 판정이 이름을 보지 않고 있습니다.");

            // ★ '고글'은 받침이 있는데도(=은/이/을/과) '으로'가 아니라 '로'다. 이 두 줄이 함께
            //   성립해야 ㄹ 예외가 실제로 구현돼 있다는 뜻이다.
            Assert.AreEqual("은", KoreanParticle.Topic("고글"), "'고글'은 ㄹ 받침이 있으므로 '은'입니다.");
            Assert.AreEqual("로", KoreanParticle.Instrumental("고글"),
                "'고글'의 도구격이 '으로'입니다 — ㄹ 받침 예외가 구현돼 있지 않습니다.");
            Assert.AreNotEqual(KoreanParticle.Instrumental("고글"), KoreanParticle.Instrumental("왕관"),
                "ㄹ 받침과 그 밖의 받침이 같은 도구격을 냅니다 — 예외 분기가 죽어 있습니다.");
        }
    }
}
