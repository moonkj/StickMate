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

        /// <summary>사용자가 보고는 있지만 캐릭터가 <b>잠깐</b> 서 있다(정지 지속 시간이 짧다).</summary>
        Calm = 1,

        /// <summary>
        /// 사용자는 있고 <b>입력도 하고 있을 수 있지만</b>, 캐릭터가 충분히 오래 제자리에 서 있다.
        ///
        /// <para><b>★ 2026-09-01 컴포지터 라운드에서 추가된 등급이고, 이 라운드의 전부다.</b>
        /// 사용자 Windows 실측이 <c>StickMate 30% + dwm 30~40%</c>였다. 그런데 그 사용자는
        /// <b>업무 중</b>이었다 — 즉 계속 키보드/마우스를 만지고 있었고, 그래서
        /// <see cref="Calm"/>의 전제(<see cref="FramePacingPolicy.RecentInputSeconds"/> = 최근 2초간
        /// 무입력)가 <b>하루 종일 한 번도 성립하지 않았다</b>. 결과: 캐릭터가 가만히 서 있는 그
        /// 시간에도 초당 60장을 제출했고, 투명 레이어드 창은 제출할 때마다 DWM이 바탕화면 전체를
        /// 다시 합성한다.</para>
        ///
        /// <para><b>그래서 이 등급은 "최근 입력"을 조건에서 뺀다.</b> 근거는 두 개다:
        /// <list type="number">
        /// <item><b>입력은 우리 앱을 향한 것이 아닐 수 있다</b>(대부분 아니다 — 상주 장식 앱이다).
        ///   정말 우리 UI를 만지는 중이라면 그 표면이 <c>FramePacing.HoldActiveForInteraction()</c>으로
        ///   스스로 알린다(관측). 무입력 시간은 그 관측을 대신하던 <b>추정</b>이었고, 홀드 배선이
        ///   완성된 지금은 추정을 유지할 이유가 사라졌다.</item>
        /// <item><b>캐릭터가 서 있을 때 실제로 움직이는 것의 크기를 재 봤다</b>(1080p 기준
        ///   45픽셀/월드유닛, 캐릭터 배율 0.75):
        ///   호흡 = 진폭 0.012유닛 x 0.8Hz -> <b>진폭 0.4픽셀</b>(peak-to-peak 0.8px, 서브픽셀),
        ///   호흡 팔각도 ±1.5도 -> 손끝 <b>0.3픽셀</b>,
        ///   눈동자 커서 추적 = 최대 오프셋 0.09유닛 -> <b>편도 3픽셀</b>.
        ///   즉 이 구간에서 "부드러움"을 구성하는 움직임의 총량이 <b>수 픽셀</b>이다. 프레임을
        ///   1/4로 줄여도 프레임당 변화가 1픽셀 남짓이라 사람 눈에 계단이 생길 크기가 아니다.
        ///   (걷기는 다르다 — 한 걸음에 관절이 수십 도 움직인다. 그래서 걷는 순간 즉시 <see cref="Active"/>로
        ///   복귀하며, 그 복귀는 폴링을 기다리지 않는다.)</item>
        /// </list></para>
        ///
        /// <para><b>대가</b>: "캐릭터 상태 ID = Idle"은 <b>화면에 아무것도 안 움직인다</b>와 같은 말이
        /// 아니다. 커서 추종 펫(<c>Interaction/CharacterPetRenderer.cs</c>의 PetCursor)은 캐릭터가
        /// 서 있어도 커서를 따라 화면을 가로지른다. 그 하나는 렌더러가 스스로 홀드를 거는 방식으로
        /// 처리했다(같은 파일 <c>TickCursorFriend</c>). 앞으로 "캐릭터와 독립적으로 크게 움직이는"
        /// 연출을 추가하면 <b>같은 방식으로 자기 자신을 신고</b>해야 한다.</para>
        /// </summary>
        Still = 2,

        /// <summary>자리 비움 — 오랫동안 입력이 없고 <b>그 사이 캐릭터도 제자리에 서 있다</b>.
        /// 두 조건은 AND다. 무입력만으로는 이 등급이 아니다(<see cref="FramePacingPolicy.AwaySeconds"/>
        /// 문서의 "무입력은 이탈이 아니라 몰입 신호일 수 있다" 절).</summary>
        Away = 3,

        /// <summary>전체화면 게임 감지로 캐릭터를 숨긴 상태(기존 Suspend 경로).</summary>
        Suspended = 4,

        /// <summary>디스플레이가 잠들어 화면이 꺼졌다 — 볼 수 있는 사람이 물리적으로 없다.</summary>
        DisplayOff = 5,
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

        /// <summary>
        /// <c>targetFrameRate</c> 기구(<see cref="VSyncCount"/> == 0, 즉 Windows)에서 이 계획이
        /// 실제로 <b>내보내는</b> 초당 장수. vsync 기구(macOS)에서는 디스플레이 주사율을 알아야
        /// 하므로 -1(모름)을 준다.
        ///
        /// <para><b>왜 필요한가</b>: 2026-09-01부터 같은 절감이 <c>targetFrameRate</c>가 아니라
        /// <c>renderFrameInterval</c>로 표현될 수 있다(<see cref="FramePacingPolicy.BuildPlan"/>).
        /// 그래서 <c>plan.TargetFrameRate</c>만 읽고 "몇 장 그리는가"를 판단하면 <b>틀린다</b> —
        /// 60/2가 30인데 30으로 안 보인다. 그 실수를 구조적으로 막는 단 하나의 창구다.</para>
        /// </summary>
        public int EffectiveTargetFps => VSyncCount > 0 || TargetFrameRate <= 0
            ? -1
            : TargetFrameRate / RenderFrameInterval;

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
    /// <item>사람이 보고 있는 등급(Active/Calm/Still)에서는 <c>vSyncCount</c>도 <c>targetFrameRate</c>도
    ///   <b>절대 바꾸지 않고</b> <c>renderFrameInterval</c>만 조정한다(2026-09-01부터 Windows 포함 —
    ///   <see cref="BuildPlan"/> 문서). vSyncCount를 바꾸는 것은 표시 동기화 기구 자체를 바꾸는
    ///   일이라 전환 순간 한 프레임이 튈 수 있고, 이번 사용자 신고가 하필 "부드럽지 않다"였다.
    ///   renderFrameInterval은 디스플레이 위상을 그대로 둔 채 "이번 프레임은 안 그린다"만 정하므로
    ///   남는 프레임의 간격이 여전히 정확히 균일하다(60fps 위상 위의 30fps).</item>
    /// <item>사람이 볼 수 없는 등급(Away/Suspended/DisplayOff)에서만 vSyncCount/targetFrameRate까지
    ///   내린다. 여기서는 위상 균일성이 아무 의미가 없고, 게임 루프 자체를 늦춰 절감을 극대화하는 것이
    ///   맞다(메인 스레드가 전체 비용의 약 60%라 렌더만 건너뛰는 것으로는 절반밖에 못 줄인다).</item>
    /// </list></para>
    ///
    /// <para><b>안전 설계 — 신호를 놓쳐도 절대 얼지 않는다</b>: 가장 깊은 "보는 중" 등급(Still)조차
    /// 기본 15fps이고 가장 얕은 절감 등급(Calm)은 30fps다.
    /// 즉 "캐릭터가 IDLE이다"라는 판단이 틀려도 최악의 결과가 <b>15fps로 그려지는 것</b>이지 정지 화면이
    /// 아니다. 이것이 이 프로젝트에서 render-on-demand(변화가 없으면 아예 안 그림)를 채택하지 않고
    /// 적응형 프레임레이트를 택한 이유다 — 렌더러가 40개가 넘는 코드베이스에서 "깨우기 신호"를 하나라도
    /// 빠뜨리면 그 연출이 통째로 얼어붙는데, 그 실패는 사용자에게 <b>버그로 보인다</b>.</para>
    /// </summary>
    public static class FramePacingPolicy
    {
        /// <summary>
        /// 자리 비움 판정의 <b>필요조건</b>(충분조건이 아니다) — 이 시간(초) 이상 입력이 없어야 한다.
        ///
        /// <para><b>★ 2026-09-01 정정 — 무입력만으로 Away를 주면 "움직임이 부드럽지 않다"가 된다.</b>
        /// 원래 이 상수 하나가 곧 Away 판정이었다. 그 판정은 <b>캐릭터가 무엇을 하고 있는지를 보지
        /// 않았고</b>, 그래서 다음이 성립했다:
        /// <list type="number">
        /// <item>사용자가 마우스에서 손을 떼고 <b>캐릭터를 구경한다</b>(이 앱의 기본 액션이다 —
        ///   docs/UX_FLOW.md 2절).</item>
        /// <item>정확히 180초 뒤 Away로 내려가 프레임이 1/4이 된다(60Hz -> 15fps).</item>
        /// <item>그런데 캐릭터는 계속 걷고 있다. 보행 주기 1.35Hz면 한 걸음이 <b>약 11프레임</b>으로만
        ///   그려져 무릎 관절이 프레임당 최대 9도씩 <b>점프</b>한다.</item>
        /// <item>사용자가 마우스를 조금이라도 움직이면 즉시 사라진다 — 그래서 재현이 어렵고 오래 살았다.</item>
        /// </list>
        /// 즉 <b>보고 있는 사람 앞에서 프레임을 4분의 1로 깎고 있었다</b>. 절감 등급의 대전제
        /// ("아무도 보고 있지 않은 시간에만 깎는다")를 정면으로 어긴 유일한 경로였다.</para>
        ///
        /// <para><b>왜 "무입력 시간을 더 줄이자"가 아니라 "조건을 더하자"인가</b>: 이 앱에서 무입력은
        /// 이탈 신호가 아니라 <b>몰입 신호일 수 있다</b>. 지켜보기가 곧 기본 상호작용이라, 무입력
        /// 임계값을 앞당기는 방향(예: 30~60초)은 증상을 <b>더 자주</b> 만든다. 그래서 임계값은 180초
        /// 그대로 두고, 판정에 <c>characterIdle</c>을 AND로 더했다. 캐릭터가 서 있다면 프레임을 깎아도
        /// 깎이는 것이 없다(정지 화면에는 부드러움이라는 성질 자체가 없다).</para>
        ///
        /// <para><b>DisplayOff는 이 정정의 대상이 아니다</b> — 화면이 물리적으로 꺼진 것은 관측된
        /// 사실이고, 그때는 캐릭터가 걷든 말든 볼 수 있는 사람이 없다. 그래서 걷는 중에도 4fps로
        /// 내려간다(<see cref="DisplayOffTargetFps"/>).</para>
        ///
        /// <para><b>대가(의도적으로 받아들인 것)</b>: 자율 배회는 Idle 2~6초 / Walk 1.5~4초를 반복하므로
        /// 사용자가 실제로 자리를 비운 밤에도 등급이 Away와 Active를 오간다 — 절감량이 줄어든다.
        /// 그 절반은 <see cref="FramePacingTier.DisplayOff"/>(화면 슬립)가 회수하고, 나머지는
        /// "보고 있는 사람 앞에서 끊기지 않는다"의 값이 더 크다고 판단한 결과다.</para>
        /// </summary>
        public const float AwaySeconds = 180f;

        /// <summary>Calm 등급의 전제 — 최근 이 시간(초) 안에 입력이 있었으면 사용자가 상호작용
        /// 중이라고 보고 무조건 Active를 유지한다.
        ///
        /// <para><b>★ 2026-08-31 정정 — 이것만으로는 UI 상호작용을 지키지 못한다(사용자 신고로 반증됨).</b>
        /// 원래 주석은 "UI(정보창/부채꼴메뉴/포스트잇)를 만지는 중에 프레임이 떨어지는 일을 UI 코드와
        /// 전혀 결합하지 않고 막는 장치"라고 적혀 있었다. 그 주장은 <b>틀렸다</b>. 반례가 아주 흔하다:
        /// <list type="number">
        /// <item>사용자가 정보창을 열고 <b>읽는다</b>. 마우스를 안 움직인다 -> 무입력 2초 경과.</item>
        /// <item>그 사이 캐릭터가 자율 배회의 Idle 구간(실측 2~6초)에 들어간다 -> <b>Calm</b>.</item>
        /// <item>사용자가 이제 타이틀바를 잡고 끈다. 입력이 다시 들어오지만 등급 복귀는
        ///       <b>다음 관측 폴링(최대 0.2초)</b>에 가서야 일어난다.</item>
        /// </list>
        /// 즉 <b>모든 상호작용의 첫 0.2초가 절반 프레임레이트로 시작</b>한다. Windows에서는 이것이
        /// 표시 부드러움만의 문제가 아니다 — Windows 등급은 <c>targetFrameRate</c>를 나누므로
        /// <b>게임 루프 자체가 30Hz</b>가 되고, 정보창 드래그는 <c>Update()</c>마다 OS 커서를 한 번
        /// 폴링하는 구조라 <b>커서 표본 주기도 같이 절반</b>이 된다(창이 커서를 계단식으로 따라온다).
        /// 사용자 신고 "기어 설정창조차 클릭하면 약간 렉걸린듯이 움직임"이 이 경로다.</para>
        ///
        /// <para>그래서 "최근 입력" 휴리스틱은 그대로 두되, <b>UI 표면이 자기가 열려 있음을 명시적으로
        /// 알리는</b> 두 번째 장치(<c>DecideTier(..., uiInteractionActive)</c>의 네 번째 인자)를 추가했다. 결합은 <b>한 방향 단 한 줄</b>이다(UI -> FramePacing).</para>
        /// </summary>
        public const float RecentInputSeconds = 2f;

        /// <summary>
        /// UI 표면의 60fps 홀드가 <b>마지막 조작 이후</b> 얼마나 더 유지되는가(초).
        ///
        /// <para>1초인 이유는 두 개의 다른 숫자와 맞물린다:
        /// <list type="number">
        /// <item>표면들이 커서를 훑는 주기가 0.05초(20Hz)라 커서가 창을 스치듯 들락거리면 판정이
        ///   그 주기로 깜빡일 수 있다. 여유가 없으면 홀드 자체가 진동한다.</item>
        /// <item><c>FramePacing</c>의 하강 제동(1초)과 같은 크기다. 즉 이 여유보다 짧게 잡아도
        ///   등급은 어차피 1초 안에 못 내려가므로 절감이 늘지 않고, 길게 잡으면 그만큼 절감만
        ///   줄어든다.</item>
        /// </list></para>
        /// </summary>
        public const float SurfaceHoldLingerSeconds = 1f;

        /// <summary>
        /// **"이 UI 표면이 지금 60fps를 붙잡아야 하는가"** — 열려 있는 모든 표면이 공유하는 단 하나의 판정.
        ///
        /// <para><b>★ 왜 "창이 열려 있는가"가 아닌가(2026-09-01 실측으로 반증됨)</b>: 정보창의 홀드는
        /// <c>Update()</c>에서 <b>무조건</b> 걸려 있었다. 그 전제는 "정보창은 수명이 짧은 표면"이었는데,
        /// 사용자 로그에서 <b>한 번 연 정보창이 125분 동안 열려 있었다</b>(발판 리포트 심장박동 125회로
        /// 확인). 그 125분 내내 등급 전이가 <b>0회</b>, 활성 등급 체류 <b>100%</b>였고, 창을 닫자마자
        /// 전이가 구간당 58~114회로 재개되며 GPU 점유 추정이 12~21%에서 4.3~8.6%로 떨어졌다.
        /// 즉 <b>열린 창 하나가 적응형 절전을 통째로 무력화</b>하고 있었다.</para>
        ///
        /// <para><b>왜 <see cref="AwaySeconds"/>가 이걸 못 막았나</b>: Away는 3분 무입력을 요구한다.
        /// 그런데 이 사용자는 <b>다른 앱에서 계속 타이핑 중</b>이었다 — 무입력 시계가 3분에 닿을 일이
        /// 없다. 홀드를 깨는 경로가 실질적으로 존재하지 않았다.</para>
        ///
        /// <para><b>그래서 조건을 "보인다"에서 "조작 중이다"로 바꾼다</b> — 이미
        /// <c>TodoPostItWidget</c>이 상시 HUD라는 이유로 쓰고 있던 방식과 같다(그 파일 주석이
        /// "정보창은 수명이 짧아 사정이 다르다"고 적어 함정을 스스로 알고도 비켜 갔는데, 그 가정이
        /// 틀렸다).</para>
        ///
        /// <para><b>★ 반응성을 해치지 않는 근거(이 판정의 경계가 여기서 정해진다)</b>:
        /// <list type="number">
        /// <item><b>입력 처리 속도는 애초에 안 떨어진다.</b> 2026-09-01부터 "보는 사람이 있는"
        ///   등급(Calm/Still)은 양 플랫폼 모두 <c>renderFrameInterval</c>만 바꾼다
        ///   (<see cref="BuildPlan"/>). 게임 루프는 60Hz 그대로라 커서 폴링/히트테스트/클릭 판정
        ///   주기가 <b>전혀 변하지 않는다</b>. 절감 등급에서 실제로 느려지는 것은 <b>화면 갱신</b>뿐이다.</item>
        /// <item><b>커서가 창에 닿는 순간 되돌아온다.</b> 표면들은 커서를 20Hz로 훑고
        ///   (<c>ClickPollInterval</c> 0.05초), <c>FramePacing.HoldActiveForInteraction()</c>은
        ///   관측 폴링을 기다리지 않고 <b>그 자리에서</b> 등급을 재평가한다. 최악의 복귀 지연은
        ///   0.05초 + 1프레임 ≈ <b>0.07초</b>다 — 커서가 아직 창 테두리에 막 올라온 시점이고, 사람이
        ///   "굼뜨다"고 느끼는 문턱(약 0.1초)보다 짧다.</item>
        /// <item><b>이 앱의 창에는 휠 스크롤이 없다.</b> 페이지 이동도 캐러셀도 전부 창 위에서의
        ///   클릭/드래그라, "커서가 창 위에 있다"가 모든 조작을 선행한다(휠 스크롤이 생기면 커서가
        ///   창 위에 있는 것은 그대로이므로 이 판정은 여전히 성립한다).</item>
        /// </list>
        /// 남는 대가는 하나다: 창이 열려 있고 커서가 <b>다른 앱</b>에 있는 동안 창의 표시가 최대
        /// 1/4(15fps)로 갱신된다. 그 시간에 창에서 움직이는 것을 실제로 재 보면:
        /// 초상화 호흡이 진폭 <c>TotalHeight × 0.006</c> / 주기 2초 —— Retina 배율 2에서 액자 표시
        /// 높이 376px 기준 <b>진폭 약 2.3px</b>이고, 15fps면 한 주기에 30장이라 <b>프레임당 최대
        /// 0.5px</b>다(서브픽셀). 나머지는 0.25초마다 다시 그리는 숫자뿐이다. 즉 계단이 생길 크기의
        /// 변화가 창 안에 없다. 반대급부가 위 실측의 <b>2.5배 절감</b>이다.
        /// (걷기처럼 큰 움직임은 애초에 이 등급에 들어가지 않는다 — 캐릭터가 움직이면 Active다.)</para>
        /// </summary>
        /// <param name="cursorOverSurface">커서가 이 표면의 화면 사각형 안에 있는가(조작 직전 신호).</param>
        /// <param name="manipulating">지금 실제로 끌거나 편집 중인가(창 드래그/슬라이더/캐러셀/이름 입력).
        /// 커서가 사각형 밖으로 나가도 조작은 계속될 수 있으므로 따로 받는다.</param>
        /// <param name="secondsSinceLastTouch">위 둘 중 하나가 마지막으로 참이었던 뒤 지난 시간(초).
        /// 한 번도 없었으면 큰 값을 넘긴다.</param>
        public static bool ShouldHoldForSurface(bool cursorOverSurface, bool manipulating,
            float secondsSinceLastTouch)
        {
            if (manipulating || cursorOverSurface) return true;
            return secondsSinceLastTouch >= 0f && secondsSinceLastTouch <= SurfaceHoldLingerSeconds;
        }

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
            => DecideTier(presence, suspendedForFullscreen, characterIdle, uiInteractionActive: false);

        /// <summary>
        /// 위 규칙에 <b>"지금 사용자가 이 앱의 UI 표면을 붙잡고 있다"</b>는 사실 하나를 더한 판정.
        ///
        /// <para><b>왜 필요한가</b>: <see cref="RecentInputSeconds"/> 문서의 반례 참고 — "창을 읽는
        /// 동안 Calm으로 내려갔다가, 끌기 시작하는 첫 0.2초를 절반 프레임레이트로 시작"하는 구멍을
        /// 최근 입력 휴리스틱만으로는 막을 수 없다. 열려 있는 모달은 <b>사용자가 보고 있다는 사실
        /// 그 자체</b>라, 추정(무입력 시간)이 아니라 관측으로 다뤄야 한다.</para>
        ///
        /// <para><b>★ 우선순위를 어디에 끼웠는지가 이 함수의 전부다</b> — DisplayOff / Suspended /
        /// Away <b>아래</b>다. 이 셋을 이기게 두면 각각 다음이 깨진다:
        /// <list type="bullet">
        /// <item><b>DisplayOff</b>: 화면이 꺼진 것은 관측된 사실이다. 창이 열려 있어도 볼 사람이
        ///   물리적으로 없다.</item>
        /// <item><b>Suspended</b>: 전체화면 게임이 감지된 상태다. 여기서 프레임을 유지하는 것은
        ///   CLAUDE.md 원칙 2(비침해) 정면 위반이다. (덧붙여 정보창류 표면은 그 순간 스스로 닫히므로 실제로는 도달하지도 않는다.)</item>
        /// <item><b>Away</b>: <b>24시간 상주 앱의 안전장치</b>다. 창을 열어 둔 채 사용자가 자리를
        ///   비우면(3분 무입력 + 캐릭터 정지) 이 홀드가 60fps를 <b>영구히</b> 붙잡아 밤새 컴포지터를
        ///   돌린다. Away가 이기게 두면 "잊고 열어 둔 창"이 절감을 통째로 무력화하지 못한다. 그리고 이
        ///   배치는 신고된 증상을 조금도 되살리지 않는다 — <b>끌고 있는 동안에는 정의상 입력이 계속</b>
        ///   들어오므로 Away 조건이 성립할 수 없다. (2026-09-01부터 Away는 <c>characterIdle</c>까지
        ///   요구하므로 이 홀드가 지는 범위는 오히려 더 좁아졌다 — 걷는 중이면 애초에 Away가 아니다.)</item>
        /// </list>
        /// 즉 이 홀드가 실제로 이기는 대상은 <b>Calm 하나</b>이고, 그것이 신고된 버그의 원인이었다.</para>
        /// </summary>
        /// <param name="uiInteractionActive">이 앱의 UI 표면(정보창 등)이 열려 있거나 끌리는 중인가.
        /// 호출부가 <c>FramePacing.HoldActiveForInteraction()</c>으로 갱신한 홀드의 유효 여부다.</param>
        public static FramePacingTier DecideTier(in ViewerPresenceSnapshot presence,
            bool suspendedForFullscreen, bool characterIdle, bool uiInteractionActive)
            => DecideTier(presence, suspendedForFullscreen, characterIdle, uiInteractionActive,
                characterStill: false);

        /// <summary>
        /// 위 규칙에 <b>"캐릭터가 충분히 오래 서 있다"</b>는 사실 하나를 더한 판정
        /// (2026-09-01 컴포지터 라운드 — <see cref="FramePacingTier.Still"/> 도입).
        ///
        /// <para><b>왜 인자가 <c>characterIdle</c>과 따로 있는가</b>: 둘은 같은 사실의 서로 다른
        /// 임계값이다(<c>characterIdle</c> = 지금 Idle 상태, <c>characterStill</c> = 그 상태가
        /// <c>FramePacing.StillDwellSeconds</c> 이상 이어졌다). 지속 시간을 여기서 세지 않는 이유는
        /// 이 함수가 <b>무상태 순수 함수</b>여야 하기 때문이다 — 시간은 호출부(FramePacing)가 센다.
        /// <c>characterStill</c>이 true면 <c>characterIdle</c>도 반드시 true여야 하지만, 그 불변식을
        /// 여기서 강제하지 않는다(호출부가 깨뜨려도 결과는 "조금 이르게 Still" 정도라 안전하다).</para>
        ///
        /// <para><b>★ 우선순위: Away 아래, Calm 위.</b> Away/Suspended/DisplayOff를 이기지 못하는
        /// 이유는 위 4인자 오버로드의 목록과 같다(더 확실한 관측이 먼저다). UI 홀드도 이긴다 —
        /// 홀드는 "사용자가 지금 우리 창을 만지고 있다"는 관측이고, 그때는 캐릭터가 얼마나 오래
        /// 서 있었든 60fps가 맞다.</para>
        /// </summary>
        /// <param name="characterStill">캐릭터가 <b>충분히 오래</b> 제자리에 서 있는가
        /// (호출부가 <c>FramePacing.StillDwellSeconds</c>로 판단해 넘긴다).</param>
        public static FramePacingTier DecideTier(in ViewerPresenceSnapshot presence,
            bool suspendedForFullscreen, bool characterIdle, bool uiInteractionActive,
            bool characterStill)
        {
            if (presence.Valid && presence.DisplayAsleep) return FramePacingTier.DisplayOff;
            if (suspendedForFullscreen) return FramePacingTier.Suspended;

            // ★ Away는 "무입력"과 "캐릭터 정지"의 AND다. 무입력만으로 내려가면 구경 중인 사용자
            //   앞에서 걷기가 15fps로 끊긴다(근거: AwaySeconds 문서). DisplayOff는 위에서 이미
            //   빠져나갔으므로 이 AND가 화면 꺼짐 절감을 약화시키지는 않는다.
            if (presence.Valid && presence.SecondsSinceUserInput >= AwaySeconds && characterIdle)
            {
                return FramePacingTier.Away;
            }

            if (uiInteractionActive) return FramePacingTier.Active;

            // ★ 여기서 관측(presence)을 보지 않는다는 것이 이 줄의 전부다. "사용자가 지금 무엇을
            //   하고 있는가"와 무관하게 **캐릭터가 오래 서 있으면** 제출을 줄인다. 근거는
            //   FramePacingTier.Still 문서(움직임의 총량이 수 픽셀)와 사용자 실측(업무 중이라
            //   무입력 조건이 하루 종일 성립하지 않았다).
            //   관측 실패(Valid=false)에서도 성립한다 — Still은 OS 관측을 필요로 하지 않는 유일한
            //   절감 등급이다. 캐릭터 상태는 우리가 직접 아는 사실이므로 추정이 아니다.
            if (characterStill) return FramePacingTier.Still;

            if (characterIdle && presence.Valid
                && presence.SecondsSinceUserInput >= RecentInputSeconds)
            {
                return FramePacingTier.Calm;
            }
            return FramePacingTier.Active;
        }

        /// <summary>
        /// 저전력 모드로 <b>활성 등급까지</b> 한 칸 낮출 것인가를 정하는 유일한 곳
        /// (<see cref="BuildPlan"/>의 <c>lowPowerMode</c> 인자에 넣을 값).
        ///
        /// <para><b>왜 분리했는가(2026-08-31 드래그 렉 라운드에서 발견한 두 번째 구멍)</b>:
        /// <c>BuildPlan</c>은 <c>lowPowerMode &amp;&amp; divisor == 1</c>일 때, 즉 <b>Active 등급에서도</b>
        /// 나누기 2를 건다. 그래서 Windows 배터리 세이버(또는 macOS 저전력 모드)가 켜진 노트북에서는
        /// 등급이 항상 Active여도 <c>targetFrameRate</c>가 30으로 고정된다 — <b>창을 끄는 내내</b>
        /// 그렇다. 관측 실패도 아니고 Calm도 아니라서 위 <see cref="DecideTier"/>로는 절대 잡히지
        /// 않는 별개의 경로다.</para>
        ///
        /// <para>사용자가 OS에서 저전력을 켠 것은 존중해야 할 의사표시지만, <b>그 사용자가 지금 이
        /// 앱의 창을 직접 끌고 있는 그 몇 초</b>까지 반값으로 그려 줄 이유는 없다(그 몇 초의 전력은
        /// 무시할 수 있고, 대가는 "이 앱은 끊긴다"는 인상이다). 홀드가 끝나면 즉시 원래대로 돌아간다.</para>
        /// </summary>
        public static bool ShouldApplyLowPowerDownshift(in ViewerPresenceSnapshot presence,
            bool uiInteractionActive)
            => presence.Valid && presence.LowPowerMode && !uiInteractionActive;

        /// <summary>
        /// <see cref="FramePacingTier.Still"/>에서 제출을 몇 분의 1로 줄일 것인가(기본 4 = 60 -> 15).
        ///
        /// <para><b>왜 상수가 아니라 인자인가</b>: 이 숫자만이 이 라운드에서 <b>실기 A/B가 필요한
        /// 유일한 값</b>이다. 4와 8의 차이는 dwm 부하에서는 크지만(15 vs 7.5 제출/초) 화면에서는
        /// 눈동자 3픽셀이 몇 프레임에 나뉘느냐의 차이뿐이라, 어느 쪽이 "충분히 부드러운가"는
        /// 사용자 눈으로만 정해진다. 그래서 <c>FramePacing</c>이 환경변수
        /// <c>STICKMATE_STILL_DIVISOR</c>로 이 값을 바꿔 넣을 수 있게 해 두고, 재빌드 없이
        /// 비교할 수 있게 했다.</para>
        /// </summary>
        public const int DefaultStillDivisor = 4;

        /// <summary>Still 분주의 허용 범위. 1이면 절감이 아예 없고(= 기능 끄기), 8을 넘으면
        /// 호흡 주기(0.8Hz)의 표본이 한 주기당 8장 아래로 떨어져 <b>서브픽셀 움직임조차</b>
        /// 깜빡임으로 보이기 시작한다.</summary>
        public const int MinStillDivisor = 1;
        public const int MaxStillDivisor = 8;

        /// <summary>
        /// 등급 -> 실제 손잡이 값.
        ///
        /// <para><b>★ 2026-09-01 — "보는 사람이 있는 등급"에서는 플랫폼과 무관하게
        /// <c>renderFrameInterval</c>만 쓴다(Windows 거동 변경).</b> 그 전에는
        /// <paramref name="baseVSyncCount"/>가 0인 플랫폼(Windows)에서 <c>targetFrameRate</c>를
        /// 나눴는데, 그것은 <b>게임 루프 자체</b>를 늦추는 기구다:
        /// <list type="bullet">
        /// <item>입력/커서 폴링 주기가 같이 나뉜다 -> 신고 "기어 설정창조차 클릭하면 약간 렉걸린듯이
        ///   움직임"의 직접 원인이었다(<see cref="RecentInputSeconds"/> 문서 참고).</item>
        /// <item>그런데 <b>컴포지터 비용을 줄이는 데 필요한 것은 제출 횟수뿐</b>이다. 루프를 늦추는 것은
        ///   그 목적에 아무 기여를 하지 않으면서 반응성만 깎는다.</item>
        /// </list>
        /// <c>renderFrameInterval</c>은 루프를 60Hz로 유지한 채 렌더/제출만 건너뛴다
        /// (<c>OnDemandRendering.effectiveRenderFrameRate = targetFrameRate / renderFrameInterval</c>).
        /// 즉 이 변경은 절감을 <b>더 깊게</b> 하면서 동시에 Windows의 입력 반응성을 <b>되돌려</b> 준다.
        /// 보는 사람이 없는 등급(Away/Suspended/DisplayOff)은 그대로 루프까지 늦춘다 — 거기서는
        /// 반응성보다 절감이 우선이고, 기존 거동을 바꿀 이유가 없다.</para>
        /// </summary>
        /// <param name="baseVSyncCount">평소(Active) vSyncCount. macOS 기본 2.</param>
        /// <param name="baseTargetFrameRate">평소(Active) targetFrameRate. Windows 기본 60.</param>
        /// <param name="lowPowerMode">OS 저전력 모드. Active 등급을 한 칸 낮추는 데만 쓴다.</param>
        /// <param name="stillDivisor"><see cref="FramePacingTier.Still"/> 전용 분주
        /// (<see cref="DefaultStillDivisor"/>). 범위를 벗어나면 clamp된다.</param>
        public static FramePacingPlan BuildPlan(FramePacingTier tier, int baseVSyncCount,
            int baseTargetFrameRate, bool lowPowerMode, int stillDivisor = DefaultStillDivisor)
        {
            // 화면이 꺼졌을 때만 예외적으로 절대값을 쓴다(디스플레이 주기와의 관계 자체가 무의미하다).
            if (tier == FramePacingTier.DisplayOff)
            {
                return new FramePacingPlan(tier, 0, DisplayOffTargetFps, 1);
            }

            int divisor = tier switch
            {
                FramePacingTier.Calm => 2,       // 60 -> 30
                FramePacingTier.Still => Mathf.Clamp(stillDivisor, MinStillDivisor, MaxStillDivisor),
                FramePacingTier.Away => 4,       // 60 -> 15
                FramePacingTier.Suspended => 2,  // 60 -> 30 (기존 동작 유지)
                _ => 1,
            };

            // 저전력 모드는 사용자가 OS에서 명시적으로 켠 의사표시다. Active만 한 칸 낮춘다
            // (이미 낮아진 등급을 더 낮추지는 않는다 — 곱하면 Away가 7fps가 되어 과하다).
            if (lowPowerMode && divisor == 1) divisor = 2;
            if (divisor == 1) return new FramePacingPlan(tier, baseVSyncCount, baseTargetFrameRate, 1);

            bool viewerPresent = tier == FramePacingTier.Active
                || tier == FramePacingTier.Calm
                || tier == FramePacingTier.Still;

            if (viewerPresent)
            {
                // ★ 보는 사람이 있다 -> 표시 기구(vSyncCount)도 게임 루프(targetFrameRate)도 손대지
                //   않고 renderFrameInterval만 나눈다. **플랫폼 분기가 없다** — 위 문서의 2026-09-01
                //   절 참고(Windows도 이 경로를 쓰도록 바뀌었다).
                return new FramePacingPlan(tier, baseVSyncCount, baseTargetFrameRate, divisor);
            }

            if (baseVSyncCount <= 0)
            {
                // 보는 사람이 없다 + targetFrameRate 기구(Windows). 루프까지 늦춘다.
                int fps = baseTargetFrameRate > 0 ? Mathf.Max(5, baseTargetFrameRate / divisor) : -1;
                return new FramePacingPlan(tier, 0, fps, 1);
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
            FramePacingTier.Calm => "정적(캐릭터가 잠깐 서 있고 최근 입력 없음)",
            FramePacingTier.Still => "정지(캐릭터가 오래 서 있음 — 사용자 입력 여부와 무관)",
            FramePacingTier.Away => "자리비움(오랫동안 입력 없고 캐릭터도 정지)",
            FramePacingTier.Suspended => "전체화면 숨김",
            FramePacingTier.DisplayOff => "디스플레이 꺼짐(볼 수 있는 사람 없음)",
            _ => tier.ToString(),
        };
    }
}
