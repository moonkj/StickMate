using System;
using UnityEngine;

namespace StickMate.Core
{
    // ============================================================================
    // ★ 장비 도형 데이터 계약 v2 — 2026-09-05 인계본 「보이는 그대로」 이식
    //    (GAME_ARCHITECTURE_REVIEW §15 안 D → EQUIPMENT_HANDOFF_PORT_SPEC §13-4 → ★ §14-6 R16 로 정정)
    // ============================================================================
    // 인계본 91조각은 획 배수 8단계·흰 하이라이트·알파 채움을 쓰는데 우리 Shape 는 획 2단계·Tone 0/1/2 뿐이라
    // 담을 칸이 없었다. 이 파일은 그 칸을 <b>한 번에</b> 연다 — 스키마를 두 번 태우지 않는다(§15-6).
    //
    // R16(§14-6)이 안 D 를 다음처럼 정정했다(리더 결정):
    //   · 카드와 몸은 <b>좌표 한 벌</b>(인계본 정면 기하). 표면 비트는 망토 무대 도형(Body 전용)에만 실질 의미.
    //   · 획 등급(0~3 이산) → <b>연속 배수</b>(strokeMult) + 조각별 명목 폭(strokeInR) + 액세서리 전용 하한 1pt.
    //   · 조각별 알파를 <b>몸도 런타임으로 그린다</b>(불투명 정책 폐기). ★ R18 재질 팔레트 뒤로 채움은 재질색 불투명(α 1.0)이고
    //     투명 렌즈의 카드 워시(α0.16/0.20)만 예외다.
    //   · ★ R18/L-1: 채움 = 재질색 M/M2(카탈로그 주색/보조색, 카드·몸 같은 hex) · 윤곽 = 잉크 · 독립선 = 재질선 · <b>등급색은 조각에 0개</b>.
    //     흰 하이라이트·대비색은 계산 톤이라 WornColor 를 타지 않는다. 고정색(Fixed)·bodyAlpha·fixedFill/fixedLine 은 2026-09-05 에
    //     제거됐다(139조각 실사용 0 — I-22/I-23. 고정색은 WornColor 안전장치를 우회하는 유일한 임의색 통로였다).
    //   · 정원·보조색 게이트 폐지.
    // 여기 있는 것은 Core 에 있어야 한다: AccessoryWornShapeData(에셋)와 ItemIconPart(폴백 아이콘)가 같은
    // 도메인을 써야 하고, Interaction/AccessoryShapeBuilder.Shape 는 그 위에 얹힌다.

    /// <summary>
    /// 조각이 <b>어느 표면에 나오는가</b>. ★ <b>0(미설정)은 Body|Card 로 읽는다</b> — 구 에셋(팩 계약 v1)은 이 키가
    /// 없어 0이고, 인계본 16종도 카드와 몸이 같은 좌표라 0 이다. 명시 비트는 망토(무대 도형 = Body 전용 · 아이콘 = Card 전용)와
    /// R17 「몸 전용 조건부 조각」(외알안경 반대쪽 눈 = Body)에 쓴다. Portrait 비트는 예약 — 초상화는 Body 표면을 그린다.
    /// </summary>
    [Flags]
    public enum AccessorySurface : byte
    {
        None = 0,
        Body = 1,
        Card = 2,
        Portrait = 4,
    }

    public static class AccessorySurfaces
    {
        /// <summary>에셋/코드가 아무것도 적지 않은 값. <b>없음</b>이 아니라 <b>기본</b>이다.</summary>
        public const byte Unset = 0;

        public const AccessorySurface Default = AccessorySurface.Body | AccessorySurface.Card;

        public static AccessorySurface Effective(byte surfaces)
            => surfaces == Unset ? Default : (AccessorySurface)surfaces;

        public static bool IsOn(byte surfaces, AccessorySurface surface)
            => (Effective(surfaces) & surface) != 0;

        /// <summary>미설정(0)이 아니라 <b>명시적으로</b> 그 표면을 켠 조각인가.</summary>
        public static bool IsExplicit(byte surfaces, AccessorySurface surface)
            => surfaces != Unset && ((AccessorySurface)surfaces & surface) != 0;
    }

    /// <summary>
    /// 획 — 연속 배수. 인계본 8단계(×0.7·0.75·0.8·0.9·1.0·1.1·1.4·1.5)를 양자화하지 않고 그대로 나른다(§14-6 #2:
    /// 배율 1.00 의 HEAD/NECK 에서 명목 획이 하한 위에 살아 배수가 눈에 띈다).
    /// <para>몸의 실폭 = max(<c>strokeInR × R</c>, 액세서리 하한 <see cref="StickConfig.MinAccessoryStrokeScreenPoints"/>).
    /// 카드의 실폭 = 인계본 아이콘 획(2.2/64 × 상자) × <c>strokeMult</c>. 두 표면의 명목이 다른 이유는 인계본이 그렇게 선언했기
    /// 때문이다(아이콘 획 2.2 · 착용 슬롯 획 2.8/3.4/3.4/2).</para>
    /// </summary>
    public static class AccessoryStroke
    {
        /// <summary>미설정(0)은 ×1.0 이다.</summary>
        public static float Multiplier(float strokeMult) => strokeMult > 0f ? strokeMult : 1f;

        /// <summary>인계본 조각(계약 v2)인가 — 명목 폭이 적혀 있으면 그렇다. 0 이면 v1 규칙(비례 획 + 2pt/1pt 하한)으로 그린다.</summary>
        public static bool IsHandoff(float strokeInR) => strokeInR > 0f;
    }

    /// <summary>
    /// 색 <b>역할</b>. 조각은 색을 모르고 역할만 나른다 — 카드/몸/초상화/폴백 네 곳이 같은 표 하나를 본다.
    /// <para>★ 2026-09-05 재질 팔레트(<c>docs/EQUIPMENT_PALETTE.md</c> §2, 사용자 지시 "각각 아이템들의 색상을 채워 넣어야함"):
    /// 인계본 조각도 v1 과 <b>같은 뜻</b>으로 0/1/2 를 쓴다 — 0 = 재질색 M(<c>entry.PrimaryColor</c>), 1 = 보조 재질색 M2
    /// (<c>entry.SecondaryColor</c>), 2 = 그늘 M×0.28. <b>채움은 재질색 불투명(α 1.0)이고 카드·몸이 같은 hex 원천 하나</b>다.
    /// 채움이 있는 조각의 윤곽 = 잉크(카드 <c>CardIconInk</c> · 몸 = 유저 잉크), 채움 없는 독립선 = 그 조각의 M/M2(역할 0/1),
    /// 채움 위 내부 낱선 = 잉크(역할 <see cref="HeadInk"/>), 하이라이트 = 흰 α0.42. <b>등급색은 조각에 0개</b>(리더 판정 L-1 —
    /// 등급은 카드 프레임·리본·낱말만). 재질색·흰·대비색은 <c>WornColor</c> 틴트를 <b>우회</b>한다.</para>
    /// </summary>
    public static class AccessoryTone
    {
        /// <summary>아이템 재질색 M(= <c>PrimaryColor</c>). 채움이면 M 불투명, 채움 없는 선이면 M 재질선.</summary>
        public const byte Primary = 0;
        /// <summary>보조 재질색 M2(= <c>SecondaryColor</c>). 채움이면 M2 불투명, 채움 없는 선이면 M2 재질선.</summary>
        public const byte Accent = 1;
        /// <summary>주색을 어둡게 한 그늘(M × <see cref="ShadeFactor"/>). 채운 면 위에만(야구모자 띠).</summary>
        public const byte Shade = 2;
        /// <summary>흰 하이라이트 선(인계본 H, α0.42). 카드는 밑색 위 사전 합성, 몸은 런타임 알파. 틴트 우회.</summary>
        public const byte Highlight = 3;
        /// <summary>잉크. 채움이면 머리 채움색(= 캐릭터 잉크 / 카드 바탕), <b>채움 없는 선이면 잉크선</b>(채움 위 결·주름·중앙 막대 —
        /// 카드 <c>CardIconInk</c> · 몸 유저 잉크).
        /// <para>★ 번호 4: 2026-09-05 에 「고정색(Fixed)」을 제거하며 당겨 썼다(I-22/I-23 — 실사용 0, WornColor 우회 통로). 그 번호를
        /// 적은 팩 에셋은 출하 전이라 없다(기본 16종은 이 라운드가 재생성했다).</para></summary>
        public const byte HeadInk = 4;
        /// <summary>잉크의 <b>대비색</b>(어두운 잉크면 흰, 밝은 잉크면 목탄). 외알안경 착용 시 반대쪽 눈(R17 E-1, 리더 판정 #3). 틴트 우회.</summary>
        public const byte InkContrast = 5;
        /// <summary>★ R20 유리 렌즈(동그란안경·외알안경) — <b>바탕 위에 M2 를 <c>alpha</c> 로 미리 합성한 불투명 판</b>.
        /// 바탕은 카드 = 카드 바탕(<c>CardSurfaceMuted</c>), 몸 = 캐릭터 잉크(머리가 균일 잉크라 합성 결과가 「투명 워시」와 같다).
        /// 조각의 <c>alpha</c>는 채움 알파가 아니라 <b>합성 비율</b>이고 결과는 α 1.0 이다 — 그래서 가림 판정(EyesVisorOpacityTests)도
        /// 채움으로 통과한다. 여기서 <c>alpha</c>가 두 뜻을 갖는 유일한 역할이다.</summary>
        public const byte Glass = 6;

        public const int Count = 7;

        /// <summary>
        /// 채움 색을 그 위에 얹는 윤곽선/그늘 색으로 낮추는 계수 — 스펙 14-2(리더 판정 2026-09-03).
        /// 세 축(자기 채움 / 부모 채움 / 그늘 낱선)에서 대비 3.0을 지키는 천장이 0.3063(부모 채움)이고,
        /// 0.28은 그 천장에 +0.0263 여유다. 값의 전문은 <c>Interaction/AccessoryShapeBuilder.FillOutlineShadeFactor</c>.
        /// </summary>
        public const float ShadeFactor = 0.28f;

        /// <summary>인계본 H 원시함수의 흰색 알파(<c>#FFFFFF</c> α 0.42).</summary>
        public const float HighlightWhiteAlpha = 0.42f;

        public static Color Shaded(Color fill)
            => new Color(fill.r * ShadeFactor, fill.g * ShadeFactor, fill.b * ShadeFactor, fill.a);

        public static Color Highlighted(Color under)
            => new Color(Mathf.Lerp(under.r, 1f, HighlightWhiteAlpha),
                Mathf.Lerp(under.g, 1f, HighlightWhiteAlpha),
                Mathf.Lerp(under.b, 1f, HighlightWhiteAlpha), under.a);

        /// <summary>v1 역할 -> 실제 색(아이템 팔레트). <paramref name="underTone"/>은 하이라이트가 얹히는 조각의 역할이다.</summary>
        public static Color Resolve(byte tone, byte underTone, Color primary, Color secondary)
        {
            switch (tone)
            {
                case Accent: return secondary;
                case Shade: return Shaded(primary);
                case Highlight:
                    return Highlighted(Resolve(underTone == Highlight ? Primary : underTone, Primary, primary, secondary));
                default: return primary;
            }
        }

        /// <summary>색 칸이 <b>채워져 있는가</b> — 알파 0 이 「없음」이다(직렬화 기본값 (0,0,0,0)).</summary>
        public static bool HasColor(Color c) => c.a > 0f;

        public static bool IsKnown(byte tone) => tone < Count;
    }

    /// <summary>
    /// ★ 조각별 <b>레이어</b>(2026-09-05, R19 대비). 아이템 한 벌 안에서 조각마다 몸의 다른 층에 설 수 있다 —
    /// 모자 뒤층(머리 뒤)과 앞층(머리 위), 망토 뒤판(몸 뒤)과 칼라·걸쇠(몸통 앞), 배낭 뒷판과 어깨끈.
    /// 값은 <b>슬롯 기본 층 대비 어디인가</b>이고 실제 정렬 번호는 렌더 층(<c>AccessoryShapeBuilder.LayerOrder</c>)이 정한다 —
    /// 팩 데이터가 정렬 번호를 직접 적으면 남의 아이템 위로 올라오는 사고를 팩 하나가 낼 수 있다.
    /// <para>0(미설정) = 슬롯 기본 층. 구 에셋은 이 키가 없어 0 이다.</para>
    /// </summary>
    public enum AccessoryPieceLayer : byte
    {
        /// <summary>슬롯 기본 층(HEAD 10 · EYES 8 · NECK 7 · BACK −1 · HAIR 6).</summary>
        Slot = 0,
        /// <summary>몸 <b>뒤</b>(<c>SortBack</c>) — 망토 뒤판, 모자 뒤층, 배낭 뒷판. 어느 슬롯이든 섞어 쓸 수 있다.</summary>
        Back = 1,
        /// <summary>몸통 <b>앞</b>(<c>SortCapeFront</c>: 팔다리 앞, 머리 링 뒤) — 망토 칼라·걸쇠, 배낭 어깨끈.</summary>
        BodyFront = 2,
    }

    /// <summary>카드 워시 합성 상수(§13-3-1). 인계본 B 그라디언트(α 0.34→0.08)의 평면 대체값 — <c>alpha</c>가 0(미설정)인
    /// 채움은 이 값으로 그린다(카드·몸 공통, §14-6 #5 「평면 α0.21 로 대체 가능(미확인)」).</summary>
    public static class AccessoryCardWash
    {
        public const float GradientMeanAlpha = 0.21f;
    }
}
