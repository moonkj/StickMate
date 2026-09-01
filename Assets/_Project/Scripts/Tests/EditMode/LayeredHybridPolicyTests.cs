using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-01 (debugger) — <b>"레이어드 + DWM 하이브리드"를 해소하다가 원칙 2(클릭 관통 기본 ON)를
    /// 깨지 않는다</b>는 것을 코드로 잠근다.
    ///
    /// ============================================================================
    /// 왜 이 잠금이 특별히 중요한가
    /// ============================================================================
    /// 이 앱의 오버레이 창은 <b>화면 전체</b>를 덮는다. 클릭 관통이 깨지면 사용자는 바탕화면의 어떤
    /// 것도 클릭할 수 없다 — 지금 고치려는 어떤 증상(겹침/번짐/렉)보다 압도적으로 나쁜 회귀이며,
    /// 앱을 강제 종료하는 것 말고는 빠져나갈 방법이 없다. 그래서 이 규칙의 기본값은 전부
    /// <b>"모르면 하지 않는다 / 모르면 되돌린다"</b>이고, 아래 테스트가 그 방향을 고정한다.
    ///
    /// 규칙 자체는 OS 호출이 없는 순수 함수(<see cref="LayeredHybridPolicy"/>)라 Windows 실기 없이
    /// 이 맥에서 전 분기를 돌릴 수 있다.
    /// </summary>
    public sealed class LayeredHybridPolicyTests
    {
        /// <summary>"제거해도 되는" 표준 상태 — 각 테스트는 여기서 <b>한 가지만</b> 어긋뜨린다.</summary>
        private static LayeredHybridObservation Strippable() => new LayeredHybridObservation
        {
            OsStyleReadOk = true,
            HasLayeredStyle = true,
            HasClickThroughStyle = true,
            TransparentType = LayeredHybridPolicy.TransparentTypeAlpha,
            Disabled = false,
            OptedOut = false,
            StripCount = 0,
            MaxStrips = LayeredHybridPolicy.DefaultMaxStrips,
        };

        [Test]
        public void HybridWithLiveClickThroughIsTheOnlyStrippableShape()
        {
            Assert.AreEqual(LayeredHybridHold.None, LayeredHybridPolicy.EvaluateGate(Strippable()));
        }

        [Test]
        public void NoLayeredStyleMeansNothingToDo()
        {
            var o = Strippable(); o.HasLayeredStyle = false;
            Assert.AreEqual(LayeredHybridHold.NotHybrid, LayeredHybridPolicy.EvaluateGate(o));
        }

        [Test]
        public void UnreadableStyleNeverStrips()
        {
            var o = Strippable(); o.OsStyleReadOk = false;
            Assert.AreEqual(LayeredHybridHold.StyleUnreadable, LayeredHybridPolicy.EvaluateGate(o));
        }

        /// <summary>ColorKey 투명화는 <c>enableTransparentBySetLayered()</c>가 WS_EX_LAYERED를
        /// <b>필요로 한다</b>. 여기서 떼면 투명화가 죽어 회색 전체화면 창이 된다.</summary>
        [Test]
        public void ColorKeyPathIsNeverTouched()
        {
            var o = Strippable(); o.TransparentType = 2;
            Assert.AreEqual(LayeredHybridHold.ColorKeyNeedsLayered, LayeredHybridPolicy.EvaluateGate(o));
        }

        /// <summary>지금 WS_EX_TRANSPARENT가 없으면 "관통이 유지되는가"를 검증할 방법이 없다.
        /// <b>검증할 수 없는 변경은 하지 않는다.</b></summary>
        [Test]
        public void WithoutClickThroughStyleWeCannotVerifySoWeDoNotAct()
        {
            var o = Strippable(); o.HasClickThroughStyle = false;
            Assert.AreEqual(LayeredHybridHold.ClickThroughOffRightNow, LayeredHybridPolicy.EvaluateGate(o));
        }

        [Test]
        public void OnceRolledBackItNeverTriesAgain()
        {
            var o = Strippable(); o.Disabled = true;
            Assert.AreEqual(LayeredHybridHold.Disabled, LayeredHybridPolicy.EvaluateGate(o));
        }

        [Test]
        public void UserOptOutWinsOverEverything()
        {
            var o = Strippable(); o.OptedOut = true; o.Disabled = true;
            Assert.AreEqual(LayeredHybridHold.OptedOut, LayeredHybridPolicy.EvaluateGate(o),
                "사용자가 끈 경우가 가장 먼저 보고돼야 로그에서 원인을 즉시 알 수 있습니다.");
        }

        /// <summary>상한이 없으면 예상 못 한 주체와 스타일 비트를 두고 24시간 싸우게 된다.</summary>
        [Test]
        public void StripCountIsCapped()
        {
            var o = Strippable();
            o.StripCount = o.MaxStrips;
            Assert.AreEqual(LayeredHybridHold.AttemptCapReached, LayeredHybridPolicy.EvaluateGate(o));
            o.StripCount = o.MaxStrips - 1;
            Assert.AreEqual(LayeredHybridHold.None, LayeredHybridPolicy.EvaluateGate(o));
        }

        // ============================================================================
        // 대조군 — "관통을 관측할 수단이 유효한가"를 먼저 묻는다
        // ============================================================================

        /// <summary>제거 <b>전에</b> 관통이 관측되지 않으면, 제거 후의 "관통된다"는 관측도 무의미하다
        /// (원래부터 그렇게 보였을 수 있다). 그때는 실험 자체를 포기한다.</summary>
        [Test]
        public void ControlArmMustObservePassThroughBeforeWeTouchAnything()
        {
            Assert.AreEqual(LayeredHybridHold.None, LayeredHybridPolicy.EvaluateControl(true));
            Assert.AreEqual(LayeredHybridHold.OracleInvalid, LayeredHybridPolicy.EvaluateControl(false));
        }

        [Test]
        public void ControlArmRunsOnlyUntilItHasSucceededOnce()
        {
            Assert.IsTrue(LayeredHybridPolicy.RequiresControlProbe(verifiedOnce: false));
            Assert.IsFalse(LayeredHybridPolicy.RequiresControlProbe(verifiedOnce: true));
        }

        // ============================================================================
        // 되돌림 — 원칙 2의 마지막 방어선
        // ============================================================================

        [Test]
        public void PassThroughLostAfterStripMeansImmediateRollback()
        {
            Assert.IsTrue(LayeredHybridPolicy.RequiresRollback(
                clickThroughStyleStillSet: true, passThroughObservedAfterStrip: false),
                "제거 후 관통이 사라졌는데 되돌리지 않으면 화면 전체를 덮는 창이 모든 클릭을 먹습니다 " +
                "— 원칙 2 위반이자 사용자가 앱을 강제 종료해야 하는 상태입니다.");
        }

        /// <summary><b>판정할 수 없으면 되돌린다.</b> 되돌리는 쪽의 비용은 "예전 상태로 복귀"뿐이고,
        /// 안 되돌리는 쪽의 비용은 "아무것도 클릭할 수 없는 데스크톱"이다 — 비대칭이 명백하다.</summary>
        [Test]
        public void UnjudgeableStateAlsoRollsBack()
        {
            Assert.IsTrue(LayeredHybridPolicy.RequiresRollback(
                clickThroughStyleStillSet: false, passThroughObservedAfterStrip: true));
            Assert.IsTrue(LayeredHybridPolicy.RequiresRollback(
                clickThroughStyleStillSet: false, passThroughObservedAfterStrip: false));
        }

        [Test]
        public void PassThroughSurvivingMeansKeepTheStrip()
        {
            Assert.IsFalse(LayeredHybridPolicy.RequiresRollback(
                clickThroughStyleStillSet: true, passThroughObservedAfterStrip: true));
        }

        // ============================================================================
        // 로그 품질 — 사용자가 Player.log 한 줄로 이해할 수 있어야 한다
        // ============================================================================

        // ============================================================================
        // 소스 스캔 — 창 스타일 <쓰기>가 흩어지지 않게 한 파일에 가둔다
        // ============================================================================

        /// <summary>
        /// 이 저장소는 <c>Win32WindowService.cs</c>에서 쓰기 계열 Win32 호출을 전부 제거한 이력이 있고
        /// (2026-08-30), 그 뒤로 "오버레이 제어는 라이브러리에 맡긴다"가 규약이다. 이번 라운드는 그
        /// 규약에 <b>단 하나의 예외</b>를 만든다 — 우리 자신의 창에서 <c>WS_EX_LAYERED</c> 비트를 떼는 것.
        ///
        /// <para>예외를 만들 때 가장 위험한 것은 <b>다음 사람이 그 예외를 근거로 여기저기 스타일을
        /// 쓰기 시작하는 것</b>이다. 그래서 쓰기 API가 <see cref="AllowedWriteFile"/> 한 파일 밖으로
        /// 나가는 순간 이 테스트가 깨진다. (타 프로세스 창을 건드리는 API는 별도로
        /// <c>UserAssetImmutabilityAuditTests</c>가 원칙 3 관문에서 전면 금지한다 — 이 테스트는
        /// 그것을 대체하지 않고 <b>자기 창 스타일 쓰기</b>라는 다른 축을 잠근다.)</para>
        /// </summary>
        private const string AllowedWriteFile = "WindowsLayeredHybridResolver.cs";

        private static readonly string[] SelfWindowStyleWriteApis =
        {
            "SetWindowLongPtrW",
            "SetWindowLongW",
            "SetLayeredWindowAttributes(",
        };

        [Test]
        public void 자기창_스타일_쓰기_API는_해소기_한_파일에만_있다()
        {
            string scriptsRoot = Path.Combine(UnityEngine.Application.dataPath, "_Project", "Scripts");
            string testsRoot = (Path.Combine(scriptsRoot, "Tests") + Path.DirectorySeparatorChar).Replace('\\', '/');

            var violations = new List<string>();
            foreach (string path in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (path.Replace('\\', '/').StartsWith(testsRoot, StringComparison.Ordinal)) continue;
                string fileName = Path.GetFileName(path);
                if (fileName == AllowedWriteFile) continue;

                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i];
                    // 주석/문서는 제외 — 이 저장소는 근거를 주석에 길게 남기는 것이 규약이다.
                    string trimmed = line.TrimStart();
                    if (trimmed.StartsWith("//", StringComparison.Ordinal)
                        || trimmed.StartsWith("///", StringComparison.Ordinal)
                        || trimmed.StartsWith("*", StringComparison.Ordinal)) continue;

                    foreach (string api in SelfWindowStyleWriteApis)
                    {
                        if (!line.Contains(api)) continue;
                        // GetLayeredWindowAttributes(는 읽기 — 이름이 겹치지 않게 접두를 확인한다.
                        if (api == "SetLayeredWindowAttributes(" && line.Contains("GetLayeredWindowAttributes(")
                            && !line.Contains("SetLayeredWindowAttributes(")) continue;
                        violations.Add($"{fileName}:{i + 1}: {api} — {line.Trim()}");
                    }
                }
            }

            Assert.IsTrue(violations.Count == 0,
                $"창 스타일 <쓰기> API가 {AllowedWriteFile} 밖에서 발견됐습니다. 이 프로젝트에서 오버레이 창 " +
                "제어는 UniWindowController에 맡기는 것이 규약이고, 유일한 예외(레이어드 하이브리드 해소)는 " +
                "대조군/실험군/되돌림이 붙어 있는 그 파일 안에서만 성립합니다. 새 쓰기가 필요하면 " +
                "그 파일에 넣고 같은 검증 절차를 붙이세요:\n  " + string.Join("\n  ", violations));
        }

        [Test]
        public void EveryHoldReasonHasHumanReadableText()
        {
            foreach (LayeredHybridHold hold in System.Enum.GetValues(typeof(LayeredHybridHold)))
            {
                string text = LayeredHybridPolicy.Describe(hold);
                Assert.IsFalse(string.IsNullOrWhiteSpace(text), $"{hold}에 설명이 없습니다.");
                Assert.AreNotEqual(hold.ToString(), text,
                    $"{hold}가 enum 이름 그대로 찍힙니다 — 사용자가 읽는 로그에 영어 식별자만 남습니다.");
            }
        }
    }
}
