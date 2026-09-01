using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// ★ 사용자가 <b>직접 옮긴 화면 UI의 위치</b>를 담는 모델 — 2026-08-30 사용자 요청
    /// ("캐릭터 설정 기어들도 길게 클릭해서 위치 옮길 수 있게 해줘").
    ///
    /// 지금 담는 값은 우상단 톱니 아이콘(Interaction/InfoGearIconWidget.cs)의 중심 하나뿐이지만,
    /// 앞으로 "사용자가 옮길 수 있는 화면 요소"가 늘어나도 저장 스키마가 다시 갈라지지 않도록
    /// 별도 모델로 둔다. CharacterProgressionModel / CharacterStatsModel과 <b>같은 관례</b>다:
    /// 값 보관 + IsDirty만 알고, 언제 저장할지는 모른다(Core/CharacterSaveStore.cs가 읽고 쓴다).
    ///
    /// ============================================================================
    /// 좌표계 — <b>창 좌상단 원점의 OS 포인트</b>다 (픽셀이 아니다)
    /// ============================================================================
    /// 저장값은 화면 해상도/Retina 배율이 바뀌어도 같은 물리적 자리를 가리켜야 한다. 그래서
    /// Unity 픽셀이 아니라 OS 포인트로 담는다(Platform/ScreenCoordinateConverter.cs의 단위 규약).
    /// x는 창 왼쪽 끝에서 오른쪽으로, y는 창 <b>위쪽</b> 끝에서 아래로 자란다 — 화면 좌표를 눈으로
    /// 읽을 때의 감각과 같고, 창 세로 크기가 변해도 위쪽 기준이라 메뉴바 근처 배치가 흔들리지 않는다.
    ///
    /// 화면 밖으로 나가지 않게 하는 클램프는 <b>여기서 하지 않는다</b> — 화면 크기와 아이콘 치수를
    /// 아는 것은 위젯이고, 이 모델은 그 결과만 받는다(복원 직후의 클램프도 위젯이 수행한 뒤 다시
    /// <see cref="SetGearCenter"/>로 되돌려 준다).
    /// </summary>
    public static class UiLayoutModel
    {
        /// <summary>사용자가 톱니를 한 번이라도 옮겼는가. false면 위젯이 기본 위치(우상단)를 쓴다.</summary>
        public static bool HasGearCenter { get; private set; }

        /// <summary>큰 기어 중심의 위치(창 좌상단 원점, OS 포인트). <see cref="HasGearCenter"/>가
        /// false면 의미 없는 값이다.</summary>
        public static Vector2 GearCenterPoints { get; private set; }

        /// <summary>마지막 저장 이후 값이 바뀌었는가(CharacterStatsModel.IsDirty와 같은 역할).</summary>
        public static bool IsDirty { get; private set; }

        // ====================================================================================
        // ★ 구석 호버 패널이 다루는 값 2종 (2026-08-31, docs/UX_FLOW.md 34-9 #8)
        // ====================================================================================
        // 크기와 on/off를 <b>여기</b>에 두는 이유: 둘 다 "사용자가 화면 UI를 자기 방식대로 맞춘 결과"라
        // 톱니 위치와 정확히 같은 성격이다(캐릭터의 능력치도, 게임 진행도도 아니다).
        //
        // ★ 크기는 StickConfig.characterScale에도 <b>동시에</b> 들어간다(런타임 반영의 단일 소스이자
        //   ResolveWalkSpeed의 유일한 입력이기 때문). 그런데 StickConfig는 <b>에셋</b>이라 재시작하면
        //   에디터에 구워진 값으로 되돌아간다 — 즉 에셋만으로는 "사용자가 고른 크기"를 기억할 수 없다.
        //   그래서 기억은 여기가, 적용은 StickmanAgent.ApplyCharacterScale이 맡는다.

        /// <summary>사용자가 크기를 한 번이라도 정했는가. false면 배포 기본 배율을 그대로 쓴다.</summary>
        public static bool HasCharacterScale { get; private set; }

        /// <summary>사용자가 고른 캐릭터 배율(StickConfig.Min/MaxCharacterScale 구간).</summary>
        public static float CharacterScale { get; private set; } = 0.75f;

        /// <summary>
        /// ★ <b>죽은 설정이다 — 저장 파일 호환을 위해서만 살아 있다.</b> 2026-09-01 사용자 요청으로
        /// 좌하단 구석 호버 패널이 통째로 삭제되면서 이 값을 보는 기능이 하나도 남지 않았다.
        ///
        /// <para>그래도 지우지 않는 이유는 <b>하위 호환</b>이다. 저장 스키마 v6부터 <c>cornerPanelEnabled</c>
        /// 키가 실제 사용자 파일에 들어 있고, <see cref="CharacterSaveStore"/>가 그 키를 읽고 쓴다.
        /// 여기서 이 속성을 지우면 스키마 버전을 올려야 하고, 그러면 <b>이미 배포된 파일</b>의 마이그레이션이
        /// 한 벌 더 늘어난다 — 아무 기능도 없는 bool 하나 때문에. 지금처럼 값만 왕복시키면 옛 파일도
        /// 새 파일도 경고 없이 열리고, 스키마 버전은 그대로다.</para>
        ///
        /// <para>바꾸는 문(<c>SetCornerPanelEnabled</c>)은 <b>없앴다</b> — 아무도 못 바꾸는 값이어야
        /// 나중에 "이 토글이 왜 아무 일도 안 하지"가 생기지 않는다. 훗날 스키마 버전을 올릴 일이
        /// 생기면 그때 이 속성과 저장 필드를 함께 정리하면 된다.</para>
        /// </summary>
        public static bool CornerPanelEnabled { get; private set; } = true;

        /// <summary>배율의 "의미 있는 변화" 하한. 스냅 단위(CharacterScaleController.ValueStep)가
        /// 0.05라 그 절반보다 작으면
        /// 같은 눈금이다(부동소수 흔들림만으로 저장을 두드리지 않는다 — 위 MeaningfulMovePoints와 같은 이유).</summary>
        private const float MeaningfulScaleDelta = 0.001f;

        public static void SetCharacterScale(float scale)
        {
            if (float.IsNaN(scale) || scale <= 0f) return;
            if (HasCharacterScale && Mathf.Abs(CharacterScale - scale) < MeaningfulScaleDelta) return;
            CharacterScale = scale;
            HasCharacterScale = true;
            IsDirty = true;
        }

        /// <summary>0.05pt 미만의 변화는 무시한다 — 클램프 결과를 매 프레임 되돌려 주는 호출 경로가
        /// 있어(위젯의 화면 경계 보정) 부동소수 흔들림만으로 IsDirty가 계속 서면 주기 저장이 매번
        /// 디스크를 두드리게 된다(하루 종일 켜져 있는 앱이다).</summary>
        private const float MeaningfulMovePoints = 0.05f;

        public static void SetGearCenter(Vector2 centerPoints)
        {
            if (float.IsNaN(centerPoints.x) || float.IsNaN(centerPoints.y)) return;
            if (HasGearCenter && (GearCenterPoints - centerPoints).sqrMagnitude < MeaningfulMovePoints * MeaningfulMovePoints) return;

            GearCenterPoints = centerPoints;
            HasGearCenter = true;
            IsDirty = true;
        }

        /// <summary>저장 파일 복원 전용(Core/CharacterSaveStore.cs). 이벤트를 쏘지 않는 이유는
        /// 다른 모델의 RestoreFromSave와 같다(복원은 변화가 아니라 초기 상태 확정).</summary>
        internal static void RestoreFromSave(bool hasCenter, float centerXPoints, float centerYPoints)
        {
            HasGearCenter = hasCenter && !float.IsNaN(centerXPoints) && !float.IsNaN(centerYPoints);
            GearCenterPoints = HasGearCenter ? new Vector2(centerXPoints, centerYPoints) : Vector2.zero;
            IsDirty = false;
        }

        /// <summary>저장 파일 복원 전용. 위 RestoreFromSave와 같은 규약.
        ///
        /// <para>이름에 CornerPanel이 남아 있는 것은 이 메서드가 <b>캐릭터 크기</b>도 함께 복원하기
        /// 때문이다(둘이 v6에서 같이 들어왔다). 구석 패널은 2026-09-01에 삭제됐지만 크기 복원은
        /// 이 경로가 유일하다 — 세 번째 인자만 죽은 값이다(<see cref="CornerPanelEnabled"/> 문서 참고).</para></summary>
        internal static void RestoreCornerPanelFromSave(bool hasScale, float scale, bool cornerPanelEnabled)
        {
            HasCharacterScale = hasScale && !float.IsNaN(scale) && scale > 0f;
            if (HasCharacterScale) CharacterScale = scale;
            CornerPanelEnabled = cornerPanelEnabled;
            IsDirty = false;
        }

        internal static void MarkSaved() => IsDirty = false;

        /// <summary>테스트/디버그 전용 완전 초기화(정적 상태가 테스트 사이에 새지 않게).</summary>
        public static void ResetForTesting()
        {
            HasGearCenter = false;
            GearCenterPoints = Vector2.zero;
            HasCharacterScale = false;
            CharacterScale = 0.75f;
            CornerPanelEnabled = true;
            IsDirty = false;
        }
    }
}
