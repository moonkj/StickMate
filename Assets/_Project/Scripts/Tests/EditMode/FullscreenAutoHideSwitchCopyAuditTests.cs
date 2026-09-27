using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.Platform;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ <b>설정창 [일반] 「전체화면 자동 숨김」 행의 라벨·캡션이 등급 1의 실재와 어긋나지 않는다</b> —
    /// 2026-09-27 리더 배정(설계 근거: <c>docs/narrative/FULLSCREEN_AUTOHIDE_SWITCH_WORDING.md</c> §9).
    ///
    /// ============================================================================
    /// 무엇이 비어 있었나 — <b>이 두 문장을 아무도 잠그지 않았다</b>
    /// ============================================================================
    /// 라벨은 <c>Assets/</c> 전체에서 <b>양성 단언 0건</b>이었다(적중 1건 = 그 리터럴 자기 자신).
    /// 캡션은 PlayMode <c>SettingsUserFacingCopyTests</c>가 <b>부분문자열 두 개</b>(클릭·창)만 봤다.
    /// 그래서 <b>문장이 거짓이어도 아무것도 빨개지지 않는 구간</b>이 있었다 — 실제로 옛 캡션
    /// (「켜면 캐릭터도 열린 창도 함께 사라집니다」)은 <b>등급 1에서 거짓</b>인 채로 출하돼 있었다.
    ///
    /// ============================================================================
    /// 이 감사가 잠그는 명제 셋
    /// ============================================================================
    /// <list type="number">
    /// <item><b>라벨이 「무엇이 물러나는가」를 말한다.</b> 주어가 빠지면 사용자는 「캐릭터가 사라진다」로
    ///   읽고, 그것은 <b>등급 1의 실재와 반대</b>다(<see cref="ForeignFullscreenTierPolicy.SuspendsCharacter"/>는
    ///   <see cref="ForeignFullscreenTier.Full"/>에서만 참인데
    ///   <see cref="ForeignFullscreenTierPolicy.RetreatsPanels"/>·
    ///   <see cref="ForeignFullscreenTierPolicy.SuppressesAutoDance"/>는 등급 1에서도 참이다).
    ///   같은 이유로 <b>방아쇠를 「게임」으로 좁히는 것</b>도 막는다 — 그것이 옛 라벨의 병이었다.</item>
    /// <item><b>캡션이 「게임일 때만 캐릭터」라는 조건을 유지한다.</b> 조건절 없이 캐릭터 은신을 말하면
    ///   <b>2026-08-31 사용자 신고로 닫힌 문을 다시 여는 것</b>이다(원문: "엑셀같은 프로그램 전체화면에서
    ///   엑셀 클릭하면 캐릭터가 없어져버림"). <c>CLAUDE.md</c> 절대 불변 원칙 2 아래 정정 블록이 정본이다.</item>
    /// <item><b>캡션의 춤 절이 「게임」 조건 <i>밖</i>에 있다.</b> 「게임이면 …숨고 춤도 멈춥니다」로 줄이면
    ///   <b>「게임일 때만 춤이 멈춘다」</b>로 읽히는데, 실제로는 등급 1(발표·회의)에서도 멈춘다.
    ///   설계 문서가 이 함정(D5)을 <b>스스로 기각한 기록</b>으로 남겨 두었고, 다음 사람이 줄을 줄일 때
    ///   가장 먼저 밟을 자리라고 지목했다.</item>
    /// <item>★★ <b>두 문장이 상자 안에 들어간다</b>(<see cref="자동숨김_라벨과_캡션이_상자_폭_안에_들어간다"/>).
    ///   이 자리에는 <b>말줄임이 없다</b> — <c>SettingsControls.BeginRow</c>가 <c>cap.text = …</c>로 곧장
    ///   대입하고 <c>UiChrome.AddText</c>의 기본값이 <c>wrap:false</c>(<c>Overflow</c>)이므로,
    ///   넘쳐도 <b>잘리지 않고 카드 밖으로 흘러나간다</b>. 즉 <b>아무것도 빨개지지 않는 사고</b>다.
    ///   그래서 폭을 글자 수가 아니라 <b>폰트에게 물어</b> 잠근다.</item>
    /// </list>
    ///
    /// <para>★ <b>라벨을 늘리기 전에 멈출 자리</b>: 같은 <c>BeginRow</c>가 단축키 칩을
    /// <c>labelText.preferredWidth + 8f</c>에 놓는다 — 라벨이 길어지면 <b>칩이 같이 밀린다</b>.
    /// 이 행에는 칩이 없지만 같은 함수를 쓰는 다른 행에는 있다. 라벨 상자 안에 들어가는 것만으로는
    /// 충분하지 않을 수 있다는 뜻이고, 그 판정은 실기 캡처다.</para>
    ///
    /// <b>중복하지 않는 것</b>: ⓐ 내부 개발 문자열 노출은 PlayMode
    /// <c>SettingsUserFacingCopyTests.NoInternalDeveloperStringIsRenderedInAnyTab</c>이 이미 잠근다.
    /// ⓑ 끄면 치르는 대가(클릭·창 고지)는 같은 파일의 J3 테스트가 <b>렌더된</b> 캡션에서 잠근다.
    /// ⓒ 등급 술어의 진리표는 <c>FullscreenGameSuspendPolicyTests</c>가 소유한다 — 여기서는 <b>전제</b>로만
    /// 읽고 다시 증명하지 않는다.
    ///
    /// ============================================================================
    /// ★ 어떻게 짜지 <b>않았는가</b> — 이 저장소가 반복해 당한 함정
    /// ============================================================================
    /// <list type="bullet">
    /// <item><b>프로덕션 문장을 니들로 베끼지 않는다.</b> 라벨·캡션은 아직 호출부 리터럴이라(설계 문서 §8이
    ///   그 사실을 자백해 뒀다) 글자를 베끼면 다음 교체에서 <b>조용히 낡거나 거짓 빨강</b>이 된다
    ///   (실제 사고: <c>UiInteractionFramePacingHoldTests</c>가 <c>_agent.IsSuspended</c>를 베꼈다가
    ///   프로덕션이 넓어지자 <c>IndexOf = -1</c>로 거짓 빨강). 그래서 이 감사는 <b>문장을 파일에서 읽어
    ///   와서 뜻을 재고</b>, 뜻을 재는 낱말은 <b>프로덕션 문장에서 교정</b>한다
    ///   (<see cref="판정에_쓰는_낱말이_프로덕션_문장에_실재한다"/>).</item>
    /// <item><b>부재 단언을 홀로 두지 않는다.</b> 부재 단언은 썩으면 <b>조용히 초록</b>이 된다. 그래서
    ///   변이 표본을 <b>실제 문안에서 기계적으로 빼</b> 만들고(손으로 베낀 표본은 낡는다),
    ///   판정기가 그 변이를 <b>실제로 빨갛게</b> 만드는지 같은 테스트에서 대조한다
    ///   (<see cref="문안_판정기가_주어없는_라벨과_조건절없는_캡션을_실제로_빨갛게_만든다"/>).</item>
    /// <item><b>타입이 아니라 소스를 읽는다.</b> 활성 빌드 타깃 반대편 플랫폼 타입은 존재하지 않으므로
    ///   리플렉션 감사는 그쪽 절반을 구조적으로 못 본다(<c>CLAUDE.md</c> 「활성 빌드 타깃 규칙」).
    ///   이 파일이 읽는 <c>SettingsWindow.cs</c>에는 플랫폼 <c>#if</c>가 없어 양 타깃에서 같은 답이 나온다.</item>
    /// <item><b>앵커는 컴파일러가 검사한다.</b> 행을 찾는 열쇠는 문자열이 아니라
    ///   <c>nameof(AppSettingsModel.SetAutoHideOnFullscreen)</c>다 — 이름이 바뀌면 <b>이 파일이 컴파일되지
    ///   않는다</b>(조용한 초록이 불가능하다). 인수 자리도 손으로 세지 않고
    ///   <see cref="SettingsCardBuilder.AddToggle"/>의 <b>매개변수 이름·순서를 리플렉션으로</b> 읽는다.</item>
    /// </list>
    ///
    /// <para><b>한계(정직하게 적는다)</b>:
    /// <list type="bullet">
    /// <item>이 감사는 <b>낱말의 존재와 순서</b>를 본다. 문법적으로 뜻이 뒤집힌 문장
    ///   (예: 「창부터 물러나지 <b>않기</b>」)은 잡지 못한다.</item>
    /// <item><b>폭 판정은 이 머신의 폰트에 의존한다.</b> 번들 폰트(<c>LegacyRuntime.ttf</c>)에 한글
    ///   글리프가 없어 <b>운영체제 폴백</b>에서 오므로, 여기서 나온 pt 값은 <b>이 플랫폼의 값</b>이다.
    ///   ⇒ <b>macOS 초록이 Windows 초록을 증명하지 않는다.</b> 그래서 단언은 <b>상자 이하</b>라는
    ///   부등식이고(치수 동등이 아니다), 폰트가 0을 내는 경우를 <b>양성 대조로 먼저</b> 걸러낸다.</item>
    /// <item><b>「글자 수 × 상수」 모형은 쓰지 않는다.</b> 이 저장소가 폐기한 계열이다 — 창 6곳이 그
    ///   모형으로 상자를 정하다 라틴에서 글리프의 두 배 가까이 부풀어 <b>창 밖으로 밀려났다</b>
    ///   (<c>Tests/PlayMode/UiTextWidthModelTests</c>가 그 사고를 적는다). 글자 수 단언이 한 건도 없다.</item>
    /// <item><b>최종 판정은 실기 캡처다</b> — 설계 문서가 캡처 2장을 대기 항목으로 남겨 두었다.
    ///   폭 여유가 얇다고 보고된 자리라 더 그렇다.</item>
    /// <item>문안이 <c>const</c>나 문자열 테이블로 올라가면 이 추출기는 <b>빨갛게 멈춘다</b>
    ///   (리터럴 앞에 다른 코드가 있으면 즉시 실패한다) — 그때 추출기를 그 원천에서 읽도록 고치는 것이
    ///   맞는 방향이라 <b>일부러 조용히 통과시키지 않았다</b>.</item>
    /// </list></para>
    /// </summary>
    public sealed class FullscreenAutoHideSwitchCopyAuditTests
    {
        // ============================================================================
        // 판정에 쓰는 낱말 — 전부 프로덕션 문장에서 교정한다(아래 교정 테스트가 그 대조다)
        // ============================================================================

        /// <summary><b>물러나는 주체</b>로 인정하는 낱말. 등급 1 설명(<c>Describe(PanelsOnly)</c>)이 실제로
        /// 쓰는 낱말만 넣는다 — 교정 테스트가 이 둘이 그 문장에 실재함을 매번 확인한다.</summary>
        private static readonly string[] SurfaceNouns = { "창", "패널" };

        /// <summary>캐릭터 본체를 가리키는 낱말. 설정창 캡션은 캐릭터를 <b>3인칭</b>으로 부른다.</summary>
        private const string CharacterNoun = "캐릭터";

        /// <summary>등급 2를 가르는 조건 낱말.</summary>
        private const string GameWord = "게임";

        /// <summary>등급 1 이상 전반을 가리키는 넓은 낱말(방아쇠가 「게임」으로 좁는 것을 가른다).</summary>
        private const string AppWord = "앱";

        /// <summary>축 5(자동 발동 춤 억제)를 가리키는 낱말.</summary>
        private const string DanceWord = "춤";

        /// <summary>음성 대조용 센티널 — 프로덕션에 있을 수 없는 글자. 「포함 검사가 항상 참이 아님」을
        /// 보이는 데만 쓴다(합성 문자열이라 낡을 수가 없다).</summary>
        private const string DeadNeedleSentinel = "없는낱말센티널";

        /// <summary>문장 경계. 조건절의 지배 범위를 「한 문장」으로 본다.</summary>
        private static readonly char[] SentenceEnders = { '.', '!', '?', '\n' };

        // ============================================================================
        // 앵커 — 문자열이 아니라 컴파일러가 검사하는 이름
        // ============================================================================

        /// <summary>자동 숨김 행을 가리키는 앵커. 이 행의 콜백만 이 세터를 부른다.</summary>
        private static string AutoHideCallbackAnchor =>
            nameof(AppSettingsModel) + "." + nameof(AppSettingsModel.SetAutoHideOnFullscreen);

        /// <summary>같은 카드의 형제 행(음악 춤). <see cref="DanceWord"/>를 교정하는 데 쓴다.</summary>
        private static string DanceCallbackAnchor =>
            nameof(AudioReactiveDanceGate) + "." + nameof(AudioReactiveDanceGate.SetMutedForThisSession);

        /// <summary><see cref="SettingsCardBuilder.AddToggle"/>의 라벨 매개변수 이름.
        /// 리플렉션으로 <b>실재를 단언</b>하므로, 이름이 바뀌면 조용히 지나가지 않고 빨개진다.</summary>
        private const string LabelParameterName = "label";

        /// <summary>캡션 매개변수 이름(이름 있는 인수 <c>caption:</c>을 찾는 토큰의 원천).</summary>
        private const string CaptionParameterName = "caption";

        /// <summary>추출기가 의존하는 <b>위치 인자 순서</b>. 리플렉션 값과 어긋나면 즉시 빨개진다 —
        /// 순서가 바뀐 날 조용히 다른 인수를 라벨로 집는 것을 막는다.</summary>
        private const int LabelPositionalIndex = 1;

        /// <summary><see cref="UiChrome.PlaceTopLeft"/>의 폭 매개변수 이름(상자 폭을 그 인수에서 읽는다).</summary>
        private const string WidthParameterName = "width";

        /// <summary>라벨 상자를 놓는 호출. 지역 변수 이름이 들어 있으므로 <b>실재를 단언</b>해 쓴다 —
        /// 이름이 바뀌면 「앵커 없음」으로 시끄럽게 실패한다(조용한 초록이 아니다).</summary>
        private static string LabelBoxAnchor => nameof(UiChrome.PlaceTopLeft) + "(labelText.rectTransform";

        /// <summary>캡션 상자를 놓는 호출. 같은 파일의 <c>caption.rectTransform</c>(다른 부품)과 갈라지도록
        /// 변수 이름까지 붙인다 — 겹치면 <see cref="IndexOfExactlyOnce"/>가 먼저 시끄럽게 실패한다.</summary>
        private static string CaptionBoxAnchor => nameof(UiChrome.PlaceTopLeft) + "(cap.rectTransform";

        // ============================================================================
        // 소스에서 문안 읽기
        // ============================================================================

        private readonly struct ToggleCopy
        {
            public readonly string Label;
            public readonly string Caption;

            public ToggleCopy(string label, string caption)
            {
                Label = label;
                Caption = caption;
            }
        }

        private static string InteractionPath(string fileName) => Path.Combine(
            Application.dataPath, "_Project", "Scripts", "Interaction", fileName);

        private static string ReadSource(string fileName)
        {
            string path = InteractionPath(fileName);
            Assert.IsTrue(File.Exists(path),
                $"소스를 찾지 못했다: {path}. 파일이 옮겨졌다면 이 경로도 함께 고쳐라 — " +
                "경로가 틀린 채로 두면 이 감사가 통째로 죽는다.");
            return File.ReadAllText(path).Replace("\r\n", "\n");
        }

        private static string ReadSettingsWindowSource() => ReadSource("SettingsWindow.cs");

        private static string ReadSettingsControlsSource() => ReadSource("SettingsControls.cs");

        private static ParameterInfo[] AddToggleParameters()
        {
            MethodInfo method = typeof(SettingsCardBuilder).GetMethod(
                nameof(SettingsCardBuilder.AddToggle), BindingFlags.Public | BindingFlags.Instance);
            Assert.IsNotNull(method,
                $"{nameof(SettingsCardBuilder)}.{nameof(SettingsCardBuilder.AddToggle)}를 리플렉션으로 찾지 못했다 — " +
                "오버로드가 생겼거나 접근 수준이 바뀌었다면 이 추출기를 함께 고쳐라.");
            return method.GetParameters();
        }

        /// <summary>매개변수 이름으로 자리를 찾는다 — 없으면 <b>빨갛게</b> 실패한다(조용한 초록 금지).</summary>
        private static int ParameterIndex(ParameterInfo[] parameters, string role)
        {
            int index = -1;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (!string.Equals(parameters[i].Name, role, StringComparison.Ordinal)) continue;
                Assert.AreEqual(-1, index, $"매개변수 이름 '{role}'이 둘 이상이다 — 추출기가 어느 쪽인지 알 수 없다.");
                index = i;
            }
            Assert.GreaterOrEqual(index, 0,
                $"{nameof(SettingsCardBuilder.AddToggle)}에 '{role}' 매개변수가 없다 — 이름이 바뀌었다면 이 감사의 " +
                "앵커도 함께 고쳐라. 이 단언이 없으면 추출기가 엉뚱한 인수를 집고도 초록이 된다.");
            Assert.AreEqual(typeof(string), parameters[index].ParameterType,
                $"'{role}' 매개변수가 더 이상 문자열이 아니다 — 문안이 다른 형태로 들어온다면 추출기를 고쳐라.");
            return index;
        }

        /// <summary>
        /// <b>주석·문자열을 지운 코드</b>에서 한 <c>AddToggle(</c> 호출을 찾아 라벨과 캡션 리터럴을 꺼낸다.
        ///
        /// <para>주석을 먼저 지우는 것이 핵심이다 — 이 행의 인수 사이에는 <b>옛 캡션을 따옴표째 인용한
        /// 주석</b>이 있고, 그것을 코드로 세면 추출기가 <b>화면에 없는 문장</b>을 판정하게 된다.
        /// 지우는 일은 이미 있는 <c>SourceTextScanner</c>에 맡긴다(같은 스캐너를 두 번 짜지 않는다).
        /// 그 스캐너는 <b>길이를 보존</b>하고 리터럴의 <b>따옴표는 남긴 채 내용만</b> 공백으로 바꾸므로,
        /// 위치는 원본과 1:1이고 내용은 원본에서 그대로 떠올 수 있다.</para>
        /// </summary>
        private static ToggleCopy ReadToggleCopy(string source, string callbackAnchor, string role)
        {
            string code = SourceTextScanner.BlankCommentsAndStrings(source, null);
            Assert.AreEqual(source.Length, code.Length,
                "스캐너가 길이를 바꿨다 — 위치가 1:1이 아니면 원본에서 리터럴 내용을 떠올 수 없다.");

            int anchor = IndexOfExactlyOnce(code, callbackAnchor, role);

            string callNeedle = nameof(SettingsCardBuilder.AddToggle) + "(";
            int call = code.LastIndexOf(callNeedle, anchor, StringComparison.Ordinal);
            Assert.GreaterOrEqual(call, 0,
                $"{role}: 앵커({callbackAnchor}) 앞에서 {callNeedle} 호출을 찾지 못했다 — 행이 다른 부품으로 " +
                "바뀌었다면 이 추출기도 함께 고쳐라.");

            int open = call + callNeedle.Length - 1;
            int close = MatchingParen(code, open, role);
            Assert.Greater(close, anchor,
                $"{role}: 앵커가 그 {callNeedle} 호출 바깥에 있다 — 엉뚱한 행을 집었다.");

            ParameterInfo[] parameters = AddToggleParameters();
            int labelIndex = ParameterIndex(parameters, LabelParameterName);
            int captionIndex = ParameterIndex(parameters, CaptionParameterName);
            Assert.AreEqual(LabelPositionalIndex, labelIndex,
                $"라벨이 더 이상 {LabelPositionalIndex}번째 위치 인자가 아니다 — 추출기가 의존하는 순서다. " +
                "순서가 바뀌었으면 이 상수를 함께 고쳐라(조용히 다른 인수를 라벨로 집는 것을 막는 단언이다).");

            List<int[]> args = ArgumentSpans(code, open, close);
            Assert.Greater(args.Count, labelIndex,
                $"{role}: 인수를 {args.Count}개밖에 못 갈랐다 — 인수 나누기가 깨졌다.");

            string label = ReadLiteral(code, source, args[labelIndex][0], args[labelIndex][1], role + " 라벨");

            string captionToken = parameters[captionIndex].Name + ":";
            int captionArg = -1;
            for (int i = 0; i < args.Count; i++)
            {
                if (code.IndexOf(captionToken, args[i][0], args[i][1] - args[i][0], StringComparison.Ordinal) < 0) continue;
                Assert.AreEqual(-1, captionArg, $"{role}: '{captionToken}' 인수가 둘 이상이다.");
                captionArg = i;
            }
            Assert.GreaterOrEqual(captionArg, 0,
                $"{role}: 이름 있는 인수 '{captionToken}'을 찾지 못했다 — 캡션이 위치 인자로 바뀌었거나 사라졌다. " +
                "사라졌다면 그것 자체가 회귀다(캡션이 등급 구분을 지고 있다).");

            int afterToken = code.IndexOf(captionToken, args[captionArg][0], StringComparison.Ordinal) + captionToken.Length;
            string caption = ReadLiteral(code, source, afterToken, args[captionArg][1], role + " 캡션");

            return new ToggleCopy(label, caption);
        }

        private static int IndexOfExactlyOnce(string code, string needle, string role)
        {
            int first = code.IndexOf(needle, StringComparison.Ordinal);
            Assert.GreaterOrEqual(first, 0,
                $"{role}: 앵커 '{needle}'가 코드에 없다 — 주석이나 문자열이 아니라 <b>코드</b>에서 찾는다. " +
                "배선이 옮겨졌다면 이 감사의 앵커도 함께 옮겨라.");
            int second = code.IndexOf(needle, first + needle.Length, StringComparison.Ordinal);
            Assert.AreEqual(-1, second,
                $"{role}: 앵커 '{needle}'가 두 번 이상 나온다 — 어느 행이 사용자 화면의 그 행인지 알 수 없다.");
            return first;
        }

        private static int MatchingParen(string code, int open, string role)
        {
            Assert.AreEqual('(', code[open], $"{role}: 여는 괄호 자리를 잘못 잡았다.");
            int depth = 0;
            for (int i = open; i < code.Length; i++)
            {
                if (code[i] == '(') depth++;
                else if (code[i] == ')' && --depth == 0) return i;
            }
            Assert.Fail($"{role}: 호출의 닫는 괄호를 찾지 못했다 — 괄호 짝이 맞지 않는다.");
            return -1;
        }

        /// <summary>깊이 1의 콤마로 인수를 가른다(각 항목은 <c>[시작, 끝)</c>).</summary>
        private static List<int[]> ArgumentSpans(string code, int open, int close)
        {
            var spans = new List<int[]>();
            int paren = 0, brace = 0, bracket = 0;
            int start = open + 1;
            for (int i = open; i < close; i++)
            {
                char c = code[i];
                if (c == '(') paren++;
                else if (c == ')') paren--;
                else if (c == '{') brace++;
                else if (c == '}') brace--;
                else if (c == '[') bracket++;
                else if (c == ']') bracket--;
                else if (c == ',' && paren == 1 && brace == 0 && bracket == 0)
                {
                    spans.Add(new[] { start, i });
                    start = i + 1;
                }
            }
            spans.Add(new[] { start, close });
            return spans;
        }

        /// <summary>
        /// 인수 구간에서 문자열 리터럴(<c>+</c>로 이어 붙인 것 포함)을 읽는다.
        ///
        /// <para>★ 리터럴 <b>앞에 다른 코드가 있으면 실패</b>한다. 문안이 <c>const</c>·문자열 테이블·삼항식으로
        /// 옮겨가는 날 <b>조용히 한쪽만 판정하는 것</b>을 막는다 — 그때는 이 추출기를 그 원천에서 읽도록
        /// 고치는 것이 맞다. 남은 따옴표가 있는 경우도 같은 이유로 실패시킨다.</para>
        /// </summary>
        private static string ReadLiteral(string code, string source, int from, int to, string role)
        {
            var text = new StringBuilder();
            int i = from;
            int pieces = 0;

            while (true)
            {
                int quote = code.IndexOf('"', i);
                if (quote < 0 || quote >= to) break;

                string between = code.Substring(i, quote - i).Trim();
                if (pieces == 0)
                {
                    Assert.IsEmpty(between,
                        $"{role}: 리터럴 앞에 코드가 있다(\"{between}\") — 문안이 상수·문자열 테이블·조건식으로 " +
                        "옮겨갔다면 이 추출기를 그 원천에서 읽도록 고쳐라. 조용히 통과시키면 다음 사람이 " +
                        "화면에 없는 문장을 잠근 줄 알게 된다.");
                }
                else
                {
                    Assert.AreEqual("+", between,
                        $"{role}: 리터럴 사이에 '+'가 아닌 것이 있다(\"{between}\") — 추출기를 고쳐라.");
                }

                Assert.IsFalse(quote > 0 && (code[quote - 1] == '$' || code[quote - 1] == '@'),
                    $"{role}: 보간·verbatim 문자열은 다루지 않는다 — 문안이 그 형태가 되었다면 추출기를 고쳐라.");

                int end = code.IndexOf('"', quote + 1);
                Assert.IsTrue(end > quote && end < to, $"{role}: 리터럴의 닫는 따옴표를 찾지 못했다.");

                string piece = source.Substring(quote + 1, end - quote - 1);
                Assert.IsFalse(piece.IndexOf('\\') >= 0,
                    $"{role}: 리터럴에 이스케이프가 있다 — 원본을 그대로 떠오는 방식이라 뜻이 달라질 수 있다. " +
                    "추출기를 고쳐라.");

                text.Append(piece);
                pieces++;
                i = end + 1;
            }

            Assert.Greater(pieces, 0, $"{role}: 문자열 리터럴을 찾지 못했다.");
            int extra = code.IndexOf('"', i);
            Assert.IsTrue(extra < 0 || extra >= to,
                $"{role}: 인수 안에 리터럴이 더 있다 — 문안이 조건에 따라 갈렸다면 판정기가 한쪽만 보게 된다.");
            return text.ToString();
        }

        // ============================================================================
        // 판정기 — 순수 함수(실제 문안과 변이 표본에 똑같이 쓴다)
        // ============================================================================

        private static string[] Sentences(string text)
            => text.Split(SentenceEnders, StringSplitOptions.RemoveEmptyEntries);

        private static bool ContainsAny(string text, string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (text.IndexOf(needles[i], StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }

        /// <summary>라벨이 <b>무엇이 물러나는가</b>를 말하고, 방아쇠를 「게임」으로 좁히지 않는가.</summary>
        private static bool LabelNamesWhatRetreats(string label, out string why)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                why = "라벨이 비어 있다";
                return false;
            }
            if (!ContainsAny(label, SurfaceNouns))
            {
                why = "무엇이 물러나는지를 말하지 않는다 — 주어가 없으면 사용자는 「캐릭터가 사라진다」로 " +
                      "읽고, 그것은 등급 1의 실재와 반대다";
                return false;
            }

            bool saysCharacter = label.IndexOf(CharacterNoun, StringComparison.Ordinal) >= 0;
            bool saysGame = label.IndexOf(GameWord, StringComparison.Ordinal) >= 0;

            if (saysCharacter && !saysGame)
            {
                why = $"조건 없이 {CharacterNoun} 은신을 말한다 — 캐릭터가 숨는 것은 전체화면 {GameWord}뿐이다";
                return false;
            }
            if (saysGame && label.IndexOf(AppWord, StringComparison.Ordinal) < 0)
            {
                why = $"방아쇠를 「{GameWord}」으로 좁힌다 — {GameWord}이 아닌 전체화면 {AppWord}에서도 표면은 " +
                      "물러난다(그것이 옛 라벨의 병이었다)";
                return false;
            }

            why = null;
            return true;
        }

        /// <summary>캡션이 <b>「게임일 때만 캐릭터」</b>라는 조건을 유지하는가(조건절이 앞에 오는가).</summary>
        private static bool CaptionKeepsGameConditionOnCharacter(string caption, out string why)
        {
            foreach (string sentence in Sentences(caption))
            {
                int character = sentence.IndexOf(CharacterNoun, StringComparison.Ordinal);
                if (character < 0) continue;

                int game = sentence.IndexOf(GameWord, StringComparison.Ordinal);
                if (game < 0)
                {
                    why = $"「{sentence.Trim()}」이 조건 없이 {CharacterNoun} 은신을 말한다 — " +
                          "2026-08-31 신고로 닫힌 문을 다시 여는 문장이다";
                    return false;
                }
                if (game > character)
                {
                    why = $"「{sentence.Trim()}」에서 조건({GameWord})이 {CharacterNoun}보다 뒤에 온다 — " +
                          "조건절로 읽히지 않는다";
                    return false;
                }
            }
            why = null;
            return true;
        }

        /// <summary>캡션이 춤을 말하고, 그 절이 <b>「게임」 조건 밖</b>에 있는가(설계 문서가 기각한 D5 함정).</summary>
        private static bool CaptionPutsDanceOutsideGameCondition(string caption, out string why)
        {
            bool sawDance = false;
            foreach (string sentence in Sentences(caption))
            {
                int dance = sentence.IndexOf(DanceWord, StringComparison.Ordinal);
                if (dance < 0) continue;
                sawDance = true;

                int game = sentence.IndexOf(GameWord, StringComparison.Ordinal);
                if (game >= 0 && game < dance)
                {
                    why = $"「{sentence.Trim()}」에서 {DanceWord}이 조건({GameWord}) 뒤에 온다 — " +
                          $"「{GameWord}일 때만 멈춘다」로 읽히지만 실제로는 등급 1(발표·회의)에서도 멈춘다";
                    return false;
                }
            }
            if (!sawDance)
            {
                why = $"캡션이 {DanceWord}을 한 번도 말하지 않는다 — 자동 춤 억제가 고지되지 않는다";
                return false;
            }
            why = null;
            return true;
        }

        // ============================================================================
        // 테스트
        // ============================================================================

        [Test]
        public void 자동숨김_라벨과_캡션이_등급1_실재와_어긋나지_않는다()
        {
            // ★ 전제 — 이 감사의 요구는 등급 술어에서 나온다. 술어가 바뀌면 요구도 다시 정해야 하므로
            //   여기서 멈춘다(진리표 자체의 소유자는 FullscreenGameSuspendPolicyTests다 — 재증명하지 않는다).
            Assert.IsTrue(
                ForeignFullscreenTierPolicy.SuspendsCharacter(ForeignFullscreenTier.Full)
                && !ForeignFullscreenTierPolicy.SuspendsCharacter(ForeignFullscreenTier.PanelsOnly),
                "전제가 깨졌다: 「캐릭터는 전체화면 게임에서만 숨는다」가 더 이상 참이 아니다. " +
                "캡션의 조건절 요구가 이 술어에 매달려 있으니, 술어를 넓혔다면 그것은 2026-08-31 신고 " +
                "회귀 여부를 사용자에게 확인해야 하는 사안이고 이 감사의 요구도 함께 다시 정해야 한다.");
            Assert.IsTrue(
                ForeignFullscreenTierPolicy.RetreatsPanels(ForeignFullscreenTier.PanelsOnly)
                && ForeignFullscreenTierPolicy.SuppressesAutoDance(ForeignFullscreenTier.PanelsOnly),
                "전제가 깨졌다: 등급 1에서 표면·자동 춤이 더 이상 물러나지 않는다. " +
                "라벨의 「무엇이 물러나는가」와 캡션의 춤 절이 그 사실을 말하고 있으니 함께 고쳐야 한다.");

            ToggleCopy copy = ReadToggleCopy(ReadSettingsWindowSource(), AutoHideCallbackAnchor, "전체화면 자동 숨김 행");

            Assert.IsTrue(LabelNamesWhatRetreats(copy.Label, out string labelWhy),
                $"라벨 「{copy.Label}」 — {labelWhy}. 등급 1에서 캐릭터는 계속 걸어다니고 걷히는 것은 " +
                "창·패널·부채꼴과 그 클릭 차단막이다(그리고 자동 춤이 멈춘다).");

            Assert.IsTrue(CaptionKeepsGameConditionOnCharacter(copy.Caption, out string captionWhy),
                $"캡션 「{copy.Caption}」 — {captionWhy}.");

            Assert.IsTrue(CaptionPutsDanceOutsideGameCondition(copy.Caption, out string danceWhy),
                $"캡션 「{copy.Caption}」 — {danceWhy}.");
        }

        [Test]
        public void 문안_판정기가_주어없는_라벨과_조건절없는_캡션을_실제로_빨갛게_만든다()
        {
            // ★ 이 테스트의 표본은 프로덕션을 겨눈 니들이 <b>아니다</b> — 판정기의 입력이다.
            //   프로덕션 문안은 위 테스트가 파일에서 읽는다. 표본을 손으로 베끼지 않고
            //   (가) 실제 문안에서 기계적으로 빼거나 (나) 교정된 낱말로 조립한다 — 베낀 표본은 낡는다.
            ToggleCopy copy = ReadToggleCopy(ReadSettingsWindowSource(), AutoHideCallbackAnchor, "전체화면 자동 숨김 행");

            // (가-1) 주어를 빼면 반드시 빨개진다 — 옛 라벨이 정확히 이 상태였다(「자동으로 물러나기」).
            string subjectless = copy.Label;
            for (int i = 0; i < SurfaceNouns.Length; i++) subjectless = subjectless.Replace(SurfaceNouns[i], string.Empty);
            Assert.AreNotEqual(copy.Label, subjectless,
                "변이가 실제로 일어나지 않았다 — 라벨에 표면 낱말이 없으면 이 대조는 아무것도 재지 않는다.");
            Assert.IsFalse(LabelNamesWhatRetreats(subjectless, out _),
                $"주어를 뺀 라벨 「{subjectless}」을 판정기가 통과시켰다 — 판정기가 죽었다.");

            // (가-2) 조건절을 빼면 반드시 빨개진다.
            string conditionless = copy.Caption.Replace(GameWord, string.Empty);
            Assert.AreNotEqual(copy.Caption, conditionless, "변이가 일어나지 않았다 — 캡션에 조건 낱말이 없다.");
            Assert.IsFalse(CaptionKeepsGameConditionOnCharacter(conditionless, out _),
                $"조건절을 뺀 캡션 「{conditionless}」을 판정기가 통과시켰다 — 판정기가 죽었다.");

            // (가-3) 춤 절을 빼면 반드시 빨개진다(옛 캡션이 축 5를 빠뜨린 상태가 이것이다).
            string danceless = copy.Caption.Replace(DanceWord, string.Empty);
            Assert.AreNotEqual(copy.Caption, danceless, "변이가 일어나지 않았다 — 캡션에 춤 낱말이 없다.");
            Assert.IsFalse(CaptionPutsDanceOutsideGameCondition(danceless, out _),
                $"춤을 뺀 캡션 「{danceless}」을 판정기가 통과시켰다 — 판정기가 죽었다.");

            // (나-1) 조건 없이 캐릭터 은신을 말하는 라벨 — 표면 낱말은 있으므로 걸러지는 규칙이 하나로 좁혀진다.
            Assert.IsFalse(
                LabelNamesWhatRetreats(SurfaceNouns[0] + "부터 물러나고 " + CharacterNoun + "도 숨기", out _),
                "조건 없이 캐릭터 은신을 말하는 라벨을 통과시켰다 — 2026-08-31 신고로 닫힌 문이다.");

            // (나-2) 방아쇠를 「게임」으로 좁힌 라벨(옛 라벨의 병) — 표면 낱말은 있다.
            Assert.IsFalse(
                LabelNamesWhatRetreats("전체화면 " + GameWord + " 감지 시 " + SurfaceNouns[0] + "부터 물러나기", out _),
                "방아쇠를 「게임」으로 좁힌 라벨을 통과시켰다 — 등급 1에서도 표면은 물러난다.");

            // (나-3) D5 함정 — 조건절이 춤까지 지배하는 캡션. 조건절 자체는 올바른 자리에 있으므로
            //        이 표본이 걸리는 규칙은 춤 판정기 하나다(두 판정기가 서로 독립임을 보인다).
            string d5 = GameWord + "이면 " + CharacterNoun + "까지 숨고 " + DanceWord + "도 멈춥니다.";
            Assert.IsTrue(CaptionKeepsGameConditionOnCharacter(d5, out _),
                "D5 표본은 조건절 판정에서는 통과해야 한다 — 그래야 춤 판정기가 독립으로 걸린다는 것이 보인다.");
            Assert.IsFalse(CaptionPutsDanceOutsideGameCondition(d5, out _),
                $"「{d5}」을 통과시켰다 — 「게임일 때만 춤이 멈춘다」로 읽히는 형태이고, 실제로는 등급 1에서도 멈춘다.");

            // (나-4) 음성 대조 — 판정기가 「항상 거짓」이 아님을 보인다(조립한 표본으로).
            Assert.IsTrue(
                LabelNamesWhatRetreats("전체화면 " + AppWord + "이 뜨면 " + SurfaceNouns[0] + "부터 물러나기", out string okLabelWhy),
                $"올바른 형태의 라벨을 판정기가 거부했다 — {okLabelWhy}");
            string okCaption = DanceWord + "도 멈추고, " + GameWord + "이면 " + CharacterNoun + "까지 숨어요. 끄면 " +
                SurfaceNouns[0] + "이 막는 클릭까지 그대로 남아요.";
            Assert.IsTrue(CaptionKeepsGameConditionOnCharacter(okCaption, out string okCaptionWhy),
                $"올바른 형태의 캡션을 조건절 판정기가 거부했다 — {okCaptionWhy}");
            Assert.IsTrue(CaptionPutsDanceOutsideGameCondition(okCaption, out string okDanceWhy),
                $"올바른 형태의 캡션을 춤 판정기가 거부했다 — {okDanceWhy}");

            // (다) 추출기 대조 — 주석 속 인용문과 로그 문자열을 문안으로 집지 않는가.
            //     이 합성 소스에는 앵커가 <b>주석에도</b> 한 번 들어 있다. 주석을 지우지 않으면
            //     「앵커가 두 번」으로 실패하고, 지우면 코드의 한 곳만 남는다 — 지우기가 살아 있다는 증거다.
            string synthetic =
                "            // 옛 라벨은 \"전체화면 " + GameWord + " 감지 시 자동 숨김\"이었다(" +
                AutoHideCallbackAnchor + " 시절).\n" +
                "            t = card." + nameof(SettingsCardBuilder.AddToggle) + "(\"k\", \"라벨자리\", v,\n" +
                "                on =>\n" +
                "                {\n" +
                "                    " + AutoHideCallbackAnchor + "(on);\n" +
                "                    Debug.Log($\"머리{(on ? \"켬\" : \"끔\")}꼬리\");\n" +
                "                },\n" +
                "                " + CaptionParameterName + ": \"캡션자리\");\n";
            ToggleCopy fromSynthetic = ReadToggleCopy(synthetic, AutoHideCallbackAnchor, "합성 표본");
            Assert.AreEqual("라벨자리", fromSynthetic.Label,
                "추출기가 라벨 자리를 잘못 집었다 — 주석 속 인용문이나 로그 문자열을 집으면 화면에 없는 " +
                "문장을 판정하게 된다(실제로 이 행의 인수 사이에는 옛 캡션을 따옴표째 인용한 주석이 있다).");
            Assert.AreEqual("캡션자리", fromSynthetic.Caption,
                "추출기가 캡션 자리를 잘못 집었다 — 보간 문자열 구멍 안의 따옴표에 걸렸는지 확인하라.");
        }

        [Test]
        public void 판정에_쓰는_낱말이_프로덕션_문장에_실재한다()
        {
            // ★ 니들을 쓸 수밖에 없을 때는 그것이 <b>실재하는지</b>를 같은 테스트에서 못박는다
            //   (CLAUDE.md 협업 프로토콜). 등급 설명은 프로덕션이 런타임에 만드는 문장이므로,
            //   낱말이 바뀌면 여기서 <b>시끄럽게</b> 빨개진다 — 조용히 초록이 될 수 없다.
            string panelsOnly = ForeignFullscreenTierPolicy.Describe(ForeignFullscreenTier.PanelsOnly);
            string full = ForeignFullscreenTierPolicy.Describe(ForeignFullscreenTier.Full);

            Assert.IsNotEmpty(SurfaceNouns, "표면 낱말 목록이 비었다 — 빈 목록 위의 순회는 아무것도 재지 않는다.");
            for (int i = 0; i < SurfaceNouns.Length; i++)
            {
                StringAssert.Contains(SurfaceNouns[i], panelsOnly,
                    $"표면 낱말 「{SurfaceNouns[i]}」이 등급 1 설명에 없다 — 프로덕션이 다른 낱말을 쓰기 시작했다면 " +
                    "이 목록을 함께 고쳐라. 고치지 않으면 라벨 판정이 죽은 니들 위에서 돌게 된다.");
            }
            StringAssert.Contains(AppWord, panelsOnly,
                $"넓은 낱말 「{AppWord}」이 등급 1 설명에 없다 — 방아쇠 좁힘 판정의 기준이 사라졌다.");
            StringAssert.Contains(CharacterNoun, full,
                $"「{CharacterNoun}」이 등급 2 설명에 없다 — 캡션 조건절 판정의 기준이 사라졌다.");
            StringAssert.Contains(GameWord, full,
                $"「{GameWord}」이 등급 2 설명에 없다 — 캡션 조건절 판정의 기준이 사라졌다.");

            // 음성 대조 — 위 포함 검사가 「항상 참」이 아님을 보인다.
            Assert.IsFalse(panelsOnly.Contains(DeadNeedleSentinel),
                "있을 수 없는 낱말이 등급 1 설명에서 발견됐다 — 포함 검사가 무엇이든 참으로 만들고 있다.");

            // 춤 낱말은 등급 술어에 없다. 같은 카드의 형제 행(음악 춤)에서 교정한다 —
            // 그 행의 라벨이 사용자에게 이 동작을 부르는 이름이다.
            ToggleCopy dance = ReadToggleCopy(ReadSettingsWindowSource(), DanceCallbackAnchor, "음악 춤 행");
            StringAssert.Contains(DanceWord, dance.Label,
                $"형제 행의 라벨 「{dance.Label}」에 「{DanceWord}」이 없다 — 화면이 이 동작을 다른 이름으로 " +
                "부르기 시작했다면 캡션 판정의 낱말도 함께 고쳐라.");
        }

        // ============================================================================
        // 폭 — 이 자리에는 말줄임이 없다. 넘치면 카드 밖으로 흘러나가고 아무것도 빨개지지 않는다
        // ============================================================================

        /// <summary>
        /// <see cref="UiChrome.PlaceTopLeft"/>의 폭 인수를 <b>프로덕션 호출부에서 읽는다</b>.
        ///
        /// <para>★ <b>이 상자 폭에는 이름 붙은 상수가 없다</b> — <c>SettingsControls.BeginRow</c>의
        /// <b>호출부 리터럴</b>이다. 그래서 숫자를 베끼는 대신(<c>CLAUDE.md</c>: 상한값은 그 상수를
        /// 참조해서 검증한다) 그 호출부에서 <b>그대로 읽어 온다</b> — 상자를 넓히거나 좁히면 이 감사의
        /// 상한도 <b>따라 움직인다</b>. 값이 식(상수 참조·산술)으로 바뀌면 파싱이 실패해 <b>빨갛게</b>
        /// 멈추므로, 그때는 그 상수를 참조하도록 고치면 된다(조용히 통과하지 않는다).</para>
        /// </summary>
        private static float ReadBoxWidth(string source, string anchor, string role)
        {
            string code = SourceTextScanner.BlankCommentsAndStrings(source, null);
            Assert.AreEqual(source.Length, code.Length, "스캐너가 길이를 바꿨다 — 위치가 1:1이 아니다.");

            int at = IndexOfExactlyOnce(code, anchor, role);
            int open = code.IndexOf('(', at);
            Assert.GreaterOrEqual(open, 0, $"{role}: 여는 괄호를 찾지 못했다.");
            int close = MatchingParen(code, open, role);

            List<int[]> args = ArgumentSpans(code, open, close);
            int widthIndex = PlaceTopLeftWidthIndex();
            Assert.Greater(args.Count, widthIndex,
                $"{role}: 인수를 {args.Count}개밖에 못 갈랐다 — 폭은 {widthIndex}번째 인수다.");

            string raw = code.Substring(args[widthIndex][0], args[widthIndex][1] - args[widthIndex][0]).Trim();
            string number = raw.EndsWith("f", StringComparison.OrdinalIgnoreCase)
                ? raw.Substring(0, raw.Length - 1) : raw;
            Assert.IsTrue(
                float.TryParse(number, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float width),
                $"{role}: 폭 인수 \"{raw}\"를 수로 읽지 못했다 — 상수나 식으로 바뀌었다면 그 상수를 " +
                "참조하도록 이 감사를 고쳐라. 숫자를 여기 베끼지 마라(그 순간 기준과 대상이 갈라진다).");
            Assert.Greater(width, 0f, $"{role}: 폭이 0 이하로 읽혔다({width}) — 0 상자에는 무엇이든 들어가지 않는다.");
            return width;
        }

        /// <summary>폭이 몇 번째 인수인가 — <b>손으로 세지 않고</b> 리플렉션으로 읽는다.
        /// <para>위 문자열 전용 <see cref="ParameterIndex"/>와 합치지 않은 이유는 <b>기대 타입이 다르기</b>
        /// 때문이다(폭은 <c>float</c>). 합치면 「문자열인가」라는 단언이 사라진다.</para></summary>
        private static int PlaceTopLeftWidthIndex()
        {
            MethodInfo found = null;
            foreach (MethodInfo m in typeof(UiChrome).GetMethods(BindingFlags.Public | BindingFlags.Static))
            {
                if (!string.Equals(m.Name, nameof(UiChrome.PlaceTopLeft), StringComparison.Ordinal)) continue;
                Assert.IsNull(found,
                    $"{nameof(UiChrome.PlaceTopLeft)} 오버로드가 둘 이상이다 — 어느 것이 이 호출인지 알 수 없다. " +
                    "오버로드가 생겼다면 인수 모양으로 가르도록 이 감사를 고쳐라.");
                found = m;
            }
            Assert.IsNotNull(found,
                $"{nameof(UiChrome.PlaceTopLeft)}를 리플렉션으로 찾지 못했다 — 배치 창구가 바뀌었다면 " +
                "상자 폭을 읽는 이 경로도 함께 고쳐라.");

            ParameterInfo[] parameters = found.GetParameters();
            int index = -1;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (!string.Equals(parameters[i].Name, WidthParameterName, StringComparison.Ordinal)) continue;
                Assert.AreEqual(-1, index, $"'{WidthParameterName}' 매개변수가 둘 이상이다.");
                index = i;
            }
            Assert.GreaterOrEqual(index, 0,
                $"{nameof(UiChrome.PlaceTopLeft)}에 '{WidthParameterName}' 매개변수가 없다 — 이름이 바뀌었다면 " +
                "이 감사도 함께 고쳐라. 이 단언이 없으면 엉뚱한 인수를 폭으로 읽고도 초록이 된다.");
            Assert.AreEqual(typeof(float), parameters[index].ParameterType,
                $"'{WidthParameterName}' 매개변수가 더 이상 float이 아니다.");
            return index;
        }

        /// <summary>테스트가 <b>직접</b> 만드는 자. 프로덕션의 <c>SettingsControls.MeasuredWidth</c>를
        /// 부르지 않는다 — 그러면 그 함수가 틀어질 때 기대값도 함께 틀어져 아무것도 못 잰다
        /// (<c>CharacterStatReadoutTests</c>·<c>UiTextWidthModelTests</c>가 같은 판단을 이미 적어 뒀고,
        /// 그 함수의 본문은 지금 이 두 줄과 같다). 실측 경로가 <b>살아 있는지</b>는
        /// <c>PlatformParityAuditTests</c>가 리플렉션으로 잠그므로 여기서 중복하지 않는다.</summary>
        private static float Ink(UnityEngine.UI.Text text, string content)
        {
            text.text = content ?? string.Empty;
            return Mathf.Ceil(text.preferredWidth);
        }

        [Test]
        public void 자동숨김_라벨과_캡션이_상자_폭_안에_들어간다()
        {
            ToggleCopy copy = ReadToggleCopy(ReadSettingsWindowSource(), AutoHideCallbackAnchor, "전체화면 자동 숨김 행");

            string controls = ReadSettingsControlsSource();
            float labelBox = ReadBoxWidth(controls, LabelBoxAnchor, "라벨 상자");
            float captionBox = ReadBoxWidth(controls, CaptionBoxAnchor, "캡션 상자");
            Assert.AreNotEqual(labelBox, captionBox,
                $"두 상자 폭이 같게 읽혔다({labelBox} / {captionBox}) — 파서가 같은 자리를 두 번 읽었을 수 있다. " +
                "진짜로 같아졌다면 이 단언을 그때 함께 고쳐라.");

            var host = new GameObject("자동숨김문안폭측정", typeof(RectTransform), typeof(Canvas));
            try
            {
                host.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

                // 프로덕션과 <b>같은 글꼴 크기</b>로 잰다(라벨 FontBody / 캡션 FontCaption). 숫자를 베끼지 않고
                // 그 상수를 참조한다. wrap은 AddText 기본값(false = Overflow)이라 실제 행과 같다.
                UnityEngine.UI.Text labelProbe = UiChrome.AddText(host.transform, "LabelProbe",
                    UiChrome.FontBody, TextAnchor.MiddleLeft, UiChrome.TextPrimary);
                UnityEngine.UI.Text captionProbe = UiChrome.AddText(host.transform, "CaptionProbe",
                    UiChrome.FontCaption, TextAnchor.MiddleLeft, UiChrome.InkMeta);

                // ---- 양성 대조: 폰트가 실제로 재는가 ----
                //   0을 재고 「들어간다」고 말하면 그게 거짓 초록이다(이 저장소 거짓 통과 #4의 형태).
                float hangul = Ink(labelProbe, "가나다라마");
                float latin = Ink(labelProbe, "abcde");
                Assert.Greater(hangul, 0f,
                    "한글 5자의 폭이 0이다 — 폰트가 올라오지 않았다. 아래 숫자는 전부 무효이므로 이 판정을 " +
                    "믿지 말고 PlayMode(UiTextWidthModelTests와 같은 자리)에서 다시 재라.");
                Assert.Greater(hangul, latin,
                    $"한글 5자({hangul:F1}pt)가 라틴 5자({latin:F1}pt)보다 넓지 않다 — 자가 이상하다.");

                float labelInk = Ink(labelProbe, copy.Label);
                float captionInk = Ink(captionProbe, copy.Caption);

                Debug.Log($"[자동숨김문안폭-TEST] 라벨 {labelInk:F1}pt / 상자 {labelBox:F0}pt " +
                          $"(여유 {labelBox - labelInk:F1}) · 캡션 {captionInk:F1}pt / 상자 {captionBox:F0}pt " +
                          $"(여유 {captionBox - captionInk:F1}). 글꼴 {UiChrome.FontBody}/{UiChrome.FontCaption}pt. " +
                          "한글 글리프는 OS 폴백에서 오므로 이 수는 이 플랫폼의 값이다.");

                Assert.LessOrEqual(labelInk, labelBox,
                    $"라벨 「{copy.Label}」이 {labelInk:F1}pt로 상자 {labelBox:F0}pt를 넘는다 — 이 Text는 " +
                    "Overflow라 잘리지 않고 카드 밖으로 흘러나간다. 게다가 라벨이 길어지면 같은 행의 단축키 칩이 " +
                    "labelText.preferredWidth + 8f 자리에서 함께 밀린다. 문안을 줄이는 것은 design-narrative 소관이다.");
                Assert.LessOrEqual(captionInk, captionBox,
                    $"캡션 「{copy.Caption}」이 {captionInk:F1}pt로 상자 {captionBox:F0}pt를 넘는다 — 잘리지 않고 " +
                    "카드 밖으로 흘러나간다(말줄임이 없는 자리다). 이 행의 여유는 원래 얇다고 보고된 자리다.");

                // ---- 음성 대조: 이 자가 「안 들어간다」고 말할 수 있는가 ----
                Assert.Greater(Ink(labelProbe, copy.Label + copy.Label + copy.Label), labelBox,
                    "라벨을 세 번 이어 붙여도 상자에 들어간다고 나온다 — 위 「들어간다」 판정이 공허하다.");
                Assert.Greater(Ink(captionProbe, copy.Caption + copy.Caption + copy.Caption), captionBox,
                    "캡션을 세 번 이어 붙여도 상자에 들어간다고 나온다 — 위 「들어간다」 판정이 공허하다.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }
    }
}
