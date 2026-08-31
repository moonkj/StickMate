using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// **"지금 이 화면을 보고 있는 사람이 있는가"** 를 OS에게 물어보는 읽기 전용 창구.
    ///
    /// <para>왜 이 인터페이스가 존재하는가(2026-08-31 2차 성능 라운드의 핵심 발견):
    /// 이 앱의 CPU 비용은 <b>"무엇을 그리는가"가 아니라 "몇 장을 내보내는가"에만 비례한다</b>. 실측:
    /// <code>
    ///   StickMate 실행 중 : WindowServer 20.2%  +  StickMate 22.4%  = 42.6% (코어 1개 기준)
    ///   StickMate 종료 후 : WindowServer  2.2%
    ///   -> 이 앱 한 개가 OS 컴포지터에 부과하는 비용이 18.0%p. 앱 자신의 CPU%에는 잡히지 않는다.
    /// </code>
    /// 같은 표본의 `sample` 프로파일에서 <b>관리 코드(C# 스크립트)는 5,306 표본 중 13개(0.25%)</b>에
    /// 불과했고, 메인 스레드의 바쁜 구간은 특정 함수에 몰리지 않고 수백 개 주소에 평평하게 흩어져 있었다.
    /// 즉 <b>줄일 핫스팟이 없다 — 프레임 한 장의 존재 자체가 비용</b>이다.</para>
    ///
    /// <para>그래서 유일하게 남는 절감 수단은 "프레임을 덜 내보내는 것"인데, 부드러움을 해치지 않고
    /// 그렇게 할 수 있는 시간대가 실제로 존재한다 — <b>아무도 보고 있지 않은 시간</b>이다. 24시간 상주
    /// 앱에서 그 시간은 하루의 대부분이다(디스플레이 슬립, 자리 비움). 이 인터페이스는 그 판단에 필요한
    /// 사실만 OS에서 읽어온다. <b>어떤 값도 쓰지 않는다</b>(CLAUDE.md 원칙 3: 유저 자산 불변).</para>
    ///
    /// <para><b>플랫폼 패리티 주의</b>: 2026-08-31 오전에 "macOS에서만 고친 것이 Windows에 전파되지 않아
    /// 같은 버그가 몇 달 살아남은" 사고가 있었다(VisibleTopEdgeSolver 도입 경위). 그래서 이 기능은
    /// 처음부터 인터페이스 + 양 플랫폼 구현 + 플랫폼 중립 판단 함수(<see cref="FramePacingPolicy"/>)로
    /// 나눠 둔다. <b>판단 로직은 한 곳뿐이고 테스트도 그 한 곳을 겨냥한다.</b></para>
    /// </summary>
    public interface IViewerPresenceService
    {
        /// <summary>지금 시점의 관측값을 채워 준다. 조회에 실패하면 false(호출부는 항상 Active로 폴백).</summary>
        bool TryGetPresence(out ViewerPresenceSnapshot snapshot);
    }

    /// <summary>
    /// OS에서 읽어온 "보는 사람" 관측값 한 묶음. 구조체이므로 매 폴링마다 할당이 없다(24시간 상주 컨벤션).
    /// </summary>
    public readonly struct ViewerPresenceSnapshot
    {
        /// <summary>조회가 실제로 성공했는가. false면 나머지 필드는 의미가 없고 정책은 Active로 간다.</summary>
        public readonly bool Valid;

        /// <summary>디스플레이가 잠들어 있는가(화면이 꺼져 있다). 이때는 <b>누구도 이 앱을 볼 수 없다</b>.</summary>
        public readonly bool DisplayAsleep;

        /// <summary>마지막 사용자 입력(키/마우스/트랙패드)으로부터 지난 초. 알 수 없으면 음수.</summary>
        public readonly float SecondsSinceUserInput;

        /// <summary>OS 저전력 모드(사용자가 명시적으로 켠 것)인가.</summary>
        public readonly bool LowPowerMode;

        /// <summary>배터리로 구동 중인가(AC 연결이 아님).</summary>
        public readonly bool OnBattery;

        public ViewerPresenceSnapshot(bool displayAsleep, float secondsSinceUserInput,
            bool lowPowerMode, bool onBattery)
        {
            Valid = true;
            DisplayAsleep = displayAsleep;
            SecondsSinceUserInput = secondsSinceUserInput;
            LowPowerMode = lowPowerMode;
            OnBattery = onBattery;
        }

        public override string ToString() => Valid
            ? $"화면꺼짐={DisplayAsleep}, 무입력={SecondsSinceUserInput:F0}초, 저전력={LowPowerMode}, 배터리={OnBattery}"
            : "(조회실패)";
    }

    /// <summary>프레임 페이싱 등급. 숫자가 클수록 더 깊게 잠든다.</summary>
    public enum FramePacingTier
    {
        /// <summary>평소 — 사용자가 보고 있고 뭔가 움직인다. <b>여기는 절대 건드리지 않는다</b>
        /// (2026-08-31 사용자 확정: 움직일 때는 60fps).</summary>
        Active = 0,

        /// <summary>사용자가 보고는 있지만 캐릭터가 가만히 서 있고 최근 입력도 없다.</summary>
        Calm = 1,

        /// <summary>자리 비움 — 오랫동안 입력이 없다.</summary>
        Away = 2,

        /// <summary>전체화면 게임 감지로 캐릭터를 숨긴 상태(기존 Suspend 경로).</summary>
        Suspended = 3,

        /// <summary>디스플레이가 잠들어 화면이 꺼졌다 — 볼 수 있는 사람이 물리적으로 없다.</summary>
        DisplayOff = 4,
    }

    /// <summary>
    /// 한 등급에서 실제로 적용할 손잡이 값. 플랫폼 중립이며, 어느 플랫폼이든 이 세 값만 적용하면 된다.
    /// </summary>
    public readonly struct FramePacingPlan
    {
        /// <summary><c>QualitySettings.vSyncCount</c>에 넣을 값. 0이면 vsync를 끄고
        /// <see cref="TargetFrameRate"/>로 제어한다는 뜻.</summary>
        public readonly int VSyncCount;

        /// <summary><c>Application.targetFrameRate</c>에 넣을 값(-1 = 제한 없음/무시).</summary>
        public readonly int TargetFrameRate;

        /// <summary><c>OnDemandRendering.renderFrameInterval</c>에 넣을 값(1 = 매 프레임 렌더).</summary>
        public readonly int RenderFrameInterval;

        public readonly FramePacingTier Tier;

        public FramePacingPlan(FramePacingTier tier, int vSyncCount, int targetFrameRate, int renderFrameInterval)
        {
            Tier = tier;
            VSyncCount = vSyncCount;
            TargetFrameRate = targetFrameRate;
            RenderFrameInterval = Mathf.Max(1, renderFrameInterval);
        }

        public bool SameAs(in FramePacingPlan other) =>
            VSyncCount == other.VSyncCount
            && TargetFrameRate == other.TargetFrameRate
            && RenderFrameInterval == other.RenderFrameInterval;
    }

    /// <summary>
    /// **등급 판단과 손잡이 계산을 하는 유일한 곳** — 순수 함수, 할당 0, OS 호출 0, 테스트 가능.
    ///
    /// <para><b>★ 설계 원칙 하나로 전부 설명된다: "보는 사람이 있을 때는 표시 기구를 바꾸지 않는다."</b>
    /// <list type="bullet">
    /// <item>사람이 보고 있는 등급(Active/Calm)에서는 <c>vSyncCount</c>를 <b>절대 바꾸지 않고</b>
    ///   <c>renderFrameInterval</c>만 조정한다. vSyncCount를 바꾸는 것은 표시 동기화 기구 자체를 바꾸는
    ///   일이라 전환 순간 한 프레임이 튈 수 있고, 이번 사용자 신고가 하필 "부드럽지 않다"였다.
    ///   renderFrameInterval은 디스플레이 위상을 그대로 둔 채 "이번 프레임은 안 그린다"만 정하므로
    ///   남는 프레임의 간격이 여전히 정확히 균일하다(60fps 위상 위의 30fps).</item>
    /// <item>사람이 볼 수 없는 등급(Away/Suspended/DisplayOff)에서만 vSyncCount/targetFrameRate까지
    ///   내린다. 여기서는 위상 균일성이 아무 의미가 없고, 게임 루프 자체를 늦춰 절감을 극대화하는 것이
    ///   맞다(메인 스레드가 전체 비용의 약 60%라 렌더만 건너뛰는 것으로는 절반밖에 못 줄인다).</item>
    /// </list></para>
    ///
    /// <para><b>안전 설계 — 신호를 놓쳐도 절대 얼지 않는다</b>: 가장 얕은 절감 등급(Calm)조차 30fps다.
    /// 즉 "캐릭터가 IDLE이다"라는 판단이 틀려도 최악의 결과가 <b>30fps로 그려지는 것</b>이지 정지 화면이
    /// 아니다. 이것이 이 프로젝트에서 render-on-demand(변화가 없으면 아예 안 그림)를 채택하지 않고
    /// 적응형 프레임레이트를 택한 이유다 — 렌더러가 40개가 넘는 코드베이스에서 "깨우기 신호"를 하나라도
    /// 빠뜨리면 그 연출이 통째로 얼어붙는데, 그 실패는 사용자에게 <b>버그로 보인다</b>.</para>
    /// </summary>
    public static class FramePacingPolicy
    {
        /// <summary>이 시간(초) 이상 입력이 없으면 "자리 비움"으로 본다.</summary>
        public const float AwaySeconds = 180f;

        /// <summary>Calm 등급의 전제 — 최근 이 시간(초) 안에 입력이 있었으면 사용자가 상호작용
        /// 중이라고 보고 무조건 Active를 유지한다. UI(정보창/부채꼴메뉴/포스트잇)를 만지는 중에
        /// 프레임이 떨어지는 일을 <b>UI 코드와 전혀 결합하지 않고</b> 막는 장치다.</summary>
        public const float RecentInputSeconds = 2f;

        /// <summary>디스플레이가 꺼져 있을 때의 절대 fps. 0이 아닌 이유: 깨어남 감지 폴링이
        /// Update()에서 돌기 때문에 완전히 멈추면 영영 못 깨어난다(기존 Suspend 경로와 같은 이유).
        /// 4fps면 깨어남 지연이 최대 0.25초라 사람이 화면을 켜고 눈을 맞추기 전에 이미 복귀한다.</summary>
        public const int DisplayOffTargetFps = 4;

        /// <summary>
        /// 등급을 정한다. 우선순위는 "절감이 큰 순서"가 아니라 <b>"확실한 순서"</b>다 —
        /// 화면이 꺼진 것은 관측된 사실이고, 자리 비움은 추정이다.
        /// </summary>
        public static FramePacingTier DecideTier(in ViewerPresenceSnapshot presence,
            bool suspendedForFullscreen, bool characterIdle)
        {
            if (presence.Valid && presence.DisplayAsleep) return FramePacingTier.DisplayOff;
            if (suspendedForFullscreen) return FramePacingTier.Suspended;
            if (presence.Valid && presence.SecondsSinceUserInput >= AwaySeconds) return FramePacingTier.Away;

            if (characterIdle && presence.Valid
                && presence.SecondsSinceUserInput >= RecentInputSeconds)
            {
                return FramePacingTier.Calm;
            }
            return FramePacingTier.Active;
        }

        /// <summary>
        /// 등급 -> 실제 손잡이 값. <paramref name="baseVSyncCount"/>가 0이면(Windows처럼 vsync를 끄고
        /// targetFrameRate로 제어하는 플랫폼) targetFrameRate 쪽을 나눈다.
        /// </summary>
        /// <param name="baseVSyncCount">평소(Active) vSyncCount. macOS 기본 2.</param>
        /// <param name="baseTargetFrameRate">평소(Active) targetFrameRate. Windows 기본 60.</param>
        /// <param name="lowPowerMode">OS 저전력 모드. Active 등급을 한 칸 낮추는 데만 쓴다.</param>
        public static FramePacingPlan BuildPlan(FramePacingTier tier, int baseVSyncCount,
            int baseTargetFrameRate, bool lowPowerMode)
        {
            // 화면이 꺼졌을 때만 예외적으로 절대값을 쓴다(디스플레이 주기와의 관계 자체가 무의미하다).
            if (tier == FramePacingTier.DisplayOff)
            {
                return new FramePacingPlan(tier, 0, DisplayOffTargetFps, 1);
            }

            int divisor = tier switch
            {
                FramePacingTier.Calm => 2,       // 60 -> 30
                FramePacingTier.Away => 4,       // 60 -> 15
                FramePacingTier.Suspended => 2,  // 60 -> 30 (기존 동작 유지)
                _ => 1,
            };

            // 저전력 모드는 사용자가 OS에서 명시적으로 켠 의사표시다. Active만 한 칸 낮춘다
            // (이미 낮아진 등급을 더 낮추지는 않는다 — 곱하면 Away가 7fps가 되어 과하다).
            if (lowPowerMode && divisor == 1) divisor = 2;
            if (divisor == 1) return new FramePacingPlan(tier, baseVSyncCount, baseTargetFrameRate, 1);

            bool viewerPresent = tier == FramePacingTier.Active || tier == FramePacingTier.Calm;

            if (baseVSyncCount <= 0)
            {
                // targetFrameRate 기구(Windows). 위상 개념이 없으므로 등급과 무관하게 같은 방식으로 나눈다.
                int fps = baseTargetFrameRate > 0 ? Mathf.Max(5, baseTargetFrameRate / divisor) : -1;
                return new FramePacingPlan(tier, 0, fps, 1);
            }

            if (viewerPresent)
            {
                // ★ 보는 사람이 있다 -> vSyncCount는 손대지 않는다(위 클래스 문서의 설계 원칙).
                return new FramePacingPlan(tier, baseVSyncCount, baseTargetFrameRate, divisor);
            }

            // 보는 사람이 없다 -> 게임 루프까지 늦춘다. vSyncCount는 4가 상한이므로 남는 몫만
            // renderFrameInterval로 넘긴다(예: base 2, divisor 4 -> vSync 4 + interval 2 = 15fps).
            int wanted = baseVSyncCount * divisor;
            int vsync = Mathf.Clamp(wanted, 1, 4);
            int leftover = Mathf.Max(1, wanted / vsync);
            return new FramePacingPlan(tier, vsync, baseTargetFrameRate, leftover);
        }

        /// <summary>등급이 바뀔 때 로그로 남길 사람 읽는 설명.</summary>
        public static string DescribeTier(FramePacingTier tier) => tier switch
        {
            FramePacingTier.Active => "활성(사용자가 보고 있고 캐릭터가 움직인다)",
            FramePacingTier.Calm => "정적(캐릭터가 서 있고 최근 입력 없음)",
            FramePacingTier.Away => "자리비움(오랫동안 입력 없음)",
            FramePacingTier.Suspended => "전체화면 숨김",
            FramePacingTier.DisplayOff => "디스플레이 꺼짐(볼 수 있는 사람 없음)",
            _ => tier.ToString(),
        };
    }
}
