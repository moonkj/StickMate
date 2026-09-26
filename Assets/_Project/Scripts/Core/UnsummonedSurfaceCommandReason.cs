namespace StickMate.Core
{
    /// <summary>
    /// ★★ 2026-09-26 (N-20 해제 조건 7 · 7-b) — <b>사용자가 부르지 않은 표면을 억제하는 동안</b>
    /// (<c>Platform.UserSurfaceSummonPolicy.SuppressesUnsummonedSurfaces</c>) 명령 타일이 회색인 이유 한 줄.
    ///
    /// ============================================================================
    /// 왜 글자가 <b>이 파일 한 곳</b>에 있는가
    /// ============================================================================
    /// 이 사유는 크랙 전용이 아니다. <b>같은 가드 식(U = P ∨ g)을 받는 형제 명령</b>이 같은 글자를 쓴다 —
    /// 명령 크랙(해제 조건 7 · A1)과 그라피티(해제 조건 7-b)가 지금 그렇고, 창 도둑도 같은 가드를 받는 날
    /// 합류한다(그쪽은 N-8 목록 B등급이라 E-3 증거 결속이 풀린 뒤다). 문구를 Director마다 적으면
    /// 한쪽만 고치는 날 <b>같은 상황에 다른 말</b>이 뜬다. 그래서 글자는 여기에만 있고 각 Director는
    /// 이것을 <b>참조</b>한다 — 문자열 복제 0.
    ///
    /// <para>자리 근거: 설계 문서가 «<see cref="HiddenCharacterCommandGate"/>(Core)와 같은 「명령 게이트」 자리»를
    /// 지목했다(<c>docs/narrative/CRACK_COMMAND_DISABLED_REASON.md</c> §7 2). 그 파일과 <b>같은 형태</b>다 —
    /// 사유 <c>const</c> 하나와 미리 만든 결과 하나.</para>
    ///
    /// <para>★ <b>판정식은 여기 두지 않는다 — 일부러다.</b> 억제 창구 호출
    /// (<c>SuppressesUnsummonedSurfaces(agent.ArePanelsSuppressed, agent.IsUserSummonGrantActive)</c>)은
    /// <b>각 Director의 판정 함수 본문에</b> 그대로 있어야 한다. 그 호출형을 소스로 훑어 «자동 표면인가 사용자 표면인가»를
    /// 잠그는 감사가 있어서(<c>Tests/EditMode/UnsummonedSurfaceAxisTests</c> T-B), 술어를 이 파일로 감싸면
    /// 그 감사가 <b>눈이 먼다</b>. 이 파일은 글자만 소유한다.</para>
    ///
    /// <para><b>N-8</b>: 이 파일은 화면 변경 경로 목록 밖이다(<c>docs/verify/DISPLAY_CHANGE_PATH_FILES.md</c>의
    /// 목록 블록에 있는 <c>Core/</c> 항목은 <c>StickmanAgent.cs</c> · <c>SpectacleEventLock.cs</c> ·
    /// <c>CharacterPreservationFreeze.cs</c> 셋뿐이다). 사유를 <c>Core/StickmanAgent.cs</c>(B등급)에 두면
    /// 크랙·그라피티 가드까지 E-3 증거 결속에 묶이므로 <b>거기에 두지 않는다</b>(설계 문서 §7 3).</para>
    ///
    /// <para><b>영어</b>: 프로덕션에는 영어 UI 문자열 경로가 아직 없다(<c>Assets/</c> 전체에 영어 사유 0건 —
    /// 같은 검색의 양성 대조로 한국어 사유는 잡힌다). 번역이 들어오는 날 쓸 초안은 설계 문서 §3-3에 있다:
    /// <c>Once full screen ends, close StickMate windows and menus; retry in 3s</c>.
    /// ★ <b>그 초안은 폭 한도를 넘을 수 있다</b> — 타일 설명 한 줄의 폭 한도는 <b>372pt</b>인데 라틴 폭 가정
    /// 0.70F에서 <b>439.0pt</b>로 계산됐다(0.55F에서는 352.0pt). 줄바꿈 설계나 실제 빌드 캡처 확인 없이
    /// 그대로 넣지 마라(설계 문서 §6 · localization · ux-designer 인계 항목).</para>
    /// </summary>
    public static class UnsummonedSurfaceCommandReason
    {
        /// <summary>
        /// 문구 정본: <c>docs/narrative/CRACK_COMMAND_DISABLED_REASON.md</c> §3-3 <b>S6</b>
        /// (design-narrative 5판 추천 · 리더 확정 2026-09-26).
        ///
        /// <para>★ 원인이 아니라 <b>이 사유가 사라지는 충분조건</b>을 말한다. 이 글자가 화면에 보이는 칸은
        /// <b>FTT</b>(전체화면 앱 위에서 연 명령창)와 <b>FFT</b>(전체화면이 끝났는데 그때 연 창·부채꼴이 남음)
        /// 둘뿐이고, 두 칸 모두에서 참이다(같은 문서 §3-3 · §4).</para>
        ///
        /// <para>「3초쯤 뒤」가 글자에 있는 이유: 전체화면 앱을 끝내도 판정은 폴링에서만 풀려
        /// <b>1.5~3.0초</b> 더 참으로 남는다(폴링 주기 × 디바운서 유지). 그 사이에 다시 열면 허가 입구가
        /// 허가를 새로 내 한 번 더 회색이 된다 — 옛 문구(§0 R1)는 그 칸에서 약속이 깨졌다(같은 문서 §4 C1 행).</para>
        ///
        /// <para>테스트는 이 상수를 <b>참조</b>한다 — 글자를 베끼지 않는다(CLAUDE.md).
        /// 문구를 바꾸는 날 테스트가 조용히 초록으로 남지 않게 하는 것이 이 배치의 목적이다.</para>
        /// </summary>
        public const string Text = "전체화면이 끝나면 StickMate 창과 버튼을 다 닫고, 3초쯤 뒤 다시 열면 돼요";

        /// <summary>미리 만든 결과 하나 — 이 값은 명령창이 열려 있는 동안 <b>0.25초마다 타일마다</b> 다시 계산된다.
        /// 그때마다 문자열을 만들면 하루 종일 켜져 있는 앱에서 그게 곧 GC다
        /// (<see cref="CommandAvailability"/> 문서의 할당 0 계약 · <see cref="HiddenCharacterCommandGate.WhileHidden"/>과 같은 형태).</summary>
        public static readonly CommandAvailability WhileSuppressed = CommandAvailability.Blocked(Text);
    }
}
