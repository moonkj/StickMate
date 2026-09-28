using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★★ 잠그는 계약 두 개 (2026-09-28).
    ///
    /// ============================================================================
    /// ① <see cref="SpectacleEventLock"/>의 두 거절 사유가 <b>실제로 갈린다</b>
    /// ============================================================================
    /// <c>TryAcquire</c>의 <c>bool</c> 하나가 「남이 쥐고 있다」와 「보존 동결이 빈 락의 신규 획득을
    /// 막는다」에 <b>똑같이 <c>false</c></b>를 냈다 — 이 저장소가 반복해서 당한 <b>「실패한 측정과 성공한
    /// 측정이 똑같이 생겼다」의 프로덕션판</b>이다. 여기서는 <b>두 방향</b>을 잰다:
    /// <list type="number">
    ///   <item><b>사유가 갈리는가</b> — 같은 <c>false</c> 뒤에 서로 <b>다른</b> 사유가 온다.
    ///     ★ 「둘 다 false다」만 재면 보강 전과 구별되지 않으므로, 두 사유가 <b>서로 같지 않음</b>을
    ///     직접 단언한다(그것이 이 변경의 본체다).</item>
    ///   <item><b>소비처에 도달하는가</b> — 갈린 사유를 <b>실제로 읽는</b> 자리가 있는가.
    ///     사유를 구분해도 아무도 안 읽으면 값이 0이다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ② 숨어 있는 캐릭터에게 <b>포즈를 잡히지 않는다</b>
    /// ============================================================================
    /// <c>FocusWatchDirector</c>의 포즈 관문이 <see cref="HiddenCharacterCommandGate"/>를 본다.
    /// 다섯 호출부(시작·취소·완주·시작 재시도·완주 재시도)가 <b>한 관문</b>을 지나므로 「시작만 막고
    /// 완주는 안 막는」 비대칭이 구조적으로 불가능하다.
    ///
    /// ============================================================================
    /// 니들 위생 — <b>문자열을 베끼지 않는다</b>
    /// ============================================================================
    /// <para>사유 문구는 <see cref="SpectacleEventLock.Describe"/>를 <b>참조</b>하고, 멤버 이름은
    /// <c>nameof</c>로 조립한다(이름이 바뀌면 조용히 초록이 되는 대신 <b>컴파일되지 않는다</b>).
    /// <c>private</c> 메서드 이름 둘만 문자열일 수밖에 없어서, <b>존재 단언</b>을 같은 테스트 안에
    /// 붙였다 — 그 시그니처를 못 찾으면 「감사가 낡았다」로 <b>빨개진다</b>(부재 단언이 조용히 초록이
    /// 되는 형태를 만들지 않는다).</para>
    ///
    /// <para>★ <b>주석은 계수 대상이 아니다</b>(TEAM.md 규칙 16): 프로덕션의 새 주석 블록이 같은 낱말을
    /// 그대로 적고 있으므로, 소스 감사는 <b>행 주석을 벗긴 뒤</b>에만 판정한다. 벗기지 않으면 가드를
    /// 지워도 주석이 남아 <b>초록</b>이 된다 — 아래 양성 대조가 바로 그것을 잡는다.</para>
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립(순수 정적 상태 + 소스 텍스트). 창 열거·네이티브 호출 없음.
    /// 시간 예산 없음 — 벽시계 대기가 하나도 없다.</para>
    /// </summary>
    public sealed class SpectacleLockDenialReasonTests
    {
        private const string LogPrefix = "[락사유]";

        [SetUp]
        public void SetUp()
        {
            DisplayChangeHoldStatus.ResetForTesting();
            ReleaseAnyLock();
        }

        [TearDown]
        public void TearDown()
        {
            // 새면 뒤 테스트의 스펙터클 락 획득이 전부 거절된다(CharacterPreservationFreezeTests와 같은 사정).
            DisplayChangeHoldStatus.ResetForTesting();
            ReleaseAnyLock();
        }

        private static void ReleaseAnyLock()
        {
            object owner = SpectacleEventLock.CurrentOwner;
            if (owner != null) SpectacleEventLock.Release(owner);
        }

        private static void StartHold(int episode) =>
            DisplayChangeHoldStatus.PublishStarted(episode, DisplayChangeHoldStartReason.TopologyChangeDetected, 100);

        private static void ReleaseHold(int episode) =>
            DisplayChangeHoldStatus.PublishReleased(episode, DisplayChangeHoldReleaseReason.Settled, 200);

        // ====================================================================
        // ① 두 사유가 갈린다 — 같은 false 뒤에 서로 다른 사유가 온다
        // ====================================================================

        [Test]
        public void 남이_쥐고_있음과_보존_동결은_같은_false에_서로_다른_사유를_낸다()
        {
            object holder = new object();
            object newcomer = new object();

            // ── 음성 대조: 아무것도 막지 않으면 사유가 없다(그리고 실제로 잡힌다).
            Assert.AreEqual(SpectacleLockDenialReason.None, SpectacleEventLock.EvaluateAcquire(newcomer),
                $"{LogPrefix} 전제 — 빈 락에 거절 사유가 있습니다. 이 상태로는 아래 두 사유가 «원래 있던 것»과 " +
                "구별되지 않습니다.");

            // ── 사유 A: 남이 쥐고 있다.
            Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.Graffiti, holder),
                $"{LogPrefix} 전제 — 빈 락을 잡지 못했습니다.");
            bool heldOk = SpectacleEventLock.TryAcquire(SpectacleEventKind.Dance, newcomer,
                out SpectacleLockDenialReason heldReason);
            Assert.IsFalse(heldOk, $"{LogPrefix} 남이 쥐고 있는데 획득에 성공했습니다.");
            Assert.AreEqual(SpectacleLockDenialReason.HeldByOther, heldReason,
                $"{LogPrefix} 남이 쥔 상황의 사유가 {heldReason}입니다.");

            // ── 같은 주인의 재진입은 동결 중에도 거절이 아니다(진행 중 연출을 끊지 않는 계약).
            StartHold(1);
            Assert.AreEqual(SpectacleLockDenialReason.None, SpectacleEventLock.EvaluateAcquire(holder),
                $"{LogPrefix} 이미 쥔 주인의 재진입에 거절 사유가 붙었습니다 — 진행 중 연출이 끊깁니다.");
            SpectacleEventLock.Release(holder);
            Assert.IsFalse(SpectacleEventLock.IsActive, $"{LogPrefix} 전제 — 락이 비어야 합니다.");

            // ── 사유 B: 락은 비었는데 보존 동결이 신규 획득을 막는다.
            bool frozenOk = SpectacleEventLock.TryAcquire(SpectacleEventKind.Dance, newcomer,
                out SpectacleLockDenialReason frozenReason);
            Assert.IsFalse(frozenOk, $"{LogPrefix} 동결 중인데 신규 획득에 성공했습니다.");
            Assert.AreEqual(SpectacleLockDenialReason.PreservationFreeze, frozenReason,
                $"{LogPrefix} 동결 상황의 사유가 {frozenReason}입니다.");
            Assert.IsFalse(SpectacleEventLock.IsActive,
                $"{LogPrefix} 거절이 락을 반쯤 잡은 채 남겼습니다.");

            // ── ★ 이 변경의 본체: 같은 false인데 사유가 <b>다르다</b>.
            Assert.AreNotEqual(heldReason, frozenReason,
                $"{LogPrefix} 두 상황이 같은 사유({heldReason})를 냅니다 — 「실패한 측정과 성공한 측정이 " +
                "똑같이 생겼다」가 그대로 남아 있습니다. 이 단언이 이 라운드의 전부입니다.");
            Assert.AreEqual(heldOk, frozenOk,
                $"{LogPrefix} 전제 — 두 상황의 bool이 갈렸습니다. 이 테스트의 요점은 «bool은 같은데 사유는 " +
                "다르다»이므로, bool이 갈리면 재는 대상이 바뀝니다.");

            // ── 양성 대조: 동결이 끝나면 같은 요청이 잡힌다(위 거절이 동결 때문이었음을 가른다).
            ReleaseHold(1);
            Assert.AreEqual(SpectacleLockDenialReason.None, SpectacleEventLock.EvaluateAcquire(newcomer),
                $"{LogPrefix} 동결이 끝났는데도 거절 사유가 남았습니다.");
            Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.Dance, newcomer),
                $"{LogPrefix} 동결이 끝났는데 같은 요청이 거절됐습니다.");

            Debug.Log($"{LogPrefix} ① 통과 — 남이 쥠={heldReason} / 동결={frozenReason}, 둘 다 bool {heldOk}.");
        }

        [Test]
        public void 배선_사고는_락_상태와_무관한_제3의_사유다()
        {
            // null 소유자는 락이 비었든 차 있든 같은 사유다 — 다른 둘과 섞이면 안 된다.
            Assert.AreEqual(SpectacleLockDenialReason.NullOwner, SpectacleEventLock.EvaluateAcquire(null),
                $"{LogPrefix} 빈 락에서 null 소유자의 사유가 갈렸습니다.");

            object holder = new object();
            Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.Graffiti, holder),
                $"{LogPrefix} 전제 — 락을 잡지 못했습니다.");
            Assert.AreEqual(SpectacleLockDenialReason.NullOwner, SpectacleEventLock.EvaluateAcquire(null),
                $"{LogPrefix} 락이 차 있을 때 null 소유자가 HeldByOther로 보고됩니다 — 배선 사고가 " +
                "「남이 쥐고 있다」에 가려집니다.");

            StartHold(1);
            Assert.AreEqual(SpectacleLockDenialReason.NullOwner, SpectacleEventLock.EvaluateAcquire(null),
                $"{LogPrefix} 동결 중 null 소유자가 PreservationFreeze로 보고됩니다.");

            Debug.Log($"{LogPrefix} ② 통과 — 배선 사고는 세 상황에서 모두 같은 사유다.");
        }

        [Test]
        public void 사유_문구는_사유마다_서로_다르다()
        {
            // 사유를 갈라 놓고 문구를 하나로 합치면 로그에서 다시 구별할 수 없다 — 그 퇴화를 막는다.
            var all = (SpectacleLockDenialReason[])Enum.GetValues(typeof(SpectacleLockDenialReason));
            Assert.Greater(all.Length, 1, $"{LogPrefix} 사유가 {all.Length}개뿐입니다 — 빈 목록으로 아무것도 " +
                "재지 않고 초록이 되는 형태를 막습니다.");

            for (int i = 0; i < all.Length; i++)
            {
                string text = SpectacleEventLock.Describe(all[i]);
                Assert.IsFalse(string.IsNullOrWhiteSpace(text),
                    $"{LogPrefix} 사유 {all[i]}의 문구가 비어 있습니다.");
                for (int j = i + 1; j < all.Length; j++)
                {
                    Assert.AreNotEqual(text, SpectacleEventLock.Describe(all[j]),
                        $"{LogPrefix} 사유 {all[i]}와 {all[j]}의 문구가 같습니다 — 로그에서 두 사유를 " +
                        "구별할 수 없습니다.");
                }
            }

            Debug.Log($"{LogPrefix} ③ 통과 — 사유 {all.Length}종의 문구가 전부 서로 다르다.");
        }

        // ====================================================================
        // ② 소비처 도달 + 숨김 관문 — 소스로 본다(활성 빌드 타깃과 무관)
        // ====================================================================

        /// <summary>
        /// 갈린 사유를 <b>실제로 읽는</b> 자리가 있는가. 지금 그 자리는
        /// <c>FocusWatchDirector.DescribePoseGate</c>이고, 예전에는 <see cref="SpectacleEventLock.IsActive"/>와
        /// <see cref="SpectacleEventLock.CurrentOwner"/>만 봐서 <b>동결에 막힌 순간에도 「관문은 지금 열려
        /// 있습니다」라고 거짓을 적었다</b>. 그 한 줄이 이 변경의 유일한 관측 가능한 산출이다.
        /// </summary>
        [Test]
        public void 갈린_사유를_읽는_소비처가_실재한다()
        {
            string body = MethodBodyOrFail(DirectorSource(), DescribeGateSignature);
            string needle = nameof(SpectacleEventLock) + "." + nameof(SpectacleEventLock.EvaluateAcquire);

            Assert.GreaterOrEqual(body.IndexOf(needle, StringComparison.Ordinal), 0,
                $"{LogPrefix} 「{needle}」를 읽는 소비처가 없습니다 — 사유를 갈라 놓고 아무도 읽지 않으면 " +
                "이 변경의 값은 0입니다.");
            Assert.GreaterOrEqual(
                body.IndexOf(nameof(SpectacleLockDenialReason.HeldByOther), StringComparison.Ordinal), 0,
                $"{LogPrefix} 소비처가 「남이 쥐고 있다」를 사유로 구분하지 않습니다.");
            Assert.GreaterOrEqual(
                body.IndexOf(nameof(SpectacleEventLock.Describe), StringComparison.Ordinal), 0,
                $"{LogPrefix} 소비처가 사유 문구를 내지 않습니다 — 갈린 사유가 로그에 도달하지 않습니다.");

            // ★ 양성 대조: 소비를 지운 사본에서 같은 판정이 실제로 빨개지는가(주석만 남아도 초록이면 안 된다).
            string gutted = body.Replace(needle, nameof(SpectacleEventLock) + ".NotAReasonQuery");
            Assert.Less(gutted.IndexOf(needle, StringComparison.Ordinal), 0,
                $"{LogPrefix} 양성 대조를 세우지 못했습니다 — 지운 사본에 니들이 그대로 남아 있습니다.");

            Debug.Log($"{LogPrefix} ④ 통과 — 소비처가 EvaluateAcquire를 읽고 사유별로 갈라 적는다.");
        }

        /// <summary>
        /// 포즈 관문이 숨김을 본다. ★ <b>다섯 호출부가 한 관문을 지난다</b>는 것이 요점이라, 관문 <b>하나</b>에
        /// 가드가 있고 그것이 <b>락 획득보다 먼저</b> 오는지를 본다(뒤에 있으면 숨은 채로 락을 집었다 놓는다).
        /// </summary>
        [Test]
        public void 포즈_관문이_락_획득보다_먼저_숨김을_본다()
        {
            string src = DirectorSource();
            string gate = nameof(HiddenCharacterCommandGate) + "." + nameof(HiddenCharacterCommandGate.BlocksNow);
            string acquire = nameof(SpectacleEventLock) + "." + nameof(SpectacleEventLock.TryAcquire);

            string body = MethodBodyOrFail(src, TryTriggerSignature);
            Assert.IsNull(Diagnose(body, gate, acquire), $"{TryTriggerSignature} — {Diagnose(body, gate, acquire)}");

            // ★ 양성 대조: 가드를 지운 사본에서 같은 판정이 빨개지는가. 행 주석을 벗기지 않으면
            //   프로덕션 주석 블록이 같은 낱말을 적고 있어 여기서 초록이 난다(규칙 16).
            Assert.IsNotNull(Diagnose(body.Replace(gate, nameof(HiddenCharacterCommandGate) + ".NotAHiddenGate"),
                    gate, acquire),
                $"{LogPrefix} 가드를 지운 사본에서도 초록입니다 — 매처가 눈이 멀었습니다(주석을 세고 있을 수 있습니다).");

            // 다섯 호출부가 전부 이 한 관문을 지난다 — 「시작만 막고 완주는 안 막는」 비대칭 방지.
            string callee = TryTriggerSignature.Substring(TryTriggerSignature.IndexOf("TryTriggerPoseState",
                StringComparison.Ordinal));
            callee = callee.Substring(0, callee.IndexOf('(') + 1);
            int calls = 0;
            for (int at = src.IndexOf(callee, StringComparison.Ordinal); at >= 0;
                 at = src.IndexOf(callee, at + 1, StringComparison.Ordinal))
            {
                calls++;
            }
            // 선언 1회 + 호출 5회.
            Assert.AreEqual(6, calls,
                $"{LogPrefix} 「{callee}」가 {calls}회 나옵니다(선언 1 + 호출 5 = 6 기대). 호출부가 늘거나 " +
                "줄었으면 그 자리도 이 한 관문을 지나는지 확인하고 이 기대값을 함께 고치세요.");

            Debug.Log($"{LogPrefix} ⑤ 통과 — 숨김 가드가 락 획득보다 먼저, 호출부 5곳이 한 관문을 지난다.");
        }

        /// <summary>설명 함수의 사유 순서가 관문의 순서와 같은가 — 갈라지면 「막은 것」과 「막았다고 말하는
        /// 것」이 서로 다른 사유를 가리킨다.</summary>
        [Test]
        public void 설명_함수도_같은_순서로_숨김을_먼저_본다()
        {
            string body = MethodBodyOrFail(DirectorSource(), DescribeGateSignature);
            string gate = nameof(HiddenCharacterCommandGate) + "." + nameof(HiddenCharacterCommandGate.BlocksNow);
            string reason = nameof(HiddenCharacterCommandGate) + "." + nameof(HiddenCharacterCommandGate.HiddenReason);

            int gateAt = body.IndexOf(gate, StringComparison.Ordinal);
            Assert.GreaterOrEqual(gateAt, 0,
                $"{LogPrefix} 설명 함수가 숨김을 보지 않습니다 — 관문은 막는데 로그는 다른 사유를 적습니다.");
            Assert.GreaterOrEqual(body.IndexOf(reason, StringComparison.Ordinal), 0,
                $"{LogPrefix} 설명 함수가 숨김 사유 문구를 쓰지 않습니다(같은 사실을 두 곳에서 다르게 " +
                "말하지 않기 위해 그 상수를 참조해야 합니다).");

            int lockAt = body.IndexOf(nameof(SpectacleEventLock) + "." + nameof(SpectacleEventLock.EvaluateAcquire),
                StringComparison.Ordinal);
            Assert.GreaterOrEqual(lockAt, 0, $"{LogPrefix} 설명 함수가 락 사유를 읽지 않습니다.");
            Assert.Less(gateAt, lockAt,
                $"{LogPrefix} 설명 함수가 락 사유를 숨김보다 먼저 봅니다 — 관문 순서(숨김 → 상태 → 락)와 " +
                "어긋나서 숨은 상태에서 엉뚱한 사유를 적습니다.");

            Debug.Log($"{LogPrefix} ⑥ 통과 — 설명 함수의 순서가 관문 순서와 같다.");
        }

        // ====================================================================
        // 헬퍼 — 소스 텍스트. 주석을 벗긴 뒤에만 판정한다(규칙 16)
        // ====================================================================

        private const string TryTriggerSignature = "private bool TryTriggerPoseState(";
        private const string DescribeGateSignature = "private string DescribePoseGate()";

        private static string DirectorSource() => StripLineComments(File.ReadAllText(
            Path.Combine(Application.dataPath, "_Project", "Scripts", "Interaction", "FocusWatchDirector.cs")));

        /// <summary>성립하면 null. 판정과 양성 대조가 <b>같은 코드</b>를 쓴다(자를 두 벌 만들지 않는다).</summary>
        private static string Diagnose(string body, string guard, string mustFollowGuard)
        {
            int g = body.IndexOf(guard, StringComparison.Ordinal);
            if (g < 0) return $"숨김 가드('{guard}')가 없다";
            int at = body.IndexOf(mustFollowGuard, StringComparison.Ordinal);
            if (at < 0) return $"'{mustFollowGuard}'이(가) 본문에 없다 — 감사가 낡았다";
            if (at < g) return $"'{mustFollowGuard}'이(가) 숨김 가드보다 먼저 온다";
            return null;
        }

        private static string MethodBodyOrFail(string source, string signature)
        {
            string body = TryMethodBody(source, signature);
            Assert.IsNotNull(body,
                $"{LogPrefix} FocusWatchDirector.cs에서 「{signature}」를 찾지 못했습니다 — 감사 대상이 " +
                "없습니다(이름이 바뀌었으면 이 니들을 고치세요). 이 존재 단언이 없으면 아래 판정이 " +
                "«아무것도 재지 않은 초록»이 됩니다.");
            return body;
        }

        private static string StripLineComments(string src)
        {
            var sb = new StringBuilder(src.Length);
            foreach (string line in src.Replace("\r\n", "\n").Split('\n'))
            {
                int c = line.IndexOf("//", StringComparison.Ordinal);
                sb.Append(c >= 0 ? line.Substring(0, c) : line).Append('\n');
            }
            return sb.ToString();
        }

        private static string TryMethodBody(string source, string signature)
        {
            int at = source.IndexOf(signature, StringComparison.Ordinal);
            if (at < 0) return null;
            int open = source.IndexOf('{', at);
            if (open < 0) return null;
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                if (source[i] == '{') depth++;
                else if (source[i] == '}' && --depth == 0) return source.Substring(open, i - open + 1);
            }
            return null;
        }
    }
}
