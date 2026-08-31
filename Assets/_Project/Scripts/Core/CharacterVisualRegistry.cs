using System.Collections.Generic;
using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// 등록된 잉크가 캐릭터 몸에 <b>붙어 있는가</b>. 화면 여백 계산이 이 값으로 갈린다.
    /// </summary>
    public enum CharacterVisualAnchor
    {
        /// <summary>몸을 그대로 따라다닌다(액세서리: 모자/망토/안경…). 몸의 시각 반폭에 포함된다.</summary>
        BodyAttached = 0,

        /// <summary>몸과 독립적으로 움직이거나 월드에 고정된다(펫은 커서까지 따라가고, FX 발자국은
        /// 땅에 남는다). <b>시각 반폭에 넣으면 안 된다</b> — 넣으면 커서 친구가 화면 끝에 갈 때마다
        /// 캐릭터가 "내가 그만큼 넓다"고 착각해 화면 안쪽으로 밀려난다.</summary>
        Detached = 1,
    }

    /// <summary>
    /// 캐릭터 본체(프리팹에 구워진 12개) <b>바깥</b>에서 잉크를 얹는 부품이 "지금 내가 실제로 그리고
    /// 있는 렌더러"를 스스로 신고하는 통로. 구현체는 캐릭터 루트(또는 그 자손)의 MonoBehaviour여야 한다.
    ///
    /// <para>Core/ICharacterInkExtentProvider와 같은 방향(잉크를 더하는 쪽이 스스로 답한다)이고,
    /// 이유도 같다 — 부품 목록을 소비자 쪽에 적어 두면 새 부품을 추가한 사람이 그 목록을 고치는 것을
    /// 잊는 순간 그 부품만 조용히 규칙 밖으로 빠져나간다.</para>
    /// </summary>
    public interface ICharacterVisualSource
    {
        /// <summary>
        /// 지금 이 프레임에 그리고 있는 렌더러를 <paramref name="sink"/>에 전부 넣는다.
        /// 아무것도 그리지 않으면 아무것도 넣지 않는다(파괴됐거나 비활성인 것도 넣지 않는다).
        /// <para>할당 금지 — 이 앱은 하루 종일 켜져 있다. 구현체는 자기가 이미 들고 있는 리스트/배열을
        /// 순회하기만 한다.</para>
        /// </summary>
        void CollectVisuals(CharacterVisualRegistry sink);
    }

    /// <summary>
    /// ★ <b>"지금 이 캐릭터가 실제로 그리고 있는 모든 것"의 단일 창구.</b>
    ///
    /// ============================================================================
    /// 왜 만들었나 (2026-08-31, test-engineer 능동 탐색이 찾은 결함 3+1건의 공통 뿌리)
    /// ============================================================================
    /// <see cref="StickmanAgent"/>는 Awake에서 <c>GetComponentsInChildren&lt;Renderer&gt;(true)</c>로
    /// 렌더러를 <b>한 번</b> 스냅샷했다. 그런데 액세서리(EquipmentAccessories)·펫(CharacterPet)·
    /// FX(CharacterFx)는 전부 <b>그 뒤에 런타임 생성</b>된다 — 게다가 펫/FX는 캐릭터의 자식조차
    /// 아니라(독립 GameObject) 다시 조회해도 잡히지 않는다. 그 결과 그 배열의 소비자 넷 중 셋이 깨졌다:
    ///  · 전체화면 자동 숨김/가출 은신 → 몸만 사라지고 모자·망토·펫이 0.25초 더 남았다(<b>원칙 2 위반</b>).
    ///  · 화면상 최소 획 두께 하한 → 액세서리/펫에 안 걸려 출하 배율(0.75)에서도 1.47pt였다(하한 2pt).
    ///  · 화면 여백(시각 반폭) → 액세서리가 계산에 아예 안 들어갔다.
    ///
    /// 소비처를 하나씩 땜질하면 <b>다섯 번째 소비자</b>가 생길 때 같은 사고가 또 난다. 그래서 방향을
    /// 뒤집어, 잉크를 더하는 쪽이 스스로 신고하게 하고 소비자는 전부 이 창구 하나만 본다.
    ///
    /// ============================================================================
    /// 왜 등록/해제(push)가 아니라 매번 물어보기(pull)인가 — 실패 유형을 하나 없앤다
    /// ============================================================================
    /// "만들 때 등록하고 부술 때 해제한다"는 <b>해제를 빠뜨리면 조용히 새는</b> 구조다. 이 프로젝트의
    /// 액세서리는 착용 변경뿐 아니라 <b>좌우가 바뀔 때마다</b> 컨테이너를 통째로 다시 굽는다(걷는 동안
    /// 수시로 일어난다). 24시간 상주 앱에서 그 경로에 해제 누락이 하나라도 있으면 목록이 무한히
    /// 자라고, 파괴된 Unity 오브젝트가 섞인 목록은 <b>조용히 스킵</b>되어 버그를 감춘다(정확히 그
    /// 실패가 Tests/PlayMode/CharacterScaleRuntimeTests를 무력화시켰다).
    /// pull은 그 실패 유형이 존재하지 않는다 — 목록은 매번 소유자가 지금 들고 있는 것에서 새로 만들어지고,
    /// 소유자가 그리지 않게 된 것은 그 순간부터 목록에 없다.
    ///
    /// <para>호출 빈도: 숨기기/재개(전체화면·가출), 배율 변경, 시각 반폭 갱신(0.25초 주기)뿐이라
    /// 매 프레임 경로가 아니다. 그래도 버퍼를 재사용해 정상 상태 할당은 0이다.</para>
    /// </summary>
    public sealed class CharacterVisualRegistry
    {
        /// <summary>등록된 잉크 하나.</summary>
        public readonly struct Entry
        {
            public readonly Renderer Renderer;

            /// <summary><see cref="Renderer"/>가 LineRenderer면 같은 객체, 아니면 null.
            /// 소비자가 매번 형변환을 다시 하지 않게 등록 시점에 한 번만 판정해 들고 있는다.</summary>
            public readonly LineRenderer Line;

            public readonly CharacterVisualAnchor Anchor;

            public Entry(Renderer renderer, LineRenderer line, CharacterVisualAnchor anchor)
            {
                Renderer = renderer;
                Line = line;
                Anchor = anchor;
            }
        }

        private ICharacterVisualSource[] _sources = System.Array.Empty<ICharacterVisualSource>();
        private readonly List<Entry> _entries = new List<Entry>(64);
        private bool _collecting;

        /// <summary>이 창구에 신고하는 부품 수(진단/테스트용). 0이면 배선이 끊긴 것이다.</summary>
        public int SourceCount => _sources.Length;

        public int Count => _entries.Count;

        public Entry this[int index] => _entries[index];

        /// <summary>캐릭터 루트에서 1회 수집한 부품 목록을 꽂는다(<see cref="StickmanAgent"/>.Awake).</summary>
        public void BindSources(ICharacterVisualSource[] sources)
        {
            _sources = sources ?? System.Array.Empty<ICharacterVisualSource>();
        }

        /// <summary>
        /// 지금 그려지고 있는 것으로 목록을 다시 채운다. 소비자는 읽기 직전에 이것을 부른다.
        /// </summary>
        public void Refresh()
        {
            _entries.Clear();
            _collecting = true;
            try
            {
                for (int i = 0; i < _sources.Length; i++)
                {
                    ICharacterVisualSource source = _sources[i];
                    if (source == null) continue;
                    // 파괴된 컴포넌트는 C# 참조가 살아 있어도 Unity의 == 오버로드가 null로 답한다.
                    if (source is MonoBehaviour behaviour && behaviour == null) continue;
                    source.CollectVisuals(this);
                }
            }
            finally
            {
                _collecting = false;
            }
        }

        /// <summary>
        /// 부품이 자기 렌더러를 신고한다. <see cref="ICharacterVisualSource.CollectVisuals"/> 안에서만
        /// 부를 수 있다 — 밖에서 부르면 다음 <see cref="Refresh"/>에 조용히 사라져 "등록했는데 반영이
        /// 안 된다"가 되므로 즉시 알린다.
        /// </summary>
        public void Add(Renderer renderer, CharacterVisualAnchor anchor)
        {
            if (!_collecting)
            {
                Debug.LogError("[캐릭터잉크] CharacterVisualRegistry.Add()는 CollectVisuals() 안에서만 " +
                    "부를 수 있습니다 — 이 창구는 등록/해제가 아니라 '지금 그리는 것을 매번 물어보는' 방식입니다.");
                return;
            }
            if (renderer == null) return;
            _entries.Add(new Entry(renderer, renderer as LineRenderer, anchor));
        }

        /// <summary>여러 개를 한 번에 신고하는 편의 오버로드(이미 리스트/배열로 들고 있는 쪽).
        /// 제네릭 제약이 <c>Renderer</c>라 박싱도 형변환 검사도 없다.</summary>
        public void AddRange<T>(IReadOnlyList<T> renderers, CharacterVisualAnchor anchor) where T : Renderer
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Count; i++) Add(renderers[i], anchor);
        }
    }
}
