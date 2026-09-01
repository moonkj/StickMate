using NUnit.Framework;
using UnityEngine;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 사용자 신고 "캐릭터가 창에서 가끔 갑자기 떨어짐"의 <b>근본 원인 3</b> 회귀 잠금
    /// (디버거 가설 H3 — 부분 적중).
    ///
    /// <para>실측 증거(Player.log.prevround): 오버레이 원점이
    /// <c>(0,0) → (0,-805) → (0,-936) → (0,-937) → (0,-78) → (0,0)</c>으로 요동친 직후
    /// <c>[발판상실]</c>이 발생했다. 화면 높이가 982pt이므로 -936은 창의 95%가 화면 위로 빠져나간
    /// 값이다 — 실재할 수 없는 창이고, 창 애니메이션 도중의 일시적 오독이다.</para>
    ///
    /// <para>원점이 틀리면 <see cref="ScreenCoordinateConverter.WorldToOsScreen"/>이 통째로 틀어져
    /// "발 OS y"와 "발판 상단 y"의 비교가 무너진다 = <b>창은 그대로인데 접지가 풀린다.</b></para>
    ///
    /// <para>이 테스트는 <b>영구 고착 방지</b>(같은 값이 연속으로 오면 실제 이동으로 인정)까지 함께
    /// 잠근다 — 위생 검사가 만들 수 있는 가장 심각한 부작용이 "낡은 원점에 영원히 갇히기"이기 때문이다.</para>
    /// </summary>
    public sealed class OverlayOriginSanityTests
    {
        // 실측 로그의 그 화면(1512x982). 숫자를 여기 적는 것은 "재현할 사건"이지 제품 상수가 아니다.
        private static readonly Rect Desktop = new Rect(0f, 0f, 1512f, 982f);
        private static readonly Rect HealthyOverlay = new Rect(0f, 0f, 1512f, 982f);

        private Vector2 _savedOrigin;
        private bool _savedSwitch;
        private float _savedDpiScale;

        [SetUp]
        public void SetUp()
        {
            _savedOrigin = ScreenCoordinateConverter.OverlayOriginOsScreen;
            _savedSwitch = ScreenCoordinateConverter.OverlayOriginSanityCheckEnabled;
            // 이 파일은 창 사각형을 보고하므로 AutoDpiScale도 함께 움직인다 — 전역 static이라
            // 다른 테스트로 새어 나가지 않게 반드시 원복한다.
            _savedDpiScale = ScreenCoordinateConverter.AutoDpiScale;
            ScreenCoordinateConverter.OverlayOriginSanityCheckEnabled = true;
            ScreenCoordinateConverter.ResetOverlayRectSanityState();
            ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;
        }

        [TearDown]
        public void TearDown()
        {
            ScreenCoordinateConverter.OverlayOriginOsScreen = _savedOrigin;
            ScreenCoordinateConverter.OverlayOriginSanityCheckEnabled = _savedSwitch;
            ScreenCoordinateConverter.AutoDpiScale = _savedDpiScale;
            ScreenCoordinateConverter.ResetOverlayRectSanityState();
        }

        // ====================================================================
        // (1) 실측 시퀀스 재현 — 화면 밖 원점은 버려지고 직전 유효값이 유지된다
        // ====================================================================

        [Test]
        public void 화면밖으로_대부분_빠져나간_원점보고는_버려지고_직전값이_유지된다()
        {
            ScreenCoordinateConverter.ReportOverlayWindowOsRect(HealthyOverlay, Desktop);
            Assert.AreEqual(Vector2.zero, ScreenCoordinateConverter.OverlayOriginOsScreen,
                "전제 실패 — 정상 사각형이 받아들여지지 않았습니다.");

            // 실측 로그의 요동값을 순서 그대로 흘려 넣는다(매 표본이 다른 값 = 애니메이션 중 오독).
            foreach (float badY in new[] { -805f, -936f, -937f })
            {
                ScreenCoordinateConverter.ReportOverlayWindowOsRect(
                    new Rect(0f, badY, HealthyOverlay.width, HealthyOverlay.height), Desktop);

                Assert.AreEqual(Vector2.zero, ScreenCoordinateConverter.OverlayOriginOsScreen,
                    $"원점 y={badY}(화면 높이 {Desktop.height})가 받아들여졌습니다 — 그 프레임의 " +
                    "모든 OS 좌표 변환이 통째로 틀어져 발판 비교가 무너집니다.");
            }

            Assert.AreEqual(3, ScreenCoordinateConverter.RejectedOverlayRectCount,
                $"거부 횟수가 예상과 다릅니다. 마지막 사유: {ScreenCoordinateConverter.LastRejectedOverlayRectReason}");
            Debug.Log($"[원점위생] 마지막 거부 사유 — {ScreenCoordinateConverter.LastRejectedOverlayRectReason}");
        }

        [Test]
        public void 정상범위_원점은_그대로_받아들인다()
        {
            // 메뉴바 아래에서 시작하는 실제 배포 형상(실측 (0,75,1512,846)).
            var real = new Rect(0f, 75f, 1512f, 846f);
            ScreenCoordinateConverter.ReportOverlayWindowOsRect(real, Desktop);
            Assert.AreEqual(real.position, ScreenCoordinateConverter.OverlayOriginOsScreen,
                "정상적인 창 사각형이 거부됐습니다 — 위생 검사가 과하면 원점이 영영 갱신되지 않습니다.");
            Assert.AreEqual(0, ScreenCoordinateConverter.RejectedOverlayRectCount);
        }

        [Test]
        public void 절반쯤_걸친_창은_거부하지_않는다()
        {
            // "명백히 밖"의 보수적 해석 — 화면 밖으로 절반쯤 끌어다 놓는 정상 사용은 통과해야 한다.
            var half = new Rect(0f, -Desktop.height * 0.45f, HealthyOverlay.width, HealthyOverlay.height);
            ScreenCoordinateConverter.ReportOverlayWindowOsRect(half, Desktop);
            Assert.AreEqual(half.position, ScreenCoordinateConverter.OverlayOriginOsScreen,
                "55% 남은 창이 거부됐습니다 — 판정이 '명백히 밖'보다 엄격합니다.");
        }

        // ====================================================================
        // (2) 숫자가 망가진 보고 — 스위치와 무관하게 언제나 거부
        // ====================================================================

        [Test]
        public void NaN_무한대_0크기_보고는_스위치와_무관하게_거부한다()
        {
            ScreenCoordinateConverter.OverlayOriginSanityCheckEnabled = false;
            ScreenCoordinateConverter.OverlayOriginOsScreen = new Vector2(7f, 11f);

            var broken = new[]
            {
                new Rect(float.NaN, 0f, 100f, 100f),
                new Rect(0f, float.NaN, 100f, 100f),
                new Rect(0f, 0f, float.PositiveInfinity, 100f),
                new Rect(0f, 0f, 0f, 100f),
                new Rect(0f, 0f, 100f, -5f),
            };
            foreach (Rect r in broken)
            {
                ScreenCoordinateConverter.ReportOverlayWindowOsRect(r, Desktop);
                Assert.AreEqual(new Vector2(7f, 11f), ScreenCoordinateConverter.OverlayOriginOsScreen,
                    $"망가진 사각형 {r}이 받아들여졌습니다 — NaN이 좌표계에 들어가면 이후 모든 변환이 " +
                    "NaN이 되어 캐릭터가 영원히 사라집니다(복구 경로 없음).");
            }
        }

        // ====================================================================
        // (3) 영구 고착 방지 — 같은 값이 연속으로 오면 실제 이동으로 인정한다
        // ====================================================================

        [Test]
        public void 같은_화면밖_값이_연속으로_오면_실제이동으로_인정한다()
        {
            // 보조 모니터(주 디스플레이 왼쪽)로 앱을 옮긴 상황 재현 — macOS가 넘겨주는 경계는
            // 주 디스플레이뿐이라 정상 창인데도 1차 판정에는 걸린다. 그래도 갇히면 안 된다.
            var secondaryDisplay = new Rect(-Desktop.width, 0f, Desktop.width, Desktop.height);

            ScreenCoordinateConverter.ReportOverlayWindowOsRect(secondaryDisplay, Desktop);
            Assert.AreEqual(Vector2.zero, ScreenCoordinateConverter.OverlayOriginOsScreen,
                "첫 보고는 잠정 거부여야 합니다(창 애니메이션 중 오독과 구분이 안 되는 시점).");

            ScreenCoordinateConverter.ReportOverlayWindowOsRect(secondaryDisplay, Desktop);
            Assert.AreEqual(secondaryDisplay.position, ScreenCoordinateConverter.OverlayOriginOsScreen,
                "같은 사각형이 연속으로 보고됐는데도 계속 거부됐습니다 — 보조 모니터로 옮기면 " +
                "낡은 원점에 영영 갇힙니다(위생 검사가 만들 수 있는 가장 심각한 부작용).");
        }

        [Test]
        public void 매번_다른_화면밖_값은_인정하지_않는다()
        {
            // 애니메이션 중 오독은 매 표본이 다르다 = 연속 확인 카운터를 채우지 못한다.
            foreach (float badY in new[] { -805f, -936f, -937f, -900f, -850f })
            {
                ScreenCoordinateConverter.ReportOverlayWindowOsRect(
                    new Rect(0f, badY, HealthyOverlay.width, HealthyOverlay.height), Desktop);
            }
            Assert.AreEqual(Vector2.zero, ScreenCoordinateConverter.OverlayOriginOsScreen,
                "매 표본이 다른 요동값인데 원점이 갱신됐습니다 — 연속 확인 조건이 느슨합니다.");
        }

        // ====================================================================
        // (4) 네거티브 컨트롤 / 하위 호환
        // ====================================================================

        [Test]
        public void 스위치를_끄면_예전거동_그대로_받아들인다()
        {
            ScreenCoordinateConverter.OverlayOriginSanityCheckEnabled = false;
            var bad = new Rect(0f, -936f, HealthyOverlay.width, HealthyOverlay.height);
            ScreenCoordinateConverter.ReportOverlayWindowOsRect(bad, Desktop);
            Assert.AreEqual(bad.position, ScreenCoordinateConverter.OverlayOriginOsScreen,
                "스위치를 껐는데도 거부됐습니다 — 되돌릴 방법이 없으면 회귀 조사가 불가능합니다. " +
                "(이 케이스가 통과한다는 것은 위 (1)의 초록이 '검사가 실제로 동작해서'라는 증거이기도 합니다.)");
        }

        [Test]
        public void 데스크톱_경계를_모르면_예전거동_그대로다()
        {
            // 에디터/헤드리스/모바일 등 경계를 보고하지 않는 경로는 한 글자도 바뀌면 안 된다.
            var bad = new Rect(0f, -936f, HealthyOverlay.width, HealthyOverlay.height);
            ScreenCoordinateConverter.ReportOverlayWindowOsRect(bad);
            Assert.AreEqual(bad.position, ScreenCoordinateConverter.OverlayOriginOsScreen,
                "경계를 모르는 호출(구 시그니처)에서 거동이 바뀌었습니다 — 기존 테스트/플랫폼이 깨집니다.");
        }
    }
}
