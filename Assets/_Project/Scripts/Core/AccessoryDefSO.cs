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

        /// <summary>0 = 주색, 1 = 보조색. 런타임 구조체는 byte지만 에셋에는 int로 눕힌다 —
        /// YAML에서 byte/int는 같은 정수로 적히고, int 쪽이 인스펙터/JSON 도구와 마찰이 없다.</summary>
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

        /// <summary>윤곽선 아래에 <b>채움 면</b>을 한 장 깔 것인가(안에 있는 것을 가려야 하는 물건).</summary>
        public bool filled;

        /// <summary>0 = 주색, 1 = 보조색, 2 = 그림자 톤. <b>색이 아니라 역할</b>을 나른다.</summary>
        public int tone;

        /// <summary>걸을 때 흔들리는 점 구간의 시작(-1이면 흔들지 않는다).</summary>
        public int swayStart;

        public int swayCount;

        /// <summary>상태가 켜졌을 때 이 도형이 <b>로컬 원점을 축으로</b> 기우는 각(도).
        /// 0이면 기울기 자체가 없고, 그때는 로컬 벡터 목록도 스트림에 없다.</summary>
        public float swingDegrees;

        /// <summary>위 문법의 스트림.</summary>
        public float[] terms;
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

        public AccessoryWornFrame(float headRadius, float torsoLength, float neckLine,
            float shoulderLine, float headCenterLine, float hipLine, float facing)
        {
            HeadRadius = headRadius;
            TorsoLength = torsoLength;
            NeckLine = neckLine;
            ShoulderLine = shoulderLine;
            HeadCenterLine = headCenterLine;
            HipLine = hipLine;
            Facing = facing;
        }

        /// <summary>구조 검사 전용 — 치수를 1로 두면 스트림이 <b>문법적으로</b> 성립하는지만 본다.</summary>
        public static AccessoryWornFrame Unit => new AccessoryWornFrame(1f, 1f, 1f, 1f, 1f, 1f, 1f);
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

                if (!TryBasis(basis, frame, swungX, swungY, out float v))
                {
                    error = $"기저 번호 {basis}를 모릅니다.";
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

        private static bool TryBasis(int basis, in AccessoryWornFrame f, float swungX, float swungY, out float value)
        {
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
        /// ★ 이 아이템이 <b>머리카락을 가리는가</b>. 지금 <c>AccessoryShapeBuilder.HatCoverLocalY</c>는
        /// "모자면 가린다"를 전역 규칙으로 갖고 있는데, 그건 규칙이 아니라 <b>아이템별 성질</b>이다
        /// (왕관은 얹는 것이라 밑이 뚫려 있어 머리가 보이는 게 옳다).
        /// <para><b>A단계에서는 아직 아무도 읽지 않는다</b> — 값만 실제 렌더러 동작과 일치하게 채워 둔다.
        /// 렌더러를 이 필드로 갈아타게 하는 것은 별도 라운드다(Major 4).</para>
        /// </summary>
        [Tooltip("모자 계열이 머리카락을 덮는가. A단계에서는 기록만 하고 렌더러는 아직 읽지 않는다.")]
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

        [Header("카드 썸네일 (40×40 viewBox, 원점 좌상단, y 아래로)")]
        public AccessoryIconPartData[] icon;

        /// <summary>
        /// ★ <b>몸에 붙는 형상</b>. 비어 있으면 그 자리는 아직
        /// <c>Interaction/AccessoryShapeBuilder.cs</c>의 코드 분기가 갖고 있다는 뜻이다
        /// (2026-09-02 현재 NECK 6종만 여기로 내려왔다 — B-2 파일럿).
        /// <para>문법과 정확도 규약은 <see cref="AccessoryWornShapeData"/> 문단에 있다.
        /// <b>손으로 적는 값이 아니다</b> — 사슬의 순서 하나가 마지막 비트를 바꾼다.</para>
        /// </summary>
        [Header("몸에 붙는 형상 (항 스트림 — AccessoryWornShapeData 참고)")]
        public AccessoryWornShapeData[] wornShapes;

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
