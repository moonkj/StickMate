// StickMate 전역 단축키 주입기.
// 왜 osascript로 부족한가: 앱은 CGEventSourceKeyState를 20Hz(50ms)로 폴링해 조합키 3개 + 동작키가
// "같은 샘플 순간"에 눌려 있을 때만 발동한다. osascript의 `key code`는 down/up이 수 ms 안에 끝나
// 폴링 사이로 빠져나간다. 그래서 키를 명시적으로 눌러 두고(hold) 여러 샘플에 걸치게 만든다.
import Foundation
import CoreGraphics

let args = CommandLine.arguments
guard args.count >= 3, let holdMs = Int(args[1]) else {
    FileHandle.standardError.write("usage: keyhold <holdMs> <keycode> [keycode...]\n".data(using: .utf8)!)
    exit(2)
}
let codes: [CGKeyCode] = args[2...].compactMap { UInt16($0) }
guard codes.count == args.count - 2 else {
    FileHandle.standardError.write("error: 키코드는 정수여야 합니다\n".data(using: .utf8)!)
    exit(2)
}

// 조합키 -> CGEventFlags. 앱 자신은 flags를 보지 않고 키코드별 상태만 조회하지만,
// flags를 정확히 실어야 OS가 이 이벤트를 정상적인 조합키 입력으로 취급한다.
func flag(for code: CGKeyCode) -> CGEventFlags? {
    switch code {
    case 55: return .maskCommand
    case 58: return .maskAlternate
    case 59: return .maskControl
    case 56, 60: return .maskShift
    default: return nil
    }
}

guard let src = CGEventSource(stateID: .hidSystemState) else {
    FileHandle.standardError.write("error: CGEventSource 생성 실패\n".data(using: .utf8)!)
    exit(1)
}

var accumulated = CGEventFlags()
// 누르기: 조합키 -> 동작키 순서(인자 순서 그대로). 각 이벤트에 그때까지 쌓인 flags를 싣는다.
for code in codes {
    if let f = flag(for: code) { accumulated.insert(f) }
    guard let e = CGEvent(keyboardEventSource: src, virtualKey: code, keyDown: true) else { exit(1) }
    e.flags = accumulated
    e.post(tap: .cghidEventTap)
    usleep(30_000) // 조합키가 먼저 세션 상태에 반영될 시간
}

usleep(UInt32(holdMs) * 1000)

// 떼기: 역순. 동작키를 먼저 떼야 앱이 "조합키만 남은" 정상 상태를 본다.
for code in codes.reversed() {
    guard let e = CGEvent(keyboardEventSource: src, virtualKey: code, keyDown: false) else { exit(1) }
    if let f = flag(for: code) { accumulated.remove(f) }
    e.flags = accumulated
    e.post(tap: .cghidEventTap)
    usleep(30_000)
}
