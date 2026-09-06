using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ <b>독립 루트 오버레이가 캐릭터 없이 화면에 남는 구멍</b>을 구조적으로 닫는다 — 2026-09-06.
    ///
    /// ============================================================================
    /// 무엇을 재는가 (그리고 왜 런타임 테스트로는 부족한가)
    /// ============================================================================
    /// <c>StickmanAgent.Suspend()</c>의 <c>SetRenderersEnabled(false)</c>가 닿는 범위는 <b>둘뿐</b>이다:
    /// Awake에 캐시한 <b>캐릭터 자식</b> Renderer와 <see cref="StickMate.Core.ICharacterVisualSource"/>로
    /// <b>스스로 신고한</b> 잉크. 그런데 오버레이 연출들은 컨테이너를 <c>SetParent(null)</c>인
    /// <b>독립 루트</b>로 만든다 — 그래서 캐릭터만 사라지고 연출은 전체화면 게임 위에 남는다
    /// (절대 불변 원칙 2 위반). 2026-09-06 실측에서 <b>네 개</b>가 실제로 그 상태였다
    /// (기분 표시 / 투두 종이 / 가출 과자 / 활쏘기 과녁).
    ///
    /// <para><b>런타임 테스트 하나로는 이 병을 못 막는다</b>: 그것은 "지금 이 연출 하나"를 재지만,
    /// 이 결함은 <b>새 렌더러가 생길 때마다 다시 태어나는</b> 종류다(여섯 번째 렌더러를 쓰는 사람이
    /// 이 규칙을 알 방법이 없다). 그래서 소스 자체에 규칙을 건다 — 이 저장소가
    /// <c>PlatformParityAuditTests</c>/<c>FacingFlipBodySplitTests</c>에서 쓰는 것과 같은 어법이다.</para>
    ///
    /// ============================================================================
    /// 규칙
    /// ============================================================================
    /// <c>SetParent(null</c>로 자기 루트를 만드는 프로덕션 파일은 <b>둘 중 하나</b>여야 한다:
    /// <list type="number">
    ///   <item><see cref="StickMate.Core.SuspendedOverlayGate"/>를 통과한다, 또는</item>
    ///   <item>아래 <see cref="Exempt"/>에 <b>사유와 함께</b> 등재된다.</item>
    /// </list>
    /// 대장은 <b>스스로 만료된다</b> — 등재된 파일이 사라지거나 게이트를 쓰기 시작하면 빨간불이다.
    /// 사유 없이 조용히 늘어나는 것을 막는 것이 이 검사의 전부다.
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립(소스를 읽기만 한다).</para>
    /// </summary>
    public sealed class SuspendedOverlayLeakAuditTests
    {
        private const string LogPrefix = "[숨김누수감사]";
        private const string GateTypeName = "SuspendedOverlayGate";

        /// <summary>독립 루트를 만드는 표식. 공백 표기 흔들림을 흡수한다.</summary>
        private static readonly Regex DetachedRootPattern = new Regex(@"SetParent\(\s*null", RegexOptions.Compiled);

        /// <summary>
        /// 게이트를 쓰지 <b>않아도 되는</b> 파일과 그 사유. 한 줄이 곧 "이건 다른 경로로 이미 걷힌다"는 계약이다.
        /// </summary>
        private static readonly Dictionary<string, string> Exempt = new Dictionary<string, string>
        {
            // ── 게이트 자신 / 이미 같은 판정을 하는 곳 ──────────────────────────────────
            ["SuspendedOverlayGate.cs"] =
                "게이트 본체(문서 주석에 SetParent(null이 인용돼 있다).",
            // ★ 2026-09-06 — ["FocusWatchRenderer.cs"] 항목이 여기서 <b>사라졌다</b>. 그 파일이 삭제됐기
            //   때문이다(사용자 지시: 발밑 타이머 링 제거). 이 대장은 «등재된 파일이 사라지면 빨간불»
            //   이라 삭제와 동시에 이 줄이 터졌고, 그게 이 감사가 의도대로 만료된 형태다.
            ["DialogueBubbleRenderer.cs"] =
                "LateUpdate 첫 줄에서 IsSuspended면 HideImmediateInternal로 스스로 즉시 감춘다.",
            ["HardwareReactionRenderer.cs"] =
                "HardwareReactionDirector.Update가 IsSuspended에서 ClearAllVisibleReactions()로 명시적으로 걷는다.",

            // ── 상태 강제 인터럽트 → 취소 페이드로 스스로 사라지는 것 ────────────────────
            ["GraffitiRenderer.cs"] =
                "Suspend()가 Graffiti 상태를 강제 인터럽트하고 그 전이가 Cancelled 발행 → 0.18초 취소 페이드로 사라진다. " +
                "얼리면 오히려 게임이 끝난 뒤 낙서가 한 번 번쩍인다.",
            ["WindowTheftRenderer.cs"] =
                "WindowTheft도 Suspend()의 강제 인터럽트 목록에 있어 같은 취소 경로를 탄다.",
            ["WindowCrashRenderer.cs"] =
                "WindowCrashDirector가 IsSuspended/ArePanelsSuppressed를 직접 폴링해 오버레이를 별도로 취소한다.",
            ["LandingDustRenderer.cs"] =
                "착지 먼지는 상태가 아니라 자기 수명(0.4초 안팎)으로 사라지는 1회성 파티클이라 잔상이 구조적으로 짧다.",

            // ── ICharacterVisualSource로 이미 커버되는 것(SetRenderersEnabled가 직접 끈다) ──
            ["CharacterFxRenderer.cs"] = "ICharacterVisualSource 구현 — Suspend가 직접 끈다.",
            ["CharacterPetRenderer.cs"] = "ICharacterVisualSource 구현 — Suspend가 직접 끈다.",

            // ── 캐릭터 연출이 아니라 앱 소유 UI 패널: 별도 축(ArePanelsSuppressed, 등급 1)이 담당 ──
            ["CharacterInfoWindow.cs"] = "앱 소유 UI 패널 — ArePanelsSuppressed 축이 담당(등급 1).",
            ["SettingsWindow.cs"] = "앱 소유 UI 패널 — ArePanelsSuppressed 축이 담당(등급 1).",
            ["PopoverPanel.cs"] = "앱 소유 UI 패널 — ArePanelsSuppressed 축이 담당(등급 1).",
            ["GearRadialMenuWidget.cs"] = "앱 소유 UI(톱니 부채꼴) — HidesScreenSurfaces/ArePanelsSuppressed 축이 담당.",
            ["InfoGearIconWidget.cs"] = "앱 소유 UI(톱니) — HidesScreenSurfaces 축이 담당.",
        };

        /// <summary>
        /// ★ 네거티브 컨트롤 — 스캐너가 0건을 훑고도 "깨끗하다"고 말하지 못하게 한다.
        /// 지금 실측 파일 수는 18이고(2026-09-06 FocusWatchRenderer.cs 삭제로 19 → 18),
        /// 그 절반 아래로 떨어지면 정규식/경로가 깨진 것이다.
        /// </summary>
        private const int MinimumScannedFiles = 10;

        [Test]
        public void 독립루트_오버레이는_전부_숨김게이트를_지나거나_사유와_함께_등재된다()
        {
            string root = Path.Combine(Application.dataPath, "_Project", "Scripts");
            Assert.IsTrue(Directory.Exists(root), $"{LogPrefix} 스크립트 폴더를 찾지 못했습니다: {root}");

            var offenders = new List<string>();
            var gated = new List<string>();
            var exemptSeen = new HashSet<string>();
            int scanned = 0;

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Tests/")) continue;
                string text = File.ReadAllText(file);
                if (!DetachedRootPattern.IsMatch(text)) continue;

                scanned++;
                string name = Path.GetFileName(file);

                if (Exempt.ContainsKey(name)) { exemptSeen.Add(name); continue; }
                if (text.Contains(GateTypeName)) { gated.Add(name); continue; }

                offenders.Add(name);
            }

            Debug.Log($"{LogPrefix} 독립 루트 생성 파일 {scanned}개 — 게이트 통과 {gated.Count}개" +
                $"[{string.Join(", ", gated)}] / 사유 등재 {exemptSeen.Count}개 / 미분류 {offenders.Count}개.");

            Assert.GreaterOrEqual(scanned, MinimumScannedFiles,
                $"{LogPrefix} 독립 루트를 만드는 파일을 {scanned}개밖에 못 찾았습니다(최소 {MinimumScannedFiles}) — " +
                "정규식이나 스캔 경로가 깨졌습니다. 이 실행의 '깨끗함'은 무효입니다.");

            Assert.IsEmpty(offenders,
                $"{LogPrefix} 캐릭터가 숨겨져도 화면에 남을 수 있는 오버레이가 있습니다: {string.Join(", ", offenders)}.\n" +
                "이 파일들은 컨테이너를 SetParent(null)인 독립 루트로 만들므로 " +
                "StickmanAgent.Suspend()의 SetRenderersEnabled(false)가 구조적으로 닿지 못합니다 " +
                "(절대 불변 원칙 2). LateUpdate 첫 줄에 Core/SuspendedOverlayGate.FreezeAndHide(...)를 " +
                "넣거나, 다른 경로로 이미 걷힌다면 이 파일의 Exempt에 **사유와 함께** 등재하십시오.");
        }

        /// <summary>대장이 스스로 만료되게 한다 — 사라진 파일이나 더 이상 예외가 아닌 파일이 남으면 빨간불.</summary>
        [Test]
        public void 예외대장에_유령_항목이_없다()
        {
            string root = Path.Combine(Application.dataPath, "_Project", "Scripts");
            var live = new Dictionary<string, string>();

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                if (file.Replace('\\', '/').Contains("/Tests/")) continue;
                string name = Path.GetFileName(file);
                if (!Exempt.ContainsKey(name)) continue;
                live[name] = File.ReadAllText(file);
            }

            var missing = Exempt.Keys.Where(k => !live.ContainsKey(k)).ToArray();
            Assert.IsEmpty(missing,
                $"{LogPrefix} 예외 대장에 있는데 <b>독립 루트를 만들지 않는(또는 사라진)</b> 파일이 있습니다: " +
                $"{string.Join(", ", missing)}. 대장에서 지우십시오 — 낡은 예외는 검사를 조용히 헐겁게 만듭니다.");

            var noLongerDetached = live
                .Where(kv => !DetachedRootPattern.IsMatch(kv.Value))
                .Select(kv => kv.Key)
                .ToArray();
            Assert.IsEmpty(noLongerDetached,
                $"{LogPrefix} 더 이상 독립 루트를 만들지 않는 파일이 예외 대장에 남아 있습니다: " +
                $"{string.Join(", ", noLongerDetached)}. 예외가 필요 없어졌으니 지우십시오.");
        }
    }
}
