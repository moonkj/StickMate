using UnityEngine;

namespace StickMate.Core
{
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

        public static bool HasDialogueVisibleSeconds { get; private set; }
        public static float DialogueMaxVisibleSeconds { get; private set; } = 4f;

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

        public static void SetDialogueMaxVisibleSeconds(float v)
        {
            float clamped = Mathf.Clamp(v, MinVisibleSecondsChoice, MaxVisibleSecondsChoice);
            if (HasDialogueVisibleSeconds && Mathf.Approximately(DialogueMaxVisibleSeconds, clamped)) return;
            DialogueMaxVisibleSeconds = clamped;
            HasDialogueVisibleSeconds = true;
            IsDirty = true;
        }

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

        public const float MinVisibleSecondsChoice = 1.5f;
        public const float MaxVisibleSecondsChoice = 6f;

        public const int MaxChatterPercent = 200;

        // ============================================================================
        // 조회 — 소비자는 반드시 이쪽을 지난다
        // ============================================================================

        /// <summary>말풍선 글자 크기(배율 1.0 기준). 고른 적 없으면 배포 기본값.</summary>
        public static int ResolveDialogueFontSize(StickConfig config)
            => HasDialogueFontSize ? DialogueFontSize : (config != null ? config.dialogueFontSize : 16);

        /// <summary>한 말풍선의 최대 노출 시간(초).</summary>
        public static float ResolveDialogueMaxVisibleSeconds(StickConfig config)
            => HasDialogueVisibleSeconds ? DialogueMaxVisibleSeconds : (config != null ? config.dialogueMaxVisibleSeconds : 4f);

        /// <summary>
        /// 최소 노출 시간(초). 사용자가 최대치를 <b>최소치보다 짧게</b> 고를 수 있으므로 여기서 받친다 —
        /// 안 그러면 "최소 0.7초 보장"과 "최대 0.5초"가 동시에 참이어야 하는 모순이 생기고,
        /// 그 모순의 결과는 UX_FLOW 5절 규칙 4(가독성 하한)의 침묵한 위반이다.
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
            bool hasVisibleSeconds, float visibleSeconds,
            bool hasChatterPercent, int chatterPercent,
            bool hasBubbleEnabled, bool bubbleEnabled)
        {
            AutoHideOnFullscreen = autoHideOnFullscreen;
            GearIconVisible = gearIconVisible;

            HasDialogueFontSize = hasFontSize;
            if (hasFontSize) DialogueFontSize = Mathf.Clamp(fontSize, MinDialogueFontSize, MaxDialogueFontSize);

            HasDialogueVisibleSeconds = hasVisibleSeconds && !float.IsNaN(visibleSeconds);
            if (HasDialogueVisibleSeconds)
                DialogueMaxVisibleSeconds = Mathf.Clamp(visibleSeconds, MinVisibleSecondsChoice, MaxVisibleSecondsChoice);

            HasChatterPercent = hasChatterPercent;
            if (hasChatterPercent) ChatterPercent = Mathf.Clamp(chatterPercent, 0, MaxChatterPercent);

            HasDialogueBubbleEnabled = hasBubbleEnabled;
            if (hasBubbleEnabled) DialogueBubbleEnabled = bubbleEnabled;

            IsDirty = false;
        }

        internal static void MarkSaved() => IsDirty = false;

        /// <summary>테스트/디버그 전용 완전 초기화(정적 상태가 테스트 사이에 새지 않게).</summary>
        public static void ResetForTesting()
        {
            AutoHideOnFullscreen = true;
            GearIconVisible = true;
            HasDialogueFontSize = false;
            DialogueFontSize = 16;
            HasDialogueVisibleSeconds = false;
            DialogueMaxVisibleSeconds = 4f;
            HasChatterPercent = false;
            ChatterPercent = 100;
            HasDialogueBubbleEnabled = false;
            DialogueBubbleEnabled = true;
            IsDirty = false;
        }
    }
}
