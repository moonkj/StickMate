using UnityEngine;
using StickMate.Core;

namespace StickMate.Plugins
{
    /// <summary>
    /// DLC/확장 모션 매니페스트 (절대 불변 원칙 4: 플러그인 구조).
    /// 신규 모션을 기본 상태머신/애니메이션 로직 무수정으로 추가하기 위한 데이터 컨테이너.
    /// Phase 0에서는 필드만 정의하고, 실제 소비(적용) 로직은 이후 Phase의 MotionPluginRegistry 등이 담당한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMotionPlugin", menuName = "StickMate/Plugins/Motion Plugin", order = 10)]
    public sealed class MotionPluginSO : ScriptableObject
    {
        [Tooltip("상점/설정 화면에 표시될 모션 이름")]
        public string displayName;

        [Tooltip("상점/설정 화면에 표시될 아이콘")]
        public Sprite icon;

        [Tooltip("이 모션이 적용될 대상 상태(들). 예: Walk 모션 스킨은 Walk 상태에만 적용")]
        public StickmanStateId[] applicableStates;
    }
}
