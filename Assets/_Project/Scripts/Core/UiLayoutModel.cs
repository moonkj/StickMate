using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// ★ <b>사용자가 끌어서 옮길 수 있는 창</b>의 식별자 — <see cref="UiLayoutModel"/>이 창별 위치를
    /// 이 값으로 색인한다.
    ///
    /// <para><b>숫자를 바꾸지 마라.</b> 이 enum은 모델 <b>안쪽 배열</b>의 색인일 뿐이고, 저장 파일에는
    /// 숫자가 아니라 <b>이름 붙은 필드</b>로 내려간다(<c>infoWindowPositionSaved</c> 등).
    /// 저장 스키마가 enum 순서에 의존하지 않는 이유는 <see cref="CharacterSaveStore"/>의
    /// "배열 대신 이름 붙은 필드 8개" 문단과 같다 — 누가 값을 끼워 넣는 순간 모든 사용자의
    /// 창 위치가 한 칸씩 밀리는 사고를 구조적으로 막는다.</para>
    /// </summary>
    public enum UiWindowId
    {
        /// <summary>캐릭터 정보창(Interaction/CharacterInfoWindow.cs).</summary>
        CharacterInfo = 0,

        /// <summary>설정창(Interaction/SettingsWindow.cs).</summary>
        Settings = 1,

        /// <summary>집중 모드 팝오버(Interaction/FocusSessionPopover.cs).</summary>
        FocusSession = 2,
    }

    /// <summary>
    /// ★ 사용자가 <b>직접 옮긴 화면 UI의 위치</b>를 담는 모델 — 2026-08-30 사용자 요청
    /// ("캐릭터 설정 기어들도 길게 클릭해서 위치 옮길 수 있게 해줘").
    ///
    /// 지금 담는 값은 우상단 톱니 아이콘(Interaction/InfoGearIconWidget.cs)의 중심 하나뿐이지만,
    /// 앞으로 "사용자가 옮길 수 있는 화면 요소"가 늘어나도 저장 스키마가 다시 갈라지지 않도록
    /// 별도 모델로 둔다. CharacterProgressionModel / CharacterStatsModel과 <b>같은 관례</b>다:
    /// 값 보관 + IsDirty만 알고, 언제 저장할지는 모른다(Core/CharacterSaveStore.cs가 읽고 쓴다).
    ///
    /// ★★ 2026-09-07 — <b>창 3종의 위치</b>가 여기 함께 들어왔다(사용자 요청 PART1-1:
    /// <i>"모든 창(집중모드 타이머, 캐릭터 정보창, 설정창)이 마우스로 끌어도 움직이지 않음 —
    /// 전부 드래그 이동 가능해야 함"</i>, "이동한 위치는 창별로 저장되어 재시작 후에도 유지").
    /// <b>위 문단이 예고한 그 확장이 실제로 왔다</b> — 그래서 새 모델을 만들지 않고 여기에 붙인다.
    /// 창 위치의 좌표계는 톱니와 다르다(아래 <see cref="WindowOffsetPoints"/> 문서 참고).
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
        /// <summary>사용자가 톱니를 한 번이라도 옮겼는가. false면 위젯이 기본 위치(우상단)를 쓴다.
        /// <para>세우는 문은 <see cref="SetGearCenter"/>, <b>내리는 문은
        /// <see cref="ClearGearCenter"/></b>(설정창의 [처음 자리로]) 둘뿐이다.</para></summary>
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

        /// <summary>
        /// ★ <b>사용자가 옮긴 톱니 위치를 버리고 기본 위치(우상단)로 되돌린다</b> — 2026-09-02 P0.
        ///
        /// <para><b>왜 따로 만들었나</b>: <see cref="SetGearCenter"/>는 <see cref="HasGearCenter"/>를
        /// <b>true로만</b> 만들고, 그것을 false로 되돌리는 경로는 <see cref="ResetForTesting"/> 하나뿐이었다.
        /// 그런데 그건 <b>테스트 계약</b>이다(정적 상태 격리용이라 배율·구석 패널까지 통째로 밀어 버린다).
        /// 그래서 톱니를 실수로 화면 구석에 끌어다 놓으면 <b>되돌릴 방법이 프로덕션에 존재하지 않았고</b>,
        /// 그 자리는 세이브에 앉아 재시작해도 유지됐다. 이 메서드가 그 문이다
        /// (UI: 설정창 [일반] &gt; <c>화면 위 UI</c> &gt; <c>톱니 위치 [처음 자리로]</c>, docs/UX_FLOW.md 41-8).</para>
        ///
        /// <para><b>스키마 버전은 올리지 않는다</b>: 이건 새 필드가 아니라 <c>gearPositionSaved</c>라는
        /// <b>기존 필드의 값 변경</b>(true → false)이다. <see cref="CharacterSaveStore"/>의
        /// <c>CurrentVersion</c> 주석이 정한 규칙 — <i>"필드의 「없음」이 그 필드의 0값과 다른 뜻일 때만
        /// 버전을 강제한다"</i> — 에 비추면 여기서 바뀌는 것은 뜻이 아니라 값뿐이다.</para>
        ///
        /// <para>좌표도 함께 0으로 지운다. 플래그가 false인데 좌표만 남아 있으면 다음 사람이
        /// "값이 있으니 쓰겠다"고 읽을 여지가 생긴다(<see cref="RestoreFromSave"/>가 같은 이유로
        /// 실패 시 <see cref="Vector2.zero"/>를 넣는다).</para>
        /// </summary>
        /// <returns>실제로 되돌릴 것이 있었는가. 이미 기본 위치면 false이고 <see cref="IsDirty"/>도
        /// 건드리지 않는다 — 아무것도 안 바뀐 저장으로 디스크를 두드리지 않기 위해서다
        /// (하루 종일 켜져 있는 앱).</returns>
        public static bool ClearGearCenter()
        {
            if (!HasGearCenter) return false;

            HasGearCenter = false;
            GearCenterPoints = Vector2.zero;
            IsDirty = true;
            return true;
        }

        // ====================================================================================
        // ★★ 창 위치 3종 (2026-09-07, 사용자 요청 PART1-1)
        // ====================================================================================
        //
        // ★ 좌표계가 톱니와 <b>다르다</b> — 여기 담는 것은 «화면 중앙 기준 오프셋(OS 포인트)»다.
        //   톱니는 «창 좌상단 원점 절대 좌표»다. 왜 갈랐는가:
        //     · 톱니는 «화면 오른쪽 위 구석»에 사는 물건이라 절대 좌표가 곧 그 뜻이다.
        //     · 창 3종은 전부 <b>화면 중앙에서 열리는</b> 표면이다(33-7-7). 절대 좌표로 담으면
        //       모니터를 바꾸거나 해상도가 바뀐 날 «중앙에서 조금 오른쪽»이 «화면 오른쪽 끝»이 된다.
        //       중앙 기준 오프셋은 그 변화에서 뜻이 보존된다(0 = 여전히 한가운데).
        //   그리고 이 계는 uGUI <c>anchoredPosition</c>(anchor·pivot 0.5)과 <b>비트 단위로 같다</b> —
        //   정보창·설정창은 값을 변환 없이 그대로 주고받는다.
        //
        // ★ 화면 밖으로 나가지 않게 하는 클램프는 여기서 <b>하지 않는다</b> — 톱니와 같은 규칙이다.
        //   화면 크기와 창 치수를 아는 것은 창이고, 이 모델은 그 결과만 받는다.

        private static readonly bool[] _hasWindowOffset = new bool[WindowCount];
        private static readonly Vector2[] _windowOffsets = new Vector2[WindowCount];

        /// <summary><see cref="UiWindowId"/>의 개수. 배열 길이를 손으로 적지 않기 위한 단일 출처다
        /// (테스트도 이 값을 참조한다 — 숫자를 베끼면 창이 하나 늘 때 조용히 갈라진다).</summary>
        public const int WindowCount = 3;

        /// <summary>사용자가 그 창을 한 번이라도 옮겼는가. false면 창이 기본 자리(화면 중앙 /
        /// 팝오버는 부채꼴 앵커)에서 열린다.</summary>
        public static bool HasWindowOffset(UiWindowId id)
            => IsValid(id) && _hasWindowOffset[(int)id];

        /// <summary>그 창 <b>중심</b>의 위치 — 화면 중앙 원점, OS 포인트(위 절 참고).
        /// <see cref="HasWindowOffset"/>가 false면 의미 없는 값이다.</summary>
        public static Vector2 WindowOffsetPoints(UiWindowId id)
            => IsValid(id) ? _windowOffsets[(int)id] : Vector2.zero;

        /// <summary>
        /// 창을 옮긴 결과를 확정한다. <see cref="SetGearCenter"/>와 <b>같은 세 가지 방어</b>를 쓴다:
        /// NaN 거르기 · <see cref="MeaningfulMovePoints"/> 미만 무시 · 실제로 바뀐 경우에만 IsDirty.
        /// <para>같은 방어가 필요한 이유도 같다 — 창들은 매 프레임 클램프 결과를 이 모델로 되돌려
        /// 주므로, 부동소수 흔들림만으로 IsDirty가 서면 주기 저장이 계속 디스크를 두드린다.</para>
        /// </summary>
        public static void SetWindowOffset(UiWindowId id, Vector2 offsetPoints)
        {
            if (!IsValid(id)) return;
            if (float.IsNaN(offsetPoints.x) || float.IsNaN(offsetPoints.y)) return;

            int i = (int)id;
            if (_hasWindowOffset[i] &&
                (_windowOffsets[i] - offsetPoints).sqrMagnitude < MeaningfulMovePoints * MeaningfulMovePoints)
            {
                return;
            }

            _windowOffsets[i] = offsetPoints;
            _hasWindowOffset[i] = true;
            IsDirty = true;
        }

        /// <summary>
        /// ★ 그 창을 <b>기본 자리</b>로 되돌린다 — <see cref="ClearGearCenter"/>와 같은 계약이다
        /// (영구히 저장되는 것에는 되돌리는 문이 있어야 한다, docs/UX_FLOW.md 41-8).
        ///
        /// <para><b>아직 이 문을 여는 UI 버튼은 없다.</b> 문구와 배치는 <c>ux-designer</c> 소관이라
        /// 여기서 지어내지 않았다. 다만 톱니와 달리 <b>잃어버릴 수 없는</b> 값이다 —
        /// 창 클램프가 «창 전체가 화면 안»을 매 프레임 강제하므로 [✕]가 화면 밖으로 사라지는
        /// 실패 모드가 구조적으로 없다(톱니는 예약 띠 뒤에 숨을 수 있어서 그 문이 P0였다).</para>
        /// </summary>
        /// <returns>실제로 되돌릴 것이 있었는가. 이미 기본 자리면 false이고 <see cref="IsDirty"/>도
        /// 건드리지 않는다(아무것도 안 바뀐 저장으로 디스크를 두드리지 않는다).</returns>
        public static bool ClearWindowOffset(UiWindowId id)
        {
            if (!IsValid(id) || !_hasWindowOffset[(int)id]) return false;

            _hasWindowOffset[(int)id] = false;
            _windowOffsets[(int)id] = Vector2.zero;
            IsDirty = true;
            return true;
        }

        private static bool IsValid(UiWindowId id) => (int)id >= 0 && (int)id < WindowCount;

        /// <summary>저장 파일 복원 전용(Core/CharacterSaveStore.cs) — 창 <b>하나</b>씩 받는다.
        /// <para>한 번에 배열로 받지 않는 이유는 저장 스키마가 <b>이름 붙은 필드</b>이기 때문이다.
        /// 호출부가 <c>infoWindowPositionSaved</c> 같은 이름과 enum 값을 같은 줄에 적으므로,
        /// enum에 값을 끼워 넣어도 사용자의 창 위치가 밀리지 않는다(<see cref="UiWindowId"/> 문서).</para></summary>
        internal static void RestoreWindowFromSave(UiWindowId id, bool saved, float offsetXPoints, float offsetYPoints)
        {
            if (!IsValid(id)) return;
            int i = (int)id;
            bool ok = saved && !float.IsNaN(offsetXPoints) && !float.IsNaN(offsetYPoints);
            _hasWindowOffset[i] = ok;
            _windowOffsets[i] = ok ? new Vector2(offsetXPoints, offsetYPoints) : Vector2.zero;
            IsDirty = false;
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

        /// <summary>테스트/디버그 전용 완전 초기화(정적 상태가 테스트 사이에 새지 않게).
        /// <para>★ <b>프로덕션에서 부르지 마라.</b> 이건 이 모델이 담은 <b>전부</b>(톱니 위치 + 캐릭터
        /// 배율 + 죽은 구석 패널 플래그)를 한꺼번에 미는 테스트 계약이고, <see cref="IsDirty"/>도
        /// false로 눕혀 "되돌렸다"는 사실이 저장에 내려가지 <b>않는다</b>. 사용자에게 톱니 위치를
        /// 되돌려 주는 문은 <see cref="ClearGearCenter"/>다.</para></summary>
        public static void ResetForTesting()
        {
            HasGearCenter = false;
            GearCenterPoints = Vector2.zero;
            HasCharacterScale = false;
            CharacterScale = 0.75f;
            CornerPanelEnabled = true;
            for (int i = 0; i < WindowCount; i++)
            {
                _hasWindowOffset[i] = false;
                _windowOffsets[i] = Vector2.zero;
            }
            IsDirty = false;
        }
    }
}
