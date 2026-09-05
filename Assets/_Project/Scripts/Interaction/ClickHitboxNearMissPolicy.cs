using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ 2026-09-05 진단 전용(사용자 신고 "던진 뒤 일어선 캐릭터를 마우스로 잡아끌어도 아무 반응 없음", Windows) —
    /// <b>"캐릭터를 잡으려고 눌렀는데 콜라이더가 안 잡혔다"</b>를 로그로 남기기 위한 순수 규칙.
    /// 동작을 바꾸는 코드는 한 줄도 없다. 판정·문자열만 여기 있고, OS 호출도 UnityEngine 시간 조회도 없다 —
    /// 그래서 EditMode 테스트가 벽시계 초를 인자로 넘겨 규칙을 그대로 실행한다.
    ///
    /// <para><b>왜 필요한가</b>: 전역 폴링 경로(<c>StickmanClickHitbox.ProcessGlobalButtonSample</c>)는 버튼 상승 엣지에서
    /// 커서가 콜라이더 밖이면 <b>완전히 침묵</b>했다. 그래서 "드래그 무반응" 신고를 받아도 Player.log로는
    /// (가) 우리 상태기계가 거부했는지(그건 <c>[2/6] 드래그 진입 무시</c>로 남는다), (나) 클릭이 콜라이더에 안 닿았는지,
    /// (다) 클릭 자체가 우리에게 안 왔는지를 가를 수 없었다(<c>docs/DEBUG_WINDOWS_THROW_LANDING_STALL.md</c> §5 마지막 행).
    /// 이 규칙은 (나)를 로그로 만든다.</para>
    ///
    /// <para><b>왜 "근접"만 남기는가</b>: 사용자는 바탕화면 아무 데나 클릭한다. 캐릭터에서 먼 클릭까지 남기면
    /// 24시간 상주 앱의 로그가 클릭 수만큼 자란다. 몸 중심에서 <see cref="NearRadiusHeights"/> 신장 안의 클릭만
    /// "잡으려 했던 클릭"으로 본다(콜라이더 실루엣 폭은 신장의 0.2~0.4배라 1.5배면 의도된 클릭을 놓치지 않는다).
    /// 그리고 같은 계열 로그는 <see cref="LogMinIntervalSeconds"/> 간격으로만 남긴다(벽시계 초, 프레임 수 아님).</para>
    /// </summary>
    public static class ClickHitboxNearMissPolicy
    {
        /// <summary>로그 태그. 기존 태그 관례(<c>[발판상실]</c>/<c>[발판유예]</c>/<c>[던지기회전]</c>)처럼 대괄호 한글 한 단어.</summary>
        public const string LogTag = "[히트판정]";

        /// <summary>"잡으려 했던 클릭"으로 볼 반경 — 몸 중심(발끝 + 신장/2)에서 신장 배수.</summary>
        public const float NearRadiusHeights = 1.5f;

        /// <summary>같은 계열 로그의 최소 간격(초, 벽시계). 연타해도 2초에 한 줄이다.</summary>
        public const float LogMinIntervalSeconds = 2f;

        /// <summary>커서가 몸 중심에서 "근접" 반경 안인가. 신장이 0/음수/NaN이면 판정 불가 = 근접 아님(로그 없음 쪽이 안전).</summary>
        public static bool IsNear(float distanceWorld, float characterHeightWorld)
        {
            if (!(characterHeightWorld > 0f)) return false;
            if (!(distanceWorld >= 0f)) return false;   // NaN도 여기서 걸러진다.
            return distanceWorld <= characterHeightWorld * NearRadiusHeights;
        }

        /// <summary>
        /// 지금 로그를 남겨도 되는가(레이트리밋). 첫 호출은 <c>lastLogSeconds = float.NegativeInfinity</c>로 들어와 항상 true다.
        /// 부정형(<c>!(x &lt; y)</c>)으로 쓰는 이유: 어느 한쪽이 NaN이면 비교가 false라 <b>로그를 남기는 쪽</b>으로 떨어진다 —
        /// 진단 로그는 "모르면 남긴다"가 맞다(반대로 동작 코드였다면 "모르면 안 한다"가 맞았을 것이다).
        /// </summary>
        public static bool ShouldLog(float nowSeconds, float lastLogSeconds)
            => !(nowSeconds - lastLogSeconds < LogMinIntervalSeconds);

        /// <summary>근접 미스 클릭 한 줄. 숫자는 전부 인자로 받는다(여기서 계산하지 않는다 — 호출부가 실제로 판정에 쓴 값이 찍혀야 한다).</summary>
        public static string FormatNearMiss(string source, Vector2 cursorWorld, Vector2 bodyCenterWorld, Vector2 footWorld,
            float characterHeightWorld, float distanceWorld, bool hasEnvelope, Bounds envelope,
            int activeColliders, int totalColliders, StickmanStateId state, int frame)
        {
            float heights = characterHeightWorld > 0f ? distanceWorld / characterHeightWorld : float.NaN;
            string env = hasEnvelope
                ? $"({envelope.min.x:F2}, {envelope.min.y:F2})~({envelope.max.x:F2}, {envelope.max.y:F2})"
                : "(활성 콜라이더 없음)";
            return $"{LogTag} 근접 클릭이 히트박스를 벗어남({source}) — 커서 월드={cursorWorld.ToString("F2")}, " +
                $"몸 중심={bodyCenterWorld.ToString("F2")}, 발끝={footWorld.ToString("F2")}, " +
                $"거리={distanceWorld:F2}유닛({heights:F2}신장, 근접 반경 {NearRadiusHeights:F1}신장), " +
                $"콜라이더 외접={env} 활성 {activeColliders}/{totalColliders}개, 상태={state}, 프레임#{frame}. " +
                $"이 줄은 '클릭은 왔는데 캐릭터를 못 잡았다'의 증거다(같은 계열 최소 {LogMinIntervalSeconds:F0}초 간격).";
        }

        /// <summary>클릭은 왔는데 커서 좌표를 읽지 못한 경우 — 거리를 잴 수 없으므로 근접 판정 없이(레이트리밋만) 남긴다.
        /// 커서를 못 읽으면 드래그는 원리적으로 시작될 수 없어(DragThrowState.Enter) 그 자체가 진단 사실이다.</summary>
        public static string FormatCursorUnavailable(string source, StickmanStateId state, int frame)
            => $"{LogTag} 클릭이 왔지만 커서 좌표를 읽지 못함({source}) — 상태={state}, 프레임#{frame}. " +
               "커서를 못 읽으면 드래그는 시작될 수 없다(ICursorPositionService/ScreenCoordinateConverter 확인).";
    }
}
