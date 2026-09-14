using System;
using StickMate.Platform;

namespace StickMate.Core
{
    /// <summary>
    /// ★ 2026-09-14 (리더 판정, persona-immersion 소은 #1) — 화면 변경 유예 동안의 캐릭터 <b>보존 동결</b>.
    ///
    /// <para><b>왜</b>: 유예(<see cref="DisplayChangeRenderHold"/>)는 렌더 제출만 줄이고 루프는 60Hz로 돈다.
    /// 상태기계·물리·말풍선 시계가 그대로 흐르면 재개 순간 캐릭터가 튀고, 사용자가 보지 못한 행동에 붙은
    /// 말풍선이 남는다. 신고자는 이미 "캐릭터만 보이고 멈춤"을 겪었다.</para>
    ///
    /// <para><b>전체화면 숨김(<c>StickmanAgent.IsSuspended</c>)과 다른 정지다.</b> 렌더러를 끄지 않고, 진행 중
    /// 연출을 취소하지 않고, 떠 있는 말풍선과 UI 표면을 숨기지 않는다. 그 계열(<c>IsSuspended</c> /
    /// <c>HidesScreenSurfaces</c> / <c>ArePanelsSuppressed</c>)에 섞으면 모니터를 뺄 때마다 UI 숨김과
    /// 항상위 워치독 보류가 함께 켜진다.</para>
    ///
    /// <para><b>소비자</b>(이 목록이 정본): <c>StickmanAgent</c>(상태 Tick·전신 물리·절대 기한·잡담 쿨다운·발판 폴링) /
    /// <c>DialogueBubbleRenderer</c>(수명 시계) / <c>DialogueIntent</c>(새 대사 게시) /
    /// <c>SpectacleEventLock</c>(새 연출 획득). 동결 중에는 전이가 없어야 하므로 대사도 없다(원칙 1).</para>
    /// </summary>
    public static class CharacterPreservationFreeze
    {
        /// <summary>사실의 원천 — dev-platform 계약을 그대로 읽는다. 캐시하지 않는다: 캐시 주인이 사라지면
        /// 동결이 영구화되지만, 계약은 상한 안에 반드시 거짓으로 돌아온다.</summary>
        public static bool IsDisplayChangeHoldActive => DisplayChangeHoldStatus.IsActive;

        /// <summary>새 대사를 게시하면 안 되는가.</summary>
        public static bool BlocksNewDialogue => IsDisplayChangeHoldActive;

        /// <summary>새 연출(스펙터클 락 <b>신규</b> 획득)을 시작하면 안 되는가. 이미 쥔 주인의 재진입은 막지 않는다.</summary>
        public static bool BlocksNewSpectacle => IsDisplayChangeHoldActive;

        /// <summary>
        /// 얼리는 순간 남아 있던 기한을 해제 시점 기준으로 다시 세운다 — 동결된 시간만큼 뒤로 민다.
        /// 얼리기 전에 이미 지난 기한은 그대로 둔다(되살리지 않는다).
        /// </summary>
        public static float RebaseDeadline(float deadline, float frozenAt, float now)
            => deadline > frozenAt ? deadline + Math.Max(0f, now - frozenAt) : deadline;
    }

    /// <summary><see cref="PreservationFreezeLatch.Step"/>의 결과.</summary>
    public enum PreservationFreezeEdge
    {
        None = 0,
        Started,
        Released,
        /// <summary>동결을 유지한 채 유예 에피소드 번호가 바뀌었다(해제와 새 시작이 두 읽기 사이에 끝났다).</summary>
        Continued,
    }

    /// <summary>
    /// 에이전트 한 명의 동결 가장자리 검출기. 계약이 이벤트 없이 폴링만 보증하므로(보증 4) 가장자리는 여기서 만든다.
    /// 순수 — 시계·프레임·에피소드는 호출자가 넘긴다(EditMode가 손으로 흘린다). 할당 없음.
    /// </summary>
    public sealed class PreservationFreezeLatch
    {
        public bool IsFrozen { get; private set; }
        public int Episode { get; private set; }
        public float FrozenAtUnscaledTime { get; private set; } = -1f;
        public int FrozenAtFrame { get; private set; } = -1;
        /// <summary>마지막 해제에서 잰 동결 길이(초).</summary>
        public float LastFrozenSeconds { get; private set; }
        /// <summary>마지막 해제에서 잰 동결 길이(프레임).</summary>
        public int LastFrozenFrames { get; private set; }

        public PreservationFreezeEdge Step(bool holdActive, int episode, float nowUnscaled, int frame)
        {
            if (holdActive && IsFrozen)
            {
                if (episode == Episode) return PreservationFreezeEdge.None;
                Episode = episode;
                return PreservationFreezeEdge.Continued;
            }
            if (!holdActive && !IsFrozen) return PreservationFreezeEdge.None;

            if (holdActive)
            {
                IsFrozen = true;
                Episode = episode;
                FrozenAtUnscaledTime = nowUnscaled;
                FrozenAtFrame = frame;
                return PreservationFreezeEdge.Started;
            }

            IsFrozen = false;
            LastFrozenSeconds = Math.Max(0f, nowUnscaled - FrozenAtUnscaledTime);
            LastFrozenFrames = Math.Max(0, frame - FrozenAtFrame);
            return PreservationFreezeEdge.Released;
        }
    }
}
