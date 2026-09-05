using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// ★ 「이 선은 <b>인계본 착용 조각</b>의 획이다」 표식 — 2026-09-05 계약 v2 (R16).
    ///
    /// <para><see cref="FillOutlineStroke"/>와 같은 장치다. <c>StickmanAgent.ApplyStrokeWidthsForScale</c>의 안전망
    /// 훑기는 몸 바깥의 모든 <see cref="LineRenderer"/>를 낱선 하한(2pt)으로 <b>되올린다</b> — 표식이 없으면
    /// 렌더러가 1pt 로 그린 직후 그 훑기가 2pt 로 도로 굵혀 <b>렌더러만 고치면 아무 일도 일어나지 않는다</b>
    /// (M6 이 채움 경계선에서 겪은 것과 같은 함정). 그래서 선 자신이 답한다.</para>
    /// <para>하한값은 <see cref="StickConfig.MinAccessoryStrokeScreenPoints"/> 한 곳이다.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AccessoryStrokeMark : MonoBehaviour
    {
        public static bool Is(LineRenderer line)
            => line != null && line.TryGetComponent(out AccessoryStrokeMark _);

        public static void Mark(LineRenderer line)
        {
            if (line == null || line.TryGetComponent(out AccessoryStrokeMark _)) return;
            line.gameObject.AddComponent<AccessoryStrokeMark>();
        }
    }
}
