using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ "표가 늘었는데 도형이 없다"를 <b>큰 소리로</b> 알리는 단일 신고소 — 2026-09-02 신설.
    ///
    /// ============================================================================
    /// 왜 만들었는가 (실제 결함)
    /// ============================================================================
    /// 도형을 고르는 <c>switch (itemIndex)</c>가 이 프로젝트에 8곳 있었고 <b>전부 default:가 없었다</b>.
    /// 그래서 7번째 모자를 <c>.asset</c>으로 넣으면 카드는 뜨는데(카탈로그가 그리므로) 몸에는 아무것도
    /// 안 그려지고, <b>에러도 로그도 남지 않았다</b>. 첫 증상이 "안 보인다"라 추적이 가장 비싼 형태다.
    /// 사용자 지시("추후 장비들은 계속 추가될 예정이니까 고려해줘")와 정면으로 부딪히는 구조였다.
    ///
    /// ============================================================================
    /// 두 가지로 알린다 — 그리고 <b>둘 다 필요하다</b>
    /// ============================================================================
    /// ① <see cref="Debug.LogError"/> — 릴리스 빌드에서도 낸다. 이 팀의 표준 확인 절차가
    ///    "릴리스 빌드를 켜고 <c>Player.log</c>로 확인한다"이고(Core/StickMateDevTools 문서),
    ///    개발 심볼로 잠그면 그 절차가 이 결함을 못 본다.
    /// ② <b>눈에 띄는 폴백 도형</b>(<c>AccessoryShapeBuilder.AppendMissingMarker</c>) —
    ///    <b>개발 게이트가 열렸을 때만</b>. 로그는 화면을 보고 있는 사람에게 안 보이고, 이 결함의 첫
    ///    증상이 바로 "화면에서 안 보인다"라 화면에도 흔적이 있어야 한다. 반대로 출하된 앱에서
    ///    사용자 캐릭터에 24시간 "빠진 네모"가 붙어 있는 것은 결함보다 나쁘므로 게이트를 건다
    ///    (<see cref="StickMateDevTools"/>에 환경변수 탈출구가 있어 릴리스에서도 켤 수 있다).
    ///
    /// ============================================================================
    /// 같은 자리는 한 번만 — 24시간 상주 앱이다
    /// ============================================================================
    /// 도형 재구성은 착용/방향/색이 바뀐 프레임마다 돌고, 펫 틱은 <b>매 프레임</b> 돈다. 매번 찍으면
    /// 로그가 자기 자신으로 덮여 정작 다른 사고를 가린다. 그래서 (신고자리, 값) 쌍마다 최초 1회만
    /// 찍는다. 중복 판정은 <see cref="long"/> 키 하나로 하므로 <b>할당이 없다</b> — 메시지 문자열은
    /// 중복 판정을 통과한 뒤에야 만든다(매 프레임 문자열 보간 금지 규약).
    /// </summary>
    internal static class ShapeCoverageGuard
    {
        // 신고 자리 코드. 같은 (슬롯, 번호)라도 자리가 다르면 <b>다른 사실</b>이라 따로 찍는다 —
        // "도형이 없다"와 "커버선이 없다"는 원인도 고칠 곳도 다르다.
        private const int SiteItemShape = 1;
        private const int SiteSlotDispatch = 2;
        private const int SiteHatCover = 3;
        private const int SiteFxShape = 4;
        private const int SitePetShape = 5;
        private const int SiteIconKind = 6;

        private static readonly HashSet<long> _logged = new HashSet<long>();

        /// <summary>실제로 로그를 찍은 서로 다른 자리의 수. 테스트가 "방어가 정말 작동했는가"를
        /// 이 값으로 읽는다 — 로그 문자열만 보면 <c>LogAssert</c>가 없는 환경에서 확인할 방법이 없다.</summary>
        internal static int LoggedCount { get; private set; }

        /// <summary>중복 포함 총 적발 횟수. 중복 억제가 실제로 걸렸는지(= 두 번 불러도 로그는 1건)를
        /// 이 값과 <see cref="LoggedCount"/>의 차이로 확인한다.</summary>
        internal static int HitCount { get; private set; }

        /// <summary>마지막으로 찍은 메시지. 테스트가 "무엇이 빠졌는지"까지 확인할 수 있게 남긴다.</summary>
        internal static string LastMessage { get; private set; }

        /// <summary>화면에도 흔적을 남길 것인가. 개발 게이트를 그대로 따른다(위 문단 ②).</summary>
        internal static bool ShowVisibleFallback => StickMateDevTools.Enabled;

        /// <summary>테스트 전용 초기화 — 중복 억제 때문에 테스트 순서에 따라 로그가 안 나올 수 있다.</summary>
        internal static void ResetForTests()
        {
            _logged.Clear();
            LoggedCount = 0;
            HitCount = 0;
            LastMessage = null;
        }

        /// <summary>도형 <c>switch</c>가 모르는 아이템 번호를 만났다.</summary>
        /// <returns>이번 호출에서 실제로 로그를 찍었으면 true(중복이면 false).</returns>
        internal static bool ReportMissingItemShape(EquipmentSlot slot, int itemIndex)
            => Log(SiteItemShape, (int)slot, itemIndex,
                $"[도형] {EquipmentModel.SlotCode(slot)} 카테고리 {itemIndex}번 아이템의 몸 도형이 " +
                "AccessoryShapeBuilder에 없습니다 — 표(Resources/Items의 .asset)에는 있는데 도형만 " +
                "빠졌습니다. 카드는 그대로 뜨므로 화면에서는 '몸에만 안 붙는' 증상으로 보입니다. " +
                $"Interaction/AccessoryShapeBuilder.cs의 해당 Append* switch에 case를 추가하세요.");

        /// <summary>슬롯 분배 <c>switch</c>가 모르는 자리를 만났다(= <see cref="EquipmentSlot"/>에 값이
        /// 늘었는데 분배가 안 따라왔다). FX/PET은 <b>정상적으로</b> 몸 도형이 없으므로 여기 오지 않는다.</summary>
        internal static bool ReportUnknownSlot(EquipmentSlot slot)
            => Log(SiteSlotDispatch, (int)slot, 0,
                $"[도형] 자리 {(int)slot}({slot})을(를) AccessoryShapeBuilder.Append가 모릅니다 — " +
                "EquipmentSlot에 값이 늘었는데 분배 switch가 따라오지 않았습니다. " +
                "몸 도형이 없는 자리(FX/PET)라면 '아무것도 안 그린다'를 case로 <b>명시</b>하세요.");

        /// <summary>모자 커버선 표가 모르는 번호를 만났다. 왕관(의도된 면제)·미착용과 <b>구분해서</b>
        /// 알린다 — 셋 다 +∞를 돌려주지만 사실이 다르고, 뭉뚱그리면 머리카락 클리핑이 조용히 틀어진다.</summary>
        internal static bool ReportUnknownHatCover(int hatItemIndex)
            => Log(SiteHatCover, 0, hatItemIndex,
                $"[도형] 모자 {hatItemIndex}번의 커버선(HatCoverLocalY)이 없습니다 — 지금은 왕관과 " +
                "똑같이 '아무것도 가리지 않는다'로 처리되므로 머리카락이 모자를 뚫고 나옵니다. " +
                "Interaction/AccessoryShapeBuilder.HatCoverLocalY에 이 모자의 커버선을 추가하세요" +
                "(정말 얹는 물건이면 왕관처럼 NothingCovered를 명시하세요).");

        /// <summary>이펙트 도형 <c>switch</c>가 모르는 번호를 만났다.</summary>
        internal static bool ReportMissingFxShape(int itemIndex, string where)
            => Log(SiteFxShape, Hash(where), itemIndex,
                $"[이펙트] FX {itemIndex}번의 연출이 {where}에 없습니다 — 표에는 있는데 그림이 없어 " +
                "아무 일도 일어나지 않습니다(사용자에게는 '이 이펙트만 고장'으로 보입니다).");

        /// <summary>펫 도형 <c>switch</c>가 모르는 번호를 만났다.</summary>
        internal static bool ReportMissingPetShape(int itemIndex, string where)
            => Log(SitePetShape, Hash(where), itemIndex,
                $"[펫] PET {itemIndex}번의 그림/움직임이 {where}에 없습니다 — 표에는 있는데 그림이 " +
                "없어 펫이 아예 나타나지 않습니다.");

        /// <summary>카드 썸네일이 모르는 조각 종류를 만났다(<see cref="ItemIconPartKind"/>가 늘었다).</summary>
        internal static bool ReportUnknownIconKind(ItemIconPartKind kind)
            => Log(SiteIconKind, (int)kind, 0,
                $"[아이콘] 조각 종류 {(int)kind}({kind})을(를) 그릴 줄 모릅니다 — ItemIconPartKind에 " +
                "값이 늘었는데 그리는 쪽이 따라오지 않아 그 조각만 조용히 빠집니다.");

        /// <summary>중복 억제 -> (통과했으면) 메시지 생성 -> 로그. 순서가 중요하다: 메시지 문자열
        /// 보간은 중복 판정을 통과한 뒤에만 일어나야 매 프레임 할당이 생기지 않는다.
        /// <para>C#은 인자를 먼저 계산하므로 <b>호출부의 보간 문자열은 매번 만들어진다</b>.
        /// 그래서 이 함수를 부르는 자리는 전부 <b>실패 경로</b>(= 정상 동작에서는 도달하지 않는
        /// default: 안)이고, 그 안에서만 문자열 값을 치른다.</para></summary>
        private static bool Log(int site, int a, int b, string message)
        {
            HitCount++;
            long key = ((long)site << 42) | ((long)(a & 0x1FFFFF) << 21) | (uint)(b & 0x1FFFFF);
            if (!_logged.Add(key)) return false;

            LoggedCount++;
            LastMessage = message;
            Debug.LogError(message);
            return true;
        }

        /// <summary>호출 지점 이름을 키에 섞기 위한 작은 해시. 이름이 겹쳐도 최악의 결과는
        /// "같은 자리로 묶여 로그가 1건으로 줄어드는 것"뿐이라 암호학적 품질이 필요 없다.</summary>
        private static int Hash(string where)
        {
            if (string.IsNullOrEmpty(where)) return 0;
            int h = 17;
            for (int i = 0; i < where.Length; i++) h = h * 31 + where[i];
            return h & 0xFFFF;
        }
    }
}
