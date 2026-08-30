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

        internal static void MarkSaved() => IsDirty = false;

        /// <summary>테스트/디버그 전용 완전 초기화(정적 상태가 테스트 사이에 새지 않게).</summary>
        public static void ResetForTesting()
        {
            HasGearCenter = false;
            GearCenterPoints = Vector2.zero;
            IsDirty = false;
        }
    }
}
