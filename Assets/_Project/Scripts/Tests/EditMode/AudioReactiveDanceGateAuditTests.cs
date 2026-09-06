using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★★ <b>「집중 모드가 춤보다 우선」의 구조적 잠금</b> — 2026-09-03.
    ///
    /// <para>사용자 확정: <i>"다만 문제가 집중모드일때는 춤모션이 아닌 집중모드 모션이 우선이야"</i>.</para>
    ///
    /// ============================================================================
    /// 이 파일이 잡으려는 것은 <b>«통과하는 오답»</b> 하나다
    /// ============================================================================
    /// 가장 자연스러운 구현(<c>CurrentStateId == FocusStart</c>)은 <b>25분 세션 중 2초만 막는다</b> —
    /// Focus 4종이 전부 <c>TimedSpectacleState</c>라 2.0초 뒤 <c>Idle</c>로 빠지기 때문이다(0.13%).
    /// 그리고 <b>그 버그는 «세션 시작 직후»를 재는 테스트에서 통과한다.</b> 성공한 측정과 똑같이
    /// 생긴 실패한 측정 — 이 저장소의 교과서적 형태다.
    /// ⇒ 그래서 여기서는 <b>어떤 신호를 읽는가</b>를 소스에서 못박는다.
    ///
    /// <para><b>부재 단언에는 반드시 존재 대조를 붙인다</b>(CLAUDE.md). 아래 모든 니들은
    /// <c>nameof</c>로 프로덕션 멤버를 참조하므로, 이름이 바뀌면 조용히 초록이 되는 대신
    /// <b>이 파일이 컴파일되지 않는다</b>.</para>
    /// </summary>
    public sealed class AudioReactiveDanceGateAuditTests
    {
        private static string CoreDirectory =>
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Core");

        private const string GateFile = "AudioReactiveDanceGate.cs";
        private const string HiddenGateCall = nameof(HiddenCharacterCommandGate) + "." + nameof(HiddenCharacterCommandGate.BlocksNow);

        // ====================================================================
        // ① 어떤 신호를 읽는가
        // ====================================================================

        /// <summary>
        /// ★★ 집중 모드 판정은 <b>세션 플래그</b>여야 한다 — 상태 ID가 아니다.
        /// </summary>
        [Test]
        public void 집중_모드_판정은_상태ID가_아니라_세션_플래그를_읽는다()
        {
            string body = GateBody();

            string session = "." + nameof(FocusWatchDirector.IsSessionActive);
            StringAssert.Contains(session, body,
                $"게이트가 집중 세션 플래그({session})를 읽지 않습니다:\n  " + body.Trim() +
                "\n이 값만이 세션 전 구간에서 참입니다.");

            string stateId = nameof(StickmanStateMachine.CurrentStateId);
            Assert.AreEqual(-1, body.IndexOf(stateId, StringComparison.Ordinal),
                $"★ 게이트가 상태 ID({stateId})로 집중 모드를 판정합니다:\n  " + body.Trim() +
                "\nFocus 4종은 전부 2.0초짜리 순수 타이머라 25분 세션의 0.13%만 덮습니다 — " +
                "나머지 1,498초 동안 캐릭터가 춤춥니다. 그리고 이 결함은 세션 시작 직후를 재는 " +
                "테스트에서 **통과합니다**.");

            string enumName = nameof(StickmanStateId);
            Assert.AreEqual(-1, body.IndexOf(enumName, StringComparison.Ordinal),
                $"★ 게이트가 {enumName}을 비교합니다 — 위와 같은 결함입니다:\n  " + body.Trim());
        }

        /// <summary>
        /// ★ 숨김 판정을 <b>다시 쓰지 않고 재사용</b>하는가. <i>"판정을 두 벌로 만들면 반드시
        /// 갈라진다"</i>(<see cref="HiddenCharacterCommandGate"/> 클래스 문서).
        /// </summary>
        [Test]
        public void 숨김_판정은_기존_술어를_재사용하고_다시_구현하지_않는다()
        {
            string body = GateBody();

            StringAssert.Contains(HiddenGateCall, body,
                "★ 춤 게이트가 숨김 게이트를 호출하지 않습니다:\n  " + body.Trim() +
                "\n보이지 않는 캐릭터가 춤추면 상태와 화면이 갈라지고, 그 상태에서 파생된 말풍선이 " +
                "주인 없이 뜹니다(절대 불변 원칙 1).");

            string suspended = "." + nameof(StickmanAgent.IsSuspended);
            Assert.AreEqual(-1, body.IndexOf(suspended, StringComparison.Ordinal),
                $"★ 춤 게이트가 {suspended}를 **직접** 읽습니다:\n  " + body.Trim() +
                "\n판정이 두 벌이 되면 한쪽만 고쳐지는 날이 옵니다 — " + HiddenGateCall + "를 부르십시오.");
        }

        /// <summary>
        /// ★★★ <b>2026-09-06 리더 판정으로 이 검사의 내용이 바뀌었다.</b> 예전 판은
        /// <i>"전체화면 축을 아예 읽지 마라"</i>였고, 지금은 <b>"어느 창구로 읽는가"</b>를 잠근다.
        ///
        /// ============================================================================
        /// 무엇이 바뀌었고, 무엇이 <b>안</b> 바뀌었나
        /// ============================================================================
        /// <b>바뀐 것</b>: 등급 1(게임이 아닌 전체화면 앱 — 줌·팀즈·키노트)에서 <b>자동 발동 춤만</b>
        /// 막는다. macOS 프로브가 «소리»가 아니라 «출력 스트림 개방»을 보기 때문에 <b>무음 회의에서도</b>
        /// 게이트가 열리는 것이 실측으로 확인됐고, 그 앱들이 거의 항상 전체화면이라는 사실이 값싼
        /// 상관 신호를 준다.
        ///
        /// <para><b>안 바뀐 것</b>: 캐릭터는 등급 1에서 <b>계속 보이고 계속 걸어다닌다.</b>
        /// 2026-08-31 신고(<i>"엑셀 전체화면에서 캐릭터가 없어져버림"</i>)의 회귀를 막는 함수는
        /// <c>ForeignFullscreenTierPolicy.SuspendsCharacter</c> 하나이고, 그 함수는 한 글자도 안 바뀌었다.
        /// 그 계약을 <see cref="등급1은_춤만_막고_캐릭터는_그대로_둔다"/>가 <b>실행해서</b> 다시 잰다.</para>
        ///
        /// ============================================================================
        /// ★ 그래도 <b>표면 축 두 개는 여전히 금지</b>다 — 이유가 달라졌을 뿐이다
        /// ============================================================================
        /// <see cref="StickmanAgent.ArePanelsSuppressed"/>에는 <b>사용자 임대</b> 항이 들어 있다
        /// (<c>UserSurfaceSummonPolicy</c>). 사용자가 설정창을 부르려고 임대를 켜는 순간 그 값이
        /// 거짓이 되므로, 그것으로 춤을 막으면 <i>«설정창을 여는 동안만 춤이 되살아난다»</i>는
        /// 재현 조건이 까다로운 결함이 된다. 올바른 창구는
        /// <see cref="StickmanAgent.IsForeignFullscreenAppPresent"/> 하나다.
        /// </summary>
        [Test]
        public void 춤_게이트는_전체화면을_전용_창구로만_읽는다()
        {
            string body = GateBody();

            // (가) 올바른 창구를 실제로 읽는가 — 존재 단언. 이것이 없으면 아래 부재 단언은
            //      "전체화면을 아예 안 본다"로도 조용히 통과한다(CLAUDE.md의 «부재 단언» 함정).
            string danceChannel = "." + nameof(StickmanAgent.IsForeignFullscreenAppPresent);
            StringAssert.Contains(danceChannel, body,
                $"★ 춤 게이트가 전체화면 전용 창구({danceChannel})를 읽지 않습니다:\n  " + body.Trim() +
                "\n무음 회의·발표에서 자동 춤이 발동합니다(2026-09-06 리더 판정).");

            // (나) 임대 항이 섞인 표면 축은 여전히 금지 — 부재 단언.
            foreach (string surfaceChannel in new[]
                     {
                         nameof(StickmanAgent.ArePanelsSuppressed),
                         nameof(StickmanAgent.HidesScreenSurfaces),
                     })
            {
                Assert.AreEqual(-1, body.IndexOf(surfaceChannel, StringComparison.Ordinal),
                    $"★ 춤 게이트가 표면 축({surfaceChannel})을 읽습니다:\n  " + body.Trim() +
                    "\n그 값에는 «사용자 임대» 항이 들어 있어, 사용자가 설정창을 부르는 동안에만 춤이 " +
                    "되살아납니다. 전체화면을 보려면 " + danceChannel + "를 읽으십시오.");
            }
        }

        /// <summary>
        /// ★★ <b>2026-08-31 신고의 회귀 잠금.</b> 위 검사는 소스를 읽고, 이 검사는 정책 함수를
        /// <b>실행해서</b> 잰다 — 같은 방법으로 두 번 재는 것은 검증이 아니다.
        ///
        /// <para>등급 1에서 <b>춤은 막히고</b>(<c>SuppressesAutoDance</c>=참)
        /// <b>캐릭터는 안 숨는다</b>(<c>SuspendsCharacter</c>=거짓)는 두 값을 <b>같은 테스트에서
        /// 나란히</b> 잰다. 한쪽만 재면 «둘이 갈라져 있다»는 계약 자체가 검증되지 않는다.</para>
        /// </summary>
        [Test]
        public void 등급1은_춤만_막고_캐릭터는_그대로_둔다()
        {
            Assert.IsTrue(ForeignFullscreenTierPolicy.SuppressesAutoDance(ForeignFullscreenTier.PanelsOnly),
                "★ 등급 1에서 자동 춤이 막히지 않습니다 — 무음 회의·발표에서 캐릭터가 춤춥니다.");
            Assert.IsFalse(ForeignFullscreenTierPolicy.SuspendsCharacter(ForeignFullscreenTier.PanelsOnly),
                "★★ 등급 1에서 캐릭터가 숨습니다 — 2026-08-31 사용자 신고(«엑셀같은 프로그램 전체화면에서 " +
                "엑셀 클릭하면 캐릭터가 없어져버림»)의 완전한 회귀입니다. 춤 억제 축을 캐릭터 축에 " +
                "섞지 마십시오.");

            Assert.IsFalse(ForeignFullscreenTierPolicy.SuppressesAutoDance(ForeignFullscreenTier.None),
                "★ 남의 전체화면 앱이 없는데 춤이 막힙니다 — 평상시에 이 기능이 통째로 죽습니다.");
            Assert.IsTrue(ForeignFullscreenTierPolicy.SuppressesAutoDance(ForeignFullscreenTier.Full),
                "등급 2는 등급 1을 포함해야 합니다(«등급이 올라갈수록 더 걷는다» 불변식).");
        }

        // ====================================================================
        // ③ 「이번 세션만 끄기」 — 리더 판정 2026-09-06
        // ====================================================================

        /// <summary>
        /// ★ 세션 토글이 <b>실제로 막는가</b>, 그리고 <b>되돌아오는가</b>.
        ///
        /// <para>이 토글의 안전성은 «저장되지 않는다»에 걸려 있다. 저장되면 사용자가 반년 전에 끈 것을
        /// 잊고 «춤이 고장났다»고 신고하는 경로가 생긴다(41-8: 되돌리는 문이 없는 저장 항목 금지).
        /// 그래서 <b>세이브 스키마를 건드리지 않았다</b>는 사실을
        /// <see cref="세션_토글은_세이브에_내려가지_않는다"/>가 따로 잠근다.</para>
        /// </summary>
        [Test]
        public void 세션_토글을_켜면_춤이_막히고_끄면_원래대로_돌아온다()
        {
            var go = new GameObject(nameof(세션_토글을_켜면_춤이_막히고_끄면_원래대로_돌아온다));
            try
            {
                AudioReactiveDanceGate.ResetForTesting();
                var focus = go.AddComponent<FocusWatchDirector>();
                var agent = go.AddComponent<StickmanAgent>();

                // 양성 대조 — 토글을 켜기 «전»의 값을 먼저 잰다. 이것이 없으면 아래 true가
                // «토글 때문»인지 «원래부터 막혀 있어서»인지 구분할 수 없다.
                bool before = AudioReactiveDanceGate.BlocksNow(agent, focus);
                Assert.IsFalse(before,
                    "전제가 깨졌습니다 — 갓 만든 캐릭터/감시자에서 이미 막혀 있습니다. " +
                    "이 상태에서는 아래 판정이 토글의 효과를 증명하지 못합니다.");

                AudioReactiveDanceGate.SetMutedForThisSession(true, "테스트");
                Assert.IsTrue(AudioReactiveDanceGate.MutedForThisSession, "토글 값이 서지 않았습니다.");
                Assert.IsTrue(AudioReactiveDanceGate.BlocksNow(agent, focus),
                    "★ «이번 세션만 끄기»를 켰는데 춤이 막히지 않습니다 — 사용자가 회의 중에 끌 수단이 없습니다.");

                AudioReactiveDanceGate.SetMutedForThisSession(false, "테스트");
                Assert.IsFalse(AudioReactiveDanceGate.BlocksNow(agent, focus),
                    "★ 다시 켰는데 계속 막힙니다 — 되돌리는 문이 닫혀 있습니다.");
            }
            finally
            {
                AudioReactiveDanceGate.ResetForTesting();
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        /// <summary>
        /// ★★ <b>세이브 스키마를 한 비트도 건드리지 않았다.</b> 이 토글이 세이브로 내려가면
        /// 하위 호환 테스트가 의무로 붙고(CLAUDE.md), 무엇보다 «되돌리는 문 없는 저장 항목»이 된다.
        ///
        /// <para>측정은 <b>세이브 모델의 소스</b>에서 한다 — 런타임 값으로는 «지금 저장 안 됐다»만
        /// 알 수 있고 «저장 경로가 없다»는 알 수 없다.</para>
        /// </summary>
        [Test]
        public void 세션_토글은_세이브에_내려가지_않는다()
        {
            string saveDir = Path.Combine(Application.dataPath, "_Project", "Scripts", "Core");
            string[] saveFiles = Directory.GetFiles(saveDir, "*Save*.cs", SearchOption.AllDirectories);
            Assert.Greater(saveFiles.Length, 0,
                "세이브 관련 소스를 하나도 못 찾았습니다 — 이 감사의 전제가 깨졌습니다(빈 집합 위의 " +
                "«없다»는 공허합니다).");

            string needle = nameof(AudioReactiveDanceGate.MutedForThisSession);
            foreach (string file in saveFiles)
            {
                string text = File.ReadAllText(file);
                Assert.AreEqual(-1, text.IndexOf(needle, StringComparison.Ordinal),
                    $"★ 세이브 소스 {Path.GetFileName(file)}가 «{needle}»을 참조합니다 — 이 토글은 " +
                    "비영속이어야 합니다(리더 판정 2026-09-06). 영속시키려면 스키마 버전 상승과 " +
                    "하위 호환 테스트가 함께 와야 합니다.");
            }
        }

        // ====================================================================
        // ② null 기본값이 명령 게이트와 반대인가 (자동 발동 경로의 안전 방향)
        // ====================================================================

        /// <summary>
        /// ★★ <b>자동 발동 경로의 안전한 방향은 「모르면 안 한다」다.</b>
        /// <see cref="HiddenCharacterCommandGate"/>는 <c>null → 막지 않는다</c>를 <b>의도적으로</b>
        /// 골랐고(사용자가 사유를 읽는 명령 경로라 그게 옳다), 춤은 <b>정반대</b>여야 한다.
        /// <b>그대로 복사하면 배선이 없는 채로 춤이 발동하고, 아무도 그것을 안 본다.</b>
        ///
        /// <para>같은 테스트에서 <b>두 게이트를 나란히</b> 잰다 — 그래야 «반대여야 한다»가
        /// 한쪽만 바뀌는 날 걸린다.</para>
        /// </summary>
        [Test]
        public void 배선이_없으면_춤은_막히고_명령은_막히지_않는다()
        {
            Assert.IsTrue(AudioReactiveDanceGate.BlocksNow(null, null),
                "★ 배선이 없는데(둘 다 null) 춤이 막히지 않습니다 — 자동 발동 경로라 보여줄 사유도, " +
                "그것을 읽을 사람도 없습니다. 아무도 모르는 사이에 춤이 발동합니다.");

            Assert.IsFalse(HiddenCharacterCommandGate.BlocksNow(null),
                "★ 명령 게이트의 null 기본값이 바뀌었습니다 — 그쪽은 «숨어 있어요»가 «배선이 없어요»를 " +
                "가리지 않도록 일부러 false입니다. 두 기본값이 반대라는 것이 이 라운드의 판단이므로, " +
                "그쪽을 바꿨다면 이 대조도 함께 다시 판단하십시오.");
        }

        /// <summary>한쪽만 <c>null</c>이어도 막는가. <c>&amp;&amp;</c>와 <c>||</c>를 헷갈리면 여기서 걸린다.</summary>
        [Test]
        public void 한쪽_배선만_없어도_춤은_막힌다()
        {
            var go = new GameObject(nameof(한쪽_배선만_없어도_춤은_막힌다));
            try
            {
                var focus = go.AddComponent<FocusWatchDirector>();

                Assert.IsTrue(AudioReactiveDanceGate.BlocksNow(null, focus),
                    "캐릭터 배선이 없는데 막지 않습니다.");
                Assert.IsTrue(AudioReactiveDanceGate.BlocksNow(null, null),
                    "집중 배선도 없는데 막지 않습니다.");
                Assert.IsFalse(focus.IsSessionActive,
                    "양성 대조 — 갓 만든 감시자는 세션이 꺼져 있어야 합니다. 이 전제가 깨지면 위 판정의 " +
                    "원인이 «세션이 켜져 있어서»인지 «배선이 없어서»인지 구분할 수 없습니다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // ====================================================================
        // 도구
        // ====================================================================

        /// <summary><c>BlocksNow</c> 본문만 뜬다. 클래스 문서(주석)에는 위 니들들이 <b>설명으로</b>
        /// 여러 번 나오므로, 파일 전체를 훑으면 이 감사는 통째로 무의미해진다.</summary>
        private static string GateBody()
        {
            string path = Path.Combine(CoreDirectory, GateFile);
            Assert.IsTrue(File.Exists(path), $"소스를 찾지 못했습니다: {path}");
            string src = File.ReadAllText(path).Replace("\r\n", "\n");

            const string signature = "public static bool " + nameof(AudioReactiveDanceGate.BlocksNow) + "(";
            int at = src.IndexOf(signature, StringComparison.Ordinal);
            Assert.Greater(at, 0,
                $"{GateFile}에서 {signature}...)을 찾지 못했습니다 — 이름이나 시그니처가 바뀌었다면 " +
                "이 감사도 함께 고치십시오(이 단언이 없으면 빈 문자열 위에서 모든 검사가 통과합니다).");

            int open = src.IndexOf('{', at);
            Assert.Greater(open, at, "메서드 본문의 여는 중괄호를 찾지 못했습니다.");

            int depth = 0;
            for (int i = open; i < src.Length; i++)
            {
                if (src[i] == '{') depth++;
                else if (src[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        string body = src.Substring(open, i - open + 1);
                        // ★ 양성 대조 — 본문을 실제로 떴는가. 빈/짧은 문자열 위의 "없다"는 공허하다.
                        Assert.Greater(body.Length, 40,
                            "본문을 " + body.Length + "자밖에 못 떴습니다 — 스캔이 깨졌고 아래 부재 판정은 무효입니다.");
                        return body;
                    }
                }
            }

            Assert.Fail("메서드 본문의 닫는 중괄호를 찾지 못했습니다 — 스캔이 깨졌습니다.");
            return string.Empty;
        }
    }
}
