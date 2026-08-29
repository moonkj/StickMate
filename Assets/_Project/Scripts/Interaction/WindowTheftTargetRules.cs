using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Interaction
{
    /// <summary>
    /// 창 도둑(docs/UX_FLOW.md 27-1)의 <b>대상 선정 규칙만</b> 떼어낸 순수 함수 모음.
    ///
    /// 왜 별도 클래스인가 — 이 규칙은 두 번 조용히 죽은 이력이 있다.
    ///   (1) 폭 상한이 캐릭터 신장에만 비례해, characterScale을 0.75로 내린 순간 상한이 237pt로 줄어
    ///       macOS 표준 창 대부분이 후보에서 빠졌다(배율 0.5였다면 158pt).
    ///   (2) 후보 소스가 발판 목록(가려짐 필터를 통과한 창)이라, 작은 창이 큰 창 뒤에 있으면 폭을
    ///       따지기도 전에 사라졌다.
    /// 둘 다 "MonoBehaviour 안에 파묻힌 조건식"이라 테스트로 잠글 수 없었던 것이 재발의 조건이었다.
    /// 여기로 끌어내면 EditMode 테스트가 씬 없이 절대 조건으로 고정할 수 있다.
    ///
    /// 절대 불변 원칙 3: 여기서는 사각형 숫자만 비교한다 — 창을 조작하는 API가 없다.
    /// </summary>
    public static class WindowTheftTargetRules
    {
        /// <summary>
        /// 대상 창 폭 상한(OS 포인트) = max(캐릭터 신장 x 배수, 절대 하한).
        /// 절대 하한이 있는 이유는 StickConfig.windowTheftMinTargetWidthPoints 툴팁에 적어뒀다 —
        /// 요약하면 "시각 설정(characterScale)이 게임플레이 가용성을 바꾸면 안 된다".
        /// </summary>
        public static float ComputeMaxTargetWidthOsPx(float characterHeightOsPx, StickConfig config)
        {
            float multiplier = config != null ? Mathf.Max(0.01f, config.windowTheftMaxTargetWidthMultiplier) : 3f;
            float scaled = Mathf.Max(0f, characterHeightOsPx) * multiplier;
            float floorWidth = config != null ? Mathf.Max(0f, config.windowTheftMinTargetWidthPoints) : 0f;
            return Mathf.Max(scaled, floorWidth);
        }

        /// <summary>
        /// 후보 자격: (a) 실제 창일 것 — Handle이 음수면 FallbackPlatformWindowService의 합성 발판
        /// (Dock/안전망)이라 원본이 존재하지 않는다. (b) 폭이 양수이고 상한 이하일 것.
        /// 가려짐 여부는 <b>보지 않는다</b> — 창 도둑은 딛는 연출이 아니라 미는 연출이라 뒤에 숨은 창도
        /// 그대로 대상이 된다(Platform/IRawWindowRectSource.cs 참고).
        /// </summary>
        public static bool IsEligibleTarget(in PlatformFoothold candidate, float maxWidthOsPx)
        {
            if (candidate.Handle < 0) return false;
            if (candidate.ScreenRect.width <= 0f) return false;
            return candidate.ScreenRect.width <= maxWidthOsPx;
        }

        /// <summary>
        /// source에서 자격을 갖춘 창만 buffer에 담는다(호출부가 재사용 버퍼를 넘긴다 — 24시간 상주 앱
        /// 매 프레임 할당 금지 컨벤션). 반환값은 담긴 개수.
        /// </summary>
        public static int CollectCandidates(IReadOnlyList<PlatformFoothold> source, float maxWidthOsPx,
            List<PlatformFoothold> buffer)
        {
            if (buffer == null) return 0;
            buffer.Clear();
            if (source == null) return 0;
            for (int i = 0; i < source.Count; i++)
            {
                PlatformFoothold f = source[i];
                if (!IsEligibleTarget(f, maxWidthOsPx)) continue;
                buffer.Add(f);
            }
            return buffer.Count;
        }
    }
}
