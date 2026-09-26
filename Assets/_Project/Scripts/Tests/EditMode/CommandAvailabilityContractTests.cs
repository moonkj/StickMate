using System;
using NUnit.Framework;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 행동 명령창의 "진실 한 벌" 계약 회귀 — docs/UX_FLOW.md <b>36-7</b>.
    ///
    /// <see cref="CommandAvailability"/>는 회색 처리와 실제 실행이 <b>같은 판정 하나</b>를 쓰게 만드는
    /// 강제 장치이고, <see cref="StickMateDisplayNames"/>는 그 이유 문구가 <b>실제 enum 값에서만</b>
    /// 파생되게 만드는 강제 장치다(원칙 1). 여기서는 그 두 계약을 씬 없이 잠근다.
    /// </summary>
    public sealed class CommandAvailabilityContractTests
    {
        // ==================== CommandAvailability ====================

        [Test]
        public void Ready는_이유를_들지_않는다()
        {
            Assert.IsTrue(CommandAvailability.Ready.IsReady);
            Assert.AreEqual(CommandReadyState.Ready, CommandAvailability.Ready.State);
            Assert.IsNull(CommandAvailability.Ready.Reason, "가능한데 이유가 붙어 있으면 화면에 엉뚱한 줄이 나옵니다.");
        }

        /// <summary>36-7: 씬에 Director가 없어도 <b>칸을 숨기지 않는다</b> — 어제 있던 칸이 사라지면
        /// 사용자는 자기 잘못을 의심한다. 그래서 Missing에도 읽을 수 있는 이유가 반드시 있다.</summary>
        [Test]
        public void Missing은_숨기지_않고_이유를_말한다()
        {
            Assert.IsFalse(CommandAvailability.Missing.IsReady);
            Assert.AreEqual(CommandReadyState.Missing, CommandAvailability.Missing.State);
            Assert.AreEqual(CommandAvailability.MissingReason, CommandAvailability.Missing.Reason);
            Assert.IsNotEmpty(CommandAvailability.Missing.Reason);
        }

        [Test]
        public void Blocked는_준_이유를_그대로_보관한다()
        {
            CommandAvailability blocked = CommandAvailability.Blocked("과녁 놓을 자리가 없어요");
            Assert.IsFalse(blocked.IsReady);
            Assert.AreEqual(CommandReadyState.Blocked, blocked.State);
            Assert.AreEqual("과녁 놓을 자리가 없어요", blocked.Reason,
                "이유가 변형됐습니다 — 36-7은 코드 분기와 화면 문구가 1:1이어야 한다고 못박았습니다.");
        }

        /// <summary>★ 조용한 실패 금지 — 이유를 빠뜨린 호출이 있어도 화면이 <b>빈 줄</b>이 되면 안 된다.</summary>
        [Test]
        public void Blocked에_빈_이유를_줘도_화면에_쓸_문장이_남는다()
        {
            Assert.IsNotEmpty(CommandAvailability.Blocked(null).Reason,
                "이유가 null인 Blocked가 빈 문자열을 돌려줍니다 — 타일 설명 줄이 통째로 비어 조용한 실패가 됩니다.");
            Assert.IsNotEmpty(CommandAvailability.Blocked(string.Empty).Reason);
        }

        // ==================== StickMateDisplayNames ====================

        /// <summary>
        /// ★ enum 값이 하나 늘었는데 이름을 잊으면 화면에 "다른 일 중이에요"라는 <b>정보 없는 문장</b>이
        /// 나온다. 그건 원칙 1의 조용한 위반이라(실제 값과 표시가 어긋난다) 여기서 전수 확인한다.
        /// </summary>
        [Test]
        public void 모든_스펙터클_종류에_한글_이름이_있다()
        {
            foreach (SpectacleEventKind kind in (SpectacleEventKind[])Enum.GetValues(typeof(SpectacleEventKind)))
            {
                Assert.AreNotEqual("다른 일", StickMateDisplayNames.Of(kind),
                    $"SpectacleEventKind.{kind}의 한글 이름이 없습니다 — 행동 명령창이 이유를 " +
                    "\"지금 다른 일 중이에요\"로만 말하게 됩니다(36-13 #9).");
                Assert.IsNotEmpty(StickMateDisplayNames.BusyText(kind));
            }
        }

        [Test]
        public void 모든_상태에_한글_이름이_있다()
        {
            foreach (StickmanStateId state in (StickmanStateId[])Enum.GetValues(typeof(StickmanStateId)))
            {
                Assert.AreNotEqual("딴 일", StickMateDisplayNames.Of(state),
                    $"StickmanStateId.{state}의 한글 이름이 없습니다 — 불가 이유가 " +
                    "\"지금 딴 일 중이라 못 해요\"로만 나옵니다(36-13 #10).");
                Assert.IsNotEmpty(StickMateDisplayNames.BusyText(state));
            }
        }

        /// <summary>36-7이 정한 문장 형태. 이 두 문형이 바뀌면 UX 문서와 화면이 어긋난다.</summary>
        [Test]
        public void 불가_이유_문형이_36_7_표와_같다()
        {
            Assert.AreEqual("지금 활쏘기 중이에요", StickMateDisplayNames.BusyText(SpectacleEventKind.Archery),
                "락 점유 문형이 \"지금 ○○ 중이에요\"가 아닙니다.");
            Assert.AreEqual("지금 낙하 중이라 못 해요", StickMateDisplayNames.BusyText(StickmanStateId.Fall),
                "상태 불일치 문형이 \"지금 △△ 중이라 못 해요\"가 아닙니다.");
        }

        /// <summary>
        /// ★★ <b>무할당 회귀</b> — 이 앱은 하루 종일 켜져 있고, 행동 명령창은 열려 있는 동안 0.25초마다
        /// 6개 타일의 이유를 다시 묻는다. 문구를 그때마다 보간으로 만들면 초당 24개의 쓰레기가 생긴다.
        /// 같은 값을 두 번 물었을 때 <b>같은 인스턴스</b>가 돌아오는지로 "미리 만들어 뒀는가"를 잰다
        /// (문자열 내용 비교로는 새로 만든 것과 구분할 수 없다).
        /// </summary>
        [Test]
        public void 이유_문자열은_미리_만들어져_재사용된다()
        {
            Assert.AreSame(StickMateDisplayNames.BusyText(SpectacleEventKind.Graffiti),
                StickMateDisplayNames.BusyText(SpectacleEventKind.Graffiti),
                "락 이유 문구가 호출마다 새로 만들어집니다 — 0.25초 폴링이 곧 GC 압력이 됩니다.");
            Assert.AreSame(StickMateDisplayNames.BusyText(StickmanStateId.Ragdoll),
                StickMateDisplayNames.BusyText(StickmanStateId.Ragdoll),
                "상태 이유 문구가 호출마다 새로 만들어집니다.");
            Assert.AreSame(StickMateDisplayNames.Of(StickmanStateId.Idle), StickMateDisplayNames.Of(StickmanStateId.Idle));
        }

        /// <summary>범위 밖 값(캐스팅 사고/직렬화 잔재)에도 죽지 않고 읽을 수 있는 문장을 준다.</summary>
        [Test]
        public void 알_수_없는_enum_값에도_안전하게_답한다()
        {
            Assert.IsNotEmpty(StickMateDisplayNames.Of((SpectacleEventKind)9999));
            Assert.IsNotEmpty(StickMateDisplayNames.BusyText((SpectacleEventKind)9999));
            Assert.IsNotEmpty(StickMateDisplayNames.Of((StickmanStateId)(-1)));
            Assert.IsNotEmpty(StickMateDisplayNames.BusyText((StickmanStateId)(-1)));
        }

        /// <summary>
        /// ★★ 2026-09-26 — <b>락 점유 사유에서 문형을 벗어나는 칸은 «정확히 하나»다.</b>
        ///
        /// <para><b>불변식</b>(ux-widgets): 문장은 그것이 <b>뜰 수 있는 구간 전체에서</b> 참이어야 한다.
        /// 크랙은 캐릭터 스윙이 끝난 뒤에도 금 수명까지 락을 쥐고 있어 「지금 ○○ 중이에요」가 약 2.6초 동안
        /// <b>캐릭터 상태를 거짓 주장</b>한다. 락 13종 중 그런 것은 크랙 하나뿐이므로 <b>한 칸만</b> 갈라야 하고,
        /// 나머지는 <b>지금 참이므로 건드리면 안 된다</b>. 이 테스트가 그 «하나»를 양방향으로 못박는다 —
        /// 0이면 덮어쓰기가 사라진 것이고, 2 이상이면 누가 12종까지 손댄 것이다.</para>
        ///
        /// <para>★ <b>문형 자를 런타임에 유도한다</b>: 접두 「지금 」과 접미 「 중이에요」는 프로덕션 리터럴이고
        /// 공개 상수가 없다. 그것을 테스트에 <b>베껴 적으면</b> 프로덕션이 문형을 바꾸는 날 기준과 대상이 갈라진 채
        /// 조용히 초록이 된다(CLAUDE.md 니들 규칙). 그래서 <b>바뀌지 않은 대조 칸</b>(활쏘기)의 완성 문장에서
        /// 이름을 빼내 접두·접미를 얻고, <b>그 자가 살아 있는지 먼저 단언</b>한다.</para>
        /// </summary>
        [Test]
        public void 락_사유는_크랙_한_칸만_문형에서_갈라진다()
        {
            // ---- 자 만들기: 대조 칸에서 접두·접미를 유도하고, 그 자가 살아 있음을 먼저 못박는다 ----
            const SpectacleEventKind control = SpectacleEventKind.Archery;
            string controlBusy = StickMateDisplayNames.BusyText(control);
            string controlName = StickMateDisplayNames.Of(control);
            int at = controlBusy.IndexOf(controlName, StringComparison.Ordinal);
            Assert.Greater(at, 0,
                $"대조 칸({control})의 완성 문장 «{controlBusy}»에서 이름 «{controlName}»을 찾지 못했습니다 — " +
                "이 검사의 자(문형)가 죽었습니다. 아래 «한 칸만 갈린다»는 아무것도 재지 못합니다.");
            string prefix = controlBusy.Substring(0, at);
            string suffix = controlBusy.Substring(at + controlName.Length);
            Assert.IsNotEmpty(prefix, "유도한 접두가 비었습니다(자가 죽었습니다).");
            Assert.IsNotEmpty(suffix, "유도한 접미가 비었습니다(자가 죽었습니다).");

            // ---- 전수: 문형을 벗어난 칸을 모은다 ----
            // ★ 전체 이름으로 쓴다 — 이 파일의 using 목록에 기대지 않기 위해서다(기대에 기대면 컴파일이 갈린다).
            var divergent = new System.Collections.Generic.List<SpectacleEventKind>();
            foreach (SpectacleEventKind kind in (SpectacleEventKind[])Enum.GetValues(typeof(SpectacleEventKind)))
            {
                string expected = prefix + StickMateDisplayNames.Of(kind) + suffix;
                if (!string.Equals(StickMateDisplayNames.BusyText(kind), expected, StringComparison.Ordinal))
                {
                    divergent.Add(kind);
                }
            }

            CollectionAssert.AreEqual(new[] { SpectacleEventKind.WindowCrash }, divergent,
                "★ 문형에서 갈라진 칸이 «크랙 하나»가 아닙니다: [" + string.Join(", ", divergent) + "]. " +
                "0이면 크랙 덮어쓰기가 사라져 스윙이 끝난 뒤에도 «지금 창 부수기 중이에요»가 다시 거짓말을 합니다. " +
                "2 이상이면 지금 참인 칸까지 바꾼 것입니다(전면 교체는 기각된 안입니다).");

            // ---- 그 한 칸의 값은 프로덕션 상수를 참조해 잰다(글자를 베끼지 않는다) ----
            Assert.AreEqual(StickMateDisplayNames.WindowCrashBusyText,
                StickMateDisplayNames.BusyText(SpectacleEventKind.WindowCrash),
                "크랙 칸이 공개 상수와 다른 글자입니다 — 문구가 두 벌이 됐습니다.");
            Assert.AreNotEqual(prefix + StickMateDisplayNames.Of(SpectacleEventKind.WindowCrash) + suffix,
                StickMateDisplayNames.BusyText(SpectacleEventKind.WindowCrash),
                "크랙 칸이 아직 문형 그대로입니다 — 덮어쓰기가 실행되지 않았습니다(정적 생성자 확인).");

            // ---- 이름 표는 그대로여야 한다(바꾼 것은 «사유 문장» 하나뿐이다) ----
            Assert.AreEqual(controlName, StickMateDisplayNames.Of(control), "대조 칸의 이름이 흔들렸습니다.");
            Assert.AreNotEqual("다른 일", StickMateDisplayNames.Of(SpectacleEventKind.WindowCrash),
                "크랙의 이름 표가 비었습니다 — 바꾼 것은 사유 문장이지 이름이 아닙니다.");

            // ---- 무할당 계약: 덮어쓴 칸도 «미리 만들어 둔» 같은 인스턴스여야 한다 ----
            Assert.AreSame(StickMateDisplayNames.BusyText(SpectacleEventKind.WindowCrash),
                StickMateDisplayNames.BusyText(SpectacleEventKind.WindowCrash),
                "크랙 사유가 호출마다 새로 만들어집니다 — 0.25초 폴링이 곧 GC 압력이 됩니다(이 파일의 무할당 계약).");
        }
    }
}
