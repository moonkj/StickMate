namespace StickMate.Core
{
    /// <summary>
    /// ============================================================================
    /// ★★ C층의 <b>첫 소비자</b> — 여기가 뒤따르는 모든 소비자의 어법을 정한다
    /// ============================================================================
    /// 실측(2026-09-07): <c>PackEntitlements.StateOf</c>의 프로덕션 소비자는 <b>0건</b>이었다.
    /// 코스튬 게이트가 첫 번째이고, <b>첫 소비자가 세우는 어법을 두 번째부터는 베낀다</b>.
    /// 그래서 계약서 I-5가 이 판정을 <b>이 파일 한 곳에 가뒀다</b>:
    ///
    /// <para><b><c>Interaction/</c>이나 <c>States/</c>에서 <c>PackEntitlements.StateOf</c>를 직접 부르는
    /// 코드를 한 줄도 쓰지 마라.</b> 그 순간 판정이 두 곳이 되고, 「반만 열린」 상태가 만들어진다 —
    /// 프롭은 뜨는데 포즈는 안 바뀌거나, 세션 시작에는 열렸는데 종료에는 닫힌 것 같은 형태다.
    /// 그리고 그 화면은 <b>「기능이 아직 안 붙었다」와 구별되지 않는다</b>.</para>
    ///
    /// ============================================================================
    /// ★★ 규칙 C-1 — 개방은 «엔타이틀먼트»지만, <b>무료는 「안 묻는 것」</b>이다
    /// ============================================================================
    /// <c>PackEntitlementRef.entitlementId</c>의 계약이 그것을 이미 말해 뒀다:
    /// <i>"빈 문자열을 「무료」로 읽지 않는다 — 무료는 <b>채널 항목 자체가 없는 것</b>이다."</i>
    ///
    /// <para>그래서 <b>채널이 하나도 없는 팩에는 <c>StateOf</c>를 아예 부르지 않는다.</b>
    /// 부르면 오늘 트리에서 <c>NullPackEntitlementSource</c>가 <c>Unknown</c>을 돌려주고,
    /// <c>Unknown</c>은 «새로 시작 거부»라 <b>무료 팩까지 통째로 닫힌다</b>.
    /// 그 증상은 화면상 «기능 미구현»과 완전히 같다 — 이 저장소가 아홉 번 당한 형태다.</para>
    ///
    /// ============================================================================
    /// <c>Unknown</c>은 <c>NotOwned</c>가 아니다 — 그래도 <b>새로 시작은 거부</b>한다
    /// ============================================================================
    /// <c>docs/security/ENTITLEMENT_CONTRACT.md</c> §E-2: 유지는 하되 새로 시작은 거부한다.
    /// 이 함수는 «지금 열려 있는가»라는 <b>새로 시작 쪽</b> 질문이므로 <c>Unknown</c>은 닫힘이다.
    ///
    /// <para>★ 그 «유지»는 두 겹으로 성립한다. (1) <see cref="PackEntitlements"/>가
    /// <b>확인된 보유를 프로세스 수명 동안 단조로 래치</b>한다 — 스팀이 도중에 죽어도
    /// 이미 <c>Owned</c>였던 팩은 계속 <c>Owned</c>다. (2) 세션 <b>도중의</b> «열림 → 닫힘» 전이를
    /// 적용하지 않는 것은 <b>세션 층의 책임</b>이다(규칙 C-2, <c>FocusWatchDirector</c>).
    /// 여기에 세션 캐시를 두지 <b>않는</b> 이유가 그것이다 — 세션이 뭔지 모르는 층에 세션 상태를 두면
    /// 그 캐시가 언제 비는지 아무도 모르게 된다.</para>
    ///
    /// <para>★ 그리고 <b>부정 캐시를 만들지 않는다</b>(§E-3-a). <c>NotOwned</c>/<c>Unknown</c>을
    /// 기억하지 않으므로 «재시도»라는 별도 장치가 필요 없다 — 09:00에 스팀이 안 떠 닫혔어도
    /// 09:01의 질문은 새로 답을 받는다. 그래서 이 클래스에는 <b>필드가 하나도 없다</b>.</para>
    ///
    /// ============================================================================
    /// ★★ 규칙 C-3 — 이 파일은 <b>세이브를 절대 읽지 않는다</b>
    /// ============================================================================
    /// 저장된 것은 <i>"입은 채 몇 분을 보냈는가"</i>이지 <i>"가졌는가"</i>가 아니다.
    /// 그 둘을 섞는 순간 <b>세이브 파일이 결제 우회 표적이 된다</b> —
    /// C층을 세이브에서 뺀 목적이 방어 추가가 아니라 <b>표적 제거</b>였다(§E-4-a).
    /// <b>세이브 저장소도, 코스튬 누적 모델도 여기서 참조하지 마라.</b>
    /// ★ 그 두 타입의 <b>이름조차 이 파일에 적지 않는다</b>: 이 규칙을 재는 감사는 소스 텍스트를
    /// 훑는데, 경고 문구 안의 이름과 실제 <c>using</c>이 <b>똑같이 생겼기</b> 때문이다.
    /// 이름을 안 적으면 그 감사가 주석 제거를 빠뜨려도 <b>여전히 옳은 답</b>을 낸다
    /// (죽은 프로브가 산 프로브와 똑같이 생기는 것 — 이 저장소가 반복해서 당한 형태다).
    /// </summary>
    public static class CostumeEntitlement
    {
        /// <summary>
        /// 이 팩이 <b>지금 열려 있는가</b>. 팩 서술자가 <c>null</c>이면 닫힘이다 —
        /// 「고아 코호트」(아이템은 팩 소속이라 적었는데 매니페스트가 없다)가 그 모양이고,
        /// 그건 <b>살 방법이 없는 상태</b>라 열어 주면 안 된다.
        ///
        /// <para>기본 코호트 코스튬은 이 함수를 <b>거치지 않는다</b> — 팩이 아니므로 물을 대상이 없다
        /// (<see cref="IsOpen(CostumeDescriptor, PackDescriptor)"/>가 그 갈래를 담당한다).</para>
        /// </summary>
        public static bool IsOpen(PackDescriptor pack)
        {
            if (pack == null) return false;

            // ★ 무료 — 채널 항목이 하나도 없으면 <b>StateOf를 부르지 않는다</b>(위 문단).
            if (pack.Entitlements == null || pack.Entitlements.Count == 0) return true;

            // ★ default: 를 두지 않는다(§E-1-b) — 단이 하나 늘면 컴파일러가 아니라
            //   <b>사람이 이 자리를 보게</b> 하는 것이 요점이다.
            switch (PackEntitlements.StateOf(pack.PackId))
            {
                case PackEntitlementState.Owned: return true;
                case PackEntitlementState.NotOwned: return false;
                case PackEntitlementState.Unknown: return false;
            }
            return false;
        }

        /// <summary>
        /// 이 코스튬이 <b>지금 열려 있는가</b>. <b>개방 판정의 유일한 입구다.</b>
        ///
        /// <para><paramref name="pack"/>은 <see cref="CostumeSourceKind.Pack"/>인 코스튬에서만 쓰인다.
        /// 기본 코호트 테마 코스튬은 <b>엔타이틀먼트가 없다</b> — 무료는 「안 묻는 것」이고,
        /// 그래서 이 갈래에서는 <paramref name="pack"/>이 <c>null</c>이어도 열린다.</para>
        ///
        /// <para>★ 팩 소속인데 <paramref name="pack"/>이 <c>null</c>이면 <b>닫힘</b>이다.
        /// 그 조합은 «매니페스트가 없는 팩을 가리키는 코스튬»이고, 화면에 띄우면
        /// <b>살 수도 없는 것을 보여 주는 것</b>이 된다.</para>
        /// </summary>
        public static bool IsOpen(CostumeDescriptor costume, PackDescriptor pack)
        {
            if (costume == null) return false;
            if (!costume.IsPackSourced) return true;
            return IsOpen(pack);
        }

        /// <summary>
        /// 사람이 읽을 <b>사유 키</b>. 닫힌 이유를 화면이 말해야 할 때 쓴다.
        ///
        /// <para><b>완성 문장이 아니라 키인 이유</b>는 <see cref="PackEntitlements.ReasonKey"/>와 같다 —
        /// 이 파일은 <c>Core/</c>이고 화면 문구는 <c>ux-designer</c>/<c>coder-ui</c> 소관이다.
        /// 그리고 <b><c>NotOwned</c>와 <c>Unknown</c>은 반드시 다른 문구여야 한다</b>(§E-2-a):
        /// 같은 문구를 쓰면 오프라인 유저에게 <i>"당신은 안 샀습니다"</i>라고 말하게 된다.</para>
        /// </summary>
        public static string ClosedReasonKey(CostumeDescriptor costume, PackDescriptor pack)
        {
            if (costume == null) return "costume.state.unknowncostume";
            if (!costume.IsPackSourced) return "costume.state.open";
            if (pack == null) return "costume.state.nopack";
            if (pack.Entitlements == null || pack.Entitlements.Count == 0) return "costume.state.open";
            return PackEntitlements.ReasonKey(PackEntitlements.StateOf(pack.PackId));
        }
    }
}
