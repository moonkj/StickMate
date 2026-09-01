// 이 프로세스가 "조합키가 아닌 일반 키"의 전역 눌림 상태를 읽을 수 있는지 보고한다.
//
// 왜 이게 스킬의 핵심 진단인가: StickMate는 CGEventSourceKeyState로 단축키를 읽는데, macOS는
// 조합키(Ctrl/Opt/Cmd)는 누구에게나 보여주지만 **일반 키(B, Q, A...)는 Input Monitoring
// (kTCCServiceListenEvent) 권한이 있는 프로세스에만** 보여준다. 권한이 없으면 조합키만 True가 되고
// 동작키가 영원히 False라서, 앱은 "단축키를 눌러도 아무 일도 일어나지 않는" 상태가 된다.
//
// TCC 권한은 "책임 프로세스(responsible process)" 단위로 붙는다. 그래서 이 도구를 드라이버와 같은
// 셸에서 실행하면, 같은 셸이 띄울 StickMate가 갖게 될 권한과 정확히 같은 결과가 나온다.
import Foundation
import CoreGraphics
import IOKit.hid
import ApplicationServices

let access = IOHIDCheckAccess(kIOHIDRequestTypeListenEvent)
let accessText: String
switch access.rawValue {
case 0: accessText = "granted(허용)"
case 1: accessText = "denied(거부)"
default: accessText = "unknown(미결정)"
}
print("AXIsProcessTrusted(접근성)      = \(AXIsProcessTrusted())")
print("IOHIDCheckAccess(입력 모니터링) = \(accessText)")

// 실측: 조합키 55(Cmd)와 일반키 11(B)를 스스로 눌러 보고 되읽는다.
guard let src = CGEventSource(stateID: .hidSystemState) else {
    print("판정: 불가 — CGEventSource 생성 실패"); exit(1)
}
func press(_ code: CGKeyCode, _ down: Bool, _ flags: CGEventFlags) {
    let e = CGEvent(keyboardEventSource: src, virtualKey: code, keyDown: down)
    e?.flags = flags
    e?.post(tap: .cghidEventTap)
}
press(55, true, .maskCommand); usleep(60_000)
let modOk = CGEventSource.keyState(.combinedSessionState, key: 55)
press(11, true, .maskCommand); usleep(60_000)
let letterOk = CGEventSource.keyState(.combinedSessionState, key: 11)
press(11, false, .maskCommand); usleep(30_000)
press(55, false, CGEventFlags()); usleep(30_000)

print("조합키(Cmd) 관측  = \(modOk)")
print("일반키(B) 관측    = \(letterOk)")
if letterOk {
    print("판정: OK — 이 셸이 띄우는 StickMate는 전역 단축키를 인식합니다.")
    exit(0)
} else {
    print("판정: 불가 — 일반 키가 보이지 않습니다. 이 셸에 Input Monitoring 권한이 없으므로")
    print("      여기서 띄운 StickMate도 단축키를 인식하지 못합니다(SKILL.md의 Gotchas 참고).")
    exit(3)
}
