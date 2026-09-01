using System;

namespace StickMate.Core
{
    /// <summary>
    /// ★ 사용자가 <b>직접 고른 캐릭터 외형</b>을 담는 모델 — 지금은 잉크색(검정/흰색) 하나뿐이다.
    /// 2026-08-31 R5, "잉크색 배포 에셋 오염" 수정과 함께 신설.
    ///
    /// ============================================================================
    /// 왜 별도의 모델이 필요한가 — 두 가지 사실이 겹쳤다
    /// ============================================================================
    /// (1) <b>StickConfig는 배포 에셋이라 사용자의 선택을 담을 수 없다.</b> 지금까지 잉크 스와치는
    ///     <c>_config.inkColor</c>(직렬화 필드)에 직접 썼는데, 그 에셋은 프리팹 16개 컴포넌트에
    ///     배선된 <b>출하 기본값</b>이다. 에디터는 플레이 모드 중의 ScriptableObject 변경을 되돌리지
    ///     않으므로 그대로 커밋되어 전 사용자에게 나간다(characterScale에서 이미 한 번 겪은 사고 —
    ///     Core/StickConfig.cs의 "이번 실행의 배율은 이 에셋에 기록되지 않는다" 문단).
    /// (2) 그런데 <b>빌드에서는 반대로 아무것도 남지 않았다.</b> 에셋 변경이 남는 것은 에디터뿐이라,
    ///     실제 사용자는 앱을 껐다 켤 때마다 잉크색이 검정으로 돌아갔다(세이브 스키마에 잉크색
    ///     필드가 아예 없었다). 즉 "에디터는 영구 오염 / 빌드는 매번 초기화"라는 최악의 조합이었다.
    ///
    /// 그래서 <b>기억은 여기가, 적용은 StickConfig의 런타임 오버라이드가</b> 맡는다 —
    /// Core/UiLayoutModel의 캐릭터 크기와 정확히 같은 분업이다.
    ///
    /// 관례는 다른 모델들과 동일하다: 값 보관 + IsDirty만 알고, 언제 저장할지는 모른다
    /// (Core/CharacterSaveStore.cs가 읽고 쓰며, 주기 저장은 Interaction/CharacterProgressionDirector).
    /// </summary>
    public static class CharacterAppearanceModel
    {
        /// <summary>사용자가 잉크색을 한 번이라도 고른 적이 있는가. false면 배포 기본값(StickConfig의
        /// 직렬화 <c>inkColor</c>)을 그대로 쓴다. 별도 플래그를 두는 이유는 톱니 위치/캐릭터 크기와
        /// 같다 — Black(0)은 <b>실제로 고를 수 있는 값</b>이라 "값이 Black이면 안 고른 것"으로
        /// 해석할 수 없다(그러면 검정을 고른 사용자가 배포 기본값 변경에 휩쓸린다).</summary>
        public static bool HasInkColor { get; private set; }

        /// <summary>사용자가 고른 잉크색. <see cref="HasInkColor"/>가 false면 의미 없는 값이다.</summary>
        public static StickmanInkColor InkColor { get; private set; } = StickmanInkColor.Black;

        /// <summary>마지막 저장 이후 값이 바뀌었는가(Core/UiLayoutModel.IsDirty와 같은 역할).</summary>
        public static bool IsDirty { get; private set; }

        /// <summary>사용자가 잉크색을 골랐다. 같은 값이면 아무 일도 하지 않는다(주기 저장이 매번
        /// 디스크를 두드리지 않게 — 하루 종일 켜져 있는 앱이다).</summary>
        public static void SetInkColor(StickmanInkColor ink)
        {
            if (HasInkColor && InkColor == ink) return;
            InkColor = ink;
            HasInkColor = true;
            IsDirty = true;
        }

        /// <summary>저장 파일 복원 전용(Core/CharacterSaveStore.cs). 이벤트를 쏘지 않는 것은 다른
        /// 모델의 RestoreFromSave와 같은 규약이다(복원은 변화가 아니라 초기 상태 확정).
        ///
        /// <para>저장값을 <b>이름 문자열</b>로 받는 이유: 숫자를 적으면 훗날 <see cref="StickmanInkColor"/>에
        /// 값을 하나 끼워 넣는 순간 모든 사용자의 색이 한 칸씩 밀린다(장비 아이디를 문자열로 적는 것과
        /// 같은 판단 — Core/EquipmentModel.cs의 "인덱스 vs 문자열 아이디" 문단). 모르는 이름이면
        /// "고른 적 없음"으로 떨어뜨린다 — 파일이 말하지 않은 것을 화면이 보여주지 않게.</para></summary>
        internal static void RestoreFromSave(bool hasInk, string inkName)
        {
            HasInkColor = false;
            InkColor = StickmanInkColor.Black;

            if (hasInk && !string.IsNullOrEmpty(inkName)
                && Enum.TryParse(inkName, false, out StickmanInkColor parsed)
                && Enum.IsDefined(typeof(StickmanInkColor), parsed))
            {
                HasInkColor = true;
                InkColor = parsed;
            }

            IsDirty = false;
        }

        /// <summary>저장 파일에 적을 이름. 고른 적 없으면 빈 문자열(= "없음"을 정확히 말한다).</summary>
        internal static string InkColorSaveName() => HasInkColor ? InkColor.ToString() : string.Empty;

        internal static void MarkSaved() => IsDirty = false;

        /// <summary>테스트/디버그 전용 완전 초기화(정적 상태가 테스트 사이에 새지 않게).</summary>
        public static void ResetForTesting()
        {
            HasInkColor = false;
            InkColor = StickmanInkColor.Black;
            IsDirty = false;
        }
    }
}
