using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// Windows 기동 초반 ~2초 흰 배경 노출 버그의 수정을 잠그는 회귀 테스트
    /// (2026-09-06 사용자 실기 확인 → 2026-09-07 수정, dev-platform 라운드).
    ///
    /// ============================================================================
    /// 신고 & 확정된 원인 (다른 라운드가 진단만 확정, 이번 라운드가 수정)
    /// ============================================================================
    /// Windows에서 StickMate 실행 초반 ~2초간 화면 전체 크기의 불투명 근백색(0.94,0.94,0.94)
    /// 배경 위에 캐릭터만 정상적으로 보이는 버그가 실기로 확인됐다(작업표시줄과는 무관).
    ///
    /// <c>WindowsOverlayStateEnforcer.TickFullScreenBounds()</c>는 자체 타이머
    /// <c>_fullScreenTimer</c>가 <see cref="OverlayStateReapplyPolicy.ReapplyIntervalSeconds"/>
    /// (0.5초)를 채울 때까지 전체화면→창모드 전환 시도 자체를 하지 않는다. 그 전환이 실제로
    /// 걸려야 라이브러리(UniWinC)의 보더리스/투명 네이티브 처리가 먹히고, 그 전까지는 씬 카메라의
    /// 배경색이 OS 레벨 투명 마킹 이전이라 알파가 무시된 채 RGB 그대로 불투명하게 그려진다.
    ///
    /// 문제는 창 부착이 감지된 시점(<c>_attachDetected</c>가 참이 되는 지점)에서 이 타이머가
    /// <b>프리로드되지 않았다</b>는 것이다 — 그래서 부착 직후에도 다시 처음부터 0.5초를 채워야
    /// <c>TickFullScreenBounds()</c>의 첫 시도가 일어났다. 같은 파일 안의 다른 세 재무장 지점
    /// (<c>TickDisplayTopology</c>의 재무장, <c>ReArmFullScreenFitForNewTarget</c>,
    /// <c>ReArmFullScreenFitAfterNativeWindowMove</c>)은 전부 이미
    /// <c>_fullScreenTimer = ReapplyIntervalSeconds;</c> 관용구로 "다음 틱에서 곧바로 1회"를
    /// 보장하고 있었는데, 정작 최초 부착 지점에만 이 관용구가 빠져 있었다.
    ///
    /// ============================================================================
    /// 처방 — 순수 타이밍 수정 (재시도 주기·상한·이후 로직은 무변경)
    /// ============================================================================
    /// <c>_attachDetected = true;</c> 블록 안에 같은 관용구를 추가해, 그 직후 같은 프레임에서
    /// 도는 <c>TickFullScreenBounds()</c> 첫 시도가 신규 0.5초를 기다리지 않고 곧바로 통과하게
    /// 한다. macOS판(<c>MacOverlayStateEnforcer</c>, 필드명 <c>_timerFullScreen</c>)에도 코드
    /// 구조상 완전히 같은 갭이 있어(같은 두 재무장 지점이 같은 관용구를 이미 쓰고 있었다) 같은
    /// 라운드에 함께 고쳤다 — CLAUDE.md "플랫폼 동시 검토" 원칙.
    ///
    /// ============================================================================
    /// 왜 타입 리플렉션이 아니라 소스 텍스트 스캔인가
    /// ============================================================================
    /// <c>WindowsOverlayStateEnforcer</c>는 파일 전체가 <c>#if UNITY_STANDALONE_WIN</c> 안이고,
    /// <c>MacOverlayStateEnforcer</c>는 <c>#if UNITY_STANDALONE_OSX</c> 안이다. 이 개발 머신의
    /// 활성 빌드 타깃이 무엇이냐에 따라 둘 중 하나(또는 둘 다 아닌 경우)만 컴파일되므로, 타입
    /// 리플렉션 기반 검사는 반대편 타깃에서 EditMode를 돌리면 <b>타입이 아예 없어 아무것도 못
    /// 본다</b>(CLAUDE.md "활성 빌드 타깃 규칙"). 그래서 이 저장소의 확립된 관례
    /// (<c>OverlayResizeRatchetTests</c>, <c>TopmostZOrderWatchdogTests</c>)를 그대로 따라
    /// <b>소스 파일을 직접 읽어</b> 타깃과 무관하게 두 플랫폼을 한 번에 검증한다.
    /// </summary>
    public class FullScreenAttachTimerPreloadTests
    {
        private static string PlatformRoot => Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform");

        private static string ReadWindowsEnforcer()
            => File.ReadAllText(Path.Combine(PlatformRoot, "Windows", "WindowsOverlayStateEnforcer.cs"));

        private static string ReadMacEnforcer()
            => File.ReadAllText(Path.Combine(PlatformRoot, "MacOS", "MacOverlayStateEnforcer.cs"));

        /// <summary>주석을 지운 텍스트에서 "_attachDetected = true;" 직후부터 그다음
        /// "TickFullScreenBounds();" <b>호출</b>(정의부 "private void TickFullScreenBounds()"는
        /// 뒤에 세미콜론이 없어 이 패턴과 겹치지 않는다) 사이만 잘라낸다 — 부착 감지 블록과 그
        /// 블록이 끝난 직후 같은 프레임에 도는 첫 호출 지점 사이에 우리가 정말 그 자리를 잘랐는지
        /// 확인하기 위함이다. 파일에 다른 위치(재무장 함수 3~4곳)에도 같은 필드 대입이 있으므로
        /// 전체 파일 텍스트에 대고 "존재하는가"만 물으면 그 다른 자리들과 헷갈릴 수 있다 —
        /// 반드시 이 구간으로 좁혀서 검사한다.</summary>
        private static string ExtractAttachToFirstFullScreenTick(string source)
        {
            string stripped = StripLineComments(source);
            int attachIdx = stripped.IndexOf("_attachDetected = true;");
            Assert.Greater(attachIdx, -1,
                "_attachDetected = true; 대입을 찾지 못했다 — 부착 감지 지점 자체가 사라졌거나 이름이 바뀌었다.");

            int callIdx = stripped.IndexOf("TickFullScreenBounds();", attachIdx);
            Assert.Greater(callIdx, -1,
                "부착 감지 이후 TickFullScreenBounds() 호출을 찾지 못했다 — 같은 프레임에서 곧바로 돌던 " +
                "호출 배선 자체가 사라졌다.");

            return stripped.Substring(attachIdx, callIdx - attachIdx);
        }

        private static string StripLineComments(string source)
            => Regex.Replace(source, @"//[^\n]*", string.Empty);

        [Test]
        public void Windows_부착감지_구간에_전체화면타이머_프리로드가_있다()
        {
            string segment = ExtractAttachToFirstFullScreenTick(ReadWindowsEnforcer());

            bool preloaded = Regex.IsMatch(segment, @"_fullScreenTimer\s*=\s*ReapplyIntervalSeconds\s*;");

            Assert.IsTrue(preloaded,
                "WindowsOverlayStateEnforcer의 _attachDetected 블록 안에 " +
                "'_fullScreenTimer = ReapplyIntervalSeconds;' 프리로드가 없다. 이게 없으면 부착 직후에도 " +
                "TickFullScreenBounds()의 첫 시도가 다시 0.5초를 기다려야 하고, 그 동안 전체화면→창모드 " +
                "전환(투명 네이티브 처리의 전제조건)이 시도조차 되지 않아 근백색 배경이 불투명하게 " +
                "노출된다(2026-09-06 Windows 실기 확인 버그).");
        }

        [Test]
        public void macOS_부착감지_구간에_전체화면타이머_프리로드가_있다()
        {
            string segment = ExtractAttachToFirstFullScreenTick(ReadMacEnforcer());

            bool preloaded = Regex.IsMatch(segment, @"_timerFullScreen\s*=\s*ReapplyIntervalSeconds\s*;");

            Assert.IsTrue(preloaded,
                "MacOverlayStateEnforcer의 _attachDetected 블록 안에 " +
                "'_timerFullScreen = ReapplyIntervalSeconds;' 프리로드가 없다. Windows판과 코드 구조가 " +
                "동일해 같은 0.5초 사전 대기 갭이 있으므로(CLAUDE.md 플랫폼 동시 검토) 같은 자리에 " +
                "같은 관용구가 있어야 한다.");
        }

        /// <summary>대조: 이 관용구가 <b>일반적으로 존재하는 이름</b>이 아니라 실제로 이 파일이 이미
        /// 다른 세 곳에서 쓰고 있던 것과 <b>같은 상수</b>를 참조한다는 것까지 확인한다 — 단순히
        /// "_fullScreenTimer = 0.5f;" 같은 매직넘버 사본으로 우연히 통과하는 거짓 초록을 막는다
        /// (CLAUDE.md "테스트에 프로덕션 상수를 숫자로 베끼지 않는다").</summary>
        [Test]
        public void Windows_프리로드는_재무장_지점들과_같은_상수를_참조한다()
        {
            string source = StripLineComments(ReadWindowsEnforcer());

            // 로컬 상수 자체가 플랫폼 중립 정본을 가리키는지 — 값을 베끼지 않고 참조하는지.
            Assert.IsTrue(
                Regex.IsMatch(source,
                    @"ReapplyIntervalSeconds\s*=\s*OverlayStateReapplyPolicy\.ReapplyIntervalSeconds\s*;"),
                "로컬 ReapplyIntervalSeconds가 더는 플랫폼 중립 정본을 참조하지 않는다 — 값이 두 벌로 " +
                "갈라질 위험이 있다.");

            // 재무장 관용구(_fullScreenTimer = ReapplyIntervalSeconds;)가 이 파일에서 최소 4곳
            // (최초 부착 1 + 기존 재무장 3)에서 쓰이는지 — 이번에 추가한 자리가 기존 관례와
            // 같은 모양임을 숫자로 못박는다.
            int occurrences = Regex.Matches(source, @"_fullScreenTimer\s*=\s*ReapplyIntervalSeconds\s*;").Count;
            Assert.GreaterOrEqual(occurrences, 4,
                $"'_fullScreenTimer = ReapplyIntervalSeconds;' 관용구가 {occurrences}곳뿐이다 — " +
                "최초 부착 지점 프리로드를 포함해 최소 4곳(부착 1 + 기존 재무장 3: 디스플레이 " +
                "토폴로지 재무장 / 모니터 재선택 / 네이티브 이동 후 재무장)이어야 한다.");
        }

        [Test]
        public void macOS_프리로드는_재무장_지점들과_같은_상수를_참조한다()
        {
            string source = StripLineComments(ReadMacEnforcer());

            Assert.IsTrue(
                Regex.IsMatch(source,
                    @"ReapplyIntervalSeconds\s*=\s*OverlayStateReapplyPolicy\.ReapplyIntervalSeconds\s*;"),
                "로컬 ReapplyIntervalSeconds가 더는 플랫폼 중립 정본을 참조하지 않는다.");

            // macOS는 네이티브 이동 후 재무장(ReArmFullScreenFitAfterNativeWindowMove)이 없다
            // (SetBorderless가 프레임을 건드리지 않아 필요 없다 — 클래스 문서 참고). 그래서 기대치는
            // Windows보다 하나 적은 최소 3곳(부착 1 + 기존 재무장 2: 디스플레이 토폴로지 재무장 /
            // 모니터 재선택)이다.
            int occurrences = Regex.Matches(source, @"_timerFullScreen\s*=\s*ReapplyIntervalSeconds\s*;").Count;
            Assert.GreaterOrEqual(occurrences, 3,
                $"'_timerFullScreen = ReapplyIntervalSeconds;' 관용구가 {occurrences}곳뿐이다 — " +
                "최초 부착 지점 프리로드를 포함해 최소 3곳(부착 1 + 기존 재무장 2)이어야 한다.");
        }

        /// <summary>0.5초라는 체감 지연의 근거가 되는 실제 상수값을 잠근다 — 진단 문서의 "0.5초"가
        /// 숫자로 어긋나면 이 테스트가 먼저 빨개져야 한다.</summary>
        [Test]
        public void 재적용_간격_상수는_0점5초다()
        {
            Assert.AreEqual(0.5f, OverlayStateReapplyPolicy.ReapplyIntervalSeconds,
                "이 상수가 바뀌면 위 두 프리로드 테스트가 잠그는 '단축 폭'의 크기도 바뀐다 — " +
                "진단 문서의 0.5초 서술과 실제 값이 갈라지지 않았는지 여기서 먼저 잡는다.");
        }
    }
}
