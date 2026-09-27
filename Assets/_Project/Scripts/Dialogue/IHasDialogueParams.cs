namespace StickMate.Dialogue
{
    /// <summary>
    /// 상태(IStickmanState 구현체)가 자신의 대사 파생용 파라미터를 구조적으로 노출하기 위해 선택적으로
    /// 구현하는 인터페이스 (BUG-M7 대응, docs/BUG_REPORT_PHASE0.md — "DialogueIntent의
    /// Func&lt;StickmanStateId,string&gt; 시그니처가 상태 파라미터를 못 실어 나름").
    ///
    /// 왜 필요한가: UX_FLOW.md 5절 규칙 #2는 "ATTACK 상태에 shotsRemaining 파라미터가 있으면 말풍선은
    /// 그 값으로부터 파생되어야 하며, 텍스트가 상태 파라미터와 별개로 하드코딩되어 어긋날 수 있는 구조를
    /// 금지"한다고 명시한다. 예전 시그니처(Func&lt;StickmanStateId,string&gt;)는 상태 ID만 받고 실제
    /// 파라미터(예: 남은 타격 횟수)를 받을 방법이 없어, 상태 구현체가 지역 필드를 클로저로 캡처하는
    /// 임시방편에 의존할 수밖에 없었다 — "enum+params → 텍스트 매핑 테이블 단방향"이라는 의도를
    /// 컴파일러가 강제하지 못하고 사람의 규율에만 의존하게 만드는, 원칙 1이 막으려는 바로 그 종류의
    /// 실수 재발 경로였다.
    ///
    /// 설계: DialogueIntent는 이 인터페이스를 "호출자가 넘기는 임의의 객체"가 아니라
    /// StickmanStateMachine.CurrentState(=지금 실제로 Enter() 중인 그 상태 인스턴스)에 대해서만
    /// 캐스팅해 사용한다 — 즉 파라미터는 항상 "지금 확정된 실제 상태 객체"에서 직접 읽히므로, 상태와
    /// 무관한(또는 다른 상태의) 파라미터를 끼워 넣을 수 없다. 이는 원칙 1(행동-텍스트 싱크)을 파라미터
    /// 레벨까지 확장한 것이다.
    ///
    /// ★ 결과 축(성공/실패)은 <b>지금 어느 구현도 싣지 않는다</b> — 이 인터페이스 구현 8종(Idle ·
    /// Walk · Attack · Ragdoll · LedgeHang · ParkourClimb · RopeClimb · DragThrow) 가운데 0개이고,
    /// 가장 가까운 <c>AttackState</c>의 <c>ShotsRemaining</c>조차 그 필드 문서가 「항상 기본값 0」이라고
    /// 스스로 적는다. 그 계약의 <b>정본은 한 곳뿐</b>이다: <c>docs/MOTION_SPEC.md</c>의
    /// 「9. 이 문서가 만든 새 계약 (요약 카드)」 절의 C2 행이다. 그 문장은 <b>한 줄로</b> 인용한다 —
    /// 줄을 접으면 고정 문자열 검색이 죽어 앵커 자체가 못 쓰게 된다(이 저장소가 실제로 당한 형태):
    /// 「결과가 있는 상태의 대사는 결과 축을 파라미터에 반드시 싣는다」.
    /// 같은 문서 6절의 P0 행은 <b>폐기됐다</b>(그 상태가 사라졌다). 결과가
    /// 있는 상태(미니게임·대결 등)를 새로 만들면 인계본은 그 6절 행이 아니라 이 C2 행이다.
    /// (라벨 <c>C2</c>는 다른 문서들이 서로 다른 뜻으로도 쓰므로, 찾을 때는 위 <b>절 제목</b>과
    /// 문장을 함께 앵커로 써라.)
    /// </summary>
    public interface IHasDialogueParams
    {
        /// <summary>
        /// 이 상태가 대사 매핑 함수(Func&lt;StickmanStateId, object, string&gt;)에 노출할 파라미터.
        /// 타입은 상태마다 자유이며(예: AttackState.AttackDialogueParams), 매핑 함수 구현부에서 캐스팅해
        /// 사용한다.
        /// </summary>
        object DialogueParams { get; }
    }
}
