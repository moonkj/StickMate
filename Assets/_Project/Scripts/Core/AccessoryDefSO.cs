using System;
using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// <see cref="ItemIconPart"/>의 <b>직렬화 가능한 쌍둥이</b>. 런타임 구조체는 readonly 필드라
    /// Unity 직렬화기가 손대지 못한다 — 값을 에셋에 눕히려면 쓰기 가능한 형태가 한 벌 필요하다.
    /// 두 형태의 필드는 <b>1:1</b>이고 변환은 <see cref="AccessoryDefSO.BuildIcon"/> 한 곳에서만 한다.
    /// </summary>
    [Serializable]
    public struct AccessoryIconPartData
    {
        public ItemIconPartKind kind;

        /// <summary>꺾은선이면 x0,y0,x1,y1,… / 원이면 cx,cy,r. 좌표계는 40×40 viewBox(원점 좌상단).</summary>
        public float[] values;

        /// <summary>해금 상태에서 이 조각을 칠할 색(이미 <c>Tinted()</c>가 역할에 맞는 색을 채운 결과값).</summary>
        public Color color;

        /// <summary>색 역할(<see cref="AccessoryTone"/>: 0 주색(재질색 M) / 1 보조색(M2) / 2 그늘 / 3 하이라이트 / 4 잉크 / 5 잉크 대비색).
        /// 런타임 구조체는 byte지만 에셋에는 int로 눕힌다 — YAML에서 byte/int는 같은 정수로 적히고,
        /// int 쪽이 인스펙터/JSON 도구와 마찰이 없다.
        /// <para>★ 도메인은 <c>AccessoryShapeBuilder.Shape.Tone</c>과 <b>같은 표</b>다(2026-09-05 계약 v2).
        /// 그 전에는 여기가 0/1, 몸이 0/1/2로 갈라져 있어 한쪽만 늘리면 폴백이 조용히 색을 틀렸다.</para></summary>
        public int tone;
    }

    // ============================================================================
    // ★ 몸에 붙는 형상(worn shape) — DLC 이행 B-2 파일럿 (2026-09-02, NECK 6종)
    // ============================================================================
    // <see cref="AccessoryDefSO.icon"/>이 <b>카드 썸네일</b>이었다면 아래는 <b>실제로 몸에 걸치는
    // 벡터</b>다. 지금까지 그 좌표는 <c>Interaction/AccessoryShapeBuilder.cs</c>의 아이템별 switch가
    // 갖고 있었고, 그래서 DLC 팩 하나를 붙일 때마다 <b>기본 로직 파일을 고쳐야</b> 했다(원칙 4가
    // 장비만 비껴가 있던 자리다).
    //
    // ---------------------------------------------------------------------------
    // 왜 "점 목록"이 아니라 "항(term) 목록"인가
    // ---------------------------------------------------------------------------
    // 이 앱의 액세서리 좌표에는 <b>월드유닛 절대 상수가 하나도 없다</b> — 전부 머리 반경 R이나
    // 몸통 길이의 배수다(그래서 characterScale이 바뀌어도 액세서리만 뒤에 남지 않는다).
    // 즉 형상은 "점"이 아니라 <b>치수에 대한 식</b>이다. 그 식을 그대로 눕히려면
    //   좌표 = Σ (기저 × 계수 × 계수 …)
    // 하나면 충분하다. 계수를 <b>사슬</b>로 두는 이유는 정확도 때문이다:
    // C#의 곱셈은 왼쪽 결합이고 float 곱셈은 결합법칙을 만족하지 않는다.
    // 원본이 <c>hw * 0.878f</c>(= <c>(r*0.98f)*0.878f</c>)라고 적었으면 계수를 미리 곱해
    // <c>r * 0.86044f</c>로 눕히는 순간 마지막 비트가 갈라진다. 사슬은 그 괄호를 보존한다.
    // 같은 이유로 <b>항의 순서</b>도 원본의 덧셈 순서 그대로여야 한다.
    //
    // 이 성질 덕분에 NECK 6종은 코드에서 데이터로 내려오면서 좌표가 <b>비트 단위로</b> 같았다
    // (회귀 잠금: Tests/EditMode/WornShapeDataGoldenTests.cs + Golden/NeckWornShapeGolden.txt).

    /// <summary>항이 딛고 서는 <b>치수</b>. 값은 리그에서 오고(<see cref="AccessoryWornFrame"/>),
    /// 에셋은 번호만 적는다.</summary>
    public enum AccessoryWornBasis
    {
        /// <summary>머리 반경 R.</summary>
        HeadRadius = 0,
        /// <summary>어깨-고관절 길이.</summary>
        TorsoLength = 1,
        /// <summary>목에 걸치는 것들의 부착 기준선(로컬 Y 절대값).</summary>
        NeckLine = 2,
        /// <summary>어깨선(로컬 Y 절대값).</summary>
        ShoulderLine = 3,
        /// <summary>머리 중심선(로컬 Y 절대값).</summary>
        HeadCenterLine = 4,
        /// <summary>고관절선(로컬 Y 절대값).</summary>
        HipLine = 5,

        /// <summary>이 점의 <b>로컬 벡터를 기울인 결과</b>의 x. <see cref="AccessoryWornShapeData.swingDegrees"/>가
        /// 0이 아닌 도형에서만 뜻이 있다.</summary>
        SwungX = 6,
        /// <summary>같은 것의 y.</summary>
        SwungY = 7,

        /// <summary>
        /// ★ <b>신장 H</b>(발바닥 → 정수리). 2026-09-07 추가 — <c>design-motion</c>이 프롭 좌표를
        /// <b>전부 H 배수</b>로 적는데 기저 목록에 H가 없어, 매니페스트를 쓰는 사람이 손으로
        /// R 배수로 환산해야 했다. 그 환산이 한 번 어긋나면 <b>조형이 통째로 밀린다</b>.
        ///
        /// <para>★★ <b>반드시 맨 뒤에 붙인다. 중간에 끼워 넣지 마라.</b> 이 번호는 <c>.asset</c>의
        /// 스트림에 <b>숫자 그대로</b> 눕는다(<see cref="AccessoryWornShapeData.terms"/>). 중간에 끼우면
        /// 출하된 모든 도형의 기저가 한 칸씩 밀리고, 그 사고는 <b>저장 파일을 열어봐도 안 보인다</b> —
        /// 좌표가 「다른 값」일 뿐이라 화면은 그려진다. <c>StickmanStateId</c>가 DLC 매니페스트에
        /// 나가는 정수라 못을 박아 둔 것과 같은 종류의 값이다.
        /// <c>Tests/EditMode/AccessoryWornBasisHeightTests</c>가 그 배치를 매 실행 잠근다.</para>
        ///
        /// <para>★ <b>프레임이 H를 안 실어 주면 조용히 0이 되지 않고 <u>크게 실패한다</u></b>
        /// (<see cref="AccessoryWornShapeReader"/>의 기저 해석). 0으로 떨어뜨리면 도형이
        /// <b>원점으로 무너지는데</b> 그건 「안 그려짐」과 화면상 구별되지 않는다.</para>
        /// </summary>
        Height = 8,
    }

    /// <summary>항이 <b>언제</b> 더해지는가. 상태는 착용자 쪽 사실 하나(<c>stateOn</c>)이고,
    /// 지금 그것을 쓰는 것은 줄무늬 타이의 "월요일에는 느슨해진다" 하나뿐이다.</summary>
    public enum AccessoryWornGate
    {
        Always = 0,
        WhenStateOn = 1,
        WhenStateOff = 2,
    }

    /// <summary>
    /// ★ 항의 <b>마지막 계수</b>를 삼각함수에 통과시킬 것인가.
    ///
    /// <para><b>왜 각을 눕히고 cos을 런타임에 부르는가 — 2026-09-02 실측.</b>
    /// 처음에는 <c>Mathf.Cos(a)</c>의 <b>결과</b>를 상수로 구워 넣었다. 오프라인 대조(.NET 6)에서는
    /// 20개 전부 일치했는데, <b>Unity 안에서 재 보니 10개 중 하나가 cos 6 ULP · sin 4 ULP 어긋났다</b>.
    /// 그 하나는 각이 <c>7.226 rad</c>으로 <b>10점 중 유일하게 2π를 넘는</b> 점이었다 — 런타임마다
    /// 2π 밖 인자 축소(argument reduction)가 갈린다.</para>
    ///
    /// <para>즉 <b>삼각함수 결과를 구우면 에셋이 런타임에 종속된다</b>. 지금은 macOS 에디터(Mono)와
    /// .NET 6이 갈렸고, 출하 빌드의 IL2CPP나 Windows에서 또 갈리지 않는다는 보장이 없다.
    /// 그래서 <b>각을 데이터에 두고 코사인은 엔진이 부른다</b> — 옛 코드가 하던 것과 정확히 같은
    /// 호출이라 어떤 런타임에서도 비트까지 같다. 각 자체는 평범한 float이라 왕복에 문제가 없다.</para>
    /// </summary>
    public enum AccessoryWornTrig
    {
        None = 0,
        Cos = 1,
        Sin = 2,
    }

    /// <summary>
    /// 도형 하나. 좌표는 <see cref="terms"/> 한 줄기 스트림에 들어 있다.
    ///
    /// <para><b>스트림 문법</b>(전부 float. 개수는 정수로 반올림해 읽는다):</para>
    /// <code>
    /// stream := pointCount , point * pointCount
    /// point  := [ swingDegrees != 0 이면 ] sum(localX) , sum(localY)   // 기울이기 전의 로컬 벡터
    ///           sum(x) , sum(y)
    /// sum    := termCount , term * termCount
    /// term   := basis , gate , trig , coefCount , coef * coefCount
    /// </code>
    /// <para>합은 <b>왼쪽부터</b> 누적하고(첫 항이 씨앗이다), 곱도 <b>왼쪽부터</b> 사슬로 곱한다.
    /// 항이 하나도 남지 않으면 0이다.</para>
    /// <para><b>trig</b>가 0이 아니면 <b>마지막 계수</b>는 계수가 아니라 <b>라디안 각</b>이고,
    /// 곱하기 직전에 <c>Mathf.Cos</c>/<c>Mathf.Sin</c>을 거친다. ★ 이 자리가 필요한 이유는
    /// <see cref="AccessoryWornTrig"/> 문단에 있다 — <b>실측으로 찾은 함정</b>이다.</para>
    /// </summary>
    [Serializable]
    public struct AccessoryWornShapeData
    {
        /// <summary>도형 이름. 렌더러가 레이어를 고르는 값은 아니고, 테스트와 로그가 이것으로 지목한다.</summary>
        public string name;

        /// <summary>마지막 점과 첫 점을 잇는가.</summary>
        public bool loop;

        /// <summary>윤곽선 아래에 <b>채움 면</b>을 한 장 깔 것인가(안에 있는 것을 가려야 하는 물건).
        /// ★ 2026-09-08 game-architect — <b>소비자마다 이 비트를 다르게 구현한다</b>. 장비
        /// 렌더러(<c>CharacterAccessoryRenderer.BuildFillMesh</c>)는 실제 <c>MeshRenderer</c>를
        /// 만든다. 코스튬 프롭 렌더러(<c>CostumePropRenderer</c>)는 메시를 안 만들고 같은
        /// 폴리라인을 <c>stroke × 2.2</c>로 굵게 한 번 더 긋는다 — 안쪽이 완전히 안 막히므로
        /// 획 폭이 도형 안쪽 여백보다 좁으면 <b>가운데가 뚫려 보인다</b>(design-equipment가
        /// R28에서 장비의 자를 그대로 옮겼다가 R29에서 실측으로 발견한 함정). 이 비트를 쓰는
        /// 새 소비자를 만들 때는 어느 쪽 구현을 따를지 먼저 확인해라 — 이름이 같다고 뜻도
        /// 같다고 가정하지 마라.</summary>
        public bool filled;

        /// <summary>색 역할 — <see cref="AccessoryTone"/>(0 주색 / 1 보조색 / 2 그늘 / 3 하이라이트).
        /// <b>색이 아니라 역할</b>을 나른다.</summary>
        public int tone;

        /// <summary>걸을 때 흔들리는 점 구간의 시작(-1이면 흔들지 않는다).</summary>
        public int swayStart;

        public int swayCount;

        /// <summary>상태가 켜졌을 때 이 도형이 <b>로컬 원점을 축으로</b> 기우는 각(도).
        /// 0이면 기울기 자체가 없고, 그때는 로컬 벡터 목록도 스트림에 없다.</summary>
        public float swingDegrees;

        // ---- 계약 v2 (2026-09-05, 인계본 이식 · R16 정정 · R17 선반영). 전부 <b>0/false 가 곧 v1과 같은 뜻</b>이라
        //      구 에셋은 키가 없어도 그대로 옳다(Unity는 키가 없으면 C# 기본값을 둔다). 팩이 이 필드를 쓰면
        //      StickPackManifestSO.SchemaVersion 2를 요구해야 한다 — v1 앱은 surfaces/strokeInR 을 몰라
        //      반쯤 읽는다(카드 전용 조각을 몸에 그리거나 알파 없이 불투명으로 그린다).

        /// <summary>표면 비트(<see cref="AccessorySurface"/>). <b>0 = Body|Card</b>(<see cref="AccessorySurfaces.Effective"/>).</summary>
        public int surfaces;

        /// <summary>획 배수(인계본 ×0.7~×1.5, 연속). 0 = ×1.0. 카드 획 = 아이콘 획 × 이 값.</summary>
        public float strokeMult;

        /// <summary>몸 표면의 <b>명목</b> 획 폭(머리 반경 R 배수, 배수가 이미 곱해진 값). 0 = v1 규칙(비례 획).
        /// 실폭은 액세서리 하한(<see cref="StickConfig.MinAccessoryStrokeScreenPoints"/>)에 걸린다.</summary>
        public float strokeInR;

        /// <summary>윤곽/선을 그리지 않는다(인계본 F/CF: 채움만).</summary>
        public bool noStroke;

        /// <summary>채움 알파(인계본 fillOpacity, 그라디언트는 평균 0.21 / 망토 0.81). 0 = 미설정
        /// (<see cref="AccessoryCardWash.GradientMeanAlpha"/>). 카드는 바탕 위에 사전 합성하고, 몸은 런타임 알파로 그린다.</summary>
        public float alpha;

        /// <summary>선 알파(인계본 strokeOpacity · 하이라이트 0.42). 0 = 1.</summary>
        public float lineAlpha;

        /// <summary>이 조각이 <b>얹히는</b> 조각 = 같은 아이템 목록에서 이만큼 <b>앞</b>의 조각(0 = 없음/바탕).
        /// 카드 사전 합성의 밑색이 여기서 온다(몸은 런타임 알파라 필요 없다 — §14-6 #4).</summary>
        public int underBack;

        /// <summary>조각의 몸 층(<see cref="AccessoryPieceLayer"/>: 0 슬롯 기본 · 1 몸 뒤 · 2 몸통 앞). 망토 칼라·걸쇠 = 2(§14-6 #8),
        /// R19 모자 뒤층/배낭 뒷판 = 1. 카드는 층을 모른다(목록 순서로 겹친다). 에셋에는 int 로 눕힌다(인스펙터/JSON 마찰 없음).</summary>
        public int layer;

        /// <summary>이 조각의 좌표는 <b>이미 몸 좌표계</b>라 아이템 몸 변형(배율·오프셋·반전)을 걸지 않는다 —
        /// 외알안경 반대쪽 눈(R17 E-1)처럼 몸에서만 존재하는 조각.</summary>
        public bool bodyFixed;

        // ★ 2026-09-05 제거 — bodyAlpha · fixedFill · fixedLine (계약 v2 초안). 139조각 실사용 0(game-architect I-22/I-23)이었고,
        //   fixedFill 은 팩이 WornColor 안전장치(채도·명도 상자)를 우회해 임의색을 몸에 올릴 수 있는 유일한 통로라 출하 전에 닫았다.
        //   이관: 「몸만 불투명」은 alpha = 1(채움은 이제 전부 불투명), 고정색은 역할 0/1(재질색 M/M2 — 카탈로그 색이 곧 재질색)로 적는다.
        //   에셋에 세 키가 남아 있어도 Unity 는 모르는 키를 무시하므로 읽기에는 해가 없다 — 기본 16종 에셋은 이 라운드가 재생성해 키가 없다.

        /// <summary>위 문법의 스트림.</summary>
        public float[] terms;
    }

    /// <summary>
    /// 아이템 단위 <b>몸 표면 파라미터</b>(계약 v2 · R16 그룹 알파 · R17 모자 맞춤/외알안경 반전). 카드는 모른다.
    /// 값의 출처는 둘 — 코드 자리(HEAD/EYES/BACK)는 <c>AccessoryShapeBuilder.WornTransformCode</c>(생성 표),
    /// 에셋 자리(NECK)는 <see cref="AccessoryDefSO"/>의 <c>worn*</c> 필드. 소비자는 <c>AccessoryShapeBuilder.WornTransformOf</c> 하나다.
    /// </summary>
    public readonly struct AccessoryWornTransform
    {
        /// <summary>그룹 알파(0 = 1). 등 아이콘 40%(날개·배낭).</summary>
        public readonly float GroupAlpha;
        /// <summary>머리 중심 기준 배율 u(0 = 1). 모자 맞춤(R17 H-2: 카드 배율 ×1.15~1.60).</summary>
        public readonly float Scale;
        /// <summary>세로 추가 압축 ky(0 = 1). y 배율 = Scale × ScaleY. 털모자 0.80(초상화 액자 2.551 R 제약, §14-10-1).</summary>
        public readonly float ScaleY;
        /// <summary>세로 오프셋(R 배수, 배율을 건 뒤 더한다).</summary>
        public readonly float OffsetYInR;
        /// <summary>좌우 거울 반전(몸 표면만). 외알안경 오른쪽 착용(R17 E-1).</summary>
        public readonly bool MirrorX;

        public AccessoryWornTransform(float groupAlpha, float scale, float scaleY, float offsetYInR, bool mirrorX)
        {
            GroupAlpha = groupAlpha;
            Scale = scale;
            ScaleY = scaleY;
            OffsetYInR = offsetYInR;
            MirrorX = mirrorX;
        }

        public static AccessoryWornTransform None => default;

        /// <summary>점열을 건드릴 변형이 하나라도 있는가(그룹 알파는 색이라 여기 안 든다).</summary>
        public bool IsSet => (Scale > 0f && Scale != 1f) || (ScaleY > 0f && ScaleY != 1f) || OffsetYInR != 0f || MirrorX;
    }

    /// <summary>기저 번호 -> 실제 치수. 리그를 아는 쪽(<c>Interaction</c>)이 채워서 넘긴다 —
    /// 이 파일이 리그 타입을 알면 형상 데이터가 다시 렌더링 계층에 묶인다.</summary>
    public struct AccessoryWornFrame
    {
        public float HeadRadius;
        public float TorsoLength;
        public float NeckLine;
        public float ShoulderLine;
        public float HeadCenterLine;
        public float HipLine;

        /// <summary>+1이면 오른쪽을 본다. <b>x에만</b> 곱한다(원본 <c>Rig.F</c>의 규약 그대로).</summary>
        public float Facing;

        /// <summary>★ 신장 H(발바닥 → 정수리). <see cref="AccessoryWornBasis.Height"/>가 딛는 값이다.
        /// <para><b><see cref="HeightUnavailable"/>(0)이면 「안 실어 줬다」는 뜻</b>이고, 그때
        /// H 기저를 쓰는 항은 <b>조용히 0이 되지 않고 해석이 실패한다</b>. 0을 「신장 0」이라는
        /// 실재값으로 읽지 않는 이유는 그런 캐릭터가 없기 때문이다 — 이 저장소의
        /// 「없음 ≠ 0」 판단이 여기서는 <b>둘이 같은 뜻</b>이라 동반 불리언이 필요 없다.</para></summary>
        public float Height;

        /// <summary>「신장을 안 실어 줬다」. 값 자체에 뜻이 있는 것이 아니라, 위 문단의 판정이
        /// <b>숫자를 베끼지 않게</b> 하는 자리다.</summary>
        public const float HeightUnavailable = 0f;

        /// <summary>
        /// 신장 없이 만드는 프레임. ★ <b>일부러 남겨 둔다</b> — 몸에 붙는 42종은 H 기저를 하나도
        /// 쓰지 않으므로 이 생성자를 쓰는 기존 호출부
        /// (<c>Interaction/AccessoryShapeBuilder.Frame</c>)가 <b>한 글자도 안 바뀐다</b>.
        /// 새 인자를 강제하면 그 파일을 고쳐야 하고, 그건 지금 다른 라운드가 물고 있는 파일이다.
        /// <para>H 기저를 쓰는 표면(코스튬 프롭)은 아래 8인자 생성자를 쓴다.</para>
        /// </summary>
        public AccessoryWornFrame(float headRadius, float torsoLength, float neckLine,
            float shoulderLine, float headCenterLine, float hipLine, float facing)
            : this(headRadius, torsoLength, neckLine, shoulderLine, headCenterLine, hipLine, facing,
                   HeightUnavailable)
        {
        }

        /// <summary>신장까지 실어 주는 프레임. H 기저를 쓰는 도형은 이쪽으로만 성립한다.</summary>
        public AccessoryWornFrame(float headRadius, float torsoLength, float neckLine,
            float shoulderLine, float headCenterLine, float hipLine, float facing, float height)
        {
            HeadRadius = headRadius;
            TorsoLength = torsoLength;
            NeckLine = neckLine;
            ShoulderLine = shoulderLine;
            HeadCenterLine = headCenterLine;
            HipLine = hipLine;
            Facing = facing;
            Height = height;
        }

        /// <summary>구조 검사 전용 — 치수를 1로 두면 스트림이 <b>문법적으로</b> 성립하는지만 본다.
        /// <para>★ 신장도 1이다. 안 그러면 H 기저를 쓰는 도형이 <b>문법은 옳은데</b> 검사에서
        /// 거부되고, 매니페스트를 쓰는 사람은 그 이유를 영원히 못 찾는다.</para></summary>
        public static AccessoryWornFrame Unit
            => new AccessoryWornFrame(1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f);
    }

    /// <summary>
    /// 스트림 -> 점 배열. <b>읽는 곳은 여기 하나뿐이다</b> — 문법을 두 벌로 적으면
    /// 검사기와 실행기가 갈라지고, 그 순간 "검사는 통과했는데 화면은 깨진" 상태가 만들어진다.
    /// </summary>
    public static class AccessoryWornShapeReader
    {
        /// <summary>한 도형의 점 수 상한. 값 자체에 뜻이 있는 것이 아니라, <b>망가진 스트림이
        /// 길이를 통째로 오독해 수억 개를 할당하는 것</b>을 막는 자리다.</summary>
        public const int MaxPointsPerShape = 4096;

        /// <summary>스트림을 <b>실제로 돌려</b> 문법을 확인한다. 검사기를 따로 적지 않는 이유는
        /// 위 문단 그대로다.</summary>
        public static bool Validate(in AccessoryWornShapeData shape, out string error)
            => TryBuild(shape, AccessoryWornFrame.Unit, false, out _, out error)
            && TryBuild(shape, AccessoryWornFrame.Unit, true, out _, out error);

        public static bool TryBuild(in AccessoryWornShapeData shape, in AccessoryWornFrame frame,
            bool stateOn, out Vector3[] points, out string error)
        {
            points = null;
            error = null;

            float[] t = shape.terms;
            if (t == null || t.Length == 0) { error = "형상 스트림이 비어 있습니다."; return false; }

            int i = 0;
            int count = (int)t[i++];
            if (count <= 0 || count > MaxPointsPerShape)
            {
                error = $"점 수 {count}가 1~{MaxPointsPerShape} 밖입니다.";
                return false;
            }

            bool swings = shape.swingDegrees != 0f;
            float cos = 1f, sin = 0f;
            if (swings)
            {
                // 상태가 꺼져 있으면 각을 <b>계산하지 않고</b> 정확히 0f를 쓴다 — 원본과 같은 자리다.
                float tilt = stateOn ? shape.swingDegrees * Mathf.Deg2Rad : 0f;
                cos = Mathf.Cos(tilt);
                sin = Mathf.Sin(tilt);
            }

            var pts = new Vector3[count];
            for (int p = 0; p < count; p++)
            {
                float swungX = 0f, swungY = 0f;
                if (swings)
                {
                    if (!ReadSum(t, ref i, frame, stateOn, 0f, 0f, out float fx, out error)) return false;
                    if (!ReadSum(t, ref i, frame, stateOn, 0f, 0f, out float dy, out error)) return false;
                    swungX = fx * cos - dy * sin;
                    swungY = fx * sin + dy * cos;
                }

                if (!ReadSum(t, ref i, frame, stateOn, swungX, swungY, out float x, out error)) return false;
                if (!ReadSum(t, ref i, frame, stateOn, swungX, swungY, out float y, out error)) return false;
                pts[p] = new Vector3(x * frame.Facing, y, 0f);
            }

            if (i != t.Length)
            {
                error = $"스트림이 {t.Length}칸인데 {i}칸만 쓰였습니다 — 남은 값이 있으면 " +
                        "점 하나가 통째로 빠졌다는 뜻입니다.";
                return false;
            }

            points = pts;
            return true;
        }

        private static bool ReadSum(float[] t, ref int i, in AccessoryWornFrame frame, bool stateOn,
            float swungX, float swungY, out float result, out string error)
        {
            result = 0f;
            error = null;
            if (i >= t.Length) { error = "항 개수를 읽기 전에 스트림이 끝났습니다."; return false; }

            int terms = (int)t[i++];
            if (terms < 0) { error = $"항 개수 {terms}가 음수입니다."; return false; }

            float acc = 0f;
            bool any = false;
            for (int k = 0; k < terms; k++)
            {
                if (i + 4 > t.Length) { error = "항 머리(기저/게이트/삼각/계수 수)를 읽는 중 스트림이 끝났습니다."; return false; }
                int basis = (int)t[i++];
                int gate = (int)t[i++];
                int trig = (int)t[i++];
                int coefficients = (int)t[i++];
                if (coefficients < 0 || i + coefficients > t.Length)
                {
                    error = $"계수 {coefficients}개를 읽을 수 없습니다(남은 칸 {t.Length - i}).";
                    return false;
                }
                if (trig != (int)AccessoryWornTrig.None && coefficients < 1)
                {
                    error = "삼각함수 항인데 각이 될 계수가 없습니다.";
                    return false;
                }

                if (!TryBasis(basis, frame, swungX, swungY, out float v, out string basisError))
                {
                    // ★ 사유를 구분한다 — 「모르는 번호」와 「값을 안 실어 줬다」는 고치는 방법이 다르다.
                    //   기존 기저 8개는 여기 오지 않으므로 그 경로의 문구는 한 글자도 안 바뀐다.
                    error = basisError ?? $"기저 번호 {basis}를 모릅니다.";
                    return false;
                }

                for (int c = 0; c < coefficients; c++)
                {
                    float coef = t[i + c];
                    // 마지막 계수만 각이 될 수 있다 — 옛 코드의 `Mathf.Cos(a) * radius` 그 자리다.
                    if (c == coefficients - 1)
                    {
                        if (trig == (int)AccessoryWornTrig.Cos) coef = Mathf.Cos(coef);
                        else if (trig == (int)AccessoryWornTrig.Sin) coef = Mathf.Sin(coef);
                        else if (trig != (int)AccessoryWornTrig.None)
                        {
                            error = $"삼각 번호 {trig}을 모릅니다.";
                            return false;
                        }
                    }
                    v *= coef;
                }
                i += coefficients;

                if (gate == (int)AccessoryWornGate.WhenStateOn && !stateOn) continue;
                if (gate == (int)AccessoryWornGate.WhenStateOff && stateOn) continue;
                if (gate != (int)AccessoryWornGate.Always
                    && gate != (int)AccessoryWornGate.WhenStateOn
                    && gate != (int)AccessoryWornGate.WhenStateOff)
                {
                    error = $"게이트 번호 {gate}를 모릅니다.";
                    return false;
                }

                acc = any ? acc + v : v;
                any = true;
            }

            result = any ? acc : 0f;
            return true;
        }

        /// <summary>
        /// 기저 번호 -> 값. <paramref name="error"/>는 <b>실패했고 사유가 「모르는 번호」가 아닐 때만</b>
        /// 채워진다(<c>null</c>이면 호출부가 기존 문구를 쓴다 — 옛 경로의 메시지를 보존하기 위해서다).
        /// </summary>
        private static bool TryBasis(int basis, in AccessoryWornFrame f, float swungX, float swungY,
            out float value, out string error)
        {
            error = null;
            switch ((AccessoryWornBasis)basis)
            {
                case AccessoryWornBasis.HeadRadius: value = f.HeadRadius; return true;
                case AccessoryWornBasis.TorsoLength: value = f.TorsoLength; return true;
                case AccessoryWornBasis.NeckLine: value = f.NeckLine; return true;
                case AccessoryWornBasis.ShoulderLine: value = f.ShoulderLine; return true;
                case AccessoryWornBasis.HeadCenterLine: value = f.HeadCenterLine; return true;
                case AccessoryWornBasis.HipLine: value = f.HipLine; return true;
                case AccessoryWornBasis.SwungX: value = swungX; return true;
                case AccessoryWornBasis.SwungY: value = swungY; return true;

                // ★ H는 「값이 없으면 0」으로 떨어뜨리지 <b>않는다</b>. 0으로 두면 그 항이 통째로
                //   사라져 도형이 원점으로 무너지고, 그 화면은 「안 그려짐」과 구별되지 않는다 —
                //   이 저장소가 반복해서 당한 «조용한 실패»의 형태다. 크게 실패시켜 사유를 남긴다.
                case AccessoryWornBasis.Height:
                    if (f.Height > AccessoryWornFrame.HeightUnavailable) { value = f.Height; return true; }
                    value = 0f;
                    error = "이 도형이 신장(H) 기저를 쓰는데 프레임에 신장이 실려 있지 않습니다. " +
                            "H를 받는 8인자 AccessoryWornFrame 생성자로 만들어야 합니다 — " +
                            "0으로 넘어가면 도형이 원점으로 무너지고 그건 '안 그려짐'과 화면상 같습니다.";
                    return false;

                default: value = 0f; return false;
            }
        }
    }

    /// <summary>
    /// ★ 장비 아이템 1종 = 에셋 1개 (DLC 이행 A단계, docs/ARCHITECTURE.md 5-3-3).
    ///
    /// ============================================================================
    /// 왜 만들었나
    /// ============================================================================
    /// 원칙 4("신규 모션/이펙트는 기본 로직 무수정으로 매니페스트를 통해 추가")가 선언만 되어 있고
    /// 실제로는 28종이 <c>ItemCatalog.cs</c>의 <c>new Row(...)</c> 나열이었다. 그 상태에서는 DLC 팩
    /// 하나를 붙일 때마다 <b>기본 로직 파일을 고쳐야</b> 한다. 이 에셋은 그 표를 코드 밖으로 꺼낸 것이다.
    ///
    /// ============================================================================
    /// 이 에셋이 <b>아직</b> 하지 않는 일 (A단계 경계)
    /// ============================================================================
    ///  · 도형(몸에 붙는 벡터)은 <b>자리마다 다르다</b>. 2026-09-02 B-2 파일럿으로 NECK 6종이
    ///    <see cref="wornShapes"/>로 내려왔고, 나머지 4자리(HEAD/EYES/BACK/HAIR)는 아직
    ///    <c>Interaction/AccessoryShapeBuilder.cs</c>의 switch가 갖고 있다.
    ///    <see cref="icon"/>은 <b>카드 썸네일 40×40</b>이지 몸에 붙는 도형이 아니다.
    ///  · Addressables/팩 매니페스트는 C단계다. 지금은 평범한 <c>Resources</c> 로드다.
    ///
    /// ============================================================================
    /// 아이디가 곧 세이브 키다
    /// ============================================================================
    /// <see cref="itemId"/>는 세이브 v5가 그대로 적는 값이다(<c>Core/CharacterSaveStore.cs</c>).
    /// <b>이름을 바꾸면 사용자의 차림이 사라진다</b> — 표시 이름(<see cref="displayName"/>)만 바꿔라.
    /// </summary>
    [CreateAssetMenu(fileName = "AccessoryDef", menuName = "StickMate/Accessory Def", order = 1)]
    public sealed class AccessoryDefSO : ScriptableObject
    {
        [Header("정체")]
        [Tooltip("세이브 파일과 훗날의 상점 SKU가 쓰는 안정적인 아이디. 절대 바꾸지 말 것.")]
        public string itemId;

        [Tooltip("이 아이템이 차지하는 카테고리.")]
        public EquipmentSlot slot;

        [Tooltip("카테고리 안에서의 자리(0~3). AccessoryShapeBuilder의 switch가 이 번호로 도형을 고른다 " +
                 "— 순서를 바꾸면 그림이 통째로 어긋난다.")]
        public int itemIndex;

        [Header("표시")]
        public string displayName;

        [TextArea(2, 4)]
        [Tooltip("없는 효과를 주장하지 말 것(전투 수치/소리). 방해성 행동에는 탈출구를 명시할 것.")]
        public string description;

        [Header("규칙")]
        [Tooltip("이 아이템을 보유하게 되는 레벨. 1이면 처음부터 보유.")]
        public int requiredLevel = 1;

        /// <summary>
        /// ★ 이 아이템이 <b>머리카락을 가리는가</b>.
        ///
        /// <para>★★ <b>2026-09-08 Major 4 착지 — 렌더러가 이 필드를 읽는다.</b> 다만 읽는 범위는
        /// <b>코드 표 밖의 자리(= 팩 모자)뿐</b>이다. 출하 6종(0~5)의 커버선은 여전히
        /// <c>AccessoryShapeBuilder.HatCoverLocalY</c>의 명시된 <c>case</c>가 정본이다 —
        /// 그 여섯이 <b>좌표</b>(H-2 착용선)를 갖고 있고 이 <c>bool</c>은 좌표를 실을 수 없기 때문이다.
        /// 그래서 이 필드가 답하는 것은 <b>「가리는가」</b> 하나이고 <b>「어디까지」</b>가 아니다.</para>
        ///
        /// <para>★ <b>옛 주석이 말한 「모자면 가린다는 전역 규칙」은 이미 사실이 아니었다</b>(2026-09-08 실측):
        /// 왕관은 <c>if</c> 분기가 아니라 <c>case HeadCrown: return NothingCovered;</c>라는 <b>표의 값</b>으로
        /// 이미 면제였다. 실재하던 결함은 <b><c>default:</c> 하나</b>였다 — 코드 표에 없는 번호를 전부
        /// 「모르는 모자」로 신고해서, <b>팩 HEAD 아이템이 들어오는 순간 재구성마다 결함 경로를 밟았다</b>.</para>
        ///
        /// <para>★ <b>HAIR 카테고리는 은퇴했다</b>(<c>EquipmentModel.IsRetiredSlot</c>, 2026-09-06 사용자 지시).
        /// 커버선의 유일한 소비자가 <c>AppendHair</c>이므로 <b>오늘 이 값은 화면에 한 점도 닿지 않는다.</b>
        /// 그래도 값을 옳게 적는다 — 카테고리가 되살아나는 날 이 필드가 곧 동작이 되고, 그때
        /// 틀린 값은 <b>원인 모를 그림 변화</b>로 나타난다.</para>
        /// </summary>
        [Tooltip("모자 계열이 머리카락을 덮는가. 팩 모자는 이 값이 곧 렌더러의 판정이다 " +
                 "— 좌표를 실을 수 없으므로 true로 적으면 '가린다고 선언했는데 커버선이 없다'로 신고된다. " +
                 "얹는 물건(왕관 같은)은 false.")]
        public bool hidesHair;

        /// <summary>
        /// ★ <b>등급 순위를 매기는 모집단</b>의 번호. 기본 42종은 전부
        /// <see cref="ItemCatalog.BaseCohortId"/>(= 0)이고, <b>DLC 팩은 팩마다 다른 값</b>을 쓴다.
        ///
        /// <para><b>0으로 두고 팩을 출하하면 무슨 일이 일어나는가</b>(design-systems R3 실측):
        /// 등급은 코호트 <b>안에서의</b> <c>requiredLevel</c> 순위로 정해지므로, 팩 6종이 기본 코호트에
        /// 합류하면 슬롯 모집단이 6 → 12가 되고 <b>기본 42종의 등급이 통째로 미끄러진다</b> —
        /// rank5가 전설 → 희귀, rank4가 영웅 → 희귀. 슬롯 동전 합이 650 → 230(−64.6%)이 되고,
        /// "캡 20은 기본 42종만으로 도달"이라는 사용자 확정 차단선이 아무도 안 건드렸는데 깨진다.
        /// 증상은 <b>팩을 산 사람이 아니라 안 산 사람에게</b> 나타난다.</para>
        ///
        /// <para>★ <b>기본값이 0인 것은 우연이 아니라 요구사항이다.</b> Unity는 <c>.asset</c>에 키가
        /// 없으면 그 필드를 <b>C# 기본값 그대로</b> 둔다. 기본 42종의 <c>.asset</c>에는 이 키가 없고
        /// (한 파일도 고치지 않았다) 그래서 전부 0 = <see cref="ItemCatalog.BaseCohortId"/>로 실린다.
        /// <b><see cref="ItemCatalog.BaseCohortId"/>를 0이 아닌 값으로 바꾸면 그 42종이 조용히
        /// 남의 모집단으로 넘어간다</b> — <c>ItemRarityDerivationTests</c>가 그 등식을 잠근다.</para>
        /// </summary>
        [Tooltip("등급 순위를 매길 모집단 번호. 기본 42종은 0. DLC 팩은 팩마다 다른 값을 쓸 것 — " +
                 "0으로 두면 기본 42종의 등급이 통째로 미끄러진다(팩을 안 산 사람에게 증상이 나타난다).")]
        public int cohortId = ItemCatalog.BaseCohortId;

        /// <summary>
        /// ★ 이 아이템의 등급을 <b>선언</b>한다. <see cref="DeclaredRarity.Derived"/>(기본)면 선언하지 않는
        /// 것이고, 등급은 <c>requiredLevel</c> 코호트 순위에서 파생된다(기본 42종이 전부 여기다).
        ///
        /// <para><b>왜 팩은 선언해야 하는가</b>: 팩은 자기 코호트를 쓰므로 모집단이 팩 하나뿐이고,
        /// 그 안에서 <c>requiredLevel</c> 순위를 매기면 <b>같은 팩 안에서 등급이 갈린다</b> —
        /// 현금으로 산 6종 중 하나만 전설이 되는 형태다. 팩은 단일 등급이 계약이다(DS-2).</para>
        ///
        /// <para>★★ <b>타입이 <see cref="ItemRarity"/>가 아닌 것은 우연이 아니라 요구사항이다.</b>
        /// Unity 는 키가 없으면 필드를 C# 기본값으로 두고, <c>ItemRarity</c> 의 기본값은
        /// <see cref="ItemRarity.Common"/>(= 0)이다. 그 타입으로 이 필드를 만들면 기본 42종이 <b>파일 수정 없이</b>
        /// 「일반으로 선언됨」이 되고, 실측상 <b>28/42 의 등급이 내려앉는다</b>
        /// (오프라인 하니스 <c>Tools/ShapeDump</c> 로 프로덕션 직렬화 경로에서 직접 잰 값이다).
        /// <see cref="DeclaredRarity"/> 문단에 그 실측과 대안 비교가 있다 —
        /// <b>이 필드를 <c>ItemRarity</c> 로 되돌리기 전에 그것부터 읽을 것.</b></para>
        ///
        /// <para>팩이 쓸 수 있는 상한은 <see cref="ItemCatalog.MaxDeclaredRarityForPack"/> 이고,
        /// 그 위(영웅·전설)는 타입에는 있지만 감사에서 막힌다 — <b>기본 42종보다 센 것을 팔지 않는다</b>는
        /// 페이투윈 차단선이다. 타입에서 지우지 않는 이유는, 지우면 그 차단선이 「값이 없어서」가 되고
        /// 나중에 누가 값을 되살리는 순간 아무 경보 없이 열리기 때문이다. 감사에 남겨야 이유가 남는다.</para>
        /// </summary>
        [Tooltip("등급 선언. Derived(기본)면 requiredLevel 순위에서 파생한다 — 기본 42종은 전부 이것이고 " +
                 "이 칸을 건드리면 안 된다. DLC 팩만 선언하고, 팩은 6종이 전부 같은 단이어야 하며 " +
                 "상한은 희귀다(ItemCatalog.MaxDeclaredRarityForPack).")]
        public DeclaredRarity declaredRarity = DeclaredRarity.Derived;

        /// <summary>
        /// ★ 이 아이템의 <b>세트 테마 키</b>. 비면 <see cref="ItemCatalog.ThemeUnassigned"/>(= 무소속)다.
        ///
        /// <para><b>기본 42종은 이 칸을 비운다.</b> 그쪽 테마는 <c>ItemCatalog</c>의 코드 표
        /// (<c>ThemeTable</c>, R21 안 B)가 <b>정본</b>이고, 이 칸에 무엇을 적어도
        /// <c>ItemCatalog.ResolveTheme</c>이 <b>보지 않는다</b> — 조용히 무시하면 그게 두 번째 진실이 되므로
        /// <c>ItemCatalog.AuditDeclarations</c>가 <b>결함으로 신고</b>한다.</para>
        ///
        /// <para><b>팩은 반드시 적는다</b>(스탯 4슬롯 아이템에 한해). 팩 아이템은 코드 표에 못 들어가므로
        /// 이 칸이 <b>유일한 통로</b>다(§21-10-a). 비워 두면 그 팩은 <b>세트를 영원히 완성할 수 없고</b>,
        /// 증상은 조용하다 — 화면은 멀쩡하고 +8만 안 붙는다(DS-4′-e 침묵 실패).</para>
        ///
        /// <para>★ <b>기본 6테마(<c>ink/sport/office/cyber/mil/neon</c>)를 팩이 쓰면 결함이다.</b>
        /// <c>EquipmentStatRules.IsSetComplete</c>는 <b>문자열 동등성만</b> 보고 코호트를 안 보므로
        /// (<c>StatSlotLoadout</c>이 코호트를 안 나른다), 팩이 <c>mil</c>을 쓰면 <b>기본 3종 + 팩 1종</b>
        /// 조합에 세트 보너스가 붙는다 — 무료 세트의 경제가 유료 아이템으로 새는 형태다.</para>
        ///
        /// <para>★ 값 모양은 <see cref="PackManifestKeys.IsWellFormed"/>로 잰다(ASCII 소문자·숫자·점·밑줄).
        /// 판정자를 두 벌로 적지 않는다 — 매니페스트 키와 <b>같은 자</b>를 쓴다.</para>
        /// </summary>
        [Tooltip("세트 테마 키(예: mine, arcane). 기본 42종은 비운다 — 코드 표가 정본이다. " +
                 "팩의 스탯 4슬롯 아이템은 반드시 적는다(비면 세트가 영원히 완성되지 않는다). " +
                 "기본 6테마(ink/sport/office/cyber/mil/neon)는 쓸 수 없다.")]
        public string themeKey;

        /// <summary>
        /// ★ 이 아이템의 <b>부스탯 방향</b> 선언. <see cref="DeclaredSubStat.None"/>(기본)이면
        /// 선언하지 않은 것이다.
        ///
        /// <para><b>기본 42종은 이 칸을 비운다</b> — <c>themeKey</c>와 같은 이유이고, 같은 감사가 잡는다.
        /// <b>팩은 스탯 4슬롯 아이템에 한해 반드시 적는다</b>: 안 적으면 그 아이템은 같은 자리의 무료
        /// 아이템보다 <b>부스탯 하나만큼 약한 채로</b> 팔린다(현금을 낸 쪽이 손해다).</para>
        ///
        /// <para>타입이 <see cref="CharacterStat"/>가 <b>아닌</b> 이유(직렬화 기본값 0 = 집중력)는
        /// <see cref="DeclaredSubStat"/> 문단에 실측과 함께 있다 —
        /// <b>이 필드를 <c>CharacterStat</c>으로 되돌리기 전에 그것부터 읽을 것.</b></para>
        /// </summary>
        [Tooltip("부스탯 방향 선언. None(기본)이면 없음 — 기본 42종은 전부 이것이고 이 칸을 건드리면 안 된다. " +
                 "팩의 스탯 4슬롯 아이템만 적는다. 외형 슬롯(머리/이펙트/펫)은 팩이라도 None이다.")]
        public DeclaredSubStat declaredSubStat = DeclaredSubStat.None;

        [Header("카드 썸네일 (40×40 viewBox, 원점 좌상단, y 아래로)")]
        public AccessoryIconPartData[] icon;

        /// <summary>
        /// ★ <b>카드/상점 진열 전용 비트맵 아이콘</b>(2026-09-08, 사용자 요청 «렌더링 이미지»).
        /// 비어 있으면(= 기본값 <c>null</c>) 아무 일도 일어나지 않고 카드는 지금까지처럼
        /// 벡터 경로(<see cref="AccessoryDefSO.icon"/> → <c>AccessoryCardIcon.TryBuild</c>)를 탄다.
        /// <b>출하 42종은 이 칸을 비운다</b> — 한 바이트도 고치지 않았고, 그래서 하위 호환이
        /// 「기본값이 곧 옛 동작」이라는 구조적 사실로 보장된다(<c>cohortId</c>가 간 길과 같다).
        ///
        /// <para>★★ <b>몸에 붙는 그림은 이 칸을 절대 보지 않는다.</b> 착용 표면은 여전히
        /// <see cref="wornShapes"/> 벡터 하나이고, 그것이 이 필드 이름에 «card»가 들어간 이유다.
        /// 비트맵을 몸에 붙이면 캐릭터가 <b>부위별로 다른 화법</b>(선화 몸 + 사진 같은 모자)이 되어
        /// 스틱메이트라는 그림 자체가 갈라진다 — 2026-09-08 리더 판단으로 <b>범위 밖</b>이다.
        /// 소비자를 늘리기 전에 그 판단부터 다시 받아라.</para>
        ///
        /// <para><b>왜 <see cref="icon"/>을 지우고 이걸로 대체하지 않는가</b>: 비트맵은 잉크색
        /// 전환·재질색(M/M2) 파생을 태울 수 없다. <see cref="ItemCatalogEntry.PrimaryColor"/>는
        /// <see cref="icon"/> 조각에서 뽑히고, 그 색을 상세 패널·몸·감사가 읽는다. 벡터를 지우면
        /// 그 원천이 통째로 사라진다 — 두 벌을 <b>일부러</b> 남긴다.</para>
        ///
        /// <para>값이 실리는 자리는 병렬 표(<c>ItemCatalog.CardSprite</c>) 하나다.
        /// <see cref="ItemCatalogEntry"/>에 얹지 않는 이유는 <see cref="hidesHair"/>와 <b>같다</b> —
        /// 항목에 필드를 더하면 골든 덤프·감사가 함께 흔들린다.</para>
        /// </summary>
        [Tooltip("카드/상점 진열 전용 비트맵 아이콘. 비우면(기본) 벡터 아이콘을 그대로 쓴다. " +
                 "★ 몸에 붙는 그림(wornShapes)은 이 칸을 보지 않는다 — 카드 표면 전용이다.")]
        public Sprite cardIconOverride;

        /// <summary>
        /// ★ <b>몸에 붙는 형상</b>. 비어 있으면 그 자리는 아직
        /// <c>Interaction/AccessoryShapeBuilder.cs</c>의 코드 분기가 갖고 있다는 뜻이다
        /// (2026-09-02 현재 NECK 6종만 여기로 내려왔다 — B-2 파일럿).
        /// <para>문법과 정확도 규약은 <see cref="AccessoryWornShapeData"/> 문단에 있다.
        /// <b>손으로 적는 값이 아니다</b> — 사슬의 순서 하나가 마지막 비트를 바꾼다.</para>
        /// </summary>
        [Header("몸에 붙는 형상 (항 스트림 — AccessoryWornShapeData 참고)")]
        public AccessoryWornShapeData[] wornShapes;

        // ---- 아이템 단위 몸 표면 파라미터(계약 v2 · R16/R17). 0/false = 변형 없음. 카드는 이 넷을 모른다.
        //      코드가 좌표를 갖는 자리(HEAD/EYES/BACK)는 AccessoryShapeBuilder.WornTransform 표가 같은 값을 갖는다.

        [Header("몸 표면 파라미터 (0 = 없음)")]
        [Tooltip("등 아이콘 40% 같은 그룹 알파. 조각 알파에 곱해진다. 0 = 1.")]
        public float wornGroupAlpha;

        [Tooltip("몸 표면에서 머리 중심을 축으로 조각을 키우는 배율(모자 크기 맞춤, R17). 0 = 1.")]
        public float wornScale;

        [Tooltip("몸 표면 세로 추가 압축(y 배율 = wornScale × 이 값, R17 털모자 0.80). 0 = 1.")]
        public float wornScaleY;

        [Tooltip("몸 표면 세로 오프셋(머리 반경 R 배수, R17).")]
        public float wornOffsetYInR;

        [Tooltip("몸 표면에서 좌우 거울 반전(외알안경 오른쪽 착용, R17). 카드는 파일 그대로.")]
        public bool wornMirrorX;

        // ---- 재질색(사용자 지시 2026-09-05 "각각 아이템들의 색상을 채워 넣어야함")은 <b>별도 칸이 아니다</b> — docs/EQUIPMENT_PALETTE.md §2:
        //      M = icon[].color(tone 0) = entry.PrimaryColor, M2 = tone 1 = entry.SecondaryColor. 카드·몸이 그 한 원천을 읽는다.
        //      칸을 하나 더 두면 같은 아이템의 색 원천이 둘이 된다(2026-08-30 「카드엔 색이 있는데 착용하면 없다」의 뿌리).

        /// <summary>에셋에 누운 값 -> 런타임 구조체 배열. <b>배열을 복사</b>하는 이유는, 복사하지 않으면
        /// 런타임이 들고 있는 <c>float[]</c>가 곧 임포트된 에셋의 배열이라 누가 한 칸이라도 쓰면
        /// 에디터에서 에셋이 조용히 더러워지기 때문이다. 정적 초기화 때 한 번만 도는 경로다.</summary>
        public ItemIconPart[] BuildIcon()
        {
            if (icon == null || icon.Length == 0) return null;

            var parts = new ItemIconPart[icon.Length];
            for (int i = 0; i < icon.Length; i++)
            {
                float[] src = icon[i].values;
                float[] values = src != null ? new float[src.Length] : null;
                if (src != null) Array.Copy(src, values, src.Length);

                parts[i] = new ItemIconPart(icon[i].kind, values, icon[i].color, (byte)icon[i].tone);
            }
            return parts;
        }
    }
}
