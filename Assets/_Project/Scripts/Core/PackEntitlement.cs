using System.Collections.Generic;

namespace StickMate.Core
{
    /// <summary>
    /// ★ 유료 권한(C층) 조회 결과. <b>상태는 2개가 아니라 3개다</b>
    /// (docs/security/ENTITLEMENT_CONTRACT.md §E-1).
    ///
    /// <para><b>왜 <c>bool</c> 이면 안 되는가</b>: 스팀 클라이언트가 안 떠 있으면
    /// <c>SteamAPI_Init</c> 이 실패하고(Steamworks <c>steam_api.h</c>: <i>"A running Steam client is
    /// required"</i>), 그때 소박한 구현 <c>bool owned = ...BIsDlcInstalled(id);</c> 는 <b>false</b> 로
    /// 읽힌다. 즉 <b>정상 결제한 사용자 전원이 잠긴다.</b> 이 앱은 부팅 자동 실행 + 종일 상주가 정상
    /// 사용 형태라 <b>로그인 직후 스팀이 아직 안 뜬 구간이 매일 발생</b>한다(§E-3).</para>
    ///
    /// <para>정의서 원칙: <b>정당한 유저를 한 명이라도 잠그는 조치는 무단 사용 열 건보다 비싸다.</b>
    /// 이 앱은 동료지 금고가 아니다.</para>
    ///
    /// <para>★ 이 열거형을 <c>switch</c> 할 때 <b><c>default:</c> 를 두지 마라</b>(§E-1-b).
    /// 세 갈래를 전부 적으면 단이 하나 늘 때 사람이 그 자리를 보게 되고,
    /// <c>default:</c> 를 두면 새 상태가 <b>조용히</b> 한쪽으로 흡수된다.</para>
    /// </summary>
    public enum PackEntitlementState
    {
        /// <summary>보유가 확인됐다.</summary>
        Owned = 0,

        /// <summary>미보유가 확인됐다. <b>확인됐다</b>는 것이 요점이다.</summary>
        NotOwned = 1,

        /// <summary>★ 지금은 알 수 없다. <b>미보유가 아니다.</b>
        /// 이 값이 <see cref="NotOwned"/> 로 붕괴하는 것이 이 계약이 막으려는 사고 그 자체다.</summary>
        Unknown = 2,
    }

    /// <summary>
    /// 스토어에 "이 팩을 가졌는가"를 묻는 창구. <b>구현은 이 어셈블리 밖에서 온다</b>
    /// (스팀 SDK 배선은 아직 0줄이고, 그 사실을 <c>OfflineFirstNetworkAuditTests</c> 가 잠그고 있다).
    ///
    /// <para><b>결과를 <c>bool</c> 로 돌려주는 형태를 만들지 마라</b>(§E-1-a). 그래서 이 인터페이스의
    /// 유일한 멤버는 <see cref="PackEntitlementState"/> 를 돌려준다.</para>
    ///
    /// <para><b>실행 간 캐시를 만들지 마라</b>(§E-4-a): "마지막 성공 판정"을 파일·PlayerPrefs·레지스트리
    /// 어디에도 쓰지 않는다. C층을 세이브에서 뺀 목적은 방어 추가가 아니라 <b>표적 제거</b>였고,
    /// 캐시 파일을 만들면 표적이 되돌아온다.</para>
    /// </summary>
    public interface IPackEntitlementSource
    {
        /// <summary>이 팩의 상태. <b>모르면 <see cref="PackEntitlementState.Unknown"/></b> 이지
        /// <see cref="PackEntitlementState.NotOwned"/> 가 아니다.</summary>
        PackEntitlementState Query(string packId);
    }

    /// <summary>
    /// ★ 스토어가 <b>없을 때</b>의 출처 — 전부 <see cref="PackEntitlementState.Unknown"/>.
    ///
    /// <para><b>왜 <c>NotOwned</c> 가 아닌가</b>: 스토어 SDK가 배선되지 않은 빌드는 "안 샀다"를
    /// <b>확인한 것이 아니다</b>. 물어볼 창구 자체가 없다. 그 사실을 <c>NotOwned</c> 로 적으면
    /// 이 파일이 막으려는 붕괴를 <b>기본 구현이 먼저 저지르는</b> 셈이 된다.</para>
    ///
    /// <para><b>왜 개발 편의로 <c>Owned</c> 를 돌려주지 않는가</b>: 그러면 출하 빌드에서
    /// 팩이 전부 열린 채 나가는 경로가 <b>기본값</b>이 된다. 개발용 전량 보유가 필요하면
    /// 테스트 어셈블리에서 <see cref="PackEntitlements.SetTestOverride"/> 로 넣는다 —
    /// 그 창구는 <c>internal</c> 이라 프로덕션 밖에서 부를 수 없다(§E-6-c).</para>
    ///
    /// <para>★ 그리고 <b>이 타입은 <c>EquipmentDebugUnlock</c> 을 참조하지 않는다</b>(§E-6-b).
    /// QA 해금 스위치는 A·B층(레벨 해금)의 것이고, 그것이 결제 경계를 넘으면
    /// 환경변수 한 줄이 매출을 넘긴다. <c>UnlockSwitchScopeAuditTests</c> 가 그 경계를 잰다.</para>
    /// </summary>
    public sealed class NullPackEntitlementSource : IPackEntitlementSource
    {
        public static readonly NullPackEntitlementSource Instance = new NullPackEntitlementSource();

        private NullPackEntitlementSource() { }

        public PackEntitlementState Query(string packId) => PackEntitlementState.Unknown;
    }

    /// <summary>
    /// ============================================================================
    /// ★ C층의 <b>유일한 창구</b> — 소유 판정은 여기 한 곳에서만 나온다
    /// ============================================================================
    /// 규범: <c>docs/security/ENTITLEMENT_CONTRACT.md</c> §E-1 ~ §E-6.
    ///
    /// ============================================================================
    /// 두 가지를 <b>구조로</b> 보장한다 (산문 규칙이 아니라 코드 형태로)
    /// ============================================================================
    /// <list type="number">
    ///  <item>★ <b>단조성(§E-3-b · §E-3-c)</b> — 한 번 <c>Owned</c> 로 확인된 팩은 이 프로세스가
    ///    사는 동안 계속 <c>Owned</c> 다. 스팀이 도중에 죽어 조회가 실패해도(→ <c>Unknown</c>)
    ///    <b>입고 있던 것을 회수하지 않는다</b>. 환불·가족공유 회수는 실재하지만, 종일 상주하는
    ///    앱에서 상주 중 회수는 <b>오탐 비용이 회수 이익보다 크다</b> — 다음 실행에서 정리된다.</item>
    ///  <item>★ <b>부정 캐시 없음(§E-3-a)</b> — <c>NotOwned</c>/<c>Unknown</c> 은 <b>기억하지 않는다.</b>
    ///    그래서 "재시도"라는 별도 장치가 필요 없다: 묻는 쪽이 다시 물으면 그 자리에서 다시 조회된다.
    ///    09:00에 스팀이 아직 안 떠 <c>Unknown</c> 이었어도 09:01의 질문은 새로 답을 받는다.
    ///    <b>한 번 실패한 조회를 프로세스 수명 동안 붙들지 않는다</b>는 요구가 곧 이 성질이다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 저장하지 않는다 — 그리고 그게 <b>세이브 스키마를 안 올리는 이유</b>다
    /// ============================================================================
    /// 보유 팩을 세이브에 적지 않는다(§E-4-a · §E-8-b). 그래서 이 라운드는
    /// <c>CharacterSaveStore.CurrentVersion</c> 을 <b>한 칸도 올리지 않는다</b>.
    /// <c>EntitlementNotInSaveAuditTests</c> 가 세이브 스키마 필드 이름에
    /// <c>dlc/pack/entitle/owned</c> 가 들어오는 것을 이미 막고 있고, 이 클래스는 그 계약의 반대편이다.
    ///
    /// <para>세이브에 남는 것은 <b>착용 아이템 아이디</b>뿐이고 그건 v5부터 이미 문자열이다.
    /// 팩 아이템을 입은 채 저장한 뒤 그 팩이 사라지면(환불·다른 기기)
    /// <c>ItemCatalog.IndexOfItemId</c> 가 <c>-1</c> 을 돌려주고 그 자리는 <b>미착용</b>이 된다 —
    /// 이미 있는 경로이지만 팩이 생기면 <b>처음으로 실제로 밟히는</b> 경로다.
    /// 그 성질은 <c>Tests/EditMode/PackEntitlementContractTests.cs</c> 가 잠근다.</para>
    /// </summary>
    public static class PackEntitlements
    {
        private static IPackEntitlementSource _source = NullPackEntitlementSource.Instance;

        /// <summary>★ 단조 래치(§E-3-b). 한 번 확인된 보유만 담는다 —
        /// <b>미보유와 미확인은 절대 담지 않는다.</b> 담는 순간 §E-3-a 가 깨진다.
        /// <para>디스크에 나가지 않는다(§E-4-a). 프로세스가 끝나면 사라지는 것이 요구사항이다.</para></summary>
        private static readonly List<string> _confirmedOwned = new List<string>();

        /// <summary>
        /// 이 팩의 상태. <b>C층 소유 판정의 유일한 출처</b>다.
        ///
        /// <para>빈 아이디는 <see cref="PackEntitlementState.Unknown"/> 이다 —
        /// "물을 대상이 없다"는 "안 샀다"가 아니다.</para>
        /// </summary>
        public static PackEntitlementState StateOf(string packId)
        {
            if (string.IsNullOrEmpty(packId)) return PackEntitlementState.Unknown;

            for (int i = 0; i < _confirmedOwned.Count; i++)
            {
                if (_confirmedOwned[i] == packId) return PackEntitlementState.Owned;
            }

            PackEntitlementState answer = _source != null
                ? _source.Query(packId)
                : PackEntitlementState.Unknown;

            if (answer == PackEntitlementState.Owned) _confirmedOwned.Add(packId);
            return answer;
        }

        /// <summary>
        /// ★ 사유 <b>키</b>. <see cref="PackEntitlementState.NotOwned"/> 와
        /// <see cref="PackEntitlementState.Unknown"/> 은 <b>서로 다른 문구</b>여야 한다(§E-2-a) —
        /// 같은 문구를 쓰면 오프라인 유저에게 <i>"당신은 안 샀습니다"</i>라고 말하게 된다.
        ///
        /// <para><b>왜 완성 문장이 아니라 키인가</b>: 이 파일은 <c>Core/</c> 이고 화면 문구는
        /// <c>ux-designer</c>/<c>coder-ui</c> 소관이다. 여기에 한글 원문을 두면
        /// <c>ARCHITECTURE.md</c> §528(로컬라이즈 키 · 원문 문자열 금지)을 <b>C층이 첫날부터</b> 어긴다.
        /// 키를 돌려주면 문구가 바뀌어도 이 파일이 안 바뀌고, <b>둘이 다르다</b>는 계약은
        /// 키 수준에서 이미 성립한다.</para>
        ///
        /// <para><c>default:</c> 를 두지 않는다(§E-1-b). 단이 하나 늘면 컴파일러가 아니라
        /// <b>사람이 이 자리를 보게</b> 하는 것이 요점이다.</para>
        /// </summary>
        public static string ReasonKey(PackEntitlementState state)
        {
            switch (state)
            {
                case PackEntitlementState.Owned: return "pack.state.owned";
                case PackEntitlementState.NotOwned: return "pack.state.notowned";
                case PackEntitlementState.Unknown: return "pack.state.unverified";
            }
            return "pack.state.unverified";
        }

        /// <summary>
        /// ★ 테스트 전용 출처 교체. <b><c>internal</c> 이다</b>(§E-6-c) —
        /// <c>public</c> 이면 프로덕션 어셈블리 밖에서 부를 수 있고, 그 한 줄이 결제 경계를 무력화한다.
        /// <c>UnlockSwitchScopeAuditTests</c> 의 <b>접근성 래칫</b>이 이 성질을 매 실행 잠근다.
        /// <para>단조 래치도 함께 비운다 — 안 비우면 앞 테스트가 확인한 보유가 다음 테스트로 샌다.</para>
        /// </summary>
        internal static void SetTestOverride(IPackEntitlementSource source)
        {
            _source = source ?? NullPackEntitlementSource.Instance;
            _confirmedOwned.Clear();
        }

        /// <summary>테스트 전용 원복. 출하 경로의 기본값(<see cref="NullPackEntitlementSource"/>)으로 되돌린다.</summary>
        internal static void ClearTestOverride() => SetTestOverride(null);

        /// <summary>
        /// ★ 프로덕션 출처 설치 창구(결재-1, 2026-09-05). <c>SetTestOverride</c>와 다른 성질 셋:
        /// <b>internal</b>(어셈블리 밖에서 못 부른다) · <b>1회 성공만</b>(이미 기본값이 아니면 무시하고
        /// 에러를 남긴다 — 출처를 갈아끼우는 경로를 없앤다) · <b>단조 래치를 비우지 않는다</b>(기동
        /// 1회뿐이라 비울 것이 없고, 비우는 능력을 두면 「확인된 보유를 지우는 함수」가 생긴다).
        /// <para>호출자는 스토어 어댑터 자신이다(<c>[RuntimeInitializeOnLoadMethod]</c>로 자가 설치).</para>
        /// </summary>
        internal static void UseSource(IPackEntitlementSource source)
        {
            if (source == null) return;
            if (!(_source is NullPackEntitlementSource))
            {
                UnityEngine.Debug.LogError("[C층] PackEntitlements.UseSource가 이미 설치된 출처를 " +
                    "갈아끼우려 했습니다 — 무시합니다. 출처 교체 경로는 존재하면 안 됩니다.");
                return;
            }
            _source = source;
        }
    }
}
