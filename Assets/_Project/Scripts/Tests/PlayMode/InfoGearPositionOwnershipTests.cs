using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ <b>톱니 위치의 「소유권」 회귀 테스트</b> — 2026-09-02 P0.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 문장 하나
    /// ============================================================================
    /// <b>온보딩이 톱니를 옮겼다 돌려준 뒤에도 <c>UiLayoutModel.HasGearCenter</c>는 여전히 false다.</b>
    ///
    /// <para><b>왜 이게 P0인가</b>: 그 플래그가 true가 되는 순간 그 좌표는 <b>"사용자가 직접 고른 값"</b>이
    /// 되어 세이브에 앉고 재시작해도 유지된다. 온보딩이 톱니를 <b>한 프레임이라도</b> 옮긴 채
    /// <c>PlaceOnScreen</c>이 돌면 <b>앱을 처음 켠 모든 사람</b>이 그 상태로 시작한다. 그리고 저장에는
    /// "이건 온보딩이 만든 값"이라는 표시가 남지 않으므로 <b>사후에 걸러낼 수 없다</b> —
    /// 사전 차단이 유일한 수단이고, 이 테스트가 그 차단을 붙잡아 둔다.</para>
    ///
    /// ============================================================================
    /// ★ 양성 대조를 반드시 먼저 태운다 (이 저장소 거짓 통과 9건의 공통 형태)
    /// ============================================================================
    /// "<c>HasGearCenter</c>가 false다"는 <b>두 가지 서로 다른 세계</b>에서 똑같이 생겼다:
    ///  ① 가드가 실제로 막았다(원하는 것)  ② 애초에 아무 일도 일어나지 않았다(프로브가 죽었다).
    /// 그래서 각 테스트는 <b>진짜 드래그 경로</b>를 먼저 태워 그 플래그가 실제로 <c>true</c>가 되는 것을
    /// 보이고, 같은 프로브로 온보딩 경로를 잰다. 그리고 온보딩 경로에서는 <b>톱니가 화면에서 실제로
    /// 움직였다</b>는 것까지 함께 단언한다 — 안 움직였으면 "저장 안 됐다"는 아무것도 증명하지 않는다.
    ///
    /// <para>입력 주입/저장 파일 백업 관례는 <see cref="InfoGearDragTests"/>와 같다.</para>
    /// </summary>
    public sealed class InfoGearPositionOwnershipTests
    {
        private const string LogPrefix = "[톱니소유권-TEST]";

        private InfoGearIconWidget _gear;

        /// <summary>
        /// ★★ 2026-09-02 <c>test-engineer</c> — 여기 있던 <b>백업/복원</b>은 <b>오염 보존기</b>였다.
        /// 걷어냈다. 되살리지 마라. (<c>FullscreenPanelRetreatTests</c>가 같은 날 먼저 걷어낸 것과
        /// <b>같은 코드</b>가 8개 픽스처에 남아 있었다.)
        ///
        /// <para><b>원래 근거가 사라졌다.</b> 옛 코드는 <c>OneTimeSetUp</c>에서 저장 파일을 통째로 읽어
        /// 두고 <c>OneTimeTearDown</c>에서 <b>그대로 다시 썼다</b>. 정당화는 <i>"저장 파일이 실제 앱의
        /// 것과 같은 경로"</i>였는데, 그 전제는 2026-08-31에 <c>GlobalPlayModeTestIsolation</c>이
        /// 경로를 임시 폴더로 옮기면서 <b>거짓이 됐다</b>.</para>
        ///
        /// <para><b>그리고 뜻이 정반대로 뒤집혔다.</b> 격리된 폴더에서 <c>_hadFile == true</c>는
        /// "개발자 파일이 있다"가 아니라 <b>"앞선 픽스처가 남긴 오염이 있다"</b>는 뜻이다. 옛 TearDown은
        /// 그 오염을 <b>다시 써서 되살렸고</b>, 같은 코드가 여러 픽스처에 있었으므로 오염이 스위트
        /// 전체를 타고 <b>세탁</b>됐다 — 어떤 정리도 그 다음 픽스처의 복원 한 줄에 무효화됐다.
        /// 2026-09-02 실측이 그 결과다: <c>c1-play</c>가 씬 로드 430회 중 "없음 161 → 불러옴 278"로
        /// 도중에 뒤집혔고 <c>스틱메이트 Lv.127</c>이 로그에 505회 찍혔다.</para>
        ///
        /// <para><b>대신 가드를 남긴다.</b> 격리가 꺼진 채로 이 픽스처가 돌면 씬 로드가 개발자의 실제
        /// 저장 파일을 읽고 쓴다. 그때는 조용히 진행하지 않고 <b>즉시 실패</b>한다.</para>
        /// </summary>
        [OneTimeSetUp]
        public void RequireIsolatedSaveFileAndStartClean()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                "저장 경로가 격리되지 않았습니다 — GlobalPlayModeTestIsolation이 돌지 않았습니다. " +
                "이대로 진행하면 개발자의 실제 저장 파일을 읽고 씁니다(절대 불변 원칙 3).");
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
        }

        /// <summary>격리 폴더를 다음 픽스처에 <b>넘기지 않는다</b> — 이 픽스처가 만든 저장 파일을 지운다.
        /// 옛 <c>RestoreRealSaveFile</c>이 하던 "다시 쓰기"의 정확한 반대다(위 문단 참고).</summary>
        [OneTimeTearDown]
        public void ClearIsolatedSaveFile()
        {
            GlobalPlayModeTestIsolation.PurgeIsolatedDirectories();
            UiLayoutModel.ResetForTesting();
        }

        /// <summary>매 테스트를 "한 번도 옮긴 적 없는 신규 사용자"에서 시작한다. 메모리만 지우면
        /// 씬 로드가 저장 <b>파일</b>을 다시 읽어 앞선 테스트의 자리를 되살린다(InfoGearDragTests와 같은 사정).</summary>
        [SetUp]
        public void ResetLayout()
        {
            UiLayoutModel.ResetForTesting();
            CharacterSaveStore.Save();
        }

        private IEnumerator LoadSceneAndResolve()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var found = Object.FindObjectsByType<InfoGearIconWidget>(FindObjectsSortMode.None);
            Assert.AreEqual(1, found.Length,
                $"{LogPrefix} 씬의 InfoGearIconWidget 개수가 {found.Length}개입니다 — 1개여야 합니다.");
            _gear = found[0];
            yield return null;
        }

        /// <summary>화면 안쪽의 안전한 목표(Unity 스크린 픽셀) — 작은 배치 화면에서도 클램프에 안 걸린다.</summary>
        private static Vector2 SafeInsideTarget()
            => new Vector2(Screen.width * 0.45f, Screen.height * 0.5f);

        /// <summary>실제 드래그 경로를 그대로 태워 톱니를 옮긴다(테스트 전용 분기 없음).</summary>
        private IEnumerator DragGearToSafePlace()
        {
            Vector2 start = _gear.IconScreenCenter;
            Vector2 target = SafeInsideTarget();
            _gear.FeedPointerForTests(true, start);

            // ★ 2026-09-02 — 드래그 판정이 거리 OR 시간에서 <b>거리 AND 시간</b>으로 바뀌었다
            //   (InfoGearIconWidget.ShouldBeginDrag). 예전 이 헬퍼는 누르는 프레임에 곧바로 옮겨
            //   경과 0.00초로 끌었는데, 그건 실기에서 물리적으로 불가능한 제스처였고 이제 <b>클릭</b>으로
            //   분류된다. 벽시계로 임계를 채운다(프레임 수 기반 대기 금지, CLAUDE.md).
            //   ★ 임계는 숫자로 베끼지 않고 프로덕션 상수를 참조한다 — 상수가 움직이면 이 대기도 따라온다.
            yield return new WaitForSecondsRealtime(InfoGearIconWidget.DragLongPressSeconds + 0.05f);

            _gear.FeedPointerForTests(true, target);   // 이제 시간과 거리를 둘 다 채웠다.
            Assert.IsTrue(_gear.IsDraggingIcon, $"{LogPrefix} 드래그로 전환되지 않았습니다 — 양성 대조를 태울 수 없습니다.");
            _gear.FeedPointerForTests(false, target);
            yield return null;
        }

        // ================================================================================
        // ① 본 계약 — 온보딩이 옮긴 자리는 저장되지 않는다
        // ================================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 온보딩이_톱니를_옮겨도_사용자가_옮긴_것으로_저장되지_않는다()
        {
            // ==================== 양성 대조 — 프로브가 살아 있는가 ====================
            yield return LoadSceneAndResolve();
            Assert.IsFalse(UiLayoutModel.HasGearCenter, $"{LogPrefix} 시작 상태가 '옮긴 적 없음'이 아닙니다.");

            yield return DragGearToSafePlace();

            Assert.IsTrue(UiLayoutModel.HasGearCenter,
                $"{LogPrefix} ★ 양성 대조 실패 — <b>진짜 드래그</b>조차 UiLayoutModel.SetGearCenter를 " +
                "부르지 않았습니다. 이 상태에서는 아래의 '온보딩은 안 부른다'가 아무것도 증명하지 않습니다" +
                "(프로브가 죽은 것과 구분되지 않습니다).");
            Debug.Log($"{LogPrefix} 양성 대조 통과 — 드래그 경로는 HasGearCenter를 실제로 true로 만든다.");

            // ==================== 본 검증 — 같은 프로브로 온보딩 경로를 잰다 ====================
            UiLayoutModel.ResetForTesting();
            CharacterSaveStore.Save();
            yield return LoadSceneAndResolve();
            Assert.IsFalse(UiLayoutModel.HasGearCenter, $"{LogPrefix} 신규 사용자 상태로 되돌리지 못했습니다.");

            Vector2 defaultScreenCenter = _gear.IconScreenCenter;
            Vector2 defaultPoints = _gear.IconCenterPoints;

            // 온보딩이 톱니를 화면 한가운데로 데려간다(연출은 아직 없다 — 계약만 미리 잠근다).
            Vector2 onboardingPoints = new Vector2(defaultPoints.x * 0.35f, defaultPoints.y + 160f);
            _gear.BeginOnboardingPlacement(onboardingPoints, "테스트: 온보딩 연출");
            yield return null;
            yield return null;

            // (a) ★ 톱니가 <b>진짜로</b> 그 자리에 갔는가. 이게 없으면 (b)는 "아무 일도 안 일어났다"와 같다.
            Assert.IsTrue(_gear.IsOnboardingPositionOwned, $"{LogPrefix} 온보딩 소유권이 서지 않았습니다.");
            Assert.AreEqual(onboardingPoints.x, _gear.IconCenterPoints.x, 1f,
                $"{LogPrefix} 온보딩이 준 가로 좌표로 가지 않았습니다 — 위치를 안 옮긴 채 '저장 안 됨'을 재고 있습니다.");
            Assert.AreEqual(onboardingPoints.y, _gear.IconCenterPoints.y, 1f,
                $"{LogPrefix} 온보딩이 준 세로 좌표로 가지 않았습니다.");
            Assert.Greater((defaultScreenCenter - _gear.IconScreenCenter).magnitude, 20f,
                $"{LogPrefix} 화면 좌표가 사실상 그대로입니다({defaultScreenCenter} → {_gear.IconScreenCenter}) — " +
                "톱니가 움직이지 않았다면 이 테스트는 가드가 아니라 '아무 일도 없음'을 재고 있습니다.");

            // (b) 그런데 저장 계약은 한 톨도 안 건드렸다.
            Assert.IsFalse(UiLayoutModel.HasGearCenter,
                $"{LogPrefix} ★ 온보딩이 옮긴 자리가 <사용자가 옮긴 위치>로 기록됐습니다. " +
                "이 값은 세이브에 앉아 재시작해도 유지되고, 무엇이 온보딩 자리였는지는 저장에 남지 않아 " +
                "<b>사후 복구가 불가능</b>합니다 — 모든 신규 사용자가 같은 상태로 출하됩니다.");
            Assert.IsFalse(_gear.HasCustomPosition,
                $"{LogPrefix} 위젯이 온보딩 자리를 '사용자가 옮긴 자리'로 들고 있습니다 — " +
                "온보딩이 끝나는 순간 다음 프레임에 그대로 저장됩니다.");

            // (c) ★ 최악의 타이밍 — 온보딩 도중에 주기 저장(기본 60초)이 돌아도 파일에 안 남는다.
            Assert.IsTrue(CharacterSaveStore.Save(), $"{LogPrefix} 온보딩 도중 저장에 실패했습니다.");
            UiLayoutModel.ResetForTesting();
            CharacterSaveStore.Load();
            Assert.IsFalse(UiLayoutModel.HasGearCenter,
                $"{LogPrefix} ★ 온보딩 도중 자동 저장이 돌자 온보딩 자리가 <b>디스크에</b> 기록됐습니다 — " +
                "재시작해도 유지되는 상태입니다.");

            // (d) 온보딩이 끝난 뒤에도 여전히 false이고, 톱니는 기본 위치로 돌아온다.
            _gear.EndOnboardingPlacement("테스트: 온보딩 종료");
            yield return null;
            yield return null;

            Assert.IsFalse(_gear.IsOnboardingPositionOwned, $"{LogPrefix} 온보딩 소유권이 반납되지 않았습니다.");
            Assert.IsFalse(UiLayoutModel.HasGearCenter,
                $"{LogPrefix} ★ 이 파일이 잠그는 그 문장 — 온보딩이 끝난 뒤에도 HasGearCenter는 false여야 합니다.");
            Assert.AreEqual(defaultScreenCenter.x, _gear.IconScreenCenter.x, 2f,
                $"{LogPrefix} 온보딩이 끝났는데 톱니가 기본 위치(우상단)로 돌아오지 않았습니다.");
            Assert.AreEqual(defaultScreenCenter.y, _gear.IconScreenCenter.y, 2f,
                $"{LogPrefix} 온보딩이 끝났는데 톱니의 세로 위치가 기본으로 돌아오지 않았습니다.");

            Debug.Log($"{LogPrefix} 계약 확인 — 온보딩 대여({onboardingPoints}) 중에도, 반납 뒤에도 HasGearCenter=false.");
        }

        /// <summary>
        /// ★ <b>사용자가 이미 옮겨 둔 자리는 온보딩이 지나가도 살아남는다.</b>
        ///
        /// <para>가드를 "온보딩 중에는 아무것도 안 한다"로 어설프게 넣으면 반대 방향 사고가 난다 —
        /// 온보딩이 <c>_customCenterPoints</c>를 덮어써서 <b>기존 사용자가 자기 자리를 잃는다</b>.
        /// 신규 사용자를 지키다 기존 사용자를 깨는 것은 개선이 아니다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 온보딩이_지나가도_사용자가_옮겨_둔_자리는_그대로다()
        {
            yield return LoadSceneAndResolve();
            yield return DragGearToSafePlace();

            Vector2 userPoints = _gear.IconCenterPoints;
            Assert.IsTrue(UiLayoutModel.HasGearCenter, $"{LogPrefix} 준비 단계에서 위치가 저장되지 않았습니다.");

            _gear.BeginOnboardingPlacement(new Vector2(userPoints.x + 200f, userPoints.y + 120f), "테스트: 온보딩 대여");
            yield return null;
            yield return null;
            Assert.Greater(Mathf.Abs(userPoints.x - _gear.IconCenterPoints.x), 1f,
                $"{LogPrefix} 온보딩이 톱니를 옮기지 못했습니다 — 아래 '되돌아왔다' 판정이 무의미해집니다.");

            _gear.EndOnboardingPlacement("테스트: 온보딩 종료");
            yield return null;
            yield return null;

            Assert.IsTrue(UiLayoutModel.HasGearCenter,
                $"{LogPrefix} 온보딩이 지나가자 사용자가 옮겨 둔 자리가 <b>사라졌습니다</b>.");
            Assert.AreEqual(userPoints.x, _gear.IconCenterPoints.x, 1f,
                $"{LogPrefix} 온보딩 뒤 사용자의 가로 위치가 달라졌습니다.");
            Assert.AreEqual(userPoints.y, _gear.IconCenterPoints.y, 1f,
                $"{LogPrefix} 온보딩 뒤 사용자의 세로 위치가 달라졌습니다.");
            Assert.AreEqual(userPoints.x, UiLayoutModel.GearCenterPoints.x, 1f,
                $"{LogPrefix} 모델에 남은 좌표가 사용자의 자리와 다릅니다 — 온보딩 자리가 새어 들어갔습니다.");
        }

        // ================================================================================
        // ② 되돌리기 — 프로덕션 경로(설정창 [처음 자리로]가 부르는 그 함수)
        // ================================================================================

        /// <summary>
        /// ★ <b>모델만 지우면 다음 프레임에 되살아난다</b> — 이 회귀가 이 테스트의 존재 이유다.
        ///
        /// <para>위젯은 <c>_hasCustomCenter</c>를 자기 안에 따로 들고 있고 <c>PlaceOnScreen</c>이 매 프레임
        /// 그것을 모델로 되돌려 준다. 그래서 <c>UiLayoutModel.ClearGearCenter()</c>만 부르는 구현은
        /// <b>같은 프레임 안에서 스스로 취소</b>되는데, 화면상으로는 아무 차이가 없어 눈으로 못 잡는다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 처음_자리로가_저장까지_되돌리고_다음_프레임에_되살아나지_않는다()
        {
            yield return LoadSceneAndResolve();
            Vector2 defaultScreenCenter = _gear.IconScreenCenter;

            // 양성 대조 — 옮긴 자리가 실제로 서고 파일에도 남는다.
            yield return DragGearToSafePlace();
            Assert.IsTrue(UiLayoutModel.HasGearCenter, $"{LogPrefix} 양성 대조 실패 — 드래그가 저장 플래그를 세우지 않았습니다.");
            Assert.IsTrue(_gear.HasCustomPosition);

            // 본 검증.
            Assert.IsTrue(_gear.ReturnToDefaultPosition("테스트: 설정창 [처음 자리로]"),
                $"{LogPrefix} 되돌릴 것이 있는데 '할 일 없음'(false)이 돌아왔습니다.");

            Assert.IsFalse(UiLayoutModel.HasGearCenter, $"{LogPrefix} 되돌렸는데 모델이 여전히 '옮김'입니다.");
            Assert.IsFalse(_gear.HasCustomPosition, $"{LogPrefix} 위젯이 여전히 옛 자리를 들고 있습니다.");

            // ★ 프레임을 여러 번 넘긴다 — PlaceOnScreen이 옛 값을 다시 써 넣는 회귀는 여기서만 잡힌다.
            yield return null;
            yield return null;
            yield return null;
            Assert.IsFalse(UiLayoutModel.HasGearCenter,
                $"{LogPrefix} ★ 되돌린 다음 프레임에 위젯이 옛 자리를 <b>다시 확정</b>했습니다 — " +
                "모델만 지우고 위젯의 _hasCustomCenter를 안 내린 구현의 전형적 증상입니다.");

            // 화면에서도 기본 위치(우상단)로 돌아왔는가.
            Assert.AreEqual(defaultScreenCenter.x, _gear.IconScreenCenter.x, 2f,
                $"{LogPrefix} 되돌렸는데 톱니가 우상단으로 가지 않았습니다.");
            Assert.AreEqual(defaultScreenCenter.y, _gear.IconScreenCenter.y, 2f,
                $"{LogPrefix} 되돌렸는데 톱니의 세로 위치가 기본으로 가지 않았습니다.");

            // 디스크까지 — 재시작해도 기본 위치인가(이게 없으면 "화면만 되돌아간" 절반짜리다).
            UiLayoutModel.ResetForTesting();
            CharacterSaveStore.Load();
            Assert.IsFalse(UiLayoutModel.HasGearCenter,
                $"{LogPrefix} 되돌렸는데 저장 파일에는 옛 자리가 남아 있습니다 — 재시작하면 되살아납니다.");

            Debug.Log($"{LogPrefix} 되돌리기 확인 — 화면 · 모델 · 디스크 3곳 모두 기본 위치.");
        }

        // ================================================================================
        // ③ 설정창 배선 — 버튼이 실제로 그 함수에 닿아 있는가
        // ================================================================================

        /// <summary>
        /// ★ <b>보고서가 아니라 클릭을 잰다.</b> 행을 만들어 놓고 콜백을 안 붙여도 컴파일은 통과하고
        /// 화면에도 멀쩡히 보인다 — 이 저장소에서 실제로 났던 형태의 사고다(무장 순서를 뒤집어
        /// "영원히 발동하지 않으면서 컴파일도 테스트도 통과"한 사례가 같은 파일에 주석으로 남아 있다).
        ///
        /// <para>비활성 상태도 함께 잰다: 옮긴 적이 없으면 되돌릴 것이 없으므로 행은 회색이어야 하고,
        /// 옮기고 나면 켜져야 한다. 두 상태를 다 재야 "항상 회색"과 "항상 활성"이 걸러진다.</para>
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator 설정창_톱니_위치_행은_옮긴_뒤에만_눌리고_누르면_되돌아간다()
        {
            yield return LoadSceneAndResolve();

            var settings = Object.FindFirstObjectByType<SettingsWindow>();
            Assert.IsNotNull(settings, $"{LogPrefix} 씬에 SettingsWindow가 없습니다.");

            settings.Open("테스트");
            yield return null;
            yield return null;

            // (a) 아직 옮긴 적 없음 — 행은 회색이어야 한다.
            Assert.IsFalse(settings.GearHomeRowEnabledForTests,
                $"{LogPrefix} 옮긴 적이 없는데 [톱니 위치] 행이 활성입니다 — 눌러도 아무 일이 없는 버튼입니다.");

            // (b) 실제로 옮기면 켜진다(양성 대조 — '항상 회색'이 아니다).
            settings.Close("테스트: 톱니를 드래그하기 위해 잠시 닫음");
            yield return null;
            yield return DragGearToSafePlace();
            Vector2 movedScreenCenter = _gear.IconScreenCenter;
            Assert.IsTrue(UiLayoutModel.HasGearCenter, $"{LogPrefix} 준비 단계 드래그가 저장되지 않았습니다.");

            settings.Open("테스트");
            yield return null;
            yield return null;
            Assert.IsTrue(settings.GearHomeRowEnabledForTests,
                $"{LogPrefix} 옮겼는데도 [톱니 위치] 행이 회색입니다 — 되돌릴 문이 잠겨 있습니다.");

            // (c) ★ 진짜 클릭 경로로 누른다(테스트 전용 분기 없음).
            Rect button = settings.GearHomeButtonScreenRect;
            Assert.Greater(button.width, 0f, $"{LogPrefix} [처음 자리로] 버튼의 사각형이 비어 있습니다 — 행이 만들어지지 않았습니다.");
            yield return ScrollUntilVisible(settings, () => settings.GearHomeButtonScreenRect);

            button = settings.GearHomeButtonScreenRect;
            // ★ 판정 기준은 창(PanelScreenRect)이 아니라 <b>잘리는 영역</b>(내용 뷰포트)이다 —
            //   SettingsWindow.ContainsScreenPoint가 RectMask2D 밖의 클릭을 실제로 버린다.
            Assert.IsTrue(settings.ContentViewportScreenRect.Contains(button.center),
                $"{LogPrefix} [처음 자리로] 버튼이 스크롤해도 내용 영역 안으로 들어오지 않습니다({button}) — " +
                "사용자도 이 버튼에 닿을 수 없다는 뜻입니다.");

            settings.FeedClickForTests(button.center);
            yield return null;
            yield return null;

            Assert.IsFalse(UiLayoutModel.HasGearCenter,
                $"{LogPrefix} ★ [처음 자리로]를 눌렀는데 아무 일도 일어나지 않았습니다 — " +
                "행은 만들어졌지만 콜백이 되돌리기 경로에 닿아 있지 않습니다.");
            Assert.IsFalse(settings.GearHomeRowEnabledForTests,
                $"{LogPrefix} 되돌린 뒤에도 행이 활성입니다 — 이 행의 회색 전환이 곧 <조작이 먹혔다>는 유일한 확인입니다.");
            Assert.Greater((movedScreenCenter - _gear.IconScreenCenter).magnitude, 20f,
                $"{LogPrefix} 톱니가 화면에서 움직이지 않았습니다({movedScreenCenter} → {_gear.IconScreenCenter}).");

            settings.Close("테스트 정리");
            yield return null;

            Debug.Log($"{LogPrefix} 설정창 배선 확인 — 회색 → (드래그) → 활성 → 클릭 → 되돌림 + 다시 회색.");
        }

        /// <summary>[일반] 탭은 항상 넘치므로(SettingsWindow.FooterHeight 주석의 산술) 아래쪽 행은
        /// 스크롤해야 창 안으로 들어온다. 페이지 칩을 <b>실제로 눌러</b> 내린다 —
        /// 스크롤 값을 테스트가 직접 대입하면 사용자가 도달할 수 없는 자리도 통과해 버린다.</summary>
        private static IEnumerator ScrollUntilVisible(SettingsWindow settings, System.Func<Rect> rect)
        {
            const int MaxPages = 8;
            for (int i = 0; i < MaxPages; i++)
            {
                if (settings.ContentViewportScreenRect.Contains(rect().center)) yield break;
                settings.FeedClickForTests(settings.PageDownScreenRect.center);
                // 같은 컨트롤의 연타는 창이 접는다 — 그 창구를 숫자로 베끼지 않고 참조한다.
                yield return new WaitForSecondsRealtime(SettingsWindow.ActionDedupSeconds + 0.05f);
            }
        }
    }
}
