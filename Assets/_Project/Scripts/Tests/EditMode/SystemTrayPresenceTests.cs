using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using StickMate.Platform;
using UnityEngine;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 2026-09-03 (dev-platform) — <b>시스템 트레이 아이콘</b>의 규칙·배선·경계를 잠근다.
    /// 사용자 확정 지시 <i>"실행시 시스템 트레이에 표시되어야함"</i>.
    ///
    /// <para>실행부 <c>Platform/Windows/WindowsSystemTrayIcon.cs</c>는 이 개발 머신의 활성 타깃
    /// (macOS)에서 <b>컴파일되지 않는다</b>. 그래서 <c>AppSwitcherPresenceTests</c>가 세운 방식을
    /// 그대로 따라 두 갈래로 검사한다 — <b>(A) 순수 규칙은 실행해서</b>,
    /// <b>(B) 배선은 소스 텍스트로</b>.</para>
    ///
    /// <para><b>이 파일이 증명하지 않는 것(정직하게)</b>: Windows 실기에서 아이콘이 실제로 뜨는지,
    /// 셸의 콜백이 우리 프로시저에 배달되는지, 메뉴가 바깥 클릭으로 닫히는지는 <b>여기서 확인할 수
    /// 없다.</b> 이 머신에 Windows가 없다. 잠그는 것은 <b>규칙이 옳은가</b>와 <b>배선이 끊기지
    /// 않았는가</b>뿐이다.</para>
    /// </summary>
    public sealed class SystemTrayPresenceTests
    {
        private const string LogPrefix = "[트레이-TEST]";

        private static string ScriptsRoot =>
            Path.Combine(Application.dataPath, "_Project", "Scripts");

        private static string PlatformRoot => Path.Combine(ScriptsRoot, "Platform");

        private static string PolicyPath =>
            Path.Combine(PlatformRoot, "SystemTrayPresencePolicy.cs");

        private static string RouterPath =>
            Path.Combine(PlatformRoot, "SystemTrayCommandRouter.cs");

        private static string WinTrayPath =>
            Path.Combine(PlatformRoot, "Windows", "WindowsSystemTrayIcon.cs");

        private static string WinEnforcerPath =>
            Path.Combine(PlatformRoot, "Windows", "WindowsOverlayStateEnforcer.cs");

        private static string BridgePath =>
            Path.Combine(ScriptsRoot, "Interaction", "SystemTrayCommandBridge.cs");

        [TearDown]
        public void ClearRouter() => SystemTrayCommandRouter.Clear();

        // ====================================================================
        // (A) 순수 규칙 — 실행해서 검사한다
        // ====================================================================

        /// <summary>
        /// ★ Win32 상수 값을 못박는다. <b>"프로덕션 상수를 베낀 것"이 아니다</b> — 이 숫자들은
        /// Win32 SDK가 정한 <b>바깥의 사실</b>이고, 우리 상수가 그 사실과 일치하는지가 이 단언의
        /// 내용이다(CLAUDE.md: 기대값은 프로덕션이 아니라 외부 근거에서 온다).
        ///
        /// <para><b>왜 여기가 아니면 잡을 곳이 없는가</b>: <c>NIM_ADD</c>(0)와 <c>NIM_DELETE</c>(2)를
        /// 바꿔 적으면 <b>종료할 때 아이콘을 하나 더 추가하는</b> 조용한 좀비 생성기가 되는데,
        /// 그 오타는 이 머신에서 컴파일도 실행도 되지 않는다.</para>
        /// </summary>
        [Test]
        public void A1_Win32_상수가_SDK_사실과_일치한다()
        {
            Assert.AreEqual(0u, SystemTrayPresencePolicy.NotifyIconAdd, $"{LogPrefix} NIM_ADD는 0입니다.");
            Assert.AreEqual(1u, SystemTrayPresencePolicy.NotifyIconModify, $"{LogPrefix} NIM_MODIFY는 1입니다.");
            Assert.AreEqual(2u, SystemTrayPresencePolicy.NotifyIconDelete,
                $"{LogPrefix} NIM_DELETE는 2입니다. 이 값이 틀리면 종료 시 아이콘이 지워지지 않고 " +
                "좀비로 남습니다 — 그리고 좀비는 다음 실행이 지울 수 없습니다(다른 프로세스의 창을 " +
                "가리키는 삭제 요청을 셸이 받지 않습니다).");

            Assert.AreEqual(0x01u, SystemTrayPresencePolicy.NotifyIconFlagMessage, $"{LogPrefix} NIF_MESSAGE=0x01.");
            Assert.AreEqual(0x02u, SystemTrayPresencePolicy.NotifyIconFlagIcon, $"{LogPrefix} NIF_ICON=0x02.");
            Assert.AreEqual(0x04u, SystemTrayPresencePolicy.NotifyIconFlagTip, $"{LogPrefix} NIF_TIP=0x04.");
            Assert.AreEqual(0x07u, SystemTrayPresencePolicy.NotifyIconAddFlags,
                $"{LogPrefix} 추가 플래그 묶음이 세 비트(메시지/아이콘/툴팁) 전부가 아닙니다 — " +
                "하나라도 빠지면 그 항목이 <조용히 무시>됩니다(콜백이 빠지면 메뉴가 영영 안 열립니다).");

            Assert.AreEqual(0x0202u, SystemTrayPresencePolicy.MouseLeftButtonUp, $"{LogPrefix} WM_LBUTTONUP=0x0202.");
            Assert.AreEqual(0x0205u, SystemTrayPresencePolicy.MouseRightButtonUp, $"{LogPrefix} WM_RBUTTONUP=0x0205.");

            // WM_APP=0x8000. 그 아래(WM_USER 대역)는 컨트롤 클래스가 자기 용도로 쓰므로 트레이
            // 콜백에 쓰면 다른 메시지와 충돌한다.
            Assert.GreaterOrEqual(SystemTrayPresencePolicy.TrayCallbackMessage, 0x8000u,
                $"{LogPrefix} 콜백 메시지 ID가 WM_APP(0x8000) 아래입니다 — WM_USER 대역은 " +
                "다른 용도와 충돌합니다.");

            Assert.AreEqual("TaskbarCreated", SystemTrayPresencePolicy.ShellRestartMessageName,
                $"{LogPrefix} 셸 재시작 브로드캐스트 이름이 틀리면 explorer가 죽었다 살아난 뒤 " +
                "이 앱은 OS 어디에서도 보이지 않게 됩니다(작업표시줄 버튼도 Alt+Tab도 없습니다).");
        }

        /// <summary>
        /// 메뉴 항목 ID는 <b>0이면 안 된다</b>. <c>TrackPopupMenu</c>는 <c>TPM_RETURNCMD</c>일 때
        /// <b>사용자 취소</b>를 0으로 알리므로, ID가 0인 항목은 "취소"와 구분되지 않는다 —
        /// 그 항목은 <b>영원히 실행되지 않는다</b>.
        /// </summary>
        [Test]
        public void A2_메뉴_명령_ID가_0이_아니고_서로_다르다()
        {
            var ids = new List<int>();
            foreach (TrayMenuCommand command in SystemTrayPresencePolicy.MenuOrder)
            {
                int id = SystemTrayPresencePolicy.ToCommandId(command);
                Assert.AreNotEqual(0, id,
                    $"{LogPrefix} {command}의 메뉴 ID가 0입니다 — TrackPopupMenu의 '취소'와 " +
                    "구분되지 않아 이 항목은 영원히 실행되지 않습니다.");
                ids.Add(id);
            }

            Assert.AreEqual(ids.Count, ids.Distinct().Count(),
                $"{LogPrefix} 메뉴 ID가 중복됩니다 — 두 항목이 같은 동작으로 접힙니다.");

            // 열거형 전체(메뉴에 올리지 않은 값이 생기더라도)도 0을 쓰지 않아야 한다.
            foreach (TrayMenuCommand command in Enum.GetValues(typeof(TrayMenuCommand)))
            {
                Assert.AreNotEqual(0, (int)command,
                    $"{LogPrefix} {command}의 열거형 값이 0입니다(위와 같은 이유).");
            }
        }

        /// <summary>왕복이 성립하는가. 그리고 <b>모르는 값을 받지 않는가</b>.</summary>
        [Test]
        public void A3_메뉴_결과_해석이_왕복하고_모르는_값을_거부한다()
        {
            foreach (TrayMenuCommand command in SystemTrayPresencePolicy.MenuOrder)
            {
                int id = SystemTrayPresencePolicy.ToCommandId(command);
                Assert.IsTrue(SystemTrayPresencePolicy.TryResolveCommand(id, out TrayMenuCommand back),
                    $"{LogPrefix} {command}의 ID({id})를 되돌리지 못했습니다.");
                Assert.AreEqual(command, back, $"{LogPrefix} 왕복이 다른 명령으로 갑니다.");
            }

            Assert.IsFalse(SystemTrayPresencePolicy.TryResolveCommand(0, out _),
                $"{LogPrefix} 0(사용자 취소)을 명령으로 해석했습니다 — 메뉴를 그냥 닫기만 해도 " +
                "무언가가 실행되는 상태입니다. 그 무언가가 [종료]일 수 있습니다.");

            int unknown = SystemTrayPresencePolicy.MenuOrder
                .Select(SystemTrayPresencePolicy.ToCommandId).Max() + 1;
            Assert.IsFalse(SystemTrayPresencePolicy.TryResolveCommand(unknown, out _),
                $"{LogPrefix} 목록에 없는 ID({unknown})를 받았습니다.");
        }

        /// <summary>
        /// 토글 항목의 글자가 <b>상태에 따라 뒤집히는가</b>. 고정 문구를 쓰면 사용자는 지금 캐릭터가
        /// 숨어 있는지 아닌지 메뉴만 보고는 알 수 없다(절대 불변 원칙 1의 정신 — 표시된 것과 실제가
        /// 갈라지면 안 된다).
        /// </summary>
        [Test]
        public void A4_숨김_토글_글자가_상태에_따라_뒤집힌다()
        {
            string whenVisible = SystemTrayPresencePolicy.LabelFor(
                TrayMenuCommand.ToggleCharacterHidden, characterHidden: false);
            string whenHidden = SystemTrayPresencePolicy.LabelFor(
                TrayMenuCommand.ToggleCharacterHidden, characterHidden: true);

            Assert.AreNotEqual(whenVisible, whenHidden,
                $"{LogPrefix} 숨김/보이기 항목이 두 상태에서 같은 글자입니다 — 사용자는 메뉴만 보고 " +
                "지금 어느 쪽인지 알 수 없습니다.");

            // 나머지 항목은 상태와 무관해야 한다(엉뚱한 항목이 상태에 반응하면 그게 버그다).
            foreach (TrayMenuCommand command in SystemTrayPresencePolicy.MenuOrder)
            {
                if (command == TrayMenuCommand.ToggleCharacterHidden) continue;
                Assert.AreEqual(
                    SystemTrayPresencePolicy.LabelFor(command, false),
                    SystemTrayPresencePolicy.LabelFor(command, true),
                    $"{LogPrefix} {command}의 글자가 캐릭터 표시 상태에 반응합니다.");
            }

            foreach (bool hidden in new[] { false, true })
            {
                var labels = SystemTrayPresencePolicy.MenuOrder
                    .Select(c => SystemTrayPresencePolicy.LabelFor(c, hidden)).ToList();
                CollectionAssert.AllItemsAreNotNull(labels, $"{LogPrefix} 빈 글자 항목이 있습니다.");
                Assert.IsFalse(labels.Any(string.IsNullOrWhiteSpace),
                    $"{LogPrefix} 공백뿐인 메뉴 글자가 있습니다 — 셸이 빈 항목을 그립니다.");
                Assert.AreEqual(labels.Count, labels.Distinct().Count(),
                    $"{LogPrefix} 같은 글자의 메뉴 항목이 둘 이상입니다.");
            }
        }

        /// <summary>
        /// 되돌릴 수 없는 <b>[종료]</b>가 항상 <b>맨 마지막</b>이고 <b>구분선 뒤</b>에 있는가.
        /// 사용자가 오늘 못 찾은 것이 바로 종료였고, 그것이 목록 <b>중간에서 자리를 옮겨 다니면</b>
        /// 오조작이 난다.
        /// </summary>
        [Test]
        public void A5_종료가_맨_아래_구분선_뒤에_고정되어_있다()
        {
            TrayMenuCommand[] order = SystemTrayPresencePolicy.MenuOrder;
            Assert.Greater(order.Length, 0, $"{LogPrefix} 메뉴가 비어 있습니다.");
            Assert.AreEqual(TrayMenuCommand.Quit, order[order.Length - 1],
                $"{LogPrefix} [종료]가 마지막 항목이 아닙니다.");
            Assert.IsTrue(SystemTrayPresencePolicy.NeedsSeparatorBefore(TrayMenuCommand.Quit),
                $"{LogPrefix} [종료] 앞에 구분선이 없습니다 — 되돌릴 수 없는 명령이 나머지와 " +
                "붙어 있으면 오조작이 납니다.");

            foreach (TrayMenuCommand command in order)
            {
                if (command == TrayMenuCommand.Quit) continue;
                Assert.IsFalse(SystemTrayPresencePolicy.NeedsSeparatorBefore(command),
                    $"{LogPrefix} {command} 앞에도 구분선이 붙었습니다 — 구분선의 뜻(종료를 떼어 " +
                    "놓는다)이 희석됩니다.");
            }

            // 요구된 세 항목이 실제로 다 있는가.
            CollectionAssert.Contains(order, TrayMenuCommand.Quit, $"{LogPrefix} [앱 종료]가 없습니다.");
            CollectionAssert.Contains(order, TrayMenuCommand.ToggleCharacterHidden,
                $"{LogPrefix} [캐릭터 보이기/숨기기]가 없습니다.");
            CollectionAssert.Contains(order, TrayMenuCommand.OpenSettings,
                $"{LogPrefix} [설정 열기]가 없습니다.");
        }

        /// <summary>툴팁이 Win32 고정 배열(<c>szTip</c>)을 넘지 않는가. 넘으면 마샬러가 던진다.</summary>
        [Test]
        public void A6_툴팁이_szTip_한도_안에_있다()
        {
            Assert.IsNotNull(SystemTrayPresencePolicy.Tooltip, $"{LogPrefix} 툴팁이 null입니다.");
            Assert.Less(SystemTrayPresencePolicy.Tooltip.Length, SystemTrayPresencePolicy.MaxTooltipLength,
                $"{LogPrefix} 툴팁이 szTip 한도({SystemTrayPresencePolicy.MaxTooltipLength})를 넘습니다 — " +
                "널 종단 자리까지 포함해 '미만'이어야 합니다.");
            Assert.AreEqual(128, SystemTrayPresencePolicy.MaxTooltipLength,
                $"{LogPrefix} szTip의 요소 수는 SDK가 정한 128입니다(외부 사실).");
        }

        /// <summary>설치 상한이 실제로 멈추는가. 24시간 상주 앱에서 실패한 셸 호출이 영원히 반복되면 안 된다.</summary>
        [Test]
        public void A7_설치_재시도_상한이_실제로_멈춘다()
        {
            const int max = 3;

            Assert.IsTrue(SystemTrayPresencePolicy.ShouldAttemptInstall(false, false, false, 0, max),
                $"{LogPrefix} 첫 시도조차 하지 않습니다.");
            Assert.IsTrue(SystemTrayPresencePolicy.ShouldAttemptInstall(false, false, false, max - 1, max),
                $"{LogPrefix} 상한 직전에 멈췄습니다.");
            Assert.IsFalse(SystemTrayPresencePolicy.ShouldAttemptInstall(false, false, false, max, max),
                $"{LogPrefix} 상한에 도달했는데 계속 시도합니다.");

            Assert.IsFalse(SystemTrayPresencePolicy.ShouldAttemptInstall(true, false, false, 0, max),
                $"{LogPrefix} 옵트아웃을 무시합니다.");
            Assert.IsFalse(SystemTrayPresencePolicy.ShouldAttemptInstall(false, true, false, 0, max),
                $"{LogPrefix} 이미 설치됐는데 또 세웁니다 — 아이콘이 둘이 됩니다.");
            Assert.IsFalse(SystemTrayPresencePolicy.ShouldAttemptInstall(false, false, true, 0, max),
                $"{LogPrefix} 못 쓴다고 확인된 경로를 계속 두드립니다.");
            Assert.IsFalse(SystemTrayPresencePolicy.ShouldAttemptInstall(false, false, false, 0, 0),
                $"{LogPrefix} 상한 0(기능 끄기)이 존중되지 않습니다.");
        }

        /// <summary>
        /// ★ 셸 재시작 복구가 <b>시도 상한과 분리</b>되어 있는가 — 이 라운드의 설계 판단 하나가
        /// 여기 걸려 있다.
        ///
        /// <para>셸 재시작은 <b>실패가 아니라 외부 사건</b>이다. 상한으로 눌러 버리면 하루짜리
        /// 세션에서 explorer가 두어 번 죽는 순간 아이콘이 <b>영영</b> 돌아오지 않고, 이 앱은
        /// 작업표시줄 버튼도 Alt+Tab 항목도 없으므로 그때부터 <b>끌 방법이 사라진다</b>.</para>
        /// </summary>
        [Test]
        public void A8_셸_재시작_복구는_시도_상한과_무관하다()
        {
            // 시도를 이미 다 써 버린 상태여도 재설치는 성립해야 한다.
            Assert.IsFalse(SystemTrayPresencePolicy.ShouldAttemptInstall(false, false, false, 99, 3),
                $"{LogPrefix} 사전 조건: 이 입력은 상한 초과여야 합니다.");
            Assert.IsTrue(SystemTrayPresencePolicy.ShouldReinstallAfterShellRestart(false, false),
                $"{LogPrefix} 셸 재시작 복구가 시도 상한에 묶여 있습니다 — explorer가 몇 번 죽으면 " +
                "트레이가 영영 돌아오지 않고, 그러면 이 앱을 끌 방법이 사라집니다.");

            Assert.IsFalse(SystemTrayPresencePolicy.ShouldReinstallAfterShellRestart(true, false),
                $"{LogPrefix} 옵트아웃은 셸 재시작에도 존중돼야 합니다.");
            Assert.IsFalse(SystemTrayPresencePolicy.ShouldReinstallAfterShellRestart(false, true),
                $"{LogPrefix} 못 쓴다고 확인된 경로를 셸 재시작 때 되살립니다.");
        }

        /// <summary>좌·우 클릭 <b>둘 다</b> 메뉴를 열고, 다른 마우스 메시지는 열지 않는가.</summary>
        [Test]
        public void A9_메뉴_트리거는_좌우_클릭_뗌_뿐이다()
        {
            Assert.IsTrue(SystemTrayPresencePolicy.IsMenuTriggerMessage(
                SystemTrayPresencePolicy.MouseRightButtonUp), $"{LogPrefix} 우클릭이 메뉴를 열지 않습니다.");
            Assert.IsTrue(SystemTrayPresencePolicy.IsMenuTriggerMessage(
                SystemTrayPresencePolicy.MouseLeftButtonUp),
                $"{LogPrefix} 좌클릭이 메뉴를 열지 않습니다 — 이 앱에는 '본창 띄우기'라는 좌클릭 " +
                "기본 동작이 없으므로, 좌클릭에 아무 반응이 없으면 사용자는 아이콘이 죽었다고 " +
                "판단합니다(이 라운드가 고치려는 발견 불가능성과 같은 병입니다).");

            const uint wmMouseMove = 0x0200;
            const uint wmLButtonDown = 0x0201;
            Assert.IsFalse(SystemTrayPresencePolicy.IsMenuTriggerMessage(wmMouseMove),
                $"{LogPrefix} 커서가 지나가기만 해도 메뉴가 열립니다.");
            Assert.IsFalse(SystemTrayPresencePolicy.IsMenuTriggerMessage(wmLButtonDown),
                $"{LogPrefix} 누름(Down)에 열면 뗌(Up)에서 한 번 더 열립니다.");
            Assert.IsFalse(SystemTrayPresencePolicy.IsMenuTriggerMessage(0),
                $"{LogPrefix} 0을 트리거로 받았습니다.");
        }

        /// <summary>
        /// 배달판이 <b>조용히 성공하지 않는가</b>. 수신자가 없을 때 true를 돌려주면
        /// "메뉴는 보이는데 눌러도 아무 일이 없다"가 <b>정상으로 보고된다</b>.
        /// </summary>
        [Test]
        public void A10_수신자가_없으면_배달이_실패로_보고된다()
        {
            SystemTrayCommandRouter.Clear();
            Assert.IsFalse(SystemTrayCommandRouter.HasHandler, $"{LogPrefix} 사전 조건: 비어 있어야 합니다.");
            Assert.IsFalse(SystemTrayCommandRouter.Dispatch(TrayMenuCommand.Quit, "테스트"),
                $"{LogPrefix} 수신자가 없는데 배달 성공으로 보고했습니다 — 트레이가 먹통인 상태가 " +
                "정상으로 보입니다.");

            var received = new List<TrayMenuCommand>();
            string seenSource = null;
            SystemTrayCommandRouter.Register((c, s) => { received.Add(c); seenSource = s; }, () => true);

            Assert.IsTrue(SystemTrayCommandRouter.HasHandler, $"{LogPrefix} 등록이 반영되지 않았습니다.");
            Assert.IsTrue(SystemTrayCommandRouter.Dispatch(TrayMenuCommand.OpenSettings, "출처표시"),
                $"{LogPrefix} 등록된 수신자에게 배달하지 못했습니다.");
            CollectionAssert.AreEqual(new[] { TrayMenuCommand.OpenSettings }, received,
                $"{LogPrefix} 다른 명령이 배달됐습니다.");
            Assert.AreEqual("출처표시", seenSource,
                $"{LogPrefix} 출처 문자열이 전달되지 않았습니다 — 로그에서 '누가 껐는가'를 " +
                "잃습니다(QuitApplication이 그 값을 그대로 찍습니다).");
            Assert.IsTrue(SystemTrayCommandRouter.IsCharacterHidden(),
                $"{LogPrefix} 표시 상태 조회가 등록된 값을 읽지 않습니다.");
        }

        /// <summary>
        /// 수신자/조회가 <b>던져도</b> 트레이가 죽지 않는가. 이 호출들은 Win32 메시지 처리
        /// 흐름에서 이어져 오므로, 예외가 네이티브 프레임을 가로질러 나가면 무슨 일이 벌어지는지
        /// 이 머신에서 확인할 방법이 없다.
        /// </summary>
        [Test]
        public void A11_수신자가_던져도_트레이가_죽지_않는다()
        {
            SystemTrayCommandRouter.Register(
                (c, s) => throw new InvalidOperationException("의도된 실패"),
                () => throw new InvalidOperationException("의도된 실패"));

            Assert.IsFalse(SystemTrayCommandRouter.Dispatch(TrayMenuCommand.Quit, "테스트"),
                $"{LogPrefix} 예외가 난 배달을 성공으로 보고했습니다.");
            Assert.IsFalse(SystemTrayCommandRouter.IsCharacterHidden(),
                $"{LogPrefix} 표시 상태 조회 실패가 예외로 새어 나갑니다 — 메뉴 글자 하나 때문에 " +
                "트레이 전체가 죽습니다.");
        }

        // ====================================================================
        // (B) 배선 — 소스 텍스트로 검사한다
        // ====================================================================

        /// <summary>
        /// 규칙이 <b>중립 위치</b>에 있는가. 정책이 <c>Platform/Windows/</c> 안으로 들어가면
        /// (1) macOS가 물리적으로 호출할 수 없고 (2) 이 머신에서 <b>규칙을 실행해 검증할 방법 자체가
        /// 사라진다</b>(<c>FullscreenSuspendPolicy</c> 사고).
        /// </summary>
        [Test]
        public void B1_규칙이_플랫폼_중립_위치에_있다()
        {
            Assert.IsTrue(File.Exists(PolicyPath), $"{LogPrefix} 정책이 없습니다: {PolicyPath}");
            Assert.IsTrue(File.Exists(RouterPath), $"{LogPrefix} 배달판이 없습니다: {RouterPath}");

            foreach (string path in new[] { PolicyPath, RouterPath })
            {
                string src = StripCommentLines(File.ReadAllText(path));
                StringAssert.DoesNotContain("UNITY_STANDALONE_", src,
                    $"{LogPrefix} {Path.GetFileName(path)}에 플랫폼 분기가 들어왔습니다 — " +
                    "중립 파일의 존재 이유가 사라집니다.");
                StringAssert.DoesNotContain("DllImport", src,
                    $"{LogPrefix} {Path.GetFileName(path)}가 OS를 직접 부릅니다 — 사실 조회는 " +
                    "플랫폼 코드의 몫입니다.");
            }

            // 배달판은 Interaction을 몰라야 한다(의존 방향 유지). 이 0을 지키는 것이 설계다.
            StringAssert.DoesNotContain("StickMate.Interaction", StripCommentLines(File.ReadAllText(RouterPath)),
                $"{LogPrefix} 중립 배달판이 Interaction을 직접 참조합니다 — Platform → Interaction " +
                "역방향 의존이 생깁니다. 등록은 Interaction 쪽이 합니다.");
        }

        /// <summary>
        /// Windows 실행부가 <b>정책을 부르고</b>, <b>추가와 삭제를 모두</b> 하고, <b>실패를 삼키고</b>,
        /// <b>종료 훅</b>을 다는가.
        /// </summary>
        [Test]
        public void B2_Windows_실행부가_정책을_쓰고_종료시_아이콘을_지운다()
        {
            Assert.IsTrue(File.Exists(WinTrayPath),
                $"{LogPrefix} Windows 실행부가 없습니다: {WinTrayPath}");

            string code = StripCommentLines(File.ReadAllText(WinTrayPath));

            StringAssert.Contains(SystemTrayPresencePolicy.WindowsMechanismName, code,
                $"{LogPrefix} 택한 기전({SystemTrayPresencePolicy.WindowsMechanismName})이 " +
                "실행부에 없습니다.");

            // 판정/상수는 중립 정책에서 가져와야 한다 — 사본을 두면 이 머신이 검증할 수 없다.
            StringAssert.Contains(nameof(SystemTrayPresencePolicy.ShouldAttemptInstall), code,
                $"{LogPrefix} 재시도 상한 판정이 플랫폼 코드 안에 있습니다 — 이 머신은 그 폴더를 " +
                "컴파일조차 하지 않으므로 상한 규칙을 실행해 볼 수 없게 됩니다.");
            StringAssert.Contains(nameof(SystemTrayPresencePolicy.NotifyIconAdd), code,
                $"{LogPrefix} 아이콘 추가가 없습니다.");
            StringAssert.Contains(nameof(SystemTrayPresencePolicy.NotifyIconDelete), code,
                $"{LogPrefix} ★ 종료 시 아이콘 제거(NIM_DELETE)가 없습니다 — 좀비 아이콘이 " +
                "남습니다. 그리고 좀비는 다음 실행이 지울 수 없습니다(원장 패턴이 성립하지 않는 " +
                "이유는 실행부 클래스 문서 참고).");
            StringAssert.Contains(nameof(SystemTrayPresencePolicy.ShouldReinstallAfterShellRestart), code,
                $"{LogPrefix} 셸 재시작 복구가 없습니다.");

            StringAssert.Contains("Application.quitting", code,
                $"{LogPrefix} 종료 훅이 없습니다 — 씬 배선에 기대면 씬이 바뀔 때 조용히 죽습니다.");
            StringAssert.Contains("catch (Exception", code,
                $"{LogPrefix} 예외를 삼키지 않습니다 — 트레이가 없는 환경에서 부팅이 깨집니다. " +
                "아이콘이 없는 것은 불편이지 고장이 아닙니다.");

            // 우리 보조 창이 앱 전환 표면에 되나타나면 같은 날 라운드의 자책골이다.
            StringAssert.Contains(nameof(AppSwitcherPresencePolicy.WindowsToolWindowExStyleBit), code,
                $"{LogPrefix} 트레이 호스트 창에 WS_EX_TOOLWINDOW를 얹지 않습니다 — 작업표시줄/" +
                "Alt+Tab에서 애써 빠져 놓고 보조 창으로 되나타날 수 있습니다.");
        }

        /// <summary>
        /// ★ <b>실행부가 실제로 불리는가</b>, 그리고 <b>부착 판정보다 위에서</b> 불리는가.
        ///
        /// <para>순서가 이 검사의 본체다. <c>WindowsOverlayStateEnforcer.Update</c>는 오버레이 창이
        /// 아직 붙지 않았으면 <c>return</c>한다. 트레이 호출이 그 아래로 내려가면
        /// <b>부착이 영영 실패한 환경에서 트레이가 뜨지 않는다</b> — 그 환경은 화면에 캐릭터도
        /// 톱니도 없는 환경이므로, 정확히 <b>트레이가 유일한 탈출구인 상황</b>이다.</para>
        /// </summary>
        [Test]
        public void B3_실행부가_부착_판정보다_위에서_배선되어_있다()
        {
            string src = StripCommentLines(File.ReadAllText(WinEnforcerPath));

            const string tickCall = "WindowsSystemTrayIcon.Tick(";
            int tickAt = src.IndexOf(tickCall, StringComparison.Ordinal);
            Assert.Greater(tickAt, -1,
                $"{LogPrefix} 실행부가 호출되지 않습니다 — 파일만 있고 한 번도 실행되지 않는 " +
                "상태입니다(이 저장소가 반복해 겪은 조용한 실패 모양).");

            foreach (string gate in new[] { "if (_controller == null) return;", "if (!attached)" })
            {
                int gateAt = src.IndexOf(gate, StringComparison.Ordinal);
                Assert.Greater(gateAt, -1,
                    $"{LogPrefix} 기준으로 삼은 조기 반환('{gate}')을 찾지 못했습니다 — " +
                    "Update의 형태가 바뀌었다면 이 검사도 함께 갱신하세요. 그대로 두면 " +
                    "순서를 아무도 지키지 않습니다.");
                Assert.Less(tickAt, gateAt,
                    $"{LogPrefix} ★ 트레이 호출이 조기 반환('{gate}') <아래>에 있습니다. " +
                    "오버레이 부착이 실패한 환경에서는 트레이가 뜨지 않게 되고, 그 환경이 바로 " +
                    "화면에 캐릭터도 톱니도 없는 = 트레이가 유일한 탈출구인 상황입니다.");
            }
        }

        /// <summary>
        /// 배선이 <b>기존 진입점을 재사용</b>하는가. 특히 종료는 같은 날 <b>단 하나의 자리</b>로
        /// 정리됐다 — 여기서 <c>Application.Quit()</c>을 다시 부르면 그 정리가 즉시 무의미해지고,
        /// 에디터 분기를 한 곳에서 빠뜨리면 <b>그 경로만</b> 배치모드 테스트를 얼린다.
        /// </summary>
        [Test]
        public void B4_트레이_메뉴가_기존_진입점만_재사용한다()
        {
            Assert.IsTrue(File.Exists(BridgePath), $"{LogPrefix} 배선이 없습니다: {BridgePath}");
            string code = StripCommentLines(File.ReadAllText(BridgePath));

            StringAssert.Contains(nameof(StickMate.Interaction.AppControlDirector.QuitApplication), code,
                $"{LogPrefix} 종료가 단일 종료 진입점을 부르지 않습니다.");
            StringAssert.Contains(nameof(StickMate.Core.StickmanAgent.ToggleUserHidden), code,
                $"{LogPrefix} 숨김 토글이 기존 축(IsUserHiddenOnly)을 쓰지 않습니다 — 렌더러만 끄는 " +
                "옛 경로를 되살리면 안 됩니다.");
            StringAssert.Contains(nameof(StickMate.Interaction.SettingsWindow.Open), code,
                $"{LogPrefix} 설정 열기가 기존 진입점을 부르지 않습니다 — 배타 모달 정리는 그 함수 " +
                "한 곳이 책임집니다(진입점마다 정리 코드를 흩뿌리다 실제로 샌 적이 있습니다).");
            // ★ 2026-09-05 — 니들이 IsUserHiddenOnly에서 IsUserHidden으로 <b>좁아졌다</b>.
            //   글자는 이 항목이 실제로 바꾸는 축(_userHidden)에서만 나와야 한다. IsUserHiddenOnly는
            //   「축 2<b>만</b>으로 숨었는가」라 축 1(전체화면 게임)·축 4(다른 가상 데스크톱)가 함께
            //   켜지면 false이고, 그러면 이미 숨겨 둔 상태에서 글자가 「숨기기」로 나온다(원칙 1 위반).
            //   ★ 두 이름은 접두사 관계라 StringAssert.Contains로는 <b>구분되지 않는다</b> —
            //     "IsUserHiddenOnly"가 남아 있어도 "IsUserHidden" 검사는 통과한다. 그래서 낱말 경계로 잰다.
            var userHiddenAxis = new Regex(
                nameof(StickMate.Core.StickmanAgent.IsUserHidden) + @"\b", RegexOptions.CultureInvariant);

            // 대조 — 이 정규식이 정말로 둘을 가르는가. 가르지 못하면 아래 두 판정은 무의미하다.
            Assert.IsFalse(userHiddenAxis.IsMatch(nameof(StickMate.Core.StickmanAgent.IsUserHiddenOnly)),
                $"{LogPrefix} 검사기 교정 실패 — 낱말 경계 정규식이 " +
                $"{nameof(StickMate.Core.StickmanAgent.IsUserHiddenOnly)}까지 매칭합니다. " +
                "이 자가 눈금이 어긋났으므로 아래 판정을 신뢰할 수 없습니다.");

            Assert.IsTrue(userHiddenAxis.IsMatch(code),
                $"{LogPrefix} 메뉴 글자가 <이 항목이 바꾸는 축>(" +
                $"{nameof(StickMate.Core.StickmanAgent.IsUserHidden)})을 읽지 않습니다 — " +
                "글자와 동작이 갈라집니다(원칙 1).");
            Assert.AreEqual(0,
                CountOccurrences(code, nameof(StickMate.Core.StickmanAgent.IsUserHiddenOnly)),
                $"{LogPrefix} ★ 메뉴 글자가 표면 회수용 값" +
                $"({nameof(StickMate.Core.StickmanAgent.IsUserHiddenOnly)})으로 되돌아갔습니다. " +
                "그 값은 축 1·축 4가 켜지면 false라, 사용자가 이미 숨겨 둔 상태에서 글자가 " +
                "「숨기기」로 나오고 눌러도 화면이 그대로여서 메뉴가 고장 난 것처럼 보입니다 " +
                "(다른 가상 데스크톱에서는 트레이가 그대로 보이므로 실제로 도달 가능합니다).");

            Assert.AreEqual(0, CountOccurrences(code, "Application.Quit()"),
                $"{LogPrefix} ★ 종료 로직이 복제됐습니다. 같은 날 종료가 '단 하나의 자리'로 " +
                "모였고, 트레이는 그 자리를 <한 번 더 부를> 뿐이어야 합니다.");

            StringAssert.Contains(nameof(SystemTrayCommandRouter.Register), code,
                $"{LogPrefix} 배선이 스스로 등록하지 않습니다 — 등록이 없으면 메뉴는 보이는데 " +
                "눌러도 아무 일이 없습니다.");
            StringAssert.Contains("RuntimeInitializeOnLoadMethod", code,
                $"{LogPrefix} 등록이 씬 배선에 의존합니다 — 씬이 바뀌면 조용히 죽습니다.");
        }

        /// <summary>
        /// ★ 승인된 예외 3종이 <b>우리 자신의 창에만</b> 쓰이는가, 그리고 <b>승인되지 않은</b>
        /// 창 조작 API는 여전히 들어오지 않았는가(2026-09-03 리더 승인 조건 2).
        ///
        /// <para><c>UserAssetImmutabilityAuditTests</c>가 라인 단위 화이트리스트로 같은 것을 잠그지만,
        /// 그쪽은 <b>"승인된 형태인가"</b>를 묻고 이쪽은 <b>"인자가 우리 창 하나로 고정돼 있는가"</b>를
        /// 묻는다. 승인의 전제가 바로 그것이라 <b>다른 방법으로 한 번 더</b> 잰다.</para>
        ///
        /// <para><b>부재 단언이 섞여 있어 위험하다</b> — CLAUDE.md: 부재용 니들은 <b>썩으면 조용히
        /// 초록</b>이 된다. 그래서 같은 검사기가 <b>실재하는 표본</b>에서는 그 이름을 실제로 찾아내는지를
        /// 같은 테스트 안에서 대조로 못박는다(아래 (1)).</para>
        /// </summary>
        [Test]
        public void B5_승인된_예외가_자기_창에만_쓰이고_나머지_금지는_그대로다()
        {
            // 승인된 셋 — 이 파일에서만, 우리 호스트 창에만 허용된다.
            string[] approved = { "SetForegroundWindow(", "PostMessage(", "DestroyWindow(" };
            // 승인되지 않은 것들 — 여기서도 여전히 금지다. 예외는 세 이름에만 내려졌다.
            string[] stillBanned = { "ShowWindow(", "SetWindowPos(", "MoveWindow(", "BringWindowToTop(",
                                     "SwitchToThisWindow(", "AttachThreadInput(", "CloseWindow(" };

            // ---- (1) 대조: 검사기가 실제로 이 이름들을 찾아낼 수 있는가 ----
            foreach (string needle in approved)
            {
                string sample = "        bool ok = " + needle + "hwnd);\n";
                Assert.AreEqual(1, CountOccurrences(StripCommentLines(sample), needle),
                    $"{LogPrefix} 검사기가 '{needle}'을 표본에서조차 못 찾습니다 — 아래 단언 전부가 " +
                    "공허합니다(오타 난 니들이 조용히 초록을 내는 형태).");
                Assert.AreEqual(0, CountOccurrences(StripCommentLines("        /// " + needle + "\n"), needle),
                    $"{LogPrefix} 주석 제거기가 규약을 벗어났습니다.");
            }

            string code = StripCommentLines(File.ReadAllText(WinTrayPath));

            // ---- (2) 승인된 셋: 존재하고, 호출부는 반드시 _hostWindow를 넘긴다 ----
            foreach (string needle in approved)
            {
                Assert.AreEqual(2, CountOccurrences(code, needle),
                    $"{LogPrefix} '{needle}'의 등장 줄 수가 2(extern 선언 1 + 호출 1)가 아닙니다 — " +
                    "승인 범위는 «파일 1개 · 형태 3개»이고 그 이상은 예외가 번지는 것입니다.");
            }

            foreach (string line in code.Split('\n'))
            {
                string t = line.Trim();
                foreach (string needle in approved)
                {
                    if (!t.Contains(needle)) continue;
                    bool isExternDeclaration = t.Contains("static extern");
                    if (isExternDeclaration) continue;

                    StringAssert.Contains("_hostWindow", t,
                        $"{LogPrefix} ★ 승인된 API를 <우리 호스트 창이 아닌 것>에 부르고 있습니다: {t}\n" +
                        "승인의 핵심 조건은 «남의 창 핸들을 건드리지 않는다»입니다. 인자는 " +
                        "_hostWindow 하나로 고정돼 있어야 하고, 임의 핸들을 받을 수 있게 바꾸면 " +
                        "그 조건이 무너집니다(원칙 2/3).");
                }
            }

            // ---- (3) 승인되지 않은 것들은 여전히 0건 ----
            foreach (string needle in stillBanned)
            {
                Assert.AreEqual(0, CountOccurrences(code, needle),
                    $"{LogPrefix} ★ 승인되지 않은 창 조작 API('{needle}')가 들어왔습니다. " +
                    "2026-09-03 승인은 SetForegroundWindow / PostMessage / DestroyWindow <세 이름>에만 " +
                    "내려졌습니다 — '이왕 창을 만지는 김에'가 예외가 번지는 가장 자연스러운 경로이고, " +
                    "UserAssetImmutabilityAuditTests도 함께 빨개집니다.");
            }

            // ---- (4) DestroyWindow는 종료 훅에서만 도달 가능한가(승인 조건 5) ----
            StringAssert.Contains("Application.quitting", code,
                $"{LogPrefix} 종료 훅이 없습니다 — DestroyWindow가 종료 외의 시점에 불릴 수 있습니다.");
            int destroyAt = code.IndexOf("DestroyHostWindow()", StringComparison.Ordinal);
            Assert.Greater(destroyAt, -1,
                $"{LogPrefix} 창 정리가 전용 함수(DestroyHostWindow)로 격리돼 있지 않습니다 — " +
                "호출 지점이 흩어지면 '종료에서만 부른다'는 승인 조건을 아무도 확인할 수 없습니다.");
            Assert.AreEqual(2, CountOccurrences(code, "DestroyHostWindow()"),
                $"{LogPrefix} DestroyHostWindow의 등장이 2(정의 1 + 호출 1)가 아닙니다 — 호출 지점이 " +
                "늘었다면 종료 훅 밖에서도 창을 없애고 있는지 확인하세요.");
        }

        // ====================================================================
        // 도구
        // ====================================================================

        /// <summary><b>줄 전체가 주석인 줄</b>만 비운다 —
        /// <c>UserAssetImmutabilityAuditTests.BlankOutCommentLines</c>와 <b>같은 규약</b>이다.</summary>
        private static string StripCommentLines(string source)
        {
            string[] lines = source.Replace("\r\n", "\n").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string t = lines[i].TrimStart();
                if (t.StartsWith("//", StringComparison.Ordinal)
                    || t.StartsWith("*", StringComparison.Ordinal))
                {
                    lines[i] = string.Empty;
                }
            }
            return string.Join("\n", lines);
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0, at = 0;
            while (true)
            {
                at = haystack.IndexOf(needle, at, StringComparison.Ordinal);
                if (at < 0) return count;
                count++;
                at += needle.Length;
            }
        }
    }
}
