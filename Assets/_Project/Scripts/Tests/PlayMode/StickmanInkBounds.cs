using UnityEngine;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// 캐릭터가 화면에 **실제로 그리는 잉크**의 월드 바운딩박스를 구한다(2026-08-29 바닥 높이 라운드).
    ///
    /// 왜 Renderer.bounds를 쓰면 안 되는가 — 이 프로젝트에서 실측으로 드러난 함정이다
    /// (Logs/coder_floor_probe2.log): 캐릭터의 12개 파츠는 전부 LineRenderer인데, Unity가 돌려주는
    /// LineRenderer.bounds는 Y로 정확히 **+1.0유닛(위아래 0.5씩) 부풀려져** 있다. 반지름 0.02유닛짜리
    /// 눈 한 점의 bounds.size.y가 1.027, 지름 0.33유닛짜리 머리 링이 1.330으로 나온다(뷰 정렬 빌보드
    /// 라인에 대한 엔진의 보수적 바운즈).
    /// 그 부풀림을 "캐릭터 발이 루트보다 0.55유닛 아래로 내려간다"는 실측으로 오독한 결과가 바닥 안전망
    /// 상수 BottomSafetyNetInsetPoints=40pt였고, 사용자가 세 번에 걸쳐 "캐릭터가 떠 있다"고 신고한
    /// 원인이었다(그 상수 선언부 문서 참고).
    ///
    /// 그래서 LineRenderer는 정점 좌표 ± 선 반폭으로 직접 계산하고, 그 외 렌더러 타입만 bounds를 쓴다.
    /// 테스트 전용 계측 코드이므로 매 프레임 호출 경로가 아니다(할당 금지 규칙의 대상이 아님).
    /// </summary>
    internal static class StickmanInkBounds
    {
        /// <summary>렌더러 배열이 실제로 그리는 잉크의 월드 바운즈. 하나도 못 구하면 valid=false.</summary>
        public static bool TryCompute(Renderer[] renderers, out Bounds ink)
        {
            ink = default;
            bool any = false;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;

                var lr = r as LineRenderer;
                if (lr != null)
                {
                    float half = 0.5f * Mathf.Max(lr.startWidth, lr.endWidth);
                    for (int q = 0; q < lr.positionCount; q++)
                    {
                        Vector3 p = lr.GetPosition(q);
                        Vector3 w = lr.useWorldSpace ? p : lr.transform.TransformPoint(p);
                        var b = new Bounds(w, new Vector3(half * 2f, half * 2f, 0f));
                        if (!any) { ink = b; any = true; }
                        else ink.Encapsulate(b);
                    }
                    continue;
                }

                if (!any) { ink = r.bounds; any = true; }
                else ink.Encapsulate(r.bounds);
            }

            return any;
        }
    }
}
