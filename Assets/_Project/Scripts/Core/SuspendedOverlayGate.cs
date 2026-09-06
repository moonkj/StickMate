using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// ★★★ <b>캐릭터가 안 보이는 동안에는 「캐릭터에서 나온 연출」도 화면에 남지 않는다</b> — 2026-09-06.
    ///
    /// ============================================================================
    /// 무엇을 고치는가 (정적 실측, debugger)
    /// ============================================================================
    /// <see cref="HiddenCharacterCommandGate"/>가 <b>시작</b>을 막는 문이라면, 이 게이트는 <b>이미 떠
    /// 있는 것</b>을 감추는 문이다. 둘을 갈라 두면 반드시 이 구멍이 남는다 — 연출이 시작된 <b>뒤에</b>
    /// 전체화면 앱이 켜지는 순서가 그것이다.
    ///
    /// <para><b>왜 저절로 안 걷혔나(구조적 원인)</b>: <see cref="StickmanAgent"/>의 숨김은
    /// <c>SetRenderersEnabled(false)</c>이고, 그것이 닿는 범위는 <b>딱 둘</b>이다 —
    /// (a) Awake에 스냅샷한 <b>캐릭터 자식</b> Renderer, (b) <see cref="ICharacterVisualSource"/>로
    /// <b>스스로 신고한</b> 잉크(액세서리/펫/FX). 그런데 오버레이 연출들은 컨테이너를
    /// <c>SetParent(null)</c>인 <b>독립 루트</b>로 만들고 그 창구에도 등록하지 않는다. 그래서
    /// 캐릭터만 사라지고 연출은 전체화면 게임 위에 그대로 떠 있었다(불변 원칙 2 위반).</para>
    ///
    /// <para>이 구멍을 <b>이미 막고 있던 곳은 한 곳뿐</b>이었다 —
    /// <c>Interaction/HardwareReactionDirector.Update</c>가 <c>IsSuspended</c>에서
    /// <c>ClearAllVisibleReactions()</c>를 부르고, 그 자리 주석이 이 함정을 정확히 적어 두었다.
    /// 나머지는 <c>return</c>만 해서 <b>트리거만</b> 막고 있었다(= 이미 뜬 것은 그대로 남는다).</para>
    ///
    /// ============================================================================
    /// 걷어내지 않고 <b>얼린다</b> — Suspend의 계약이 "취소가 아니라 보존"이기 때문
    /// ============================================================================
    /// <see cref="StickmanAgent"/>의 Suspend는 <b>상태/파라미터/물리를 그대로 보존</b>하고 Tick만
    /// 건너뛴다(그 메서드 문서: "IDLE 리셋 금지"). 상태머신이 멈춰 있으므로 연출을 여기서 <b>파괴</b>하면
    /// 돌아왔을 때 상태는 여전히 그 연출 중인데 그림만 사라진 <b>행동-텍스트 desync</b>가 된다
    /// (원칙 1). 그래서 컨테이너를 <c>SetActive(false)</c>로 감추고 그 연출의 자체 타이머도 함께
    /// 멈춘다 — 돌아오면 하던 자리에서 정확히 이어진다.
    ///
    /// <para><b>부수 효과가 하나 더 있고, 그것도 의도한 것이다</b>: 비활성 오브젝트 아래의
    /// <c>Collider2D</c>는 함께 죽는다. 가출 [간식 주기] 과자처럼 <b>클릭 대상</b>을 들고 있는 연출이
    /// 전체화면 게임 위에서 클릭을 먹지 않게 된다.</para>
    ///
    /// ============================================================================
    /// 어느 값으로 거는가 — <see cref="StickmanAgent.IsSuspended"/>다
    /// ============================================================================
    /// 조건은 <b>"캐릭터가 안 보인다"</b>이지 "표면을 걷는다"가 아니다.
    /// <see cref="StickmanAgent.HidesScreenSurfaces"/>로 걸면 <b>사용자 명시 숨김에서 안 막힌다</b> —
    /// 숨겨 둔 캐릭터의 발밑/어깨/손에서 나온 연출만 화면에 남는 그림이 되고, 그건 이 게이트가 없애려는
    /// 결함 그 자체다. <see cref="HiddenCharacterCommandGate"/>가 같은 이유로 같은 값을 쓴다.
    /// <b>바꾸지 마라.</b>
    ///
    /// ============================================================================
    /// 여기 오지 <b>않는</b> 것(판단 근거를 남긴다 — 다음 사람이 "왜 얘만 빠졌지"로 되돌리지 않게)
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><c>HardwareReactionRenderer</c> — Director가 이미 <b>명시적으로</b> 걷는다(위 문단).</item>
    ///   <item><c>GraffitiRenderer</c> — <c>StickmanAgent.Suspend()</c>가 Graffiti 상태를 강제
    ///     인터럽트하고, 그 전이가 Director의 <c>Cancelled</c> 발행 → 렌더러의 0.18초 취소 페이드로
    ///     이어져 <b>스스로 사라진다</b>. 여기에 얼리기를 걸면 오히려 페이드가 멈췄다가 게임이 끝난 뒤
    ///     낙서가 한 번 번쩍이고 사라진다(지금이 더 낫다).</item>
    ///   <item><c>FocusWatchRenderer</c> — 2026-09-06에 <c>FocusWatchDirector.IsTimerRingWarranted</c>
    ///     한 곳에서 같은 판정을 이미 한다(같은 라운드의 앞선 수정).</item>
    /// </list>
    /// </summary>
    public static class SuspendedOverlayGate
    {
        /// <summary>
        /// 캐릭터가 숨겨져 있으면 <paramref name="container"/>를 감추고 <b>true</b>를 돌려준다 —
        /// 호출부는 그 프레임의 갱신을 통째로 건너뛰면 된다(그러면 연출의 자체 타이머도 함께 멈춘다).
        /// 다시 보이게 되면 컨테이너를 그대로 되살리고 false를 돌려준다.
        /// </summary>
        /// <param name="hiddenFlag">호출부가 들고 있는 "지금 감춰 둔 상태인가" 플래그. 로그를 상태가
        /// <b>바뀔 때 한 번만</b> 남기기 위한 것이다 — 24시간 상주 앱에서 매 프레임 로그는 그 자체가 결함이다.</param>
        /// <param name="logTag">로그 앞머리(예: "[투두]").</param>
        /// <param name="what">무엇을 감췄는지(예: "종이").</param>
        /// <returns>이번 프레임을 건너뛰어야 하면 true.</returns>
        public static bool FreezeAndHide(StickmanAgent agent, GameObject container,
            ref bool hiddenFlag, string logTag, string what)
        {
            bool hide = agent != null && agent.IsSuspended;

            // 멱등 — 컨테이너가 이번 프레임에 새로 생겼어도(숨은 동안 만들어지는 경로가 남아 있을 수
            // 있다) 곧바로 올바른 가시성으로 맞춰진다. SetActive는 값이 다를 때만 부른다.
            if (container != null && container.activeSelf == hide) container.SetActive(!hide);

            if (hide != hiddenFlag)
            {
                hiddenFlag = hide;
                Debug.Log(hide
                    ? $"{logTag} 캐릭터가 숨겨져 {what}도 함께 감춥니다(원칙 2) — " +
                      "걷어내는 것이 아니라 얼리는 것이라, 돌아오면 하던 자리에서 이어집니다."
                    : $"{logTag} 캐릭터가 돌아와 {what}도 다시 보입니다(멈춰 있던 자리에서 이어집니다).");
            }

            return hide;
        }
    }
}
