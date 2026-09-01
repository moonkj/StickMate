namespace StickMate.Core
{
    /// <summary>
    /// ★ <b>몸</b>의 획 두께 — 신장 배수로 표현한 런타임 단일 정의처(2026-09-01).
    ///
    /// ============================================================================
    /// 왜 생겼는가 — 리더가 20:31 빌드에서 눈으로 잡은 불일치
    /// ============================================================================
    /// 바탕화면의 실제 캐릭터는 굵은 곡선인데 <b>정보창 초상화만 얇은 직선</b>이었다. 원인은 단순하다:
    /// 초상화(Interaction/CharacterPortraitStage)가 몸을 그릴 때
    /// <see cref="Interaction.AccessoryShapeBuilder"/>의 <b>액세서리</b> 획(0.048)을 쓰고 있었고,
    /// 실제 캐릭터의 몸 획은 Editor/SceneBootstrapper의 0.11~0.12 × LineWidthScale이었다.
    /// 신장으로 나누면 0.0211 대 0.0459~0.0551 — <b>2.2~2.6배</b> 차이다.
    ///
    /// 그 이중 정의는 이미 알려져 있었다(CharacterPortraitStage.cs의 "P6 소관" 주석,
    /// Tasklist 38-12 #10). 이 파일이 그 P6다.
    ///
    /// ============================================================================
    /// 왜 여기(런타임 Core)에 두는가
    /// ============================================================================
    /// 값의 원본은 Editor/SceneBootstrapper.cs다(프리팹을 굽는 쪽). 그런데 초상화는 런타임이고
    /// <b>런타임 어셈블리는 Editor 어셈블리를 참조할 수 없다</b>. 그래서 이 프로젝트가 같은 상황에서
    /// 이미 쓰고 있는 방식을 따른다 —
    /// <see cref="Interaction.AccessoryShapeBuilder.BaselineHeadVisualRadius"/>,
    /// <c>SceneBootstrapper.FilledDiscWidthPerPathRadius</c>와 같은 계열이다:
    /// <b>런타임에 정의를 두고, 그 값이 실제로 구워진 프리팹과 같다는 사실을 EditMode 테스트가
    /// 잠근다</b>(Tests/EditMode/PortraitBodyStrokeParityTests).
    ///
    /// <see cref="Interaction.AccessoryShapeBuilder"/> 안에 넣지 않은 이유는 두 가지다.
    /// (1) 그 파일의 <c>StrokeWidthRatio</c>는 <b>액세서리</b> 획이고 이건 <b>몸</b> 획이라
    ///     이름이 붙어 있으면 또 헷갈린다(이번 사고의 정확한 원인이 그 혼동이다).
    /// (2) 몸 치수는 Core의 관심사다 — Interaction이 없는 소비자도 읽어야 한다.
    ///
    /// ============================================================================
    /// ★ 화면상 하한(MinStrokeScreenPoints)은 여기 없다 — 일부러 그렇다
    /// ============================================================================
    /// 실제 캐릭터는 <c>ScaledStrokeWidth</c>로 배율을 곱한 뒤 "화면상 2pt" 하한에 걸린다
    /// (출하 배율 0.75에서 머리 링만 그 하한에 눌려 있다: 0.04725 → 0.0567376).
    /// <b>초상화는 그 하한을 적용하면 안 된다.</b> 초상화의 표시 크기는 캐릭터 배율과 무관하도록
    /// 설계돼 있고(Tests/PlayMode/PortraitScaleInvarianceTests가 그것을 잠근다), 하한은 배율에
    /// 따라 <i>비율</i>을 바꾸는 값이라 그대로 쓰면 배율 0.35에서 초상화 획만 굵어져 그 불변식이
    /// 깨진다. 그래서 여기 있는 값은 전부 <b>하한 이전의 순수 비례값</b>이다.
    /// </summary>
    public static class StickmanStrokeWidths
    {
        /// <summary>
        /// 몸 획 전체에 걸리는 배수. Editor/SceneBootstrapper.cs의 같은 이름 상수와 <b>같은 값</b>이어야
        /// 한다(그 파일에 이 값을 0.7에서 올린 근거와 "곡선화가 선행 조건"이라는 경고가 전부 있다).
        /// </summary>
        public const float LineWidthScale = 1.045f;

        /// <summary>배율 1.0 기준 몸통 획(월드 유닛). SceneBootstrapper.BaselineLineWidth와 같다.</summary>
        public const float BaselineTorsoWidth = 0.11f * LineWidthScale;

        /// <summary>배율 1.0 기준 다리 획. 몸통보다 아주 약간 굵다.</summary>
        public const float BaselineLegWidth = 0.12f * LineWidthScale;

        /// <summary>배율 1.0 기준 팔 획. 몸통보다 아주 약간 얇다 — 이 "팔 &lt; 몸통 &lt; 다리" 관계가
        /// 그림의 무게중심을 아래로 내려 준다. 초상화가 셋을 하나로 뭉뚱그리면 그 뜻이 사라진다.</summary>
        public const float BaselineArmWidth = 0.10f * LineWidthScale;

        // ── 신장 배수(= 실제로 소비되는 형태) ────────────────────────────────────
        // 그리는 쪽은 "지금 이 캐릭터의 키 × 비율"로 두께를 정한다. 키가 바뀌어도 그림의 인상이
        // 그대로인 유일한 방법이고, 초상화가 캐릭터 배율과 무관해지는 근거이기도 하다.

        /// <summary>몸통 획 / 전신 높이.</summary>
        public const float TorsoWidthRatio = BaselineTorsoWidth / StickConfig.BaselineCharacterTotalHeight;

        /// <summary>다리 획 / 전신 높이.</summary>
        public const float LegWidthRatio = BaselineLegWidth / StickConfig.BaselineCharacterTotalHeight;

        /// <summary>팔 획 / 전신 높이.</summary>
        public const float ArmWidthRatio = BaselineArmWidth / StickConfig.BaselineCharacterTotalHeight;
    }
}
