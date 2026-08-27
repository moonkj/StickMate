using System.Runtime.CompilerServices;

// 텍스트-액션 싱크 계약(StateTransitionContext.TryConsumeToken, StickmanStateMachine.CurrentState/
// CurrentTransitionGeneration 등)의 internal 멤버를 EditMode 회귀 테스트(StickMate.Tests.EditMode)가
// 직접 검증할 수 있도록 허용한다. Tasklist.md Phase 2 "텍스트-액션 싱크 회귀 테스트" 항목,
// Assets/_Project/Scripts/Tests/EditMode/ 참고. 프로덕션 동작에는 영향이 없다 — 컴파일 시점의
// 어셈블리 가시성 허용만 추가한다.
[assembly: InternalsVisibleTo("StickMate.Tests.EditMode")]
