using NUnit.Framework;
using UnityEngine;
using StickMate.Interaction;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ <b>설정창은 어느 자리에서 열리는가</b> — 2026-09-30 사용자 확정:
    /// <i>"설정창은 현재 열려있는 창(=정보창)의 위치에서 열려야 한다"</i>.
    ///
    /// ============================================================================
    /// 무엇이 문제였나
    /// ============================================================================
    /// 정보창 헤더의 [설정]을 누르면 설정창이 열리는데, 그 창은 <b>부른 쪽의 자리를 묻지 않았다</b> —
    /// 자기 저장값이 있으면 그 자리, 없으면 화면 중앙이었다. 화면 한쪽으로 옮겨 둔 정보창에서
    /// [설정]을 누르면 창이 <b>화면을 가로질러 튀어</b> 사용자가 시선을 다시 찾아야 했다.
    ///
    /// ============================================================================
    /// ★ 이 파일이 재는 것 / 못 재는 것 — 축을 나눈 이유
    /// ============================================================================
    /// 최종 좌표로 판정할 수 없는 환경이 있다: 배치 화면(640×480)은 이 창(720×560)보다 작아
    /// 클램프가 <b>모든 희망값을 한 자리로 못박는다</b>. 거기서 최종 <c>PanelOffsetPoints</c>만
    /// 보면 «물려받았다»와 «아무 일도 안 일어났다»가 <b>똑같이 생긴다</b>
    /// (<c>WindowDragPersistenceTests</c>가 같은 함정을 문서로 남겨 두었다).
    /// 그래서:
    ///  · <b>이 파일(EditMode)</b> — 가지를 고르는 <b>순수 함수</b>
    ///    (<see cref="SettingsWindow.ResolveOpenCenterPoints"/>)의 진리표 + 클램프가 실제로
    ///    화면 밖 좌표를 되끌어오는가(성질 검사). 화면도 씬도 필요 없다.
    ///  · <b>PlayMode</b>(<c>SettingsWindowOpensAtCallerPositionTests</c>) — <b>배선</b>:
    ///    정보창 [설정] 핸들러가 자기 패널 중심을 실제로 넘기는가, 그리고 단축키 경로가 안 바뀌었는가.
    ///
    /// ★ 기대값을 프로덕션 함수로 만들지 않는다(TEAM.md 「생성기와 검사기가 같이 틀린다」).
    ///   여기 기대값은 <b>테스트가 직접 고른 입력값</b>과 <b>참조한 상수</b>
    ///   (<see cref="UiWindowDrag.ScreenMarginPoints"/> · <see cref="SettingsWindow.PanelWidth"/>)뿐이다.
    /// </summary>
    public sealed class SettingsWindowCallerPlacementTests
    {
        private const string LogPrefix = "[설정창자리-TEST]";

        /// <summary>테스트가 고른 «부른 창의 자리». 0이 아니어야 한다 — 0이면 아래 단언들이
        /// 기본값(화면 중앙)과 구별되지 않아 조용히 공허해진다(거짓 통과 #5의 형태).</summary>
        private static readonly Vector2 CallerCenter = new Vector2(213f, -147f);

        /// <summary>«이 창을 옮겨 둔 자리». <see cref="CallerCenter"/>와 <b>달라야</b> 한다 —
        /// 같으면 «부른 창이 저장값을 이긴다»를 가를 수 없다.</summary>
        private static readonly Vector2 OwnSavedCenter = new Vector2(-88f, 59f);

        // ====================================================================================
        // ① 부른 창이 자리를 물려주면 — 그 자리를 목표로 삼고, 자기 저장값을 이기고, 클램프를 태운다
        // ====================================================================================

        [Test]
        public void 부른_창의_자리는_저장값을_이기고_클램프를_태운다()
        {
            Assert.AreNotEqual(CallerCenter, OwnSavedCenter,
                $"{LogPrefix} 전제 실패 — 두 표본이 같은 자리입니다. 그러면 «어느 쪽을 골랐는가»를 " +
                "이 파일이 구조적으로 가를 수 없습니다.");
            Assert.AreNotEqual(Vector2.zero, CallerCenter,
                $"{LogPrefix} 전제 실패 — 부른 창의 자리가 화면 중앙(0)입니다. 그러면 아래 좌표 단언이 " +
                "기본값과 똑같이 생겨 아무것도 증명하지 못합니다.");

            // ---- 저장값이 없을 때 ----
            Vector2 got = SettingsWindow.ResolveOpenCenterPoints(CallerCenter,
                hasSavedCenter: false, savedCenterPoints: Vector2.zero,
                out SettingsWindow.OpenPlacement placement, out bool clampRequired);

            Assert.AreEqual(SettingsWindow.OpenPlacement.CallerCenter, placement,
                $"{LogPrefix} 부른 창이 자리를 넘겼는데 그 가지를 타지 않았습니다.");
            Assert.AreEqual(CallerCenter.x, got.x, 0f, $"{LogPrefix} 물려받은 x가 바뀌었습니다.");
            Assert.AreEqual(CallerCenter.y, got.y, 0f, $"{LogPrefix} 물려받은 y가 바뀌었습니다.");
            Assert.IsTrue(clampRequired,
                $"{LogPrefix} ★ 물려받은 자리에 화면 안 고정을 요구하지 않습니다. 두 창의 크기가 달라 " +
                "(정보창 1042×802 / 설정창 720×560) 중심이 같아도 한쪽만 화면을 벗어날 수 있고, " +
                "창 밖 클릭이 닫지 않는 이 앱에서 화면 밖 창은 «닫을 수 없는 창»입니다.");

            // ---- 저장값이 있어도 부른 창이 이긴다 ----
            //    (이게 뒤집히면 «정보창에서 눌렀는데 엉뚱한 자리에서 열린다»가 그대로 돌아온다.)
            Vector2 withSaved = SettingsWindow.ResolveOpenCenterPoints(CallerCenter,
                hasSavedCenter: true, savedCenterPoints: OwnSavedCenter,
                out SettingsWindow.OpenPlacement placement2, out bool clamp2);

            Assert.AreEqual(SettingsWindow.OpenPlacement.CallerCenter, placement2,
                $"{LogPrefix} 저장값이 있으면 부른 창의 자리를 무시합니다 — 사용자가 방금 보고 있던 " +
                "창이 아니라 며칠 전에 옮겨 둔 자리에서 열립니다.");
            Assert.AreEqual(CallerCenter.x, withSaved.x, 0f,
                $"{LogPrefix} 저장값({OwnSavedCenter})이 물려받은 자리({CallerCenter})를 덮었습니다.");
            Assert.IsTrue(clamp2, $"{LogPrefix} 위와 같은 이유로 여기서도 클램프가 필요합니다.");

            Debug.Log($"{LogPrefix} ① 통과 — 물려받은 자리 {CallerCenter}가 저장값 {OwnSavedCenter}를 " +
                "이기고, 두 경우 모두 화면 안 고정을 요구합니다.");
        }

        // ====================================================================================
        // ② 음성 대조 — 부른 창이 없는 진입점(전역 단축키·트레이·자동 복귀)은 예전 그대로다
        // ====================================================================================

        [Test]
        public void 부른_창이_없는_진입점은_예전_동작_그대로다()
        {
            // ---- (가) 옮긴 적도 없다 → 화면 중앙. <b>클램프도 태우지 않는다</b>(예전과 비트 동일) ----
            Vector2 fresh = SettingsWindow.ResolveOpenCenterPoints(null,
                hasSavedCenter: false, savedCenterPoints: OwnSavedCenter,
                out SettingsWindow.OpenPlacement placement, out bool clampRequired);

            Assert.AreEqual(SettingsWindow.OpenPlacement.ScreenCenter, placement,
                $"{LogPrefix} 옮긴 적 없는 사용자가 단축키로 열었는데 «화면 중앙» 가지가 아닙니다.");
            Assert.AreEqual(0f, fresh.x, 0f, $"{LogPrefix} 화면 중앙의 x가 0이 아닙니다({fresh}).");
            Assert.AreEqual(0f, fresh.y, 0f, $"{LogPrefix} 화면 중앙의 y가 0이 아닙니다({fresh}).");
            Assert.IsFalse(clampRequired,
                $"{LogPrefix} ★ 옮긴 적 없는 자리에 클램프를 태웁니다 — 이 창은 720×560 <b>고정</b>이라 " +
                "화면이 그보다 낮으면 «넘칠 때는 상단 우선» 규칙이 발동해 창이 위로 붙습니다" +
                "(배치모드 480px 화면에서 56pt). 아무도 부탁하지 않은 이동이고, 2026-09-07 라운드가 " +
                "명시적으로 막아 둔 회귀입니다.");

            // ---- (나) 옮긴 적이 있다 → 그 자리 + 클램프(창별 위치 기억, da71068) ----
            Vector2 remembered = SettingsWindow.ResolveOpenCenterPoints(null,
                hasSavedCenter: true, savedCenterPoints: OwnSavedCenter,
                out SettingsWindow.OpenPlacement placement2, out bool clamp2);

            Assert.AreEqual(SettingsWindow.OpenPlacement.OwnSavedCenter, placement2,
                $"{LogPrefix} ★ 창별 위치 기억이 죽었습니다 — 각 창을 독립적으로 옮긴 뒤 재시작했을 때 " +
                "각자 마지막 자리에서 열려야 합니다(2026-09-07 사용자 요청 PART1-1). " +
                "2026-09-30 라운드가 바꾼 것은 «정보창 → [설정]» 경로 하나뿐입니다.");
            Assert.AreEqual(OwnSavedCenter.x, remembered.x, 0f, $"{LogPrefix} 기억한 x가 바뀌었습니다.");
            Assert.AreEqual(OwnSavedCenter.y, remembered.y, 0f, $"{LogPrefix} 기억한 y가 바뀌었습니다.");
            Assert.IsTrue(clamp2,
                $"{LogPrefix} 저장된 자리에 클램프를 안 태웁니다 — 그 값은 <b>다른 화면 크기에서 만든</b> " +
                "것일 수 있습니다(외장 모니터를 뽑았거나 해상도를 바꿨거나).");

            // ---- 세 가지 가지가 모두 서로 다른 이름을 낸다(진리표가 접히지 않았는가) ----
            Assert.AreEqual(3, System.Enum.GetValues(typeof(SettingsWindow.OpenPlacement)).Length,
                $"{LogPrefix} {nameof(SettingsWindow.OpenPlacement)}의 항목 수가 3이 아닙니다 — 가지가 " +
                "늘거나 줄었으면 이 파일의 진리표도 함께 늘려야 합니다(안 늘리면 새 가지는 " +
                "어디에서도 검증되지 않습니다).");
            Assert.AreNotEqual(placement, placement2,
                $"{LogPrefix} (가)와 (나)가 같은 가지 이름을 냅니다 — 두 동작을 로그에서도 " +
                "테스트에서도 구별할 수 없습니다.");

            Debug.Log($"{LogPrefix} ② 통과 — 부른 창이 없으면 예전 두 가지({placement} / {placement2})가 " +
                "그대로이고, «옮긴 적 없음»만 클램프를 건너뜁니다.");
        }

        // ====================================================================================
        // ③ 물려받은 자리가 화면 밖이면 실제로 되끌려 들어온다 (성질 검사 — 기대값을 만들지 않는다)
        // ====================================================================================

        [Test]
        public void 물려받은_자리가_화면_밖이면_되끌려_들어온다()
        {
            // 화면은 창(720×560)보다 <b>넉넉히 큰</b> 합성값을 쓴다. 배치 화면(640×480)에서는
            // 여유가 0이라 «되끌려 왔다»와 «원래 그 자리였다»가 똑같이 생긴다.
            var screen = new Vector2(1920f, 1080f);
            var panel = new Vector2(SettingsWindow.PanelWidth, SettingsWindow.PanelHeight);
            const float margin = UiWindowDrag.ScreenMarginPoints;

            Assert.Greater(screen.x, panel.x + margin * 2f,
                $"{LogPrefix} 전제 실패 — 합성 화면 가로에 여유가 없습니다. 클램프 성질을 못 잽니다.");
            Assert.Greater(screen.y, panel.y + margin * 2f,
                $"{LogPrefix} 전제 실패 — 합성 화면 세로에 여유가 없습니다.");

            // ---- 화면 밖으로 한참 벗어난 희망값 ----
            var wild = new Vector2(9000f, 9000f);
            Vector2 desired = SettingsWindow.ResolveOpenCenterPoints(wild,
                hasSavedCenter: false, savedCenterPoints: Vector2.zero,
                out SettingsWindow.OpenPlacement placement, out bool clampRequired);
            Assert.AreEqual(SettingsWindow.OpenPlacement.CallerCenter, placement);
            Assert.IsTrue(clampRequired, $"{LogPrefix} 전제 실패 — 이 가지가 클램프를 요구하지 않습니다.");

            Vector2 applied = UiWindowDrag.ClampCenterPoints(desired, panel, screen,
                topInsetPoints: 0f, bottomInsetPoints: 0f, marginPoints: margin);

            // 양성 대조 — 클램프가 <b>실제로 개입했는가</b>. 이게 없으면 아래 상한은 «입력이 이미
            // 안쪽이었다»와 구별되지 않는다.
            Assert.AreNotEqual(wild.x, applied.x,
                $"{LogPrefix} 화면 밖 x({wild.x})가 그대로 남았습니다 — 클램프가 통째로 죽었습니다.");
            Assert.AreNotEqual(wild.y, applied.y,
                $"{LogPrefix} 화면 밖 y({wild.y})가 그대로 남았습니다.");

            // 성질 검사 — 창 사각형 전체가 안전 영역 안이다("일부라도"가 아니다: [✕]가 화면 밖으로
            // 나가면 이 앱에서 그 창은 영영 닫을 수 없다).
            float maxX = (screen.x - panel.x) * 0.5f - margin;
            float maxY = (screen.y - panel.y) * 0.5f - margin;
            Assert.LessOrEqual(Mathf.Abs(applied.x), maxX + 0.5f,
                $"{LogPrefix} 창 가로가 안전 영역을 벗어났습니다(중심 {applied.x}, 한계 ±{maxX}).");
            Assert.LessOrEqual(Mathf.Abs(applied.y), maxY + 0.5f,
                $"{LogPrefix} 창 세로가 안전 영역을 벗어났습니다(중심 {applied.y}, 한계 ±{maxY}).");

            // ---- 음성 대조 — 이미 안쪽인 자리는 <b>한 비트도</b> 안 움직인다 ----
            //    (클램프가 모든 값을 한 점으로 스냅해 버리면 위 단언이 우연히 통과한다.)
            var inside = new Vector2(120f, -80f);
            Assert.Less(Mathf.Abs(inside.x), maxX, $"{LogPrefix} 전제 실패 — 대조 표본 x가 안쪽이 아닙니다.");
            Assert.Less(Mathf.Abs(inside.y), maxY, $"{LogPrefix} 전제 실패 — 대조 표본 y가 안쪽이 아닙니다.");

            Vector2 untouched = UiWindowDrag.ClampCenterPoints(inside, panel, screen, 0f, 0f, margin);
            Assert.AreEqual(inside.x, untouched.x, 0.001f,
                $"{LogPrefix} 음성 대조 실패 — 화면 안쪽 x까지 클램프가 옮겼습니다({inside} → {untouched}). " +
                "그러면 위 상한 단언은 «모든 값을 한 점으로 스냅했다»와 구별되지 않습니다.");
            Assert.AreEqual(inside.y, untouched.y, 0.001f,
                $"{LogPrefix} 음성 대조 실패 — 화면 안쪽 y까지 클램프가 옮겼습니다({inside} → {untouched}).");

            Debug.Log($"{LogPrefix} ③ 통과 — {wild} → {applied}로 되끌려 오고(한계 ±{maxX}, ±{maxY}), " +
                $"이미 안쪽인 {inside}는 그대로입니다.");
        }
    }
}
