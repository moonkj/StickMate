namespace StickMate.Core
{
    /// <summary>
    /// 캐릭터 본체(몸통/머리/팔다리) <b>바깥</b>에서 잉크를 더 얹는 부품이 "지금 내가 그리는 잉크의
    /// 가장 낮은 월드 Y"를 스스로 답하는 통로.
    ///
    /// 왜 인터페이스인가 — 이 값을 필요로 하는 쪽(States/GetupState의 바닥 클리어런스 리프트)은
    /// 액세서리가 32종인지 40종인지, 모자챙이 어디까지 뻗는지 알아야 할 이유가 없다. 부품 목록을
    /// 소비자 쪽에 적어 두면 <b>새 부품(DLC 모션/이펙트, 펫 등)을 추가한 사람이 그 목록을 고치는 것을
    /// 잊는 순간</b> 그 부품만 조용히 바닥을 뚫는다 — 이 프로젝트가 반복해서 겪은 실패 유형이다
    /// (접지 스냅 호출을 상태마다 흩어 놓았다가 Attack/Getup/BattleMinigame이 빠졌던 사고).
    /// 그래서 방향을 뒤집어, 잉크를 더하는 쪽이 스스로 신고하게 한다(CLAUDE.md 원칙 4 플러그인 구조).
    ///
    /// 구현체는 캐릭터 루트(또는 그 자손)의 MonoBehaviour여야 한다 —
    /// StickmanBlackboard가 루트에서 1회 수집해 캐싱한다.
    /// </summary>
    public interface ICharacterInkExtentProvider
    {
        /// <summary>
        /// 지금 화면에 그리고 있는 잉크의 가장 낮은 월드 Y. 지금 아무것도 그리지 않으면(장비 없음/
        /// 숨김/투명) false를 돌려준다 — 그 경우 호출부는 이 부품을 없는 것으로 취급한다.
        /// <para>매 프레임 호출 경로가 아니다(GETUP 보간 중에만 돈다). 그래도 구현체는 할당을
        /// 하지 않아야 한다 — 이 앱은 하루 종일 켜져 있고 RAGDOLL 복귀는 반복해서 일어난다.</para>
        /// </summary>
        bool TryGetLowestInkWorldY(out float worldY);
    }
}
