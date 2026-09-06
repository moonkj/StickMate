using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ PlayMode 쪽 인계본 게이트 — 2026-09-06 신설(test-engineer).
    ///
    /// <para><b>왜 EditMode 의 <c>HandoffTestGate</c>를 못 쓰는가</b>: <c>StickMate.Tests.PlayMode.asmdef</c>가
    /// EditMode 어셈블리를 참조하지 않는다(참조는 Runtime · TestRunner 뿐). 게다가 이 어셈블리에는
    /// <c>InternalsVisibleTo</c>가 없어 <c>AccessoryShapeBuilder</c> 자체를 볼 수 없다 —
    /// 즉 <b>"이 아이템이 인계본인가"를 빌더에게 물어볼 수 없다.</b> 그래서 판정 근거가 다르다.</para>
    ///
    /// <para><b>왜 공유 어셈블리로 올리지 않았나</b>(선택지 검토 기록):
    /// <list type="number">
    ///   <item><b>EditMode 게이트를 공유 위치로 승격</b> — 새 asmdef 하나와 양쪽 참조 추가가 필요하다.
    ///     그런데 EditMode 게이트의 판정은 <c>AccessoryShapeBuilder.Shape.IsHandoff</c>(빌더 내부 데이터)라,
    ///     공유 어셈블리로 옮겨도 <b>PlayMode 에서는 여전히 못 읽는다</b>(InternalsVisibleTo 가 EditMode 하나뿐).
    ///     즉 승격해도 문제가 안 풀리고 어셈블리만 하나 는다.</item>
    ///   <item><b><c>InternalsVisibleTo</c>에 PlayMode 추가</b> — 프로덕션 <c>AssemblyInfo.cs</c>를 고치는 일이고,
    ///     이 저장소는 그 경계를 <b>의도적으로</b> 닫아 두었다(여러 PlayMode 파일이 그 사실을 문서로 명시한다).
    ///     테스트 편의로 프로덕션 경계를 여는 것은 test-engineer 권한 밖이다.</item>
    ///   <item><b>PlayMode 전용 게이트 신설(채택)</b> — 판정을 <b>씬 사실</b>로 한다. 그림에 무엇이 그려졌는지는
    ///     PlayMode 가 <b>원래 보는 것</b>이고, 이 게이트가 끄는 검사들도 전부 «그려진 도형 이름»으로 재던 것들이다.
    ///     판정 근거와 검사 대상이 같은 층에 있으므로 둘이 조용히 갈라질 수 없다.</item>
    /// </list></para>
    ///
    /// <para><b>거짓 초록이 불가능하도록</b>(CLAUDE.md 부재 단언 규칙): 이 게이트는 세 갈래다.
    /// <list type="bullet">
    ///   <item>v1 도형 이름이 <b>실재</b>하면 → <b>건너뛰지 않는다</b>. 그 검사는 그대로 돈다.
    ///     v1 아이템이 돌아오면 게이트가 스스로 열린다(되살림 장치).</item>
    ///   <item>v1 이름이 없고 <b>인계본 조각</b>(<see cref="HandoffPiecePrefix"/>)이 있으면 → 건너뛴다.
    ///     그 검사는 지금 «없어진 이름»을 찾다가 <b>빨간불</b>이 되던 자리다.</item>
    ///   <item><b>둘 다 없으면 → 실패</b>. 장비가 아예 안 그려졌거나 조회 뿌리가 틀린 것이고,
    ///     그 상태를 «건너뜀»으로 삼키면 «아무것도 안 그려져서 조용히 초록»이 된다.</item>
    /// </list></para>
    ///
    /// <para><b>이 게이트가 잃는 것</b>: 호출부마다 인자로 적는다. 러너의 건너뜀 메시지가 곧 손실 목록이다.
    /// 각 호출부에는 <b>되살리는 방법</b>(이름 대신 무엇으로 조회하면 커버리지를 되찾는가)도 함께 적었다 —
    /// 그 작업은 <b>실기 PlayMode 실행이 필요한 재작성</b>이라 별도 라운드 몫이다(트리가 컴파일되는 시점에).</para>
    /// </summary>
    internal static class HandoffPlayModeGate
    {
        /// <summary>인계본 조각이 씬에 만들어질 때의 이름 접두사(<c>AccessoryShapeBuilder.Handoff.cs</c>의
        /// 조각 이름 규약 — 렌더러는 선에 <c>shape.Name</c>, 채움에 <c>shape.Name + "Fill"</c>을 준다).
        /// <para>★ 이 문자열은 <b>니들</b>이다. 그래서 이 게이트는 «없다»를 조용히 통과시키지 않는다 —
        /// v1 이름도 이 접두사도 없으면 <b>실패</b>한다(위 세 갈래).</para></summary>
        internal const string HandoffPiecePrefix = "Piece_";

        private const string LogPrefix = "[인계본게이트-PM]";

        /// <summary><paramref name="root"/> 아래 <b>모든</b> 자식 오브젝트 이름(자기 자신 제외).</summary>
        internal static List<string> DrawnNames(Transform root)
        {
            var names = new List<string>();
            if (root == null) return names;
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == root) continue;
                names.Add(t.name);
            }
            return names;
        }

        /// <summary>그 자리에 인계본 조각이 그려져 있는가. 조건부로 단언을 낮추는 자리에서 쓴다.</summary>
        internal static bool HasHandoffPieces(Transform root)
        {
            foreach (string n in DrawnNames(root))
            {
                if (n.StartsWith(HandoffPiecePrefix, System.StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// v1 도형 이름으로 조회하던 검사를 <b>그 이름이 사라진 경우에만</b> 건너뛴다.
        /// </summary>
        /// <param name="root">그려진 것을 찾을 뿌리(액세서리 컨테이너 · 미니 피규어 등).</param>
        /// <param name="lostRule">이 검사가 잃는 것 + 되살리는 방법. 러너 메시지가 곧 손실 목록이다.</param>
        /// <param name="v1ShapeName">v1 도형 이름(접두사 비교). <c>null</c>이면 «인계본 조각이 있으면 건너뛴다»가 된다 —
        /// v1 이름 하나로 특정되지 않는 검사(집합 비교 등)에서만 쓴다.</param>
        internal static void SkipIfHandoffRendered(Transform root, string lostRule, string v1ShapeName = null)
        {
            Assert.IsNotNull(root, $"{LogPrefix} 조회 뿌리가 null 입니다 — 게이트 판정 자체가 공허합니다.");

            List<string> names = DrawnNames(root);
            Assert.Greater(names.Count, 0,
                $"{LogPrefix} '{root.name}' 아래에 그려진 오브젝트가 하나도 없습니다 — 장비가 아예 안 그려졌거나 " +
                "조회 뿌리가 틀렸습니다. 이 상태를 건너뜀으로 삼키면 «아무것도 없어서 조용히 초록»이 됩니다.");

            bool hasHandoff = false;
            for (int i = 0; i < names.Count; i++)
            {
                if (v1ShapeName != null && names[i].StartsWith(v1ShapeName, System.StringComparison.Ordinal))
                {
                    // v1 도형이 살아 있다 — 이 검사는 <b>돌아야 한다</b>. 게이트가 스스로 열리는 자리다.
                    return;
                }
                if (names[i].StartsWith(HandoffPiecePrefix, System.StringComparison.Ordinal)) hasHandoff = true;
            }

            Assert.IsTrue(hasHandoff,
                $"{LogPrefix} '{root.name}' 아래에 v1 도형('{v1ShapeName}')도 인계본 조각('{HandoffPiecePrefix}…')도 " +
                $"없습니다. 실측: [{string.Join(", ", names)}]. 둘 다 없다는 것은 «인계본으로 갈아탔다»가 아니라 " +
                "«이 자리가 깨졌다»는 뜻이라, 건너뛰지 않고 실패로 남깁니다.");

            Assert.Ignore(
                $"★ 인계본 교체(계약 v2) — 이 검사는 v1 도형 이름" +
                (v1ShapeName != null ? $"('{v1ShapeName}')" : "(v1 이름 규약)") +
                $"으로 그림을 조회하는데, 지금 그 자리에는 인계본 조각('{HandoffPiecePrefix}…')이 그려집니다. " +
                $"잃는 것: {lostRule} " +
                $"실측 조회 결과: [{string.Join(", ", names)}]. " +
                "v1 도형이 돌아오면 이 게이트는 스스로 열린다(판정이 이름 목록이 아니라 <b>그려진 것</b>이다).");
        }
    }
}
