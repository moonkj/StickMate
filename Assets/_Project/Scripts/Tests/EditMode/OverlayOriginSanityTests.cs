using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 사용자 신고 "캐릭터가 창에서 가끔 갑자기 떨어짐"의 <b>근본 원인 3</b> 회귀 잠금
    /// (디버거 가설 H3 — 부분 적중).
    ///
    /// <para>실측 증거(Player.log.prevround): 오버레이 원점이
    /// <c>(0,0) → (0,-805) → (0,-936) → (0,-937) → (0,-78) → (0,0)</c>으로 요동친 직후
    /// <c>[발판상실]</c>이 발생했다. 화면 높이가 982pt이므로 -936은 창의 95%가 화면 위로 빠져나간 값이다.</para>
    ///
    /// <para>★ 2026-09-01 정정: 예전 주석은 이 시퀀스 <b>전체</b>를 "창 애니메이션 중의 일시적 오독"으로
    /// 적었는데 절반만 맞았다. <c>-805/-936/-78</c>은 애니메이션 프레임이 맞지만 <c>-937</c>은
    /// <b>데스크톱 표시(F11)/Exposé 상태의 정상 상태값</b>이다(대조 실험으로 확정, 재현 2회).
    /// 근본 원인은 창 플래그에 <c>.stationary</c>가 빠진 것이고 처방은
    /// <c>Platform/MacOS/MacSpaceBehaviorNative.cs</c>에 있다. 아래 (5)절이 그 아래층 방어선을 잠근다.</para>
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

        // ====================================================================
        // (5) ★ 데스크톱 표시(F11) 좌표 오염 회귀 잠금 — 2026-09-01 대조 실험 확정 건
        // ====================================================================
        // 배경: 우리 창의 collectionBehavior에 .stationary가 빠져 있어 데스크톱 표시(F11)/Exposé가
        // 창을 화면 밖으로 치웠고, 그 **정상 상태값**(원점 y가 한 화면 높이만큼 음수)이 안정적으로
        // 보고되어 좌표계 전체가 통째로 밀렸다. 실기 로그: origin=(0.00,-937.00), size=(1512x982)가 26회,
        // 그로부터 유도된 발판상단OS y=-30.0(=907-937)이 정상값 907.0과 거의 같은 빈도로 나왔다.
        //
        // 근본 처방은 Platform/MacOS/MacSpaceBehaviorNative.cs의 .stationary다(창이 애초에 안 움직인다).
        // 아래 두 테스트는 그 아래층 방어선을 잠근다:
        //   (a) 원점이 오염됐다가 돌아오면 **접지가 그대로 되돌아오는가**(캐릭터는 한 발짝도 안 움직였다).
        //   (b) 탈출구로 오염값을 받아들이는 순간 **침묵하지 않는가**(다음 사람이 또 오진하지 않도록).

        private GameObject _cameraGo;
        private Camera _camera;
        private RenderTexture _cameraTarget;
        private StickConfig _config;

        /// <summary>
        /// EditMode에는 게임 뷰가 없어 <c>Screen.width/height</c>가 환경마다 다르다. 카메라에 고정 크기
        /// 타깃 텍스처를 물려 투영을 결정적으로 만든다 — 이 테스트의 단언은 전부 <b>차분</b>(오염 전/후)이라
        /// 절대 해상도에는 의존하지 않지만, 투영 자체가 퇴화(0픽셀)하면 NaN이 되기 때문이다.
        /// </summary>
        private void SetUpCameraRig()
        {
            _cameraTarget = new RenderTexture(512, 512, 0);
            _cameraGo = new GameObject("OverlayOriginSanityTestCamera", typeof(Camera));
            _cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            _camera = _cameraGo.GetComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 5f;
            _camera.targetTexture = _cameraTarget;

            _config = ScriptableObject.CreateInstance<StickConfig>();
            // 배율은 이 테스트의 주제가 아니다. 수동 오버라이드 1로 고정해 "OS 포인트 == Unity 픽셀"로
            // 두면 아래 차분값이 곧 원점 오염량과 같아져 단언이 읽힌다.
            _config.desktopDpiScale = 1f;
        }

        private void TearDownCameraRig()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_cameraGo != null) Object.DestroyImmediate(_cameraGo);
            if (_cameraTarget != null)
            {
                _cameraTarget.Release();
                Object.DestroyImmediate(_cameraTarget);
            }
            if (_config != null) Object.DestroyImmediate(_config);
            _cameraGo = null;
            _camera = null;
            _cameraTarget = null;
            _config = null;
        }

        [Test]
        public void 원점이_한_화면만큼_오염됐다_복귀하면_접지가_그대로_돌아온다()
        {
            SetUpCameraRig();
            try
            {
                // 오염량은 숫자 937을 베끼지 않고 **화면 높이**에서 유도한다(실측값 -937은 이 화면
                // 높이 982pt의 95.4%였고, 한 화면 높이는 그보다 더 가혹한 경우다).
                float pollution = -Desktop.height;

                var footWorld = new Vector2(0f, 0f);
                Vector2 healthyFootOs =
                    ScreenCoordinateConverter.WorldToOsScreen(_camera, footWorld, _config, out _);

                // 발이 정확히 상단선에 놓인 발판 하나. 좌우로는 넉넉히 감싸 X 판정이 변수가 되지 않게 한다.
                var footholds = new[]
                {
                    new PlatformFoothold(1L,
                        new Rect(healthyFootOs.x - 200f, healthyFootOs.y, 400f, 500f), true)
                };
                float footholdTopOs = footholds[0].ScreenRect.y;

                GroundSensor.GroundInfo before =
                    GroundSensor.Sense(_camera, footWorld, footholds, _config);
                Assert.IsTrue(before.Grounded,
                    "전제 실패 — 오염 전부터 접지가 아니었습니다. 이 테스트의 나머지가 무의미해집니다.");

                // --- 오염 ---
                ScreenCoordinateConverter.OverlayOriginOsScreen = new Vector2(0f, pollution);

                Vector2 pollutedFootOs =
                    ScreenCoordinateConverter.WorldToOsScreen(_camera, footWorld, _config, out _);
                float drift = Mathf.Abs(pollutedFootOs.y - footholdTopOs);

                // 프로덕션 허용오차를 **참조**해 비교한다(숫자를 베끼면 값이 바뀔 때 조용히 무의미해진다).
                Assert.Greater(drift, _config.groundSnapTolerance,
                    "원점 오염이 접지 허용오차 안에 들어갑니다 — 이 테스트가 재현하려는 사고가 " +
                    "재현되지 않고 있다는 뜻입니다(오염량 또는 허용오차 정의가 바뀌었는지 확인하세요).");
                Assert.AreEqual(Mathf.Abs(pollution), drift, 0.001f,
                    "발 OS y가 원점 오염량만큼 정확히 밀리지 않았습니다 — WorldToOsScreen이 원점을 " +
                    "더하는 방식이 바뀌었다면 이 사고의 형태 자체가 달라진 것이므로 다시 조사해야 합니다.");

                GroundSensor.GroundInfo during =
                    GroundSensor.Sense(_camera, footWorld, footholds, _config);
                Assert.IsFalse(during.Grounded,
                    "오염된 원점에서도 접지가 유지됐습니다 — 그렇다면 접지 판정이 원점을 안 보고 " +
                    "있다는 뜻이고, 실기에서 관측된 [발판상실]을 설명할 수 없습니다.");

                // --- 복귀(F11을 한 번 더 눌러 데스크톱 표시가 풀린 순간) ---
                ScreenCoordinateConverter.OverlayOriginOsScreen = Vector2.zero;

                GroundSensor.GroundInfo after =
                    GroundSensor.Sense(_camera, footWorld, footholds, _config);
                Assert.IsTrue(after.Grounded,
                    "원점이 정상으로 돌아왔는데도 접지가 회복되지 않았습니다 — 캐릭터는 한 발짝도 " +
                    "움직이지 않았고 발판도 그대로인데 좌표계만 다녀왔을 뿐입니다. 회복되지 않으면 " +
                    "사용자에게는 '창 위에 있다가 갑자기 떨어졌다'로 보입니다.");
                Assert.AreEqual(before.GroundWorldY, after.GroundWorldY, 0.0001f,
                    "회복 후 스냅 목표 높이가 달라졌습니다 — 좌표 왕복에 잔차가 남았다는 뜻입니다.");
                Assert.AreEqual(before.CurrentFootholdLeftWorldX, after.CurrentFootholdLeftWorldX, 0.0001f,
                    "회복 후 딛고 있는 발판의 좌측 경계가 달라졌습니다(좌표 왕복 잔차).");
            }
            finally
            {
                TearDownCameraRig();
            }
        }

        [Test]
        public void 화면밖_안정보고를_탈출구로_받아들일_때_침묵하지_않는다()
        {
            // 데스크톱 표시(F11) 상태 재현: 원점이 한 화면 높이만큼 위로 밀린 **같은 값**이 계속 온다.
            var pushedOffTop = new Rect(0f, -Desktop.height, HealthyOverlay.width, HealthyOverlay.height);

            // 탈출구가 열리기 직전까지 = 프로덕션 상수를 참조한다(2를 베끼지 않는다).
            for (int i = 0; i < ScreenCoordinateConverter.OffDesktopConfirmReports - 1; i++)
            {
                ScreenCoordinateConverter.ReportOverlayWindowOsRect(pushedOffTop, Desktop);
                Assert.AreEqual(Vector2.zero, ScreenCoordinateConverter.OverlayOriginOsScreen,
                    "확인 횟수를 채우기 전에 이미 받아들여졌습니다.");
            }
            Assert.AreEqual(0, ScreenCoordinateConverter.OffDesktopAcceptedByRepeatCount);

            // 마지막 한 번에 탈출구가 열린다 — 그 순간 경보가 반드시 남아야 한다.
            LogAssert.Expect(LogType.Warning, new Regex(@"\[원점위생\].*실제 이동으로 인정"));
            ScreenCoordinateConverter.ReportOverlayWindowOsRect(pushedOffTop, Desktop);

            Assert.AreEqual(pushedOffTop.position, ScreenCoordinateConverter.OverlayOriginOsScreen,
                "탈출구가 사라졌습니다 — 보조 모니터로 옮긴 사용자가 낡은 원점에 영영 갇힙니다. " +
                "이 오인 통과는 '거부'가 아니라 **원인 쪽**(macOS .stationary / Windows IsIconic)에서 막습니다.");
            Assert.AreEqual(1, ScreenCoordinateConverter.OffDesktopAcceptedByRepeatCount,
                "탈출구 통과 횟수가 집계되지 않았습니다 — 실기 로그에서 이 사고를 세는 유일한 지표입니다.");
        }

        // ====================================================================
        // (6) ★ 변화율(튐) 판정 — 2026-09-01 "면적 문턱이 ±755pt를 통과시킨다" 건
        // ====================================================================
        // 실측(전체화면 진입/해제 24회, /tmp/stickmate-run/stickmate.log): 면적 판정이 통과시킨
        // 원점 이동 폭이 1pt부터 742pt까지 **틈 없이 연속**이었고, 거부된 값의 최솟값은 759pt였다.
        // 경계 756pt = 화면폭 1512 x 0.5. 즉 크기만으로는 애니메이션 중간 프레임과 진짜 이동을
        // 절대 가를 수 없다 — 가를 수 있는 것은 지속성과 변화율뿐이다.

        /// <summary>이 테스트 화면에서의 무확인 튐 상한(pt). 프로덕션 상수를 **참조**해 유도한다 —
        /// 숫자를 베끼면 상수를 바꾸는 순간 테스트가 조용히 무의미해진다(CLAUDE.md).</summary>
        private static float JumpLimitPoints =>
            Mathf.Max(Desktop.width, Desktop.height)
            * ScreenCoordinateConverter.MaxUnconfirmedOriginJumpFraction;

        private static void AcceptHealthyBaseline()
        {
            ScreenCoordinateConverter.ReportOverlayWindowOsRect(HealthyOverlay, Desktop);
            Assert.AreEqual(Vector2.zero, ScreenCoordinateConverter.OverlayOriginOsScreen,
                "전제 실패 — 기준선이 될 정상 사각형이 받아들여지지 않았습니다.");
        }

        [Test]
        public void 화면안에_있어도_한_보고에_크게_튄_원점은_잠정_거부된다()
        {
            AcceptHealthyBaseline();

            // 면적 판정을 **통과하는** 값을 일부러 고른다(그래야 이 테스트가 튐 판정만 잰다).
            float shift = JumpLimitPoints * 4f;
            var jumped = new Rect(-shift, 0f, HealthyOverlay.width, HealthyOverlay.height);
            float onDesktopFraction = (HealthyOverlay.width - shift) / HealthyOverlay.width;
            Assert.Greater(onDesktopFraction, 0.5f,
                "전제 실패 — 이 사각형은 면적 판정에도 걸립니다. 튐 판정만 재려면 화면 안에 " +
                "절반 이상 남아 있어야 합니다.");

            ScreenCoordinateConverter.ReportOverlayWindowOsRect(jumped, Desktop);

            Assert.AreEqual(Vector2.zero, ScreenCoordinateConverter.OverlayOriginOsScreen,
                $"화면 안에 {onDesktopFraction:P0} 남아 있다는 이유로 {shift:F0}pt 튐이 통과했습니다 — " +
                "면적 비율은 '창이 화면 밖으로 나갔나'를 재지 '한 프레임에 얼마나 튀었나'를 재지 않습니다.");
            Assert.AreEqual(1, ScreenCoordinateConverter.RejectedOverlayRectCount);
            StringAssert.Contains("튀었습니다", ScreenCoordinateConverter.LastRejectedOverlayRectReason,
                "거부 사유가 '튐'으로 구분되지 않으면 실기 로그에서 면적 거부와 가릴 수 없습니다.");
        }

        [Test]
        public void 실측된_전체화면전환_요동_시퀀스는_전량_걸러진다()
        {
            AcceptHealthyBaseline();

            // 실기 로그에서 **받아들여졌던** 원점 x 값들(면적 판정을 통과한 것들). 이 시퀀스가
            // 그대로 통과하면 이번 라운드의 수정이 되돌아간 것이다.
            float[] observed =
            {
                -135f, -732f, -72f, -559f, -407f, -371f, -173f, -93f, -227f, -246f,
                -227f, -140f, -114f, -667f, -40f, -184f, -666f, -39f, -372f, -548f,
                -636f, -368f, -285f, -305f, -228f, -156f, -75f, -742f, -110f
            };
            foreach (float x in observed)
            {
                ScreenCoordinateConverter.ReportOverlayWindowOsRect(
                    new Rect(x, 0f, HealthyOverlay.width, HealthyOverlay.height), Desktop);
                Assert.AreEqual(Vector2.zero, ScreenCoordinateConverter.OverlayOriginOsScreen,
                    $"창 슬라이드 애니메이션 중간 프레임 x={x}가 좌표계에 반영됐습니다. " +
                    "매 표본이 다른 값이므로 연속 확인 탈출구도 열려서는 안 됩니다.");
            }
            Assert.AreEqual(0, ScreenCoordinateConverter.OffDesktopAcceptedByRepeatCount,
                "탈출구가 열렸습니다 — 애니메이션 중간 프레임을 '실제 이동'으로 오인했습니다.");
        }

        [Test]
        public void 문턱_아래의_작은_이동은_지연없이_통과한다()
        {
            // ★ 가드를 조이다가 정상 이동까지 버리면 캐릭터가 화면 밖에 얼어붙는다 — 반대쪽 실패를 잠근다.
            // 기동 직후 반드시 일어나는 창장식(타이틀바 28pt) 제거가 이 구간에 들어와야 한다.
            AcceptHealthyBaseline();

            float small = JumpLimitPoints * 0.9f;
            var nudged = new Rect(0f, small, HealthyOverlay.width, HealthyOverlay.height);
            ScreenCoordinateConverter.ReportOverlayWindowOsRect(nudged, Desktop);

            Assert.AreEqual(nudged.position, ScreenCoordinateConverter.OverlayOriginOsScreen,
                $"{small:F0}pt짜리 작은 이동이 거부됐습니다 — 무확인 상한이 과하게 좁습니다. " +
                "타이틀바 제거(28pt)나 반올림 흔들림까지 0.3초 지연되면 매 실행이 손해입니다.");
            Assert.AreEqual(0, ScreenCoordinateConverter.RejectedOverlayRectCount);
        }

        [Test]
        public void 기준선이_없는_첫_보고는_튐판정을_받지_않는다()
        {
            // 기본값 (0,0)은 관측값이 아니라 가정값이다. 그것과의 거리로 첫 관측을 거부하면
            // 메뉴바 아래에서 시작하는 정상 배포 형상이 매 실행 0.3초 늦게 들어온다.
            var real = new Rect(0f, 75f, 1512f, 846f);
            Assert.Greater(Mathf.Abs(real.y), JumpLimitPoints,
                "전제 실패 — 이 형상은 문턱보다 작아 '첫 보고 예외'를 검증하지 못합니다.");

            ScreenCoordinateConverter.ReportOverlayWindowOsRect(real, Desktop);
            Assert.AreEqual(real.position, ScreenCoordinateConverter.OverlayOriginOsScreen,
                "첫 관측이 거부됐습니다 — 비교할 직전값이 없는데 튐 판정을 적용했습니다.");
        }

        [Test]
        public void 튐으로_거부된_값도_연속으로_오면_실제이동으로_인정한다()
        {
            // 영구 고착 방지는 면적 판정과 **같은 탈출구**를 공유해야 한다. 아니면 화면 안쪽으로
            // 크게 옮긴 창(해상도 변경/디스플레이 재배치)이 영영 반영되지 않는다.
            AcceptHealthyBaseline();

            float shift = JumpLimitPoints * 4f;
            var moved = new Rect(-shift, 0f, HealthyOverlay.width, HealthyOverlay.height);

            for (int i = 0; i < ScreenCoordinateConverter.OffDesktopConfirmReports - 1; i++)
            {
                ScreenCoordinateConverter.ReportOverlayWindowOsRect(moved, Desktop);
                Assert.AreEqual(Vector2.zero, ScreenCoordinateConverter.OverlayOriginOsScreen,
                    "확인 횟수를 채우기 전에 이미 받아들여졌습니다.");
            }

            LogAssert.Expect(LogType.Warning, new Regex(@"\[원점위생\].*실제 이동으로 인정"));
            ScreenCoordinateConverter.ReportOverlayWindowOsRect(moved, Desktop);

            Assert.AreEqual(moved.position, ScreenCoordinateConverter.OverlayOriginOsScreen,
                "같은 값이 연속으로 왔는데도 계속 거부됐습니다 — 낡은 원점에 영영 갇힙니다.");
        }

        [Test]
        public void 거부는_로그로_한_줄_이상_남는다()
        {
            // 예전에는 사유를 LastRejectedOverlayRectReason에만 담고 한 줄도 남기지 않아, 실기 로그만
            // 보던 사람은 "원점이 왜 안 갱신되지"를 알 방법이 없었다.
            LogAssert.Expect(LogType.Log, new Regex(@"\[원점위생\].*버렸습니다"));
            ScreenCoordinateConverter.ReportOverlayWindowOsRect(
                new Rect(0f, -Desktop.height, HealthyOverlay.width, HealthyOverlay.height), Desktop);

            Assert.AreEqual(1, ScreenCoordinateConverter.RejectedOverlayRectCount);
            Assert.IsNotEmpty(ScreenCoordinateConverter.LastRejectedOverlayRectReason);
        }
    }
}
