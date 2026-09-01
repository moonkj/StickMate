using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b>릴리스 차단 회귀</b> (2026-09-01) — "캐릭터가 없어진다" 신고의 후보 경로 하나:
    /// <b>화면 밖 + 작업표시줄 뒤에 생기는 안전망 조각</b>.
    ///
    /// ============================================================================
    /// 재현 좌표는 실기 실측값이다 (Windows)
    /// ============================================================================
    /// 안전망은 <b>오버레이 창</b> 기하에서, 작업표시줄은 <b>모니터</b> 기하(<c>GetMonitorInfo</c>)에서
    /// 나왔고 둘이 일치한다는 보장이 없었다. 실측:
    /// <list type="bullet">
    ///  <item>오버레이 원점 <c>(3851, 45)</c>, 크기 3831x2160 → 안전망 오른쪽 끝 <c>7682</c></item>
    ///  <item>모니터 <c>x[3840..7680]</c>, 하단 <c>2160</c>, 작업표시줄 상단 <c>2088</c></item>
    ///  <item>결과: 폭 <c>2pt</c> 조각이 모니터 <b>오른쪽 밖</b>에, 상단 OS y <c>≈2199</c>
    ///        (모니터 하단보다 39px 아래)에 생겨 <b>발판으로 살아남았다</b></item>
    /// </list>
    /// 그 위에 착지하면 화면 밖 + 막대 뒤라 캐릭터가 보이지 않는다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤을 반드시 함께 둔다
    /// ============================================================================
    /// <see cref="화면_경계를_모르면_예전대로_삐져나간다_네거티브_컨트롤"/>가 <b>고치기 전 증상</b>을
    /// 그대로 재현한다. 이게 없으면 "수정 후 통과"가 사실은 입력이 애초에 무해했던 것인지,
    /// 진짜로 고쳐진 것인지 구분할 수 없다(이 저장소에서 실제로 겪은 실패 유형이다).
    /// </summary>
    public sealed class BottomSafetyNetScreenBoundsTests
    {
        // ---- 실기 실측 좌표 (Windows, 2026-09-01) -------------------------------------------
        private const float OverlayOriginX = 3851f;
        private const float OverlayWidth = 3831f;   // -> 오른쪽 끝 7682
        private const float NetTopOsY = 2199f;      // 모니터 하단(2160)보다 39px 아래
        private const float NetThickness = 6f;      // BottomSafetyNetInsetPoints 계열의 얇은 띠

        private const float MonitorLeft = 3840f;
        private const float MonitorRight = 7680f;
        private const float MonitorBottom = 2160f;

        // 작업표시줄은 모니터 가로 전체를 덮는다(rcMonitor.Left..Right).
        private const float TaskbarLeft = MonitorLeft;
        private const float TaskbarRight = MonitorRight;

        private static Rect MeasuredOverlayNetRect()
            => new Rect(OverlayOriginX, NetTopOsY, OverlayWidth, NetThickness);

        private static BottomSafetyNetPolicy.Pieces ResolveMeasured(bool withScreenBounds)
            => BottomSafetyNetPolicy.Resolve(
                MeasuredOverlayNetRect(),
                withScreenBounds, MonitorLeft, MonitorRight, MonitorBottom,
                hasDock: true, dockLeftOsX: TaskbarLeft, dockRightOsX: TaskbarRight);

        /// <summary>★ 본 회귀. 화면 경계를 알면 모니터 밖 2pt 조각이 <b>아예 생기지 않는다</b>.</summary>
        [Test]
        public void 모니터_오른쪽_밖으로_삐져나간_조각은_살아남지_않는다()
        {
            BottomSafetyNetPolicy.Pieces p = ResolveMeasured(withScreenBounds: true);

            Assert.IsFalse(p.HasRight,
                $"모니터 오른쪽 끝({MonitorRight}) 바깥에 안전망 조각이 남았습니다 — rect={p.Right}. " +
                "여기 착지하면 화면 밖 + 작업표시줄 뒤라 캐릭터가 사라집니다.");
            Assert.IsFalse(p.HasLeft,
                $"작업표시줄이 모니터 가로 전체를 덮는데도 왼쪽 조각이 남았습니다 — rect={p.Left}. " +
                "그 X 구간의 바닥은 작업표시줄 발판 하나여야 합니다.");
        }

        /// <summary>★ 네거티브 컨트롤 — 고치기 전(=화면 경계를 모르는 경로)에는 실제로 그 조각이
        /// 생긴다. 이 테스트가 <b>실패하면</b> 위 테스트가 아무것도 증명하지 못하는 상태다.</summary>
        [Test]
        public void 화면_경계를_모르면_예전대로_삐져나간다_네거티브_컨트롤()
        {
            BottomSafetyNetPolicy.Pieces p = ResolveMeasured(withScreenBounds: false);

            Assert.IsTrue(p.HasRight,
                "네거티브 컨트롤이 증상을 재현하지 못했습니다 — 실측 좌표가 바뀌었거나 " +
                "다른 곳에서 이미 걸러지고 있습니다. 이 상태면 위 회귀 테스트는 아무것도 지키지 않습니다.");
            Assert.Greater(p.Right.xMin, MonitorRight - 1f,
                "재현된 조각이 모니터 오른쪽 밖이 아닙니다 — 좌표를 다시 확인하십시오.");
            Assert.Greater(p.Right.yMin, MonitorBottom,
                "재현된 조각의 상단이 모니터 하단보다 위입니다 — 화면 밖 조건이 재현되지 않았습니다.");

            // 그 조각이 최소 폭 문턱을 넘어 "살아남았다"는 것이 사고의 핵심이다.
            // 문턱값은 숫자로 베끼지 않고 상수를 참조한다(CLAUDE.md).
            Assert.Greater(p.Right.width, BottomSafetyNetPolicy.MinPieceWidthOsPoints,
                "조각이 최소 폭 문턱보다 좁습니다 — 그러면 애초에 걸러졌을 것이고 사고가 재현되지 않습니다.");
        }

        /// <summary>살아남는 조각은 <b>언제나</b> 화면 안에 있어야 한다 — 오버레이 창이 어디로 어긋나든.
        /// 실측 한 점만 잠그면 다음 어긋남(오프셋이 다른 모니터 구성)을 못 잡는다.</summary>
        [Test]
        public void 오버레이가_어디로_어긋나도_살아남는_조각은_화면_안에_있다()
        {
            // 작업표시줄이 모니터 가로 전체를 덮지 않는 구성(도킹 툴바 등)도 함께 쓸어본다 —
            // 그때는 조각이 실제로 살아남으므로 "화면 안"이 의미를 가진다.
            float[] offsetsX = { -200f, -11f, 0f, 11f, 200f };
            float[] offsetsY = { -80f, -39f, 0f, 39f, 80f };
            float[] barLefts = { MonitorLeft, MonitorLeft + 600f };
            float[] barRights = { MonitorRight, MonitorRight - 600f };

            int survived = 0;
            foreach (float dx in offsetsX)
            foreach (float dy in offsetsY)
            foreach (float barLeft in barLefts)
            foreach (float barRight in barRights)
            {
                var net = new Rect(MonitorLeft + dx, MonitorBottom - NetThickness + dy, OverlayWidth, NetThickness);
                BottomSafetyNetPolicy.Pieces p = BottomSafetyNetPolicy.Resolve(
                    net, true, MonitorLeft, MonitorRight, MonitorBottom,
                    hasDock: true, dockLeftOsX: barLeft, dockRightOsX: barRight);

                foreach (var piece in new[] { (p.HasLeft, p.Left, "왼쪽"), (p.HasRight, p.Right, "오른쪽") })
                {
                    if (!piece.Item1) continue;
                    survived++;
                    string where = $"{piece.Item3} 조각 rect={piece.Item2} (오버레이 오프셋 {dx},{dy} / 막대 x[{barLeft}..{barRight}])";

                    Assert.GreaterOrEqual(piece.Item2.xMin, MonitorLeft, $"{where} — 모니터 왼쪽 밖입니다.");
                    Assert.LessOrEqual(piece.Item2.xMax, MonitorRight, $"{where} — 모니터 오른쪽 밖입니다.");
                    Assert.LessOrEqual(piece.Item2.yMax, MonitorBottom,
                        $"{where} — 조각 바닥이 모니터 하단({MonitorBottom})보다 아래입니다. 화면 밖입니다.");
                    Assert.AreEqual(NetThickness, piece.Item2.height, 0.001f,
                        $"{where} — 두께가 변했습니다. 화면 안으로 접을 때는 잘라 얇게 만드는 것이 아니라 " +
                        "두께를 유지한 채 밀어 올려야 합니다(두께는 발끝 보정과 묶인 값입니다).");
                }
            }

            Assert.Greater(survived, 0,
                "이 스윕에서 살아남은 조각이 하나도 없습니다 — 단언이 한 번도 실행되지 않았습니다(빈 테스트).");
        }

        /// <summary>화면 경계를 모르는 경로(=macOS)는 <b>한 글자도 바뀌지 않는다</b>.
        /// 접을 근거가 없을 때 지어내지 않는다는 것을 못 박는다.</summary>
        [Test]
        public void 화면_경계가_없으면_오버레이_기하를_그대로_쓴다()
        {
            var net = new Rect(100f, 900f, 1000f, 6f);
            BottomSafetyNetPolicy.Pieces p = BottomSafetyNetPolicy.Resolve(
                net, hasScreenBounds: false, screenLeftOsX: 0f, screenRightOsX: 0f, screenBottomOsY: 0f,
                hasDock: false, dockLeftOsX: 0f, dockRightOsX: 0f);

            Assert.IsTrue(p.HasLeft, "Dock이 없으면 왼쪽 조각 하나가 전체 폭을 차지해야 합니다.");
            Assert.IsFalse(p.HasRight, "Dock이 없는데 오른쪽 조각이 생겼습니다.");
            Assert.AreEqual(net.xMin, p.Left.xMin, 0.001f);
            Assert.AreEqual(net.xMax, p.Left.xMax, 0.001f);
            Assert.AreEqual(net.yMin, p.Left.yMin, 0.001f,
                "화면 경계를 모르는데 세로로 밀었습니다 — 지어낸 값입니다(macOS 경로가 바뀝니다).");
        }

        /// <summary>Dock이 화면 안쪽에만 있는 정상적인 macOS 형태에서는 좌/우 조각이 <b>둘 다</b>
        /// 살아 있어야 한다 — 접기 로직이 멀쩡한 조각까지 지우지 않는지 본다.</summary>
        [Test]
        public void 화면_가운데_Dock이면_좌우_조각이_둘_다_남는다()
        {
            var net = new Rect(MonitorLeft, MonitorBottom - NetThickness, MonitorRight - MonitorLeft, NetThickness);
            BottomSafetyNetPolicy.Pieces p = BottomSafetyNetPolicy.Resolve(
                net, true, MonitorLeft, MonitorRight, MonitorBottom,
                hasDock: true, dockLeftOsX: MonitorLeft + 1200f, dockRightOsX: MonitorRight - 1200f);

            Assert.IsTrue(p.HasLeft, "Dock 왼쪽 바깥 조각이 사라졌습니다 — 그 X 구간에 바닥이 없어 낙하 고착됩니다.");
            Assert.IsTrue(p.HasRight, "Dock 오른쪽 바깥 조각이 사라졌습니다.");
            Assert.AreEqual(1200f, p.Left.width, 0.001f);
            Assert.AreEqual(1200f, p.Right.width, 0.001f);
            Assert.AreEqual(MonitorRight - 1200f, p.Right.xMin, 0.001f,
                "오른쪽 조각이 Dock 오른쪽 끝에서 시작하지 않습니다 — 구멍과 조각 사이에 틈/겹침이 생깁니다.");
            Assert.AreEqual(MonitorLeft + 1200f, p.Left.xMax, 0.001f,
                "왼쪽 조각이 Dock 왼쪽 끝에서 끝나지 않습니다.");
        }

        /// <summary>실오라기 조각은 접지/낙하가 매 프레임 뒤집히는 채터링만 만든다 — 문턱 아래는 버린다.
        /// (문턱값은 숫자로 베끼지 않고 상수를 참조한다.)</summary>
        [Test]
        public void 문턱보다_좁은_조각은_버린다()
        {
            float thin = BottomSafetyNetPolicy.MinPieceWidthOsPoints * 0.5f;
            var net = new Rect(MonitorLeft, MonitorBottom - NetThickness, MonitorRight - MonitorLeft, NetThickness);

            BottomSafetyNetPolicy.Pieces p = BottomSafetyNetPolicy.Resolve(
                net, true, MonitorLeft, MonitorRight, MonitorBottom,
                hasDock: true, dockLeftOsX: MonitorLeft + thin, dockRightOsX: MonitorRight - thin);

            Assert.IsFalse(p.HasLeft, $"폭 {thin}pt 실오라기가 발판으로 살아남았습니다.");
            Assert.IsFalse(p.HasRight, $"폭 {thin}pt 실오라기가 발판으로 살아남았습니다.");
        }
    }
}
