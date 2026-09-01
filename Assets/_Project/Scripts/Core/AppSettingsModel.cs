using UnityEngine;
using StickMate.Dialogue;

namespace StickMate.Core
{
    /// <summary>
    /// ★★ 2026-09-02 — <b>대사 표시 시간</b> 선택지(docs/UX_FLOW.md 42-4 확정안 A).
    ///
    /// ============================================================================
    /// 왜 초(秒) 슬라이더를 폐기했는가 — 손잡이 10칸 중 7칸이 죽어 있었다
    /// ============================================================================
    /// 노출 상한이 글자수 함수가 된 뒤(2026-09-01), 앱에 실재하는 대사 <b>35줄 전수</b>로 재 보니
    /// 슬라이더 눈금이 이렇게 나왔다:
    /// <code>
    ///   1.5초 → 35/35줄이 바뀜(단 최단 대사 효과 0.04초 = 2.4프레임)
    ///   2.0초 → 13/35     2.5초 → 3/35
    ///   3.0 ~ 6.0초(7칸)  → 0/35     ← 손잡이를 밀어도 화면이 한 톨도 안 바뀐다
    /// </code>
    /// 그리고 <b>배포 기본값 4.0초가 이미 그 죽은 구간 안</b>이었다 — 사용자가 설정창을 처음 열었을 때
    /// 보는 상태가 "손잡이를 오른쪽으로 미는 모든 행위가 무효"인 상태였다.
    ///
    /// <para>대체안은 <b>3단 세그먼트</b>다. 초는 거짓말이고 %는 무의미하다(이 컨트롤의 효과는 눈으로
    /// 0.3초를 잴 수 없어 <b>직접 관측이 불가능</b>하다). 관측할 수 없는 양에 11칸짜리 정밀도를 주는
    /// 것은 정밀도의 연기(演技)다. 사용자의 상태는 실제로 "괜찮다 / 좀 짧다 / 많이 짧다" 셋이다.</para>
    ///
    /// <para><b>값의 순서가 곧 세그먼트 칸 순서</b>이므로 중간에 끼워 넣지 않는다. 저장 파일에는
    /// 숫자가 아니라 <b>이름 문자열</b>로 적힌다
    /// (<see cref="AppSettingsModel.DialogueVisibleLengthSaveName"/>) — 그래야 열거형 순서가 바뀌어도
    /// 파일이 밀리지 않는다(잉크색이 쓰는 그 관례).</para>
    /// </summary>
    public enum DialogueVisibleLength
    {
        /// <summary>기본 — 배포 기본값. 100%.</summary>
        Default,

        /// <summary>길게 — 150%.</summary>
        Long,

        /// <summary>아주 길게 — 200%(포화 문턱).</summary>
        VeryLong,
    }

    /// <summary>
    /// ★ <b>설정창이 만지는 값</b>을 담는 모델 — 2026-09-01 설정창 신설(docs/UX_FLOW.md 35-1).
    /// <see cref="UiLayoutModel"/>(화면 배치) / <see cref="CharacterAppearanceModel"/>(잉크색)과
    /// 완전히 같은 관례다: 값 보관 + <see cref="IsDirty"/>만 알고, 언제 저장할지는 모른다
    /// (Core/CharacterSaveStore.cs가 읽고 쓴다).
    ///
    /// ============================================================================
    /// ★★ 왜 <see cref="StickConfig"/>에 직접 쓰지 않는가 (이 파일의 존재 이유)
    /// ============================================================================
    /// <c>StickConfig</c>는 프리팹 16개 컴포넌트에 배선된 <b>배포 에셋</b>
    /// (Assets/_Project/Data/DefaultStickConfig.asset)이다. 유니티 에디터는 ScriptableObject 애셋에
    /// 가한 플레이 모드 중 변경을 <b>되돌리지 않으므로</b>, 설정창 슬라이더가
    /// <c>_config.dialogueFontSize = v</c>라고 쓰는 순간 그 값이 <b>출하 기본값</b>이 되어 전 사용자에게
    /// 나간다. 이 프로젝트는 같은 실패를 이미 두 번 겪었다(2026-08-31 R3 Blocker 2 <c>characterScale</c>,
    /// R5 <c>inkColor</c>). 게다가 빌드에서는 반대로 <b>아무것도 남지 않아</b> 껐다 켜면 초기화된다.
    ///
    /// 그래서 규칙은 하나다: <b>사용자가 고른 값은 여기에, 배포 기본값은 에셋에</b>. 읽는 쪽은
    /// <c>Resolve*</c>를 지나고, 고른 적이 없으면 에셋 값이 그대로 흘러나온다(거동 무변화).
    /// <c>DeployedConfigAssetImmutabilityTests</c>의 정적 스캔이 <c>.inkColor =</c> /
    /// <c>.characterScale =</c> 패턴을 금지하는 것과 같은 계열의 방어다.
    ///
    /// ============================================================================
    /// 담는 것과 담지 않는 것
    /// ============================================================================
    ///  · <b>담는다</b>: [일반] 전체화면 자동 숨김 / 톱니 아이콘, [캐릭터] 말풍선 4종.
    ///  · <b>담지 않는다</b>: 캐릭터 크기(<see cref="UiLayoutModel"/>), 잉크색
    ///    (<see cref="CharacterAppearanceModel"/>), 구석 패널 on/off(<see cref="UiLayoutModel"/>).
    ///    이미 각자의 집이 있고, 집을 옮기면 저장 스키마 마이그레이션이 따라붙는다.
    /// </summary>
    public static class AppSettingsModel
    {
        // ============================================================================
        // [일반] 탭
        // ============================================================================

        /// <summary>전체화면 게임/영상이 감지되면 자동으로 숨을 것인가(절대 불변 원칙 2의 사용자 스위치).
        /// <b>기본 ON</b> — 끄는 것은 사용자의 명시적 선택이어야 한다.</summary>
        public static bool AutoHideOnFullscreen { get; private set; } = true;

        /// <summary>화면 우상단 상시 톱니 아이콘을 띄울 것인가. 끄면 정보창/설정창의 <b>마우스 진입점이
        /// 사라지므로</b> 설정창이 그 사실을 경고 캡션으로 알린다(35-1-5 와이어프레임).</summary>
        public static bool GearIconVisible { get; private set; } = true;

        public static void SetAutoHideOnFullscreen(bool v)
        {
            if (AutoHideOnFullscreen == v) return;
            AutoHideOnFullscreen = v;
            IsDirty = true;
        }

        public static void SetGearIconVisible(bool v)
        {
            if (GearIconVisible == v) return;
            GearIconVisible = v;
            IsDirty = true;
        }

        // ============================================================================
        // [캐릭터] 탭 — 말과 행동
        // ============================================================================
        //
        // 전부 "고른 적 있는가 + 값" 두 벌이다. Has* 플래그를 두는 이유는 CharacterAppearanceModel의
        // 잉크색과 같다 — 유효 범위 안의 값(예: 글자 크기 16)은 "안 고른 것"과 구분할 수 없다.

        public static bool HasDialogueFontSize { get; private set; }
        public static int DialogueFontSize { get; private set; } = 16;

        /// <summary>대사 표시 시간(3단). ★ 초가 아니라 <b>노출 배율</b>이다 — 근거는
        /// <see cref="StickMate.Core.DialogueVisibleLength"/> 문서.</summary>
        public static bool HasDialogueVisibleLength { get; private set; }
        public static DialogueVisibleLength DialogueVisibleLength { get; private set; } = DialogueVisibleLength.Default;

        /// <summary>잡담 빈도(%). 100이면 배포 기본 확률 그대로, 0이면 혼잣말을 하지 않는다.
        /// <b>확률값 자체를 덮어쓰지 않는 이유</b>(35-1-3 ③): 원래 값이 사라지면 되돌릴 수 없다.
        /// 배율로 두면 배포 기본값이 바뀌어도 사용자의 "평소보다 조금 더" 의도가 그대로 살아 있다.</summary>
        public static bool HasChatterPercent { get; private set; }
        public static int ChatterPercent { get; private set; } = 100;

        public static bool HasDialogueBubbleEnabled { get; private set; }
        public static bool DialogueBubbleEnabled { get; private set; } = true;

        public static void SetDialogueFontSize(int v)
        {
            int clamped = Mathf.Clamp(v, MinDialogueFontSize, MaxDialogueFontSize);
            if (HasDialogueFontSize && DialogueFontSize == clamped) return;
            DialogueFontSize = clamped;
            HasDialogueFontSize = true;
            IsDirty = true;
        }

        public static void SetDialogueVisibleLength(DialogueVisibleLength v)
        {
            DialogueVisibleLength clamped = IsDefined(v) ? v : DialogueVisibleLength.Default;
            if (HasDialogueVisibleLength && DialogueVisibleLength == clamped) return;
            DialogueVisibleLength = clamped;
            HasDialogueVisibleLength = true;
            IsDirty = true;
        }

        private static bool IsDefined(DialogueVisibleLength v)
            => v >= DialogueVisibleLength.Default && v <= DialogueVisibleLength.VeryLong;

        public static void SetChatterPercent(int v)
        {
            int clamped = Mathf.Clamp(v, 0, MaxChatterPercent);
            if (HasChatterPercent && ChatterPercent == clamped) return;
            ChatterPercent = clamped;
            HasChatterPercent = true;
            IsDirty = true;
        }

        public static void SetDialogueBubbleEnabled(bool v)
        {
            if (HasDialogueBubbleEnabled && DialogueBubbleEnabled == v) return;
            DialogueBubbleEnabled = v;
            HasDialogueBubbleEnabled = true;
            IsDirty = true;
        }

        // ==================== 슬라이더 범위 (시안 그대로 — 한 곳에서만 정의한다) ====================

        /// <summary>말풍선 글자 크기 하한. <c>DialogueBubbleRenderer.ResolveFontSize</c>가 가독성 하한을
        /// 따로 갖고 있으므로 여기 값은 "고를 수 있는 범위"일 뿐이다.</summary>
        public const int MinDialogueFontSize = 8;
        public const int MaxDialogueFontSize = 28;

        /// <summary>
        /// ★ 3단 세그먼트의 <b>내부 배율</b>. 새 숫자를 하나도 발명하지 않는다 — 양 끝은
        /// <see cref="DialogueBudget.MinVisibleScale"/>(규칙 6이 강제한 하한)과
        /// <see cref="DialogueBudget.MaxVisibleScale"/>(포화 문턱)이고, <b>가운데는 선택이 아니라
        /// 산술</b>이다(양 끝이 확정되면 3단의 중간은 유도된다).
        ///
        /// <para>체감 검산: 9자 대사 2.21 → 3.17 → 4.12초. 칸 사이 최소 간격 0.96초는
        /// 페이드아웃(0.12초)의 8배라 육안으로 구분된다.</para>
        ///
        /// <para><b>사용자에게 이 숫자를 절대 보여주지 않는다</b>(세그먼트는 값 라벨을 쓰지 않는다) —
        /// 보여줄 정직한 숫자가 없기 때문이다.</para>
        /// </summary>
        public static float ScaleOf(DialogueVisibleLength length)
        {
            switch (length)
            {
                case DialogueVisibleLength.VeryLong: return DialogueBudget.MaxVisibleScale;
                case DialogueVisibleLength.Long:
                    return (DialogueBudget.MinVisibleScale + DialogueBudget.MaxVisibleScale) * 0.5f;
                default: return DialogueBudget.MinVisibleScale;
            }
        }

        public const int MaxChatterPercent = 200;

        // ============================================================================
        // 조회 — 소비자는 반드시 이쪽을 지난다
        // ============================================================================

        /// <summary>말풍선 글자 크기(배율 1.0 기준). 고른 적 없으면 배포 기본값.</summary>
        public static int ResolveDialogueFontSize(StickConfig config)
            => HasDialogueFontSize ? DialogueFontSize : (config != null ? config.dialogueFontSize : 16);

        /// <summary>한 말풍선의 최대 노출 시간(초). <b>0 이하 = 상한 없음</b>(소비자인
        /// DialogueBubbleRenderer.ResolveMaxVisibleSeconds가 그렇게 해석한다 — 그때는 글자수 유도
        /// 상한만 남는다). 2026-09-02에 배포 기본값이 4 -> 0이 되면서 폴백도 함께 0이다.
        ///
        /// <para>★ 2026-09-02 — <b>사용자는 더 이상 이 값을 고르지 않는다.</b> 초 슬라이더가 폐기되고
        /// (<see cref="StickMate.Core.DialogueVisibleLength"/>) 사용자의 손잡이는 배율이 됐다. 여기 남은
        /// 것은 <b>배포 기본값 하나뿐</b>이라 <c>Has*</c> 분기가 없다.</para></summary>
        public static float ResolveDialogueMaxVisibleSeconds(StickConfig config)
            => config != null ? config.dialogueMaxVisibleSeconds : 0f;

        /// <summary>
        /// ★ 사용자가 고른 <b>노출 배율</b>(m). 고른 적 없으면 <see cref="DialogueVisibleLength.Default"/>
        /// = 100%이고, 그 값에서 화면 거동은 배율 도입 이전과 <b>한 톨도 다르지 않다</b>.
        ///
        /// <para><see cref="StickConfig"/> 인자를 받지 않는 이유: 이 값의 배포 기본값은 에셋이 아니라
        /// 열거형의 첫 칸(<c>Default</c>)이 소유한다. 에셋에 필드를 새로 만들면 "고른 적 없음"과
        /// "에셋이 정한 값"이 두 벌이 되고, 그 둘이 어긋나는 날 아무도 못 찾는다.</para>
        ///
        /// <para>★ 이 배율은 <b>화면 노출(하한·상한)에만</b> 곱한다. 발화 자격 게이트에는 곱하지 않는다 —
        /// <see cref="DialogueBudget.RequiredDwellSeconds"/> 문서 참고.</para>
        /// </summary>
        public static float ResolveDialogueVisibleScale()
            => ScaleOf(HasDialogueVisibleLength ? DialogueVisibleLength : DialogueVisibleLength.Default);

        /// <summary>
        /// 최소 노출 시간(초)의 <b>추가 절대 하한</b>. ★ 2026-09-01(UX_FLOW.md 5절 규칙 4-b) 이후
        /// 실제 하한은 글자수 비례 가독예산(<c>Dialogue/DialogueKind.cs</c>의 <c>DialogueBudget</c>)이
        /// 정하고, 이 값은 그 위에 얹는 바닥일 뿐이다(기본 0 = 예산에 전부 맡김).
        ///
        /// 사용자가 최대치를 <b>이 하한보다 짧게</b> 고를 수 있으므로 여기서 받친다 — 안 그러면
        /// "최소 N초 보장"과 "최대 0.5초"가 동시에 참이어야 하는 모순이 생기고, 그 모순의 결과는
        /// 규칙 4(가독성 하한)의 침묵한 위반이다.
        /// </summary>
        public static float ResolveDialogueMinVisibleSeconds(StickConfig config)
        {
            float min = config != null ? config.dialogueMinVisibleSeconds : 0.7f;
            float max = ResolveDialogueMaxVisibleSeconds(config);
            return max > 0f ? Mathf.Min(min, max) : min;
        }

        /// <summary>말풍선을 그릴 것인가. 파이프라인(DialogueIntent 생성)은 이 값과 무관하게 그대로 돈다 —
        /// 원칙 1의 행동-텍스트 파이프라인은 설정으로 끌 수 있는 물건이 아니다(그리지 않을 뿐이다).</summary>
        public static bool ResolveDialogueBubbleEnabled(StickConfig config)
            => HasDialogueBubbleEnabled ? DialogueBubbleEnabled : (config == null || config.dialogueBubbleEnabled);

        public static float ResolveIdleChatterChance(StickConfig config)
            => ScaleChance(config != null ? config.idleChatterChance : 0.28f);

        public static float ResolveWalkChatterChance(StickConfig config)
            => ScaleChance(config != null ? config.walkChatterChance : 0.14f);

        private static float ScaleChance(float baseChance)
        {
            if (!HasChatterPercent) return baseChance;
            return Mathf.Clamp01(baseChance * (ChatterPercent / 100f));
        }

        // ============================================================================
        // 저장
        // ============================================================================

        /// <summary>마지막 저장 이후 값이 바뀌었는가(다른 모델의 IsDirty와 같은 역할).</summary>
        public static bool IsDirty { get; private set; }

        /// <summary>저장 파일 복원 전용(Core/CharacterSaveStore.cs). 이벤트를 쏘지 않는 것은 다른
        /// 모델의 RestoreFromSave와 같은 규약이다(복원은 변화가 아니라 초기 상태 확정).
        ///
        /// <para>★ <paramref name="autoHideOnFullscreen"/>/<paramref name="gearIconVisible"/>는
        /// <b>기본이 true인 값</b>이라, 이 필드가 없는 옛 저장 파일을 그대로 읽으면 JsonUtility가 false로
        /// 채워 뜻이 뒤집힌다(UiLayoutModel의 <c>cornerPanelEnabled</c>가 겪은 그 함정). 그래서 호출부가
        /// 버전을 보고 "없으면 true"를 넘긴다 — 이 함수는 받은 값을 그대로 믿는다.</para></summary>
        internal static void RestoreFromSave(
            bool autoHideOnFullscreen, bool gearIconVisible,
            bool hasFontSize, int fontSize,
            bool hasVisibleLength, string visibleLengthName,
            bool hasChatterPercent, int chatterPercent,
            bool hasBubbleEnabled, bool bubbleEnabled)
        {
            AutoHideOnFullscreen = autoHideOnFullscreen;
            GearIconVisible = gearIconVisible;

            HasDialogueFontSize = hasFontSize;
            if (hasFontSize) DialogueFontSize = Mathf.Clamp(fontSize, MinDialogueFontSize, MaxDialogueFontSize);

            // ★ 이름 문자열로 받는다(잉크색과 같은 관례) — 열거형에 칸이 끼어들어도 파일이 안 밀린다.
            //   모르는 이름은 "고른 적 없음"으로 떨어뜨린다: 죽은 값을 사용자의 선택으로 오해하는 것보다
            //   배포 기본값으로 돌아가는 쪽이 언제나 안전하다.
            DialogueVisibleLength parsed = DialogueVisibleLength.Default;
            HasDialogueVisibleLength = hasVisibleLength && TryParseVisibleLength(visibleLengthName, out parsed);
            if (HasDialogueVisibleLength) DialogueVisibleLength = parsed;

            HasChatterPercent = hasChatterPercent;
            if (hasChatterPercent) ChatterPercent = Mathf.Clamp(chatterPercent, 0, MaxChatterPercent);

            HasDialogueBubbleEnabled = hasBubbleEnabled;
            if (hasBubbleEnabled) DialogueBubbleEnabled = bubbleEnabled;

            IsDirty = false;
        }

        internal static void MarkSaved() => IsDirty = false;

        /// <summary>저장 파일에 적히는 이름. 고른 적이 없으면 빈 문자열(잉크색과 같은 관례).</summary>
        internal static string DialogueVisibleLengthSaveName()
            => HasDialogueVisibleLength ? DialogueVisibleLength.ToString() : string.Empty;

        private static bool TryParseVisibleLength(string name, out DialogueVisibleLength value)
        {
            value = DialogueVisibleLength.Default;
            if (string.IsNullOrEmpty(name)) return false;
            if (!System.Enum.TryParse(name, out DialogueVisibleLength parsed) || !IsDefined(parsed)) return false;
            value = parsed;
            return true;
        }

        /// <summary>테스트/디버그 전용 완전 초기화(정적 상태가 테스트 사이에 새지 않게).</summary>
        public static void ResetForTesting()
        {
            AutoHideOnFullscreen = true;
            GearIconVisible = true;
            HasDialogueFontSize = false;
            DialogueFontSize = 16;
            HasDialogueVisibleLength = false;
            DialogueVisibleLength = DialogueVisibleLength.Default;
            HasChatterPercent = false;
            ChatterPercent = 100;
            HasDialogueBubbleEnabled = false;
            DialogueBubbleEnabled = true;
            IsDirty = false;
        }
    }
}
