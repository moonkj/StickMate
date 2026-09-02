using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ [외형] 탭이 <b>실제로 3칸</b>인가 — 2026-08-30 표정(FACE) 카테고리 삭제 회귀.
    ///
    /// ============================================================================
    /// 왜 이 테스트가 필요한가
    /// ============================================================================
    /// 카테고리를 하나 지우면 데이터 쪽(<see cref="ItemCatalog"/>/<see cref="EquipmentModel"/>)은
    /// EditMode 테스트가 잡아 준다. 그러나 정보창의 섹션 4칸은 <b>미리 구워 두고 재사용하는</b>
    /// 구조라(카드 16장 = 4섹션 × 4장), 데이터가 3개로 줄어도 네 번째 제목줄이 화면에 그대로 남는다 —
    /// 컴파일도 통과하고 EditMode도 통과하는데 <b>화면에만 빈 칸이 남는</b> 유형이다
    /// (이 프로젝트가 오늘 하루 반복해서 겪은 "컴파일 통과 ≠ 화면에 나옴").
    ///
    /// 그래서 실제 씬을 띄우고, 실제 입력 경로(<see cref="CharacterInfoWindow.FeedClickForTests"/>)로
    /// 탭을 눌러, <b>활성화된 섹션 오브젝트</b>와 그 제목 문자열을 읽는다. 숫자 3을 테스트에 적지 않고
    /// <see cref="EquipmentModel.IsAppearanceSlot"/>로 <b>센다</b> — 훗날 외형 카테고리가 늘거나 줄면
    /// 이 테스트가 함께 따라간다(상수를 적으면 그때 이 파일이 거짓말을 시작한다).
    /// </summary>
    public sealed class AppearanceTabSectionTests
    {
        private const string LogPrefix = "[외형탭-TEST]";

        private CharacterInfoWindow _window;

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
        }

        [UnityTearDown]
        public IEnumerator CloseWindow()
        {
            if (_window != null && _window.IsOpen) _window.Close("테스트 정리");
            _window = null;
            yield return null;
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator AppearanceTabShowsExactlyTheRemainingCategoriesAndNoEmptySection()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _window = Object.FindFirstObjectByType<CharacterInfoWindow>();
            Assert.IsNotNull(_window, $"{LogPrefix} 씬에 CharacterInfoWindow가 없습니다.");

            _window.Toggle("테스트");
            Assert.IsTrue(_window.IsOpen, $"{LogPrefix} 창이 열리지 않았습니다 — 관측 전제가 성립하지 않습니다.");
            yield return null;

            // 실제 사용자와 같은 경로로 [외형] 탭을 누른다(테스트 전용 분기를 만들지 않는다).
            RectTransform[] tabs = Field<RectTransform[]>("_tabRects");
            Assert.IsNotNull(tabs, $"{LogPrefix} _tabRects를 찾지 못했습니다 — 이름이 바뀌었습니다.");
            Assert.GreaterOrEqual(tabs.Length, 2, $"{LogPrefix} 탭이 2개 미만입니다.");
            _window.FeedClickForTests(ScreenCenterOf(tabs[1]));
            yield return null;

            // ---- 기대값은 세지, 적지 않는다 ----
            int expected = 0;
            var expectedNames = new System.Collections.Generic.List<string>(4);
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                var slot = (EquipmentSlot)i;
                if (!EquipmentModel.IsAppearanceSlot(slot)) continue;
                expected++;
                expectedNames.Add(EquipmentModel.SlotName(slot));
            }
            Assert.AreEqual(3, expected,
                $"{LogPrefix} 외형 계열 카테고리가 3개가 아닙니다 — 표정 삭제 후 머리/이펙트/펫만 남아야 합니다.");

            object[] sections = Field<object[]>("_sections");
            Assert.IsNotNull(sections, $"{LogPrefix} _sections를 찾지 못했습니다 — 이름이 바뀌었습니다.");

            int active = 0;
            var seen = new System.Collections.Generic.List<string>(4);
            for (int s = 0; s < sections.Length; s++)
            {
                object view = sections[s];
                Assert.IsNotNull(view, $"{LogPrefix} {s}번 섹션이 만들어지지 않았습니다.");
                var root = Member<GameObject>(view, "Root");
                Assert.IsNotNull(root, $"{LogPrefix} {s}번 섹션에 Root가 없습니다 — 껐다 켤 손잡이가 없습니다.");
                if (!root.activeInHierarchy) continue;
                active++;
                seen.Add(Member<Text>(view, "Title").text);
            }

            Assert.AreEqual(expected, active,
                $"{LogPrefix} [외형] 탭에 보이는 섹션이 {active}칸입니다 — {expected}칸이어야 합니다. " +
                $"제목=[{string.Join(", ", seen)}] (빈 제목줄이 남아 있으면 삭제한 카테고리 자리입니다).");
            CollectionAssert.AreEqual(expectedNames, seen,
                $"{LogPrefix} [외형] 탭 섹션 제목이 카탈로그 순서와 다릅니다: [{string.Join(", ", seen)}]");
            CollectionAssert.DoesNotContain(seen, "표정",
                $"{LogPrefix} 삭제한 [표정] 카테고리가 화면에 남아 있습니다.");
        }

        private T Field<T>(string name) where T : class
        {
            FieldInfo f = typeof(CharacterInfoWindow).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic);
            return f != null ? f.GetValue(_window) as T : null;
        }

        private static T Member<T>(object target, string name) where T : class
        {
            FieldInfo f = target.GetType().GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return f != null ? f.GetValue(target) as T : null;
        }

        /// <summary>uGUI 사각형의 화면 좌표 중심(FeedClickForTests가 받는 좌표계와 같다).</summary>
        private static Vector2 ScreenCenterOf(RectTransform rt)
        {
            var corners = new Vector3[4];
            rt.GetWorldCorners(corners);
            Vector3 center = (corners[0] + corners[2]) * 0.5f;
            return new Vector2(center.x, center.y);
        }
    }
}
