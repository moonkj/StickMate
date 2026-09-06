using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// 회귀 잠금 — <b>"주석이 지목한 파일은 실제로 존재한다."</b>
    ///
    /// ============================================================================
    /// 왜 있는가 (2026-09-02, code-inspection 1라운드)
    /// ============================================================================
    /// 이 저장소가 반복해서 당한 형태는 <b>거짓 주석</b>이다. 버그는 언젠가 재현되지만
    /// 주석은 아무도 실행하지 않으므로 <b>버그보다 오래 산다</b> — 다음 사람이 그걸 읽고
    /// 판단한다. 실제로 하루에 5건이 나왔고 <b>전부 사람이 우연히 발견</b>했다.
    ///
    /// 그중 <b>기계로 검증 가능한 부분집합</b>이 하나 있다: 주석이 <b>다른 파일을 지목</b>하는
    /// 경우다. 그 파일이 없으면 그 문장은 무조건 거짓이고, 판정에 사람의 판단이 끼지 않는다.
    /// 이 테스트는 그 부분집합만 잠근다. "이 함수는 X를 한다" 같은 의미 주장은 못 잰다 —
    /// <b>못 재는 것을 재는 척하지 않는다.</b>
    ///
    /// <para>이 검사가 특히 값싼 이유: 파일을 <b>쪼개거나 이름을 바꾸는</b> 순간 그 파일을
    /// 가리키던 주석이 전부 거짓이 된다. 이 저장소는 2026-09-02에 <c>CharacterInfoWindow</c>를
    /// 7개 partial로 쪼갰고, 앞으로도 쪼갤 것이다.</para>
    ///
    /// ============================================================================
    /// 이 테스트가 처음 잡은 것 (전부 실측)
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><c>Platform/VisibleTopEdgeSolver.cs</c>가 "<c>Tests/EditMode/VisibleTopEdgeSolverTests.cs</c>가
    ///     그 실측이다"라고 적었는데 그런 파일은 없다(실제 이름은 <c>VisibleTopEdgeOcclusionTests.cs</c>).</item>
    ///   <item><c>States/IdleState.cs</c> · <c>States/WalkState.cs</c>가 <c>States/IPlannedDwellSource.cs</c>를
    ///     지목하는데 그런 파일은 없다(그 인터페이스는 <c>States/IMovementIntentSource.cs</c> 안에 있다).</item>
    ///   <item><c>Core/CharacterSaveStore.cs</c>의 <c>Tests/*/GlobalTestIsolation.cs</c>는 어떤 파일에도
    ///     맞지 않는다(실제는 <c>GlobalEditModeTestIsolation.cs</c> / <c>GlobalPlayModeTestIsolation.cs</c>).</item>
    /// </list>
    ///
    /// ============================================================================
    /// 설계 규칙 3가지 (전부 이 저장소가 당한 사고에서 나왔다)
    /// ============================================================================
    /// <list type="number">
    ///   <item><b>파일명 명부로 소스를 찾지 않는다.</b> 디렉터리를 통째로 걷는다 —
    ///     명부 방식은 파일을 쪼개는 순간 눈이 먼다(이 저장소에서 2건 발생).</item>
    ///   <item><b>모든 "0건"에 양성 대조를 붙인다.</b> 아래 <c>양성대조_*</c> 테스트가
    ///     스캐너에 <b>있는 것을 실제로 찾는지</b> 먼저 증명한다. 최소 수집량 가드도 함께 둔다 —
    ///     스캐너가 아무것도 못 읽고 초록불이 되는 것이 이 저장소의 사고 #4·#5였다.</item>
    ///   <item><b>면제에는 사유를 적는다.</b> 그리고 면제가 <b>고쳐졌으면 실패</b>한다 —
    ///     명부가 조용히 늙는 것을 막는다.</item>
    /// </list>
    ///
    /// <para><b>한계(정직하게 적는다)</b>: 이 검사는 "그 파일이 있는가"만 본다.
    /// 그 파일이 주석이 말하는 <b>내용</b>을 담고 있는지는 못 잰다.
    /// 다만 <see cref="줄번호_참조는_썩지_않았는지_앵커로_확인한다"/>만은 예외로,
    /// 지목한 <b>줄</b>이 여전히 같은 문구인지까지 확인한다.</para>
    ///
    /// ============================================================================
    /// ★★ 2026-09-06 확장 — <b>이 감사 자신이 사각지대를 갖고 있었다</b>
    /// ============================================================================
    /// <c>coder-systems</c>가 재화 롤오버 배선 중 발견했다: <c>Core/CurrencyModel.cs</c>와
    /// <c>Core/CurrencyRules.cs</c>가 <c>Tests/EditMode/IncomeTimeSourceAuditTests</c> ·
    /// <c>Tests/EditMode/DailyLimitClampAuditTests</c>를 <b>"이 규칙을 지키는 감사가 있다"</b>는
    /// 근거로 인용하는데 <b>둘 다 존재하지 않았다.</b> 그런데 이 감사는 조용히 초록이었다 —
    /// <see cref="CsRefRegex"/>가 <b><c>.cs</c> 확장자가 붙은 참조만</b> 매칭하기 때문이다.
    ///
    /// <para>즉 <b>「확장자 없이 클래스명/테스트명으로 인용하는 형태」가 통째로 감사망 밖</b>이었다.
    /// 그리고 그 형태는 이 저장소의 <b>주된</b> 인용 관례다 — 실측하면
    /// <c>Tests/(EditMode|PlayMode)/…</c> 경로형이 110건, 확장자 없는 <c>*Tests</c> 낱말형이
    /// 142건으로, 확장자 붙은 <c>.cs</c> 참조 534건과 <b>같은 자릿수</b>다.</para>
    ///
    /// <para>★ <b>이 확장이 처음 잡은 것(전부 실측, 위 2건 제외 8건):</b>
    /// <c>Tests/EditMode/PortraitBodyStrokeParityTests</c>(실제로는 <c>PlayMode</c>에 있다) ·
    /// <c>Tests/PlayMode/DockLandingSilhouetteTests</c> · <c>Tests/PlayMode/InfoGearContrastTests</c> ·
    /// <c>CharacterStatCardLayoutTests</c> · <c>DancePoseFallbackParityTests</c> ·
    /// <c>MacSystemAudioSelectorTests</c> · <c>SystemAudioActivityProbeContractTests</c> ·
    /// <c>WindowsGameProcessProbeTests</c>. 여덟 개 전부 <b>현재형 단언</b>("…가 …를 잠근다")이라
    /// 읽는 사람이 검증이 있다고 믿게 만든다.</para>
    ///
    /// <para>★★ <b>2026-09-06 후속 — 여덟 중 여섯이 실제 테스트로 채워졌다</b>(test-engineer).
    /// 그리고 채우면서 <b>이 감사 자신의 기록 하나가 틀렸다는 것</b>이 나왔다:
    /// <c>CharacterStatCardLayoutTests</c> 항목에 <i>"74 = 11+18+8+5+8+13+11 검산은 지금 아무도
    /// 안 한다"</i>라고 적었는데, <see cref="CharacterStatColumnLayoutTests"/>가 <b>그 일곱 항의 합을
    /// 이미 상수에서 세고 있었다</b>. 깨진 것은 검산이 아니라 <b>이름</b>이었다
    /// (<c>…CardLayoutTests</c> vs <c>…ColumnLayoutTests</c>).
    /// <b>「인용이 깨졌다」와 「불변식이 안 지켜진다」는 다른 사실이고, 이 감사는 앞엣것만 잰다</b> —
    /// 뒤엣것을 단정하면 그 단정이 다시 거짓 주석이 된다. 이 파일이 잡으려던 병 그대로다.</para>
    ///
    /// <para>★ <b>넓히되 과하게 넓히지 않았다</b>(오탐이 몇 번이면 감사는 꺼진다):
    /// ① 경로형은 <c>Tests/EditMode/</c>·<c>Tests/PlayMode/</c> 접두에만 건다 —
    ///    <c>Platform/MacOS</c>·<c>States/Core</c>처럼 <b>디렉터리·네임스페이스</b>를 가리키는
    ///    확장자 없는 인용이 77건이고(실측), 그것까지 파일로 해석하면 전부 거짓 위반이 된다.
    /// ② 낱말형은 <b><c>Tests</c>로 끝나는 PascalCase</b>만 건다. 이 저장소에서 그 꼬리는
    ///    사실상 테스트 클래스 전용이다.
    /// ③ 단 <c>…ForTests</c>는 뺀다 — <c>ResetForTests</c>·<c>FeedClickForTests</c>처럼
    ///    <b>테스트 전용 프로덕션 메서드</b>의 이름 관례라 9건이 통째로 거짓 위반이었다(실측).</para>
    /// </summary>
    public sealed class CommentReferenceAuditTests
    {
        // ==================================================================
        // 경로
        // ==================================================================

        /// <summary><c>&lt;repo&gt;/Assets</c>. Unity가 주는 유일한 안정적 기준점이다.</summary>
        private static string AssetsRoot => Application.dataPath;

        /// <summary><c>&lt;repo&gt;</c>. <c>docs/</c>·<c>design/</c>·<c>Tools/</c> 참조를 확인할 때 쓴다.</summary>
        private static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        /// <summary>테스트 어셈블리 자신은 검사 대상이 아니다(테스트 주석은 프로덕션 독자가 읽지 않는다).
        /// 경로 조각으로 판정한다 — 파일명 명부가 아니다.</summary>
        private const string TestsPathFragment = "/Scripts/Tests/";

        // ==================================================================
        // 면제 — 사유 없이 넣지 마라
        // ==================================================================

        /// <summary>
        /// <b>저장소 밖 파일</b>. 우리가 만들지 않았고 우리 트리에 없다(UPM 패키지 / Unity 엔진 소스).
        /// 지목 자체는 정당하므로 면제하되, <b>이 목록에 없는 외부 파일은 전부 위반</b>이다 —
        /// 그래야 오타 난 우리 파일 이름이 "외부겠지"로 빠져나가지 못한다.
        /// </summary>
        private static readonly Dictionary<string, string> ExternalOwned = new Dictionary<string, string>
        {
            ["UniWinCore.cs"] = "UPM 패키지 com.kirurobo.uniwinc의 저수준 래퍼. Library/PackageCache에만 있다(gitignore).",
            ["UniWindowController.cs"] = "같은 패키지의 상위 컴포넌트.",
            ["UniWindowControllerEditor.cs"] = "같은 패키지의 에디터 검증기.",
            ["OnDemandRendering.bindings.cs"] = "Unity 엔진 소스(Runtime/Export/Graphics/). 배포본에 없다.",
        };

        /// <summary>
        /// <b>과거형으로 적힌 파일</b>. "지금 있다"가 아니라 "예전에 있었다"를 서술하므로 거짓이 아니다.
        /// 파일명만으로는 시제를 못 읽으므로 여기에 사유와 함께 적는다.
        /// </summary>
        private static readonly Dictionary<string, string> HistoricalOnly = new Dictionary<string, string>
        {
            ["WindowsFramePacing.cs"] = "Platform/FramePacing.cs 클래스 문서가 '통합 전에는 따로 있었다'로 명시한 과거 파일.",
            ["MacFramePacing.cs"] = "같은 문단의 macOS 짝.",
            ["FocusWatchRenderer.cs"] =
                "2026-09-06 사용자 지시로 발밑 타이머 링이 삭제되면서 <b>파일째</b> 지워졌다. 이 이름을 " +
                "인용하는 주석(FocusWatchDirector / FocusSessionPopover / SceneBootstrapper / " +
                "SuspendedOverlayGate / StickmanPoseAnimator)은 전부 «있었다가 사라졌다»는 과거형이고, " +
                "그 문장이 곧 「되살리지 마라」는 경고라 지우면 안 된다.",
        };

        /// <summary>
        /// ★ <b>이미 깨져 있는 참조</b> — 이 라운드가 발견했고 <b>수정은 원 담당자에게 배정된다</b>
        /// (code-inspection은 프로덕션 <c>.cs</c>를 고치지 않는다).
        ///
        /// <para>키는 <b>깨진 참조 문자열</b>, 값은 사유 + 실제로 무엇을 가리켰어야 하는가다.
        /// 고쳐지면 <see cref="이미_알려진_깨진_참조가_고쳐졌으면_명부에서_지운다"/>가 <b>실패</b>해서
        /// 명부를 지우라고 말한다 — 명부가 조용히 늙지 않게 하는 장치다.</para>
        /// </summary>
        private static readonly Dictionary<string, string> KnownBroken = new Dictionary<string, string>
        {
            ["Tests/EditMode/VisibleTopEdgeSolverTests.cs"] =
                "Platform/VisibleTopEdgeSolver.cs — 실제 파일명은 Tests/EditMode/VisibleTopEdgeOcclusionTests.cs다. " +
                "'그 실측이다'라고 단언하므로, 이 줄을 읽고 실측을 찾으러 간 사람은 빈손으로 돌아온다.",

            ["States/IPlannedDwellSource.cs"] =
                "States/IdleState.cs · States/WalkState.cs 두 곳. IPlannedDwellSource는 파일이 아니라 " +
                "States/IMovementIntentSource.cs 안에 선언된 인터페이스다.",

            ["Tests/*/GlobalTestIsolation.cs"] =
                "Core/CharacterSaveStore.cs — 와일드카드가 어떤 파일에도 맞지 않는다. 실제는 " +
                "Tests/EditMode/GlobalEditModeTestIsolation.cs / Tests/PlayMode/GlobalPlayModeTestIsolation.cs.",
        };

        /// <summary>
        /// ★ <b>확장자 없이 인용된 깨진 참조</b>(2026-09-06 확장이 처음 잡은 것).
        /// <see cref="KnownBroken"/>과 <b>같은 규약</b>이다 — 키는 인용 문자열, 값은 사유 + 실제로
        /// 무엇을 가리켰어야 하는가. 수정은 <b>원 담당자에게 배정된다</b>(이 감사는 프로덕션 <c>.cs</c>를
        /// 고치지 않는다). 고쳐지면 <see cref="이미_알려진_깨진_확장자없는_참조가_고쳐졌으면_명부에서_지운다"/>가
        /// <b>실패</b>해서 명부를 지우라고 말한다.
        ///
        /// <para>★ <c>IncomeTimeSourceAuditTests</c>·<c>DailyLimitClampAuditTests</c>는 <b>여기 없다</b> —
        /// 이 확장과 같은 라운드에 <b>실제로 만들었기</b> 때문이다. 둘이 여기 있으면
        /// 「고쳐졌으면 지운다」가 즉시 빨개진다.</para>
        ///
        /// <para>★★ <b>2026-09-06 후속 라운드(test-engineer)에서 8건 중 6건이 내려갔다.</b>
        /// <c>WindowsGameProcessProbeTests</c>(원칙 3 직결이라 최우선) ·
        /// <c>SystemAudioActivityProbeContractTests</c> · <c>MacSystemAudioSelectorTests</c> ·
        /// <c>CharacterStatCardLayoutTests</c> · <c>DancePoseFallbackParityTests</c> ·
        /// <c>Tests/PlayMode/InfoGearContrastTests</c> — 전부 <b>그 인용이 약속한 불변식을 실제로
        /// 재는 테스트</b>를 신설했다. 여섯 건 모두 <b>코드는 약속을 지키고 있었다</b>(값이 틀린 것은
        /// 하나도 없었고, 없던 것은 «잠근다»던 기계뿐이었다).</para>
        ///
        /// <para>★ 남은 2건은 <b>성격이 다르다</b>: 하나는 프로덕션 <c>.cs</c> 주석의 <b>경로 한 토큰</b>을
        /// 고쳐야 하고(이 라운드는 프로덕션 편집 권한이 없었다), 하나는 <b>PlayMode 리그</b>가 필요해
        /// 별도 배정 대상이다. 사유는 각 항목에 적었다.</para>
        /// </summary>
        private static readonly Dictionary<string, string> KnownBrokenTypeRefs =
            new Dictionary<string, string>
            {
                ["Tests/EditMode/PortraitBodyStrokeParityTests"] =
                    "Core/StickmanStrokeWidths.cs:27 · Interaction/CharacterPortraitStage.cs:111 두 곳. " +
                    "클래스는 실재하지만 EditMode가 아니라 Tests/PlayMode/PortraitBodyStrokeParityTests.cs다 — " +
                    "«파일명은 맞지만 위치가 다르다»는 이 감사가 .cs 쪽에서 이미 위반으로 세는 형태다.\n" +
                    "★ 2026-09-06: 고칠 것은 <b>테스트가 아니라 주석 한 토큰</b>이다" +
                    "(«Tests/EditMode/» -> «Tests/PlayMode/», 두 파일 각 1곳). " +
                    "test-engineer 라운드는 프로덕션 .cs 편집이 금지돼 있어 <b>패치 내용만 보고</b>했다. " +
                    "그 두 줄을 고칠 수 있는 라운드가 고치고 이 항목을 지운다.",

                ["Tests/PlayMode/DockLandingSilhouetteTests"] =
                    "Core/StickConfig.cs:2019. 그런 파일도 그런 클래스도 없다(EditMode/PlayMode 어디에도). " +
                    "«그 값을 이 테스트가 잠근다»고 읽히므로 잠금이 있다고 믿게 만든다.\n" +
                    "★ 2026-09-06: <b>다음 라운드 배정 필요.</b> 인용이 위치를 Tests/PlayMode/로 단언하는데, " +
                    "PlayMode 어셈블리는 includePlatforms가 비어 있어 <c>UnityEditor.AssetDatabase</c>를 " +
                    "쓸 수 없다 — 즉 «배포 설정값에서 다시 계산»하려면 씬/프리팹을 띄워 " +
                    "실행 중인 StickConfig를 받아 오는 리그가 필요하다" +
                    "(짝인 Tests/PlayMode/CharacterScaleInvarianceTests.cs가 그 형태다). " +
                    "검산 자체는 세 줄이다: 교차 배율 = DockGeometry.ReferenceDockDropWorldUnits / " +
                    "(문턱[H] x StickConfig.BaselineCharacterTotalHeight), 문턱은 각각 " +
                    "landingSoftAbsorbThresholdHeights / landingReactionThresholdHeights / " +
                    "LandingSitDownFallHeights.",
            };

        /// <summary>
        /// ★ <b>줄 번호 참조</b> — <c>Foo.cs:123</c>. 줄 번호는 위에 한 줄만 끼어들어도 조용히 썩는다.
        /// 새로 만들지 마라(절 이름이나 <c>nameof</c>로 걸어라). 이미 있는 것은 여기 <b>앵커 문구</b>와
        /// 함께 적어 두고, 그 줄이 여전히 같은 문구인지 매 실행 확인한다.
        ///
        /// <para>앵커는 프로덕션 <b>상수의 복사</b>가 아니다(CLAUDE.md의 하드코딩 금지와 무관하다) —
        /// "이 줄이 그 줄인가"를 재기 위한 <b>참조 무결성 표식</b>이며, 값이 아니라 위치를 잰다.</para>
        /// </summary>
        private static readonly (string RefText, string TargetRelative, int Line, string Anchor)[] KnownLineRefs =
        {
            ("InfoGearIconWidget.cs:51", "_Project/Scripts/Interaction/InfoGearIconWidget.cs", 51,
                "hitTestType=Raycast"),
            // ★ 2026-09-06 — <c>CharacterFxRenderer.cs:304</c> 항목을 <b>지웠다</b>. 그 줄 번호는
            //   FX 도형 라운드가 위쪽에 줄을 넣자마자 썩었고(이 검사가 실제로 잡았다), 처방대로
            //   가리키던 주석(CharacterPortraitStage.DrawFxPreview)을 <b>절 이름</b>으로 바꿨다.
            //   즉 명부에서 빠진 이유는 "면제"가 아니라 <b>줄 번호 참조 자체가 사라졌기 때문</b>이다.
        };

        // ==================================================================
        // 스캐너 (순수 함수 — 아래 양성 대조가 직접 먹인다)
        // ==================================================================

        /// <summary>
        /// ★ <b>끝을 <c>\b</c>로 막지 않는다.</b> .NET의 <c>\b</c>는 유니코드 단어 경계라
        /// 한글도 단어 문자로 센다 — <c>StickConfig.cs가</c>처럼 조사가 붙은 참조에서
        /// <c>s</c>와 <c>가</c> 사이에 경계가 서지 않아 <b>매치 자체가 사라진다</b>.
        /// 오프라인 예행에서 실측했다: <c>\b</c>판은 534건 중 <b>245건(46%)을 놓쳤고</b>,
        /// 놓친 것 안에 이 라운드가 손으로 찾은 진짜 위반이 들어 있었다
        /// (<c>...VisibleTopEdgeSolverTests.cs<b>가</b> 그 실측이다</c>).
        /// 그래서 "다음 글자가 ASCII 낱말 문자가 아니다"로 명시한다 — <c>.csproj</c>는 그대로 걸러진다.
        /// </summary>
        private static readonly Regex CsRefRegex =
            new Regex(@"[A-Za-z0-9_./*-]*[A-Za-z0-9_]\.cs(?![A-Za-z0-9_])", RegexOptions.Compiled);

        private static readonly Regex RepoPathRefRegex =
            new Regex(@"(?:docs|design|Tools)/[A-Za-z0-9_./-]*[A-Za-z0-9_]\.(?:md|py|sh|json|txt)(?![A-Za-z0-9_])",
                RegexOptions.Compiled);

        private static readonly Regex LineRefRegex =
            new Regex(@"[A-Za-z0-9_]+\.cs:[0-9]+", RegexOptions.Compiled);

        /// <summary>
        /// ★ <b>경로형 · 확장자 없음</b> — <c>Tests/EditMode/FooTests</c>.
        /// <see cref="CsRefRegex"/>가 구조적으로 못 보던 절반의 앞쪽이다.
        ///
        /// <para>끝을 <c>(?![A-Za-z0-9_/]|\.cs)</c>로 막는다: <c>/</c>를 뺀 것은
        /// <c>Tests/EditMode/Golden/Foo</c> 같은 더 깊은 경로에서 <b>앞 두 칸만</b> 잘라 오지 않기
        /// 위해서고, <c>.cs</c>를 뺀 것은 확장자가 붙은 것은 <see cref="CsRefRegex"/>의 몫이라
        /// <b>같은 참조를 두 번 세지 않기</b> 위해서다. <b><c>\b</c>는 쓰지 않는다</b> —
        /// .NET의 <c>\b</c>가 한글을 낱말 문자로 세는 함정은 이 파일 위쪽에 실측으로 적혀 있고,
        /// 이 저장소의 인용은 대부분 <c>…AuditTests<b>가</b> 확인한다</c> 꼴이다.</para>
        /// </summary>
        private static readonly Regex TestPathRefRegex =
            new Regex(@"Tests/(?:EditMode|PlayMode)/[A-Za-z0-9_]+(?![A-Za-z0-9_/]|\.cs)",
                RegexOptions.Compiled);

        /// <summary>
        /// ★ <b>낱말형 · 확장자 없음</b> — <c>FooAuditTests</c>. 절반의 뒤쪽이다.
        ///
        /// <para>앞을 <c>(?&lt;![A-Za-z0-9_./])</c>로 막아 <c>Tests/EditMode/FooTests</c>(경로형)와
        /// <c>Foo.cs</c>·<c>Bar.FooTests</c>(이미 다른 규칙이 보는 것)를 <b>겹쳐 세지 않는다</b>.
        /// 뒤는 <c>.cs</c>도 막는다 — <c>FooTests.cs</c>는 <see cref="CsRefRegex"/>의 몫이다.</para>
        /// </summary>
        private static readonly Regex BareTestClassRefRegex =
            new Regex(@"(?<![A-Za-z0-9_./])[A-Z][A-Za-z0-9_]*Tests(?![A-Za-z0-9_]|\.cs)",
                RegexOptions.Compiled);

        /// <summary>
        /// ★ 낱말형에서 <b>빼는</b> 꼬리. 이 저장소는 테스트 전용 프로덕션 진입점을
        /// <c>ResetForTests</c>·<c>FeedClickForTests</c>·<c>SetPlatformForTests</c>처럼 짓는다 —
        /// <b>메서드 이름이지 테스트 클래스가 아니다.</b> 안 빼면 9건이 통째로 거짓 위반이 되고,
        /// 거짓 위반이 몇 건만 있어도 이 감사는 꺼진다(실측 후 확정).
        /// </summary>
        private const string ForTestsSuffix = "ForTests";

        /// <summary><c>&lt;Assets&gt;/_Project/Scripts</c>. 경로형 참조가 <b>디렉터리</b>를 가리키는지
        /// 확인할 때만 쓴다(예: <c>Tests/EditMode/Golden</c>은 골든 <c>.txt</c>만 든 실재 폴더라
        /// 그 안에 <c>.cs</c>가 하나도 없다 — 색인으로는 디렉터리임을 알 수 없다).
        /// 트리가 옮겨지면 <see cref="양성대조_확장자없는_참조_스캐너가_능력을_증명한다"/>가 먼저 빨개진다.</summary>
        private static string ScriptsRoot => Path.Combine(AssetsRoot, "_Project", "Scripts");

        /// <summary>그 줄에서 <b>주석 부분만</b> 잘라낸다. 주석이 아니면 <c>null</c>.
        /// <c>https://</c> 같은 URL이 <c>//</c>로 오인되지 않게 <c>://</c> 앞은 주석 시작으로 보지 않는다.</summary>
        internal static string CommentPart(string line)
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("///", StringComparison.Ordinal)
                || trimmed.StartsWith("//", StringComparison.Ordinal)
                || trimmed.StartsWith("*", StringComparison.Ordinal)
                || trimmed.StartsWith("/*", StringComparison.Ordinal))
            {
                return trimmed;
            }

            int at = 0;
            while ((at = line.IndexOf("//", at, StringComparison.Ordinal)) >= 0)
            {
                if (at > 0 && line[at - 1] == ':') { at += 2; continue; }   // "https://"
                return line.Substring(at);
            }
            return null;
        }

        /// <summary>주석 한 줄에서 <c>*.cs</c> 참조를 전부 뽑는다.</summary>
        internal static List<string> ExtractCsRefs(string commentLine)
        {
            var found = new List<string>();
            foreach (Match m in CsRefRegex.Matches(commentLine))
            {
                // "A.cs(설명)/B.cs" 처럼 이어 적힌 자리에서 앞 조각의 닫는 괄호 때문에 '/'로 시작하는
                // 조각이 나온다. 그건 경로가 아니라 잘린 자국이므로 떼어 낸다.
                found.Add(m.Value.TrimStart('/'));
            }
            return found;
        }

        private static IEnumerable<string> ProductionSources()
        {
            foreach (string path in Directory.GetFiles(AssetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = path.Replace('\\', '/');
                if (normalized.Contains(TestsPathFragment)) continue;
                yield return normalized;
            }
        }

        /// <summary>Assets 아래 모든 <c>.cs</c>를 <b>파일명 -> 경로들</b>로 색인한다(테스트 포함 —
        /// 프로덕션 주석이 테스트 파일을 지목하는 것은 정상이다).</summary>
        private static Dictionary<string, List<string>> IndexAllSources()
        {
            var index = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(AssetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = path.Replace('\\', '/');
                string name = Path.GetFileName(normalized);
                if (!index.TryGetValue(name, out List<string> list)) index[name] = list = new List<string>();
                list.Add(normalized);
            }
            return index;
        }

        internal static bool Resolves(string reference, Dictionary<string, List<string>> index)
        {
            reference = reference.TrimStart('/');
            string name = reference.Substring(reference.LastIndexOf('/') + 1);
            if (!index.TryGetValue(name, out List<string> candidates)) return false;
            if (reference.IndexOf('/') < 0) return true;

            foreach (string candidate in candidates)
            {
                if (candidate.EndsWith("/" + reference, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        // ==================================================================
        // ★ 확장자 없는 참조 — 추출 · 색인 · 해석 (2026-09-06)
        // ==================================================================

        /// <summary>주석 한 줄에서 <b>경로형</b>(<c>Tests/EditMode/FooTests</c>) 참조를 전부 뽑는다.</summary>
        internal static List<string> ExtractTestPathRefs(string commentLine)
        {
            var found = new List<string>();
            foreach (Match m in TestPathRefRegex.Matches(commentLine)) found.Add(m.Value);
            return found;
        }

        /// <summary>주석 한 줄에서 <b>낱말형</b>(<c>FooAuditTests</c>) 참조를 전부 뽑는다.
        /// <c>…ForTests</c>는 뺀다(<see cref="ForTestsSuffix"/>).</summary>
        internal static List<string> ExtractBareTestClassRefs(string commentLine)
        {
            var found = new List<string>();
            foreach (Match m in BareTestClassRefRegex.Matches(commentLine))
            {
                if (m.Value.EndsWith(ForTestsSuffix, StringComparison.Ordinal)) continue;
                found.Add(m.Value);
            }
            return found;
        }

        /// <summary>
        /// <b>타입 선언 이름 -> 그 선언이 있는 경로들</b>. 파일명 색인만으로는
        /// <b>파일 이름과 클래스 이름이 다른 경우</b>를 못 본다 — 이 저장소에 실제로 있다
        /// (<c>Tests/PlayMode/PopoverAndHoverPanelOpacityTests.cs</c> 안의 클래스는
        /// <c>PopoverPanelOpacityTests</c>다). 둘 중 하나만 보면 거짓 위반이 뜬다.
        ///
        /// <para>★ <b>주석 줄은 뺀다.</b> 안 빼면 <c>// class FooTests</c>라고 적어 둔 <b>계획</b>이
        /// 선언으로 세어져, 그 이름을 인용한 거짓 참조가 <b>스스로를 해석해</b> 조용히 초록이 된다.
        /// 이 감사가 잡으려는 병이 정확히 그것이다.</para>
        ///
        /// <para>★★ <b>문자열 리터럴 안도 뺀다</b> — 같은 구멍의 두 번째 입구다. 감사 테스트들은
        /// 가짜 소스를 <c>"private sealed class FakeSave\n"</c> 처럼 <b>문자열로</b> 조립하는데,
        /// 그걸 선언으로 세면 <c>FakeSave</c>·<c>RealTests</c> 같은 유령 타입이 색인에 들어온다.
        /// 유령이 하나라도 진짜 인용과 이름이 겹치면 그 거짓 참조가 <b>스스로 해석되어</b> 조용히
        /// 초록이 된다. 판정은 <b>그 줄에서 매치 앞의 따옴표 개수가 홀수인가</b>다 —
        /// <c>[Tooltip("x")] public class Foo</c>처럼 따옴표가 짝수로 닫힌 진짜 선언은 살아남는다.</para>
        /// </summary>
        private static Dictionary<string, List<string>> IndexDeclaredTypes()
        {
            var declRegex = new Regex(@"\b(?:class|struct|interface|enum)\s+([A-Za-z0-9_]+)");
            var index = new Dictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (string path in Directory.GetFiles(AssetsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string normalized = path.Replace('\\', '/');
                foreach (string line in File.ReadAllLines(path))
                {
                    string code = CodePart(line);
                    foreach (Match m in declRegex.Matches(code))
                    {
                        if (InsideStringLiteral(code, m.Index)) continue;
                        string name = m.Groups[1].Value;
                        if (!index.TryGetValue(name, out List<string> list)) index[name] = list = new List<string>();
                        list.Add(normalized);
                    }
                }
            }
            return index;
        }

        /// <summary>그 줄에서 <paramref name="index"/> 앞의 <b>닫히지 않은</b> 따옴표가 있는가
        /// (= 그 위치가 문자열 리터럴 안인가). <c>\"</c> 이스케이프는 세지 않는다.</summary>
        internal static bool InsideStringLiteral(string line, int index)
        {
            bool open = false;
            for (int i = 0; i < index && i < line.Length; i++)
            {
                if (line[i] == '\\') { i++; continue; }
                if (line[i] == '"') open = !open;
            }
            return open;
        }

        /// <summary>그 줄에서 <b>주석을 뺀</b> 부분. <see cref="CommentPart"/>의 짝이다.</summary>
        internal static string CodePart(string line)
        {
            string comment = CommentPart(line);
            if (comment == null) return line;
            int at = line.IndexOf(comment, StringComparison.Ordinal);
            return at > 0 ? line.Substring(0, at) : string.Empty;
        }

        /// <summary>
        /// <b>경로형</b> 참조를 푼다. 셋 중 하나면 해석된다:
        /// ① 실재하는 <b>디렉터리</b>(<c>Tests/EditMode/Golden</c>) ②그 경로의 <c>.cs</c> 파일
        /// ③ <b>그 디렉터리 안</b>에 그 이름의 타입 선언.
        /// <para>★ <b>디렉터리를 요구한다</b> — 파일명만 맞고 위치가 다르면 <b>위반</b>이다.
        /// 이 파일이 <c>.cs</c> 참조에 대해 이미 그렇게 판정하고 있고
        /// (<c>Platform/ItemCatalog.cs</c> 음성 대조), 실제로 그 규칙이
        /// <c>Tests/EditMode/PortraitBodyStrokeParityTests</c>(진짜는 <c>PlayMode</c>)를 잡았다.</para>
        /// </summary>
        internal static bool ResolvesTestPathRef(
            string reference, Dictionary<string, List<string>> fileIndex,
            Dictionary<string, List<string>> typeIndex, string scriptsRoot)
        {
            if (Directory.Exists(Path.Combine(scriptsRoot,
                    reference.Replace('/', Path.DirectorySeparatorChar))))
            {
                return true;
            }

            int slash = reference.LastIndexOf('/');
            string name = reference.Substring(slash + 1);
            string directory = reference.Substring(0, slash);

            if (fileIndex.TryGetValue(name + ".cs", out List<string> files))
            {
                foreach (string candidate in files)
                {
                    if (candidate.EndsWith("/" + reference + ".cs", StringComparison.Ordinal)) return true;
                }
            }

            if (typeIndex.TryGetValue(name, out List<string> declared))
            {
                foreach (string candidate in declared)
                {
                    if (candidate.IndexOf("/" + directory + "/", StringComparison.Ordinal) >= 0) return true;
                }
            }
            return false;
        }

        /// <summary>
        /// <b>낱말형</b> 참조를 푼다. 타입 선언이 있거나 <b>같은 이름의 파일</b>이 있으면 해석된다.
        /// <para>★ 파일명도 인정하는 이유: 낱말형 인용에는 위치 주장이 없으므로 <b>사람이 찾는 방식</b>이
        /// 기준이다. <c>PopoverAndHoverPanelOpacityTests</c>는 클래스로는 없지만 <b>그 이름의 파일</b>이
        /// 있어 검색하면 즉시 나온다 — 그걸 위반으로 세면 «고칠 것이 없는 빨강»이 되고, 그런 감사는 꺼진다.</para>
        /// </summary>
        internal static bool ResolvesBareTestClassRef(
            string reference, Dictionary<string, List<string>> fileIndex,
            Dictionary<string, List<string>> typeIndex)
            => typeIndex.ContainsKey(reference) || fileIndex.ContainsKey(reference + ".cs");

        private sealed class Violation
        {
            public string File;
            public int Line;
            public string Reference;
            public string Text;
            public override string ToString() =>
                $"{File}:{Line}  ->  '{Reference}'\n      {Text}";
        }

        private static List<Violation> CollectCsRefViolations(out int totalRefs)
        {
            Dictionary<string, List<string>> index = IndexAllSources();
            var violations = new List<Violation>();
            totalRefs = 0;

            foreach (string path in ProductionSources())
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string comment = CommentPart(lines[i]);
                    if (comment == null) continue;

                    foreach (string reference in ExtractCsRefs(comment))
                    {
                        totalRefs++;
                        string name = reference.Substring(reference.LastIndexOf('/') + 1);
                        if (ExternalOwned.ContainsKey(name)) continue;
                        if (HistoricalOnly.ContainsKey(name)) continue;
                        if (Resolves(reference, index)) continue;

                        violations.Add(new Violation
                        {
                            File = path.Substring(RepoRoot.Length + 1),
                            Line = i + 1,
                            Reference = reference,
                            Text = lines[i].Trim(),
                        });
                    }
                }
            }
            return violations;
        }

        // ==================================================================
        // 1) 소스 파일 참조
        // ==================================================================

        [Test]
        public void 주석이_지목한_소스_파일이_새로_사라지지_않는다()
        {
            List<Violation> violations = CollectCsRefViolations(out int totalRefs);

            // ★ 스캐너가 눈이 멀면 "0건"이 곧 초록불이 된다 — 먼저 그게 아님을 보인다.
            Assert.Greater(totalRefs, 300,
                $"주석에서 뽑은 .cs 참조가 {totalRefs}건뿐이다 — 스캐너가 트리를 잘못 보고 있다. " +
                "이 저장소는 프로덕션 주석에만 500건 이상을 갖고 있었다(2026-09-02 실측 534건).");

            var unexpected = new List<Violation>();
            foreach (Violation v in violations)
            {
                if (!KnownBroken.ContainsKey(v.Reference)) unexpected.Add(v);
            }

            Assert.IsEmpty(unexpected,
                "주석이 지목한 소스 파일이 존재하지 않는다. 파일을 쪼개거나 이름을 바꿨다면 " +
                "그 파일을 가리키던 주석도 함께 고쳐라 — 고칠 수 없다면 " +
                $"{nameof(ExternalOwned)}/{nameof(HistoricalOnly)}/{nameof(KnownBroken)}에 " +
                "사유와 함께 적어라.\n  " + string.Join("\n  ", unexpected));
        }

        [Test]
        public void 이미_알려진_깨진_참조가_고쳐졌으면_명부에서_지운다()
        {
            List<Violation> violations = CollectCsRefViolations(out _);
            var stillBroken = new HashSet<string>(StringComparer.Ordinal);
            foreach (Violation v in violations) stillBroken.Add(v.Reference);

            var fixedAlready = new List<string>();
            foreach (KeyValuePair<string, string> entry in KnownBroken)
            {
                Assert.IsNotEmpty(entry.Value, $"{entry.Key}의 면제 사유가 비어 있다.");
                if (!stillBroken.Contains(entry.Key)) fixedAlready.Add(entry.Key);
            }

            Assert.IsEmpty(fixedAlready,
                $"아래 참조는 이미 고쳐졌다 — {nameof(KnownBroken)}에서 지워라. " +
                "남겨 두면 다음에 같은 자리가 다시 깨져도 이 감사가 침묵한다.\n  "
                + string.Join("\n  ", fixedAlready));

            if (KnownBroken.Count > 0)
            {
                var lines = new List<string>();
                foreach (KeyValuePair<string, string> e in KnownBroken) lines.Add($"{e.Key} — {e.Value}");
                Assert.Ignore(
                    $"아직 안 고친 거짓 참조 {KnownBroken.Count}건(수정은 원 담당자에게 배정). " +
                    "러너에 '건너뜀'으로 계속 보이게 남긴다 — 잊히지 않게.\n  "
                    + string.Join("\n  ", lines));
            }
        }

        // ==================================================================
        // 1-b) ★ 확장자 없는 참조 (2026-09-06 확장)
        // ==================================================================

        private static List<Violation> CollectTypeRefViolations(out int pathRefs, out int bareRefs)
        {
            Dictionary<string, List<string>> fileIndex = IndexAllSources();
            Dictionary<string, List<string>> typeIndex = IndexDeclaredTypes();
            string scriptsRoot = ScriptsRoot;

            var violations = new List<Violation>();
            pathRefs = 0;
            bareRefs = 0;

            foreach (string path in ProductionSources())
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string comment = CommentPart(lines[i]);
                    if (comment == null) continue;

                    foreach (string reference in ExtractTestPathRefs(comment))
                    {
                        pathRefs++;
                        if (ResolvesTestPathRef(reference, fileIndex, typeIndex, scriptsRoot)) continue;
                        violations.Add(new Violation
                        {
                            File = path.Substring(RepoRoot.Length + 1),
                            Line = i + 1,
                            Reference = reference,
                            Text = lines[i].Trim(),
                        });
                    }

                    foreach (string reference in ExtractBareTestClassRefs(comment))
                    {
                        bareRefs++;
                        if (ResolvesBareTestClassRef(reference, fileIndex, typeIndex)) continue;
                        violations.Add(new Violation
                        {
                            File = path.Substring(RepoRoot.Length + 1),
                            Line = i + 1,
                            Reference = reference,
                            Text = lines[i].Trim(),
                        });
                    }
                }
            }
            return violations;
        }

        [Test]
        public void 주석이_확장자_없이_지목한_테스트가_새로_사라지지_않는다()
        {
            List<Violation> violations = CollectTypeRefViolations(out int pathRefs, out int bareRefs);

            // ★ 여기서도 "0건"이 초록불이 되는 것을 먼저 막는다(설계 규칙 2).
            Assert.Greater(pathRefs, 70,
                $"경로형(Tests/EditMode|PlayMode/...) 참조가 {pathRefs}건뿐이다 — 스캐너가 눈이 멀었다. " +
                "2026-09-06 실측 110건.");
            Assert.Greater(bareRefs, 100,
                $"낱말형(*Tests) 참조가 {bareRefs}건뿐이다 — 스캐너가 눈이 멀었다. 2026-09-06 실측 142건.");

            var unexpected = new List<Violation>();
            foreach (Violation v in violations)
            {
                if (!KnownBrokenTypeRefs.ContainsKey(v.Reference)) unexpected.Add(v);
            }

            Assert.IsEmpty(unexpected,
                "주석이 <b>확장자 없이</b> 지목한 테스트가 존재하지 않는다. 이 형태는 2026-09-06까지 " +
                "이 감사가 구조적으로 못 보던 자리다 — 그래서 «…가 이 규칙을 잠근다»는 문장이 " +
                "잠그는 것 없이 여덟 건 살아 있었다. 테스트를 실제로 만들거나, 인용을 지우거나, " +
                $"{nameof(KnownBrokenTypeRefs)}에 사유와 함께 적어라(계획이면 «예정»이라고 쓰면 " +
                "현재형 단언이 아니게 된다).\n  " + string.Join("\n  ", unexpected));
        }

        [Test]
        public void 이미_알려진_깨진_확장자없는_참조가_고쳐졌으면_명부에서_지운다()
        {
            List<Violation> violations = CollectTypeRefViolations(out _, out _);
            var stillBroken = new HashSet<string>(StringComparer.Ordinal);
            foreach (Violation v in violations) stillBroken.Add(v.Reference);

            var fixedAlready = new List<string>();
            foreach (KeyValuePair<string, string> entry in KnownBrokenTypeRefs)
            {
                Assert.IsNotEmpty(entry.Value, $"{entry.Key}의 면제 사유가 비어 있다.");
                if (!stillBroken.Contains(entry.Key)) fixedAlready.Add(entry.Key);
            }

            Assert.IsEmpty(fixedAlready,
                $"아래 참조는 이미 고쳐졌다 — {nameof(KnownBrokenTypeRefs)}에서 지워라. " +
                "남겨 두면 다음에 같은 자리가 다시 깨져도 이 감사가 침묵한다.\n  "
                + string.Join("\n  ", fixedAlready));

            if (KnownBrokenTypeRefs.Count > 0)
            {
                var lines = new List<string>();
                foreach (KeyValuePair<string, string> e in KnownBrokenTypeRefs) lines.Add($"{e.Key} — {e.Value}");
                Assert.Ignore(
                    $"확장자 없이 인용된 거짓 참조 {KnownBrokenTypeRefs.Count}건(수정은 원 담당자에게 배정). " +
                    "러너에 '건너뜀'으로 계속 보이게 남긴다 — 잊히지 않게.\n  "
                    + string.Join("\n  ", lines));
            }
        }

        // ==================================================================
        // 2) 문서 · 도구 경로 참조
        // ==================================================================

        [Test]
        public void 주석이_지목한_문서와_도구_경로가_실제로_있다()
        {
            var missing = new List<string>();
            int total = 0;

            foreach (string path in ProductionSources())
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string comment = CommentPart(lines[i]);
                    if (comment == null) continue;

                    foreach (Match m in RepoPathRefRegex.Matches(comment))
                    {
                        total++;
                        if (File.Exists(Path.Combine(RepoRoot, m.Value))) continue;
                        missing.Add($"{path.Substring(RepoRoot.Length + 1)}:{i + 1}  ->  '{m.Value}'");
                    }
                }
            }

            Assert.Greater(total, 150,
                $"문서·도구 경로 참조가 {total}건뿐이다 — 스캐너 고장(2026-09-02 실측 258건).");
            Assert.IsEmpty(missing,
                "주석이 지목한 문서/도구 파일이 없다. 문서를 옮기거나 이름을 바꿨다면 그것을 " +
                "가리키던 주석도 함께 고쳐라.\n  " + string.Join("\n  ", missing));
        }

        // ==================================================================
        // 3) 줄 번호 참조 — 썩는다
        // ==================================================================

        [Test]
        public void 줄번호_참조를_새로_만들지_않는다()
        {
            var known = new HashSet<string>(StringComparer.Ordinal);
            foreach ((string refText, _, _, _) in KnownLineRefs) known.Add(refText);
            foreach (string external in ExternalOwned.Keys) known.Add(external);   // 패키지 줄 참조는 nameof로 못 건다

            var unexpected = new List<string>();
            foreach (string path in ProductionSources())
            {
                string[] lines = File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    string comment = CommentPart(lines[i]);
                    if (comment == null) continue;

                    foreach (Match m in LineRefRegex.Matches(comment))
                    {
                        string fileName = m.Value.Substring(0, m.Value.IndexOf(".cs:", StringComparison.Ordinal) + 3);
                        if (known.Contains(m.Value) || known.Contains(fileName)) continue;
                        unexpected.Add($"{path.Substring(RepoRoot.Length + 1)}:{i + 1}  ->  '{m.Value}'\n      {lines[i].Trim()}");
                    }
                }
            }

            Assert.IsEmpty(unexpected,
                "주석에 새 줄 번호 참조가 생겼다. 줄 번호는 위에 한 줄만 끼어들어도 조용히 썩고, " +
                "아무 테스트도 그것을 잡지 않는다. 대신 <b>절 이름</b>(예: \"클릭 판정\" 절)이나 " +
                $"<c>nameof</c>로 걸어라. 그래도 필요하면 {nameof(KnownLineRefs)}에 앵커 문구와 함께 적어라.\n  "
                + string.Join("\n  ", unexpected));
        }

        [Test]
        public void 줄번호_참조는_썩지_않았는지_앵커로_확인한다()
        {
            Assert.IsNotEmpty(KnownLineRefs,
                "명부가 비었다 — 아래 foreach가 아무것도 재지 않고 초록불이 된다(사고 #5). " +
                "정말 줄 번호 참조가 하나도 없다면 이 단언을 지우지 말고 '0건이 기대값'이라고 고쳐 적어라.");

            var rotten = new List<string>();
            foreach ((string refText, string relative, int line, string anchor) in KnownLineRefs)
            {
                string target = Path.Combine(AssetsRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                Assert.IsTrue(File.Exists(target), $"{refText}가 가리키는 {relative}가 없다.");

                string[] lines = File.ReadAllLines(target);
                if (line < 1 || line > lines.Length)
                {
                    rotten.Add($"{refText}: 대상 파일이 {lines.Length}줄뿐이다.");
                    continue;
                }
                if (lines[line - 1].IndexOf(anchor, StringComparison.Ordinal) < 0)
                {
                    rotten.Add($"{refText}: {line}번 줄이 더 이상 '{anchor}'를 담고 있지 않다.\n" +
                               $"      지금 그 줄: {lines[line - 1].Trim()}");
                }
            }

            Assert.IsEmpty(rotten,
                "줄 번호 참조가 썩었다. 그 사이에 줄이 끼어들었다는 뜻이다 — 주석을 읽는 사람은 " +
                "엉뚱한 줄을 보게 된다. 줄 번호를 고치지 말고 <b>절 이름/nameof</b>로 바꿔라(다시 썩는다).\n  "
                + string.Join("\n  ", rotten));
        }

        // ==================================================================
        // 4) 양성 대조 — "0건"이 진짜 0인지 먼저 증명한다
        // ==================================================================

        [Test]
        public void 양성대조_스캐너가_깨진_참조를_실제로_잡는다()
        {
            Dictionary<string, List<string>> index = IndexAllSources();

            Assert.IsTrue(Resolves("Core/ItemCatalog.cs", index), "있는 파일을 못 찾으면 이 감사 전체가 무의미하다.");
            Assert.IsTrue(Resolves("ItemCatalog.cs", index), "디렉터리 없는 참조도 풀려야 한다.");
            Assert.IsTrue(Resolves("Tests/EditMode/EquipmentMigrationTests.cs", index),
                "프로덕션 주석이 테스트 파일을 지목하는 것은 정상이다 — 색인이 테스트를 빠뜨리면 거짓 위반이 쏟아진다.");

            Assert.IsFalse(Resolves("Core/NoSuchFileAtAll.cs", index), "없는 파일을 있다고 하면 탐지력이 0이다.");
            Assert.IsFalse(Resolves("Tests/EditMode/VisibleTopEdgeSolverTests.cs", index),
                "실제로 깨져 있는 참조다 — 이게 통과로 나오면 이 감사는 아무것도 안 잡는다.");
            Assert.IsFalse(Resolves("States/IPlannedDwellSource.cs", index),
                "IPlannedDwellSource는 파일이 아니라 IMovementIntentSource.cs 안의 인터페이스다.");
            Assert.IsFalse(Resolves("Tests/*/GlobalTestIsolation.cs", index), "와일드카드는 어떤 파일에도 맞지 않는다.");

            // ★ 경로가 틀린 경우 — 파일명은 맞지만 위치가 다르면 잡아야 한다.
            Assert.IsFalse(Resolves("Platform/ItemCatalog.cs", index),
                "파일명만 보고 통과시키면 파일을 옮겨도 감사가 침묵한다.");
        }

        [Test]
        public void 양성대조_주석_추출이_코드와_URL을_구분한다()
        {
            Assert.IsNull(CommentPart("            int a = 1;"), "코드 줄을 주석으로 보면 소음이 쏟아진다.");
            Assert.IsNotNull(CommentPart("        /// Core/ItemCatalog.cs 참고"));
            Assert.IsNotNull(CommentPart("        // Core/ItemCatalog.cs 참고"));
            Assert.IsNotNull(CommentPart("            int a = 1;   // Core/ItemCatalog.cs 참고"));
            Assert.IsNull(CommentPart("            var url = \"https://example.com/a.cs\";"),
                "URL의 //를 주석 시작으로 보면 문자열 안의 경로까지 검사 대상이 된다.");

            List<string> refs = ExtractCsRefs("/// Core/A.cs 와 Interaction/B.cs, 그리고 C.cs");
            Assert.AreEqual(3, refs.Count, "한 줄에 여러 참조가 있으면 전부 잡아야 한다.");
            CollectionAssert.AreEqual(new[] { "Core/A.cs", "Interaction/B.cs", "C.cs" }, refs);

            Assert.IsEmpty(ExtractCsRefs("/// 여기에는 소스 참조가 없다"),
                "아무 줄에서나 참조를 만들어 내면 거짓 위반이 쏟아진다.");

            // ★★ 이 저장소의 사고 #4 그 자체 — "탐지력이 애초에 0"인 검사.
            //    이 파일의 첫 판은 정규식 끝을 \b로 막았고, .NET의 \b는 한글을 낱말 문자로 세기 때문에
            //    조사가 붙은 참조를 통째로 놓쳤다(534건 중 245건). 이 단언이 그 판을 되살리지 못하게 막는다.
            CollectionAssert.AreEqual(new[] { "Core/StickConfig.cs" },
                ExtractCsRefs("/// 값의 원본은 Core/StickConfig.cs가 갖고 있다"),
                "조사(가/를/이/의…)가 붙은 참조를 놓치면 이 감사의 '0건'은 전부 무효다.");
            CollectionAssert.AreEqual(new[] { "Core/StickConfig.cs" },
                ExtractCsRefs("/// Core/StickConfig.cs를 보라"));

            Assert.IsEmpty(ExtractCsRefs("/// StickMate.csproj 는 소스가 아니다"),
                ".csproj까지 소스 참조로 세면 소음이 된다.");

            CollectionAssert.AreEqual(new[] { "States/DragThrowState.cs", "RodeoCursorState.cs" },
                ExtractCsRefs("/// States/DragThrowState.cs(던진 속도)/RodeoCursorState.cs(흔들기)도"),
                "앞 조각의 닫는 괄호가 남긴 '/'를 떼지 않으면 멀쩡한 파일이 거짓 위반으로 뜬다.");
        }

        // ==================================================================
        // 5) ★ 확장자 없는 참조의 양성/음성 대조 (2026-09-06)
        //    «넓혔다»와 «아무거나 잡는다»는 다른 것이고, 둘을 구분하는 것은 대조뿐이다.
        // ==================================================================

        /// <summary>
        /// ★★ <b>이 감사가 못 보던 자리를 그 자리에서 못박는다.</b> 아래 첫 두 단언이
        /// 2026-09-06 사고의 재발 방지 본체다 — 누가 새 정규식을 «단순화»한다며 지우면
        /// 여기서 먼저 빨개진다.
        /// </summary>
        [Test]
        public void 양성대조_확장자없는_참조_스캐너가_능력을_증명한다()
        {
            Assert.IsTrue(Directory.Exists(ScriptsRoot),
                $"스크립트 루트를 찾지 못했다: {ScriptsRoot}. 트리가 옮겨졌다면 " +
                "디렉터리 판정이 통째로 false가 되어 «폴더를 가리킨 인용»이 전부 거짓 위반이 된다.");

            // ── (1) 옛 규칙이 못 보던 형태. 이 두 줄이 사고 그 자체다.
            const string incident = "/// <para><c>Tests/EditMode/IncomeTimeSourceAuditTests</c>가 이 문장을 확인한다.";
            Assert.IsEmpty(ExtractCsRefs(incident),
                ".cs 규칙이 이걸 봤다면 애초에 사고가 안 났다 — 이 단언이 거짓이 되면 " +
                "아래 확장의 존재 이유가 사라진 것이니 그때는 확장을 지워라.");
            CollectionAssert.AreEqual(new[] { "Tests/EditMode/IncomeTimeSourceAuditTests" },
                ExtractTestPathRefs(incident),
                "확장한 규칙이 사고 그 문장을 못 잡으면 이 라운드는 아무것도 고치지 않은 것이다.");

            const string bareIncident = "///      WindowsGameProcessProbeTests가 이 파일에 그 이름들이 없음을 기계로 잠근다.";
            Assert.IsEmpty(ExtractCsRefs(bareIncident));
            CollectionAssert.AreEqual(new[] { "WindowsGameProcessProbeTests" },
                ExtractBareTestClassRefs(bareIncident),
                "조사(가/를/이/의…)가 붙은 낱말형을 놓치면 142건짜리 스캔이 통째로 무효다.");

            // ── (2) 겹쳐 세지 않는다 — 같은 참조가 두 규칙에 동시에 걸리면 위반이 두 배로 보고된다.
            Assert.IsEmpty(ExtractBareTestClassRefs("/// Tests/EditMode/FooTests 를 보라"),
                "경로형은 낱말형이 다시 세면 안 된다(앞의 '/'를 lookbehind가 막는다).");
            Assert.IsEmpty(ExtractBareTestClassRefs("/// FooTests.cs 를 보라"),
                ".cs가 붙은 것은 CsRefRegex의 몫이다.");
            Assert.IsEmpty(ExtractTestPathRefs("/// Tests/EditMode/FooTests.cs 를 보라"),
                "확장자가 붙었으면 경로형이 아니라 .cs 참조다.");

            // ── (3) 실제 트리에 대고 해석 능력을 증명한다.
            Dictionary<string, List<string>> fileIndex = IndexAllSources();
            Dictionary<string, List<string>> typeIndex = IndexDeclaredTypes();

            Assert.Greater(typeIndex.Count, 300,
                $"타입 선언을 {typeIndex.Count}개밖에 못 찾았다 — 색인이 눈이 멀면 " +
                "모든 참조가 «없음»이 되어 거짓 위반이 쏟아진다. 2026-09-06 실측 960개.");

            // ★ 주석 속 «계획»이 선언으로 세어지면, 거짓 참조가 스스로를 해석해 조용히 초록이 된다.
            Assert.IsEmpty(CodePart("        /// class DockLandingSilhouetteTests 를 만들 예정").Trim(),
                "주석 줄에서 코드를 걷어내지 못했다 — 주석에 적어 둔 계획이 타입 선언으로 세어진다.");
            Assert.IsEmpty(CodePart("// public sealed class FooTests { }").Trim());
            Assert.AreEqual("public sealed class RealTests",
                CodePart("public sealed class RealTests   // 진짜 선언").TrimEnd(),
                "꼬리 주석만 떼야 한다 — 코드까지 떼면 색인이 비고 거짓 위반이 쏟아진다.");

            // ★ 같은 구멍의 두 번째 입구 — 감사 테스트가 <b>문자열로 조립한</b> 가짜 소스.
            //   이걸 선언으로 세면 유령 타입이 색인에 들어와 거짓 참조가 스스로 해석된다.
            const string assembled = "                \"private sealed class FakeSave\\n\" +";
            Assert.IsTrue(InsideStringLiteral(assembled, assembled.IndexOf("class", StringComparison.Ordinal)),
                "문자열 리터럴 안의 'class'를 선언으로 셉니다 — 유령 타입이 색인에 들어옵니다.");
            const string attributed = "        [Tooltip(\"설명\")] public sealed class RealOne";
            Assert.IsFalse(InsideStringLiteral(attributed, attributed.LastIndexOf("class", StringComparison.Ordinal)),
                "따옴표가 짝수로 닫힌 <b>진짜</b> 선언을 문자열 안으로 오판했습니다 — " +
                "그 타입이 색인에서 빠지면 그것을 지목한 멀쩡한 인용이 거짓 위반으로 뜹니다.");
            Assert.IsFalse(InsideStringLiteral("    public sealed class RealTwo", 24));

            Assert.IsTrue(ResolvesTestPathRef("Tests/EditMode/CommentReferenceAuditTests",
                    fileIndex, typeIndex, ScriptsRoot),
                "자기 자신을 못 찾으면 이 감사 전체가 무의미하다.");
            Assert.IsTrue(ResolvesTestPathRef("Tests/EditMode/Golden", fileIndex, typeIndex, ScriptsRoot),
                ".cs가 하나도 없는 골든 폴더도 실재하는 디렉터리다 — 이걸 위반으로 세면 " +
                "«고칠 것이 없는 빨강»이 3건 뜬다(ItemCatalog.cs 등, 실측).");
            Assert.IsTrue(ResolvesTestPathRef("Tests/PlayMode/PortraitBodyStrokeParityTests",
                    fileIndex, typeIndex, ScriptsRoot),
                "실재하는 쪽(PlayMode)은 반드시 풀려야 한다 — 안 풀리면 아래 음성 대조가 " +
                "«위치가 틀렸다»가 아니라 «스캐너가 죽었다»를 재고 있는 것이다.");

            Assert.IsTrue(ResolvesBareTestClassRef("CommentReferenceAuditTests", fileIndex, typeIndex));
            Assert.IsTrue(ResolvesBareTestClassRef("PopoverAndHoverPanelOpacityTests", fileIndex, typeIndex),
                "클래스 이름과 파일 이름이 다른 실례다(파일은 있고 클래스는 PopoverPanelOpacityTests). " +
                "파일명 색인을 빼면 여기서 거짓 위반이 난다.");
            Assert.IsTrue(ResolvesBareTestClassRef("PopoverPanelOpacityTests", fileIndex, typeIndex),
                "반대 방향 — 클래스는 있고 그 이름의 파일은 없다. 타입 색인을 빼면 여기서 거짓 위반이 난다.");
        }

        [Test]
        public void 음성대조_확장자없는_참조_스캐너가_없는_것을_없다고_말한다()
        {
            Dictionary<string, List<string>> fileIndex = IndexAllSources();
            Dictionary<string, List<string>> typeIndex = IndexDeclaredTypes();

            Assert.IsFalse(ResolvesTestPathRef("Tests/EditMode/NoSuchTestAtAll",
                    fileIndex, typeIndex, ScriptsRoot),
                "없는 것을 있다고 하면 탐지력이 0이다.");
            Assert.IsFalse(ResolvesBareTestClassRef("NoSuchAuditTests", fileIndex, typeIndex),
                "낱말형도 마찬가지다.");

            // ★ 이 라운드가 실제로 잡은 것 — 하나라도 통과로 나오면 확장이 죽은 것이다.
            Assert.IsFalse(ResolvesTestPathRef("Tests/EditMode/PortraitBodyStrokeParityTests",
                    fileIndex, typeIndex, ScriptsRoot),
                "클래스는 실재하지만 PlayMode에 있다. 위치를 안 보면 파일을 옮겨도 감사가 침묵한다 — " +
                "이 파일이 .cs 참조에 대해 이미 그렇게 판정하고 있다(Platform/ItemCatalog.cs 대조).");
            Assert.IsFalse(ResolvesTestPathRef("Tests/PlayMode/DockLandingSilhouetteTests",
                    fileIndex, typeIndex, ScriptsRoot),
                "아직 안 만든 하나다(PlayMode 리그가 필요해 다음 라운드로 넘겼다). " +
                "만드는 라운드는 이 줄을 아래 IsTrue 묶음으로 옮겨라.");

            // ★ 실제로 만든 것들은 <b>풀려야</b> 한다.
            //   여기가 «만들었다고 보고했는데 실은 안 만들었다»를 잡는 자리다 —
            //   보고서가 아니라 <b>트리</b>에 대고 묻는다.
            Assert.IsTrue(ResolvesTestPathRef("Tests/EditMode/IncomeTimeSourceAuditTests",
                    fileIndex, typeIndex, ScriptsRoot),
                "CurrencyModel.cs가 이 테스트를 근거로 인용한다. 없으면 그 인용이 다시 거짓이 된다.");
            Assert.IsTrue(ResolvesTestPathRef("Tests/EditMode/DailyLimitClampAuditTests",
                    fileIndex, typeIndex, ScriptsRoot),
                "CurrencyRules.cs가 두 곳에서 인용한다.");

            // ★★ 2026-09-06 후속 라운드(test-engineer)가 만든 6건.
            //    낱말형 4건 — 인용에 위치 주장이 없으므로 이름만 풀리면 된다.
            Assert.IsTrue(ResolvesBareTestClassRef("WindowsGameProcessProbeTests", fileIndex, typeIndex),
                "WindowsGameProcessProbe.cs가 «레지스트리 쓰기 API가 없음을 기계로 잠근다»고 단언한다. " +
                "CLAUDE.md 절대 불변 원칙 3(유저 자산 불변)에 직접 걸리는 인용이라 우선순위가 가장 높았다.");
            Assert.IsTrue(ResolvesBareTestClassRef("SystemAudioActivityProbeContractTests",
                    fileIndex, typeIndex),
                "WindowsSystemAudioActivityProbe.cs가 «이 머신에서는 실행으로 확인할 수 없다»며 " +
                "이 테스트를 유일한 검증 수단으로 세워 뒀다.");
            Assert.IsTrue(ResolvesBareTestClassRef("MacSystemAudioSelectorTests", fileIndex, typeIndex),
                "MacSystemAudioActivityProbe.cs가 «값을 손으로 바꾸지 마라»는 경고의 안전망으로 인용한다.");
            Assert.IsTrue(ResolvesBareTestClassRef("CharacterStatCardLayoutTests", fileIndex, typeIndex),
                "CharacterInfoWindow.Stats.cs가 «그 등식을 상수에서 직접 다시 센다»고 인용한다.");
            Assert.IsTrue(ResolvesBareTestClassRef("DancePoseFallbackParityTests", fileIndex, typeIndex),
                "StickmanBlackboard.cs가 «서로 다른 방법 두 개» 중 한쪽으로 인용한다.");

            //    경로형 1건 — 인용이 Tests/PlayMode/로 <b>위치까지</b> 단언하므로 그 디렉터리라야 한다.
            Assert.IsTrue(ResolvesTestPathRef("Tests/PlayMode/InfoGearContrastTests",
                    fileIndex, typeIndex, ScriptsRoot),
                "InfoGearIconWidget.cs가 두 곳에서 «회색 0~255 전 구간을 훑어 ≥3:1을 확인한다»고 인용한다. " +
                "★ EditMode에 만들면 이 단언이 빨개진다 — 그게 위치 규칙이 일하는 모습이다.");
        }

        /// <summary>
        /// ★ 넓힌 만큼 <b>오탐이 늘지 않았는가</b>. 이 저장소가 실제로 갖고 있는,
        /// «테스트 클래스가 아닌데 규칙에 걸릴 뻔한» 모양들을 그대로 흘린다.
        /// </summary>
        [Test]
        public void 음성대조_테스트가_아닌_것을_테스트로_오인하지_않는다()
        {
            foreach (string line in new[]
                     {
                         "/// <c>ResetForTests</c>를 부른다",
                         "/// SetPlatformForTests(플랫폼)로 갈아 끼운다",
                         "/// InfoGearIconWidget.FeedPointerForTests가 입력을 대신 넣는다",
                         "/// CharacterFxRenderer.LivePieceColorsForTests를 읽는다",
                     })
            {
                Assert.IsEmpty(ExtractBareTestClassRefs(line),
                    $"테스트 전용 <b>메서드</b>를 테스트 클래스로 셌다 → {line}\n" +
                    "이 관례는 이 저장소에 9건 있고, 오탐 9건이면 이 감사는 첫날에 꺼진다.");
            }

            // 디렉터리·네임스페이스 인용은 경로형 규칙의 사거리 밖이다(접두를 Tests/로 좁힌 이유).
            foreach (string line in new[]
                     {
                         "/// Platform/MacOS 쪽에만 있다",
                         "/// States/Dialogue 네임스페이스로 옮겼다",
                         "/// Plugins/Windows 에 넣는다",
                     })
            {
                Assert.IsEmpty(ExtractTestPathRefs(line),
                    $"디렉터리/네임스페이스 인용을 파일 참조로 셌다 → {line}\n" +
                    "이 형태가 실측 77건이라, 사거리를 Tests/로 좁히지 않으면 전부 거짓 위반이 된다.");
            }

            Assert.IsEmpty(ExtractBareTestClassRefs("/// 이 줄에는 테스트 인용이 없다"),
                "아무 줄에서나 참조를 만들어 내면 거짓 위반이 쏟아진다.");
        }
    }
}
