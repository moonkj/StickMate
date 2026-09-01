namespace StickMate.Platform
{
    /// <summary>
    /// 이 창이 지금 "레이어드 + DWM 확장 프레임" 하이브리드인지, 그렇다면 레이어드를 <b>떼도 되는지</b>를
    /// 판정하는 <b>순수 규칙</b>(2026-09-01, debugger 라운드). OS 호출이 한 줄도 없다 —
    /// 그래서 Windows 실기가 없는 개발 머신의 EditMode 테스트가 전 분기를 돌릴 수 있다
    /// (Tests/EditMode/LayeredHybridPolicyTests.cs). 실제 스타일 조작/관측은
    /// <c>Platform/Windows/WindowsLayeredHybridResolver.cs</c>에만 있다.
    ///
    /// ============================================================================
    /// 무엇을 고치려는가 — 우리가 <b>요청한 적 없는</b> 창 스타일
    /// ============================================================================
    /// 이 앱의 Windows 투명화는 <c>transparentType=Alpha</c>, 즉 네이티브
    /// <c>DwmExtendFrameIntoClientArea(hWnd, MARGINS{-1})</c> 한 줄이다(패키지 C++ 소스 실측:
    /// <c>libuniwinc.cpp</c> <c>enableTransparentByDWM()</c>). 이 경로에는 <c>WS_EX_LAYERED</c>가
    /// <b>전혀 필요 없다</b>. 그런데 같은 소스의
    /// <code>
    ///   void SetClickThrough(const BOOL bTransparent) {
    ///       if (bTransparent) { exstyle |= WS_EX_TRANSPARENT; exstyle |= WS_EX_LAYERED; ... }
    ///       else { exstyle &amp;= ~WS_EX_TRANSPARENT;  /* 레이어드는 일부러 안 지운다 */ ... }
    ///   }
    /// </code>
    /// 때문에 <b>클릭 관통을 한 번이라도 켜면 레이어드가 영구히 남는다</b>. 원칙 2(클릭 관통 기본 ON)
    /// 때문에 Windows 출하 형상은 항상 이 하이브리드다. macOS의 <c>ignoresMouseEvents</c>는 합성 경로를
    /// 건드리지 않으므로 <b>이 상태는 Windows에만 존재한다</b>.
    ///
    /// ============================================================================
    /// ★ 왜 "그냥 지운다"가 아니라 대조군까지 두는가 (원칙 2는 절대 못 깬다)
    /// ============================================================================
    /// 핵심 질문은 하나다 — <b>WS_EX_TRANSPARENT 단독으로 클릭 관통이 성립하는가?</b>
    /// 이 저장소는 Windows를 실행할 수 없고, 공개 자료도 갈린다(마이크로소프트 "Layered Windows" 문서는
    /// "레이어드 창에 WS_EX_TRANSPARENT가 있으면 마우스가 밑으로 지나간다"까지만 말하고,
    /// 비-레이어드 단독 사례는 명시하지 않는다). <b>그래서 추측으로 지우지 않는다.</b>
    /// 대신 사용자의 실기에서 <b>OS 자신에게</b> 물어보고, 답이 나쁘면 같은 프레임에 되돌린다:
    ///
    ///   (1) <b>대조군</b> — 지우기 <i>전에</i> <c>WindowFromPoint(우리 창 안의 한 점)</c>가 우리 HWND가
    ///       아닌 것을 돌려주는지 본다. 우리 HWND를 돌려주면 "관통을 관측할 수단 자체가 없다"는 뜻이므로
    ///       <b>실험을 포기</b>한다(<see cref="LayeredHybridHold.OracleInvalid"/>). 이 대조군이 없으면
    ///       "지운 뒤에도 관통된다"는 관측이 무의미해진다 — 원래부터 그렇게 보였을 수 있기 때문이다.
    ///   (2) 지운다.
    ///   (3) <b>실험군</b> — 같은 질문을 다시 한다. 관통이 사라졌으면 <b>즉시 되돌리고 영구 비활성화</b>.
    ///   (4) 그 뒤로도 매 관측마다 (3)을 계속한다 — 한 번 통과했다고 영원히 믿지 않는다.
    ///
    /// 전부 <b>같은 프레임/같은 스레드</b>에서 일어난다(라이브러리의 클릭관통 자동 토글도 메인 스레드의
    /// <c>Update</c>/<c>WaitForEndOfFrame</c>에서만 돈다) — 그래서 이 사이에 다른 주체가 스타일을
    /// 바꾸는 경합은 존재하지 않는다.
    /// </summary>
    public static class LayeredHybridPolicy
    {
        /// <summary>UniWindowController.TransparentType.Alpha. 이 값일 때만 레이어드가 불필요하다.</summary>
        public const int TransparentTypeAlpha = 1;

        /// <summary>
        /// 프로세스 수명 동안 레이어드를 떼는 최대 횟수.
        ///
        /// <para>라이브러리는 커서가 캐릭터 실루엣을 <b>벗어날 때마다</b>
        /// <c>SetClickThrough(TRUE)</c>를 다시 불러 레이어드를 도로 켠다. 즉 제거는 1회성이 아니라
        /// "커서가 캐릭터를 떠난 횟수"만큼 필요하다. 그래도 상한은 반드시 있어야 한다 — 예상 못 한
        /// 주체와 스타일 비트를 두고 무한히 싸우는 상태(24시간 상주 앱에서 가장 나쁜 실패)를 막는다.
        /// 상한에 닿으면 조용히 멈추지 않고 로그로 알린다.</para>
        /// </summary>
        public const int DefaultMaxStrips = 240;

        /// <summary>
        /// 지금 레이어드를 떼는 절차에 들어가도 되는가. <see cref="LayeredHybridHold.None"/>이면 진행.
        /// 그 외 값은 전부 "지금은 하지 않는다"이며, 값 자체가 사유(로그에 그대로 찍힌다)다.
        /// </summary>
        public static LayeredHybridHold EvaluateGate(in LayeredHybridObservation o)
        {
            if (o.OptedOut) return LayeredHybridHold.OptedOut;
            if (o.Disabled) return LayeredHybridHold.Disabled;
            if (!o.OsStyleReadOk) return LayeredHybridHold.StyleUnreadable;
            if (!o.HasLayeredStyle) return LayeredHybridHold.NotHybrid;

            // ColorKey 경로는 enableTransparentBySetLayered()가 WS_EX_LAYERED를 **필요로 한다**.
            // 거기서 레이어드를 떼면 투명화 자체가 죽어 회색 전체화면 창이 된다 — 절대 건드리지 않는다.
            if (o.TransparentType != TransparentTypeAlpha) return LayeredHybridHold.ColorKeyNeedsLayered;

            // WS_EX_TRANSPARENT가 지금 없다 = 커서가 캐릭터 위에 있어 라이브러리가 관통을 껐다.
            // 이 상태에서 떼면 "관통이 유지되는가"를 검증할 방법이 없다(원래 관통이 아니니까).
            // 검증 없는 변경은 하지 않는다 — 어차피 커서가 캐릭터를 벗어나면 곧 기회가 온다.
            if (!o.HasClickThroughStyle) return LayeredHybridHold.ClickThroughOffRightNow;

            if (o.StripCount >= o.MaxStrips) return LayeredHybridHold.AttemptCapReached;
            return LayeredHybridHold.None;
        }

        /// <summary>대조군을 매번 돌릴 필요는 없다 — 한 번이라도 "지워도 관통된다"가 확인되면 그 뒤로는
        /// 실험군(제거 후 검증)만으로 충분하다. 대조군의 목적은 <b>관측 수단이 유효한가</b>이고
        /// 그것은 이 머신에서 한 번 확인되면 바뀌지 않는다.</summary>
        public static bool RequiresControlProbe(bool verifiedOnce) => !verifiedOnce;

        /// <summary>대조군 결과 판정. 관통이 <b>원래부터</b> 관측되지 않으면 실험 자체가 무효다.</summary>
        public static LayeredHybridHold EvaluateControl(bool passThroughObservedBeforeStrip)
            => passThroughObservedBeforeStrip ? LayeredHybridHold.None : LayeredHybridHold.OracleInvalid;

        /// <summary>
        /// 제거 후(또는 제거된 상태에서의 상시 감시) 되돌려야 하는가.
        ///
        /// <para><b>판정할 수 없으면 되돌린다</b>가 규칙이다. 클릭 관통이 깨진 채로 계속 가는 것은
        /// 화면 전체를 덮는 오버레이에서 "아무것도 클릭할 수 없는 앱"을 뜻하며, 지금 고치려는 어떤
        /// 증상보다도 나쁜 회귀다(원칙 2). 되돌리는 쪽의 비용은 "예전 상태로 돌아감"뿐이다.</para>
        /// </summary>
        public static bool RequiresRollback(bool clickThroughStyleStillSet, bool passThroughObservedAfterStrip)
            => !clickThroughStyleStillSet || !passThroughObservedAfterStrip;

        /// <summary>로그에 그대로 찍히는 한국어 사유(사용자가 Player.log 한 줄로 이해할 수 있어야 한다).</summary>
        public static string Describe(LayeredHybridHold hold) => hold switch
        {
            LayeredHybridHold.None => "진행 가능",
            LayeredHybridHold.NotHybrid => "WS_EX_LAYERED 없음 — 이미 DWM 단일 경로(목표 형상)",
            LayeredHybridHold.StyleUnreadable => "창 스타일 실측 실패(핸들 미확보) — 보류",
            LayeredHybridHold.ColorKeyNeedsLayered => "transparentType이 Alpha가 아님 — ColorKey 투명화는 " +
                "레이어드를 필요로 하므로 건드리지 않는다",
            LayeredHybridHold.ClickThroughOffRightNow => "지금 WS_EX_TRANSPARENT가 없음(커서가 캐릭터 위) — " +
                "관통 유지 검증이 불가능하므로 다음 기회에",
            LayeredHybridHold.AttemptCapReached => "제거 시도 상한 도달 — 더 시도하지 않는다",
            LayeredHybridHold.OracleInvalid => "대조군에서 관통이 관측되지 않았다 — 이 머신에서는 " +
                "WindowFromPoint로 관통을 판정할 수 없으므로 실험을 포기하고 레이어드를 그대로 둔다",
            LayeredHybridHold.Disabled => "이전에 검증 실패로 되돌린 적이 있어 영구 비활성",
            LayeredHybridHold.OptedOut => "환경변수 STICKMATE_KEEP_LAYERED로 사용자가 껐다",
            _ => hold.ToString(),
        };
    }

    /// <summary>레이어드 제거를 <b>하지 않는</b> 사유. <see cref="LayeredHybridHold.None"/>만 "진행".</summary>
    public enum LayeredHybridHold
    {
        None = 0,
        NotHybrid = 1,
        StyleUnreadable = 2,
        ColorKeyNeedsLayered = 3,
        ClickThroughOffRightNow = 4,
        AttemptCapReached = 5,
        OracleInvalid = 6,
        Disabled = 7,
        OptedOut = 8,
    }

    /// <summary>순수 판정에 필요한 관측값 전부. OS 호출 결과를 담기만 한다.</summary>
    public struct LayeredHybridObservation
    {
        /// <summary>GetWindowLongPtr(GWL_EXSTYLE)가 성공했는가.</summary>
        public bool OsStyleReadOk;
        public bool HasLayeredStyle;
        /// <summary>WS_EX_TRANSPARENT(= 지금 클릭 관통이 OS 수준에서 켜져 있는가).</summary>
        public bool HasClickThroughStyle;
        /// <summary>UniWindowController.TransparentType(1=Alpha, 2=ColorKey).</summary>
        public int TransparentType;
        /// <summary>검증 실패로 영구 비활성화됐는가.</summary>
        public bool Disabled;
        /// <summary>사용자가 환경변수로 껐는가.</summary>
        public bool OptedOut;
        public int StripCount;
        public int MaxStrips;
    }

    /// <summary>해소기의 상태 — 진단 로그가 "지금 레이어드가 보이는 이유"를 설명할 수 있게 한다.</summary>
    public enum LayeredHybridResolverState
    {
        /// <summary>이 플랫폼에는 해소기가 없다(macOS/모바일/에디터).</summary>
        NotPresent = 0,
        /// <summary>가동 중이지만 아직 한 번도 제거를 검증하지 못했다.</summary>
        Pending = 1,
        /// <summary>제거 후 관통 유지가 실측으로 확인됐다(= 정상 가동).</summary>
        Verified = 2,
        /// <summary>보류 중(사유는 <see cref="LayeredHybridHold"/>).</summary>
        Holding = 3,
        /// <summary>검증 실패로 되돌리고 영구 비활성화.</summary>
        RolledBack = 4,
    }
}
