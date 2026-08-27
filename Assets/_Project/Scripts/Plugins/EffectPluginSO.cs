using UnityEngine;
using StickMate.Core;

namespace StickMate.Plugins
{
    /// <summary>
    /// DLC/확장 이펙트 매니페스트 (절대 불변 원칙 4: 플러그인 구조).
    /// 신규 VFX/파티클 연출을 기본 로직 무수정으로 추가하기 위한 데이터 컨테이너.
    /// Phase 0에서는 필드만 정의하고, 실제 소비(재생) 로직은 이후 Phase의 EffectPluginRegistry 등이 담당한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NewEffectPlugin", menuName = "StickMate/Plugins/Effect Plugin", order = 11)]
    public sealed class EffectPluginSO : ScriptableObject
    {
        [Tooltip("상점/설정 화면에 표시될 이펙트 이름")]
        public string displayName;

        [Tooltip("상점/설정 화면에 표시될 아이콘")]
        public Sprite icon;

        [Tooltip("이 이펙트가 적용될 대상 상태(들). 예: 피격 이펙트는 Ragdoll 진입 시에만 적용")]
        public StickmanStateId[] applicableStates;

        [Tooltip("이 이펙트를 재생할 VFX/파티클 프리팹")]
        public GameObject effectPrefab;
    }
}
