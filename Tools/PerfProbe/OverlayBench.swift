// OverlayBench.swift — 컴포지터 비용이 "합성 표면적"에 비례하는지 실측하기 위한 최소 오버레이.
//
// StickMate의 Unity Player가 하는 일을 최소 형태로 흉내낸다:
//   · 투명(비불투명) + 테두리 없음 + 항상 위 + 클릭 관통 창
//   · 매 프레임 창 전체 크기의 드로어블을 clear 하고 present (Unity의 present와 동일한 성질:
//     dirty-rect 부분 갱신이 아니라 전체 표면 제출)
// 파라미터로 창 크기와 "매 프레임 창 이동" 여부만 바꿔 가며 WindowServer 부하를 비교한다.
//
// usage: OverlayBench <width_pt> <height_pt> <moveEveryFrame 0|1> <durationSec> [alpha]

import AppKit
import Metal
import QuartzCore

let args = CommandLine.arguments
let altMode = args.count > 1 && args[1] == "alt"
let ai = altMode ? 1 : 0
guard args.count >= 5 + ai,
      let W = Double(args[1 + ai]), let H = Double(args[2 + ai]),
      let move = Int(args[3 + ai]), let dur = Double(args[4 + ai]) else {
    FileHandle.standardError.write("usage: OverlayBench w h move dur [alpha]\n".data(using: .utf8)!)
    exit(2)
}
let alpha = args.count >= 6 + ai ? (Double(args[5 + ai]) ?? 0.12) : 0.12
// alt 모드: OverlayBench alt <w1> <h1> <move> <dur> <alpha> <w2> <h2> <phaseSec>
let W2 = altMode && args.count >= 9 ? (Double(args[7]) ?? 640) : 640
let H2 = altMode && args.count >= 9 ? (Double(args[8]) ?? 640) : 640
let phaseSec = altMode && args.count >= 10 ? (Double(args[9]) ?? 3.0) : 3.0
// 창 이동 주기: N프레임마다 1회 이동(1 = 매 프레임). "청크 점프" 완화책의 효과 측정용.
let moveDiv = args.count >= 11 ? (Int(args[10]) ?? 1) : 1

let app = NSApplication.shared
app.setActivationPolicy(.accessory)
// App Nap 방지 — 백그라운드 액세서리 앱은 타이머가 코얼레스되어 60Hz가 나오지 않는다.
let activity = ProcessInfo.processInfo.beginActivity(
    options: [.userInitiated, .latencyCritical, .idleSystemSleepDisabled],
    reason: "overlay compositor benchmark")
_ = activity

final class MetalOverlayView: NSView {
    var device: MTLDevice!
    var queue: MTLCommandQueue!
    var metalLayer: CAMetalLayer!
    var frames: Int = 0

    override init(frame: NSRect) {
        super.init(frame: frame)
        wantsLayer = true
        device = MTLCreateSystemDefaultDevice()
        queue = device.makeCommandQueue()
        let l = CAMetalLayer()
        l.device = device
        l.pixelFormat = .bgra8Unorm
        l.framebufferOnly = true
        l.isOpaque = false
        l.maximumDrawableCount = 3
        l.displaySyncEnabled = false
        l.contentsScale = NSScreen.main?.backingScaleFactor ?? 2.0
        l.frame = bounds
        l.drawableSize = CGSize(width: frame.width * l.contentsScale,
                                height: frame.height * l.contentsScale)
        metalLayer = l
        layer = l
    }
    required init?(coder: NSCoder) { fatalError() }

    var drawableWaitNs: UInt64 = 0
    var renderNs: UInt64 = 0
    func resizeTo(_ w: Double, _ h: Double) {
        let sc = metalLayer.contentsScale
        metalLayer.frame = CGRect(x: 0, y: 0, width: w, height: h)
        metalLayer.drawableSize = CGSize(width: w * sc, height: h * sc)
    }

    func render(_ a: Double) {
        let rt0 = DispatchTime.now().uptimeNanoseconds
        let dr = metalLayer.nextDrawable()
        drawableWaitNs += DispatchTime.now().uptimeNanoseconds - rt0
        guard let drawable = dr,
              let cmd = queue.makeCommandBuffer() else { return }
        let rp = MTLRenderPassDescriptor()
        rp.colorAttachments[0].texture = drawable.texture
        rp.colorAttachments[0].loadAction = .clear
        rp.colorAttachments[0].storeAction = .store
        // 알파를 살짝 흔들어 매 프레임 표면 내용이 실제로 바뀌게 한다(컴포지터가 프레임을 버리지 못하게).
        let t = Double(frames % 120) / 120.0
        let av = a * (0.7 + 0.3 * t)
        rp.colorAttachments[0].clearColor = MTLClearColor(red: 0.0, green: 0.35 * av,
                                                          blue: 0.6 * av, alpha: av)
        if let enc = cmd.makeRenderCommandEncoder(descriptor: rp) { enc.endEncoding() }
        cmd.present(drawable)
        cmd.commit()
        frames += 1
        renderNs += DispatchTime.now().uptimeNanoseconds - rt0
    }
}

let screen = NSScreen.main!
let sf = screen.frame
let win = NSWindow(contentRect: NSRect(x: sf.minX, y: sf.maxY - H - 40, width: W, height: H),
                   styleMask: [.borderless], backing: .buffered, defer: false)
win.isOpaque = false
win.backgroundColor = .clear
win.hasShadow = false
win.level = .floating
win.ignoresMouseEvents = true
win.collectionBehavior = [.canJoinAllSpaces, .stationary, .ignoresCycle, .fullScreenAuxiliary]
let view = MetalOverlayView(frame: NSRect(x: 0, y: 0, width: W, height: H))
win.contentView = view
win.orderFrontRegardless()

var moveNanos: UInt64 = 0
var moveCalls: Int = 0
var phase: Double = 0
var lastPhase: Int = -1
var phaseIsBig = true
let startTime = DispatchTime.now().uptimeNanoseconds
let baseOrigin = win.frame.origin

// 렌더 루프를 전용 고우선순위 스레드에서 정밀 페이싱한다.
// (에이전트 셸에서 스폰된 프로세스는 백그라운드 QoS를 상속해 RunLoop/Dispatch 타이머가
//  16Hz 수준으로 코얼레스된다 — 실측으로 확인. 60Hz를 보장하려면 직접 페이싱해야 한다.)
let renderThread = Thread {
    let period = 1.0 / 60.0
    var next = Date().timeIntervalSinceReferenceDate
    while true {
        if (move == 1 || (move == 2 && phaseIsBig)) && (view.frames % moveDiv == 0) {
            phase += 0.05 * Double(moveDiv)
            let dx = CGFloat(sin(phase) * 200.0)
            let dy = CGFloat(cos(phase) * 120.0)
            DispatchQueue.main.sync {
                let t0 = DispatchTime.now().uptimeNanoseconds
                win.setFrameOrigin(NSPoint(x: baseOrigin.x + 300 + dx, y: baseOrigin.y + dy))
                let t1 = DispatchTime.now().uptimeNanoseconds
                moveNanos += (t1 - t0)
            }
            moveCalls += 1
        }
        if altMode {
            let el = Double(DispatchTime.now().uptimeNanoseconds - startTime) / 1e9
            let idx = Int(el / phaseSec)
            if idx != lastPhase {
                lastPhase = idx
                let big = (idx % 2 == 0)
                phaseIsBig = big
                if move == 3 {
                    // 표면적 실험이 아니라 "합성 대상에서 빠졌을 때"의 바닥값 측정.
                    // 창을 숨겨도 present는 계속 돌아간다(frames 카운터로 확인 가능).
                    DispatchQueue.main.sync { if big { win.orderFrontRegardless() } else { win.orderOut(nil) } }
                    print("PHASE \(big ? "BIG" : "SMALL") visible=\(big)")
                    fflush(stdout)
                    next += period
                    continue
                }
                let w = big ? W : W2, h = big ? H : H2
                DispatchQueue.main.sync {
                    win.setFrame(NSRect(x: sf.minX + 60, y: sf.maxY - h - 40, width: w, height: h),
                                 display: false)
                    view.frame = NSRect(x: 0, y: 0, width: w, height: h)
                    view.resizeTo(w, h)
                }
                print("PHASE \(big ? "BIG" : "SMALL") \(Int(w))x\(Int(h))")
                fflush(stdout)
            }
        }
        view.render(alpha)

        let elapsed = Double(DispatchTime.now().uptimeNanoseconds - startTime) / 1e9
        if elapsed >= dur {
            let avgUs = moveCalls > 0 ? Double(moveNanos) / Double(moveCalls) / 1000.0 : 0
            print("RESULT frames=\(view.frames) elapsed=\(String(format: "%.2f", elapsed)) " +
                  "fps=\(String(format: "%.1f", Double(view.frames) / elapsed)) " +
                  "drawWaitMs=\(String(format: "%.2f", Double(view.drawableWaitNs)/1e6/Double(max(view.frames,1)))) " +
                  "renderMs=\(String(format: "%.2f", Double(view.renderNs)/1e6/Double(max(view.frames,1)))) " +
                  "moveCalls=\(moveCalls) moveAvgUs=\(String(format: "%.1f", avgUs)) " +
                  "drawable=\(Int(view.metalLayer.drawableSize.width))x\(Int(view.metalLayer.drawableSize.height))")
            fflush(stdout)
            exit(0)
        }
        next += period
        let now = Date().timeIntervalSinceReferenceDate
        // 스핀 대기: 이 프로세스는 에이전트 셸에서 스폰되어 백그라운드 QoS로 클램프되며
        // Thread.sleep / dispatch 타이머가 40~60ms로 코얼레스된다(실측). 60Hz present를
        // 보장하려면 스핀할 수밖에 없다. 앱 CPU는 왜곡되지만 측정 대상(WindowServer)은
        // 별 프로세스이고, 스핀 비용은 모든 조건에 동일하게 실린다.
        if next > now {
            while Date().timeIntervalSinceReferenceDate < next { }
        } else { next = now }
    }
}
renderThread.qualityOfService = .userInteractive
renderThread.stackSize = 512 * 1024
renderThread.start()
print("PID \(ProcessInfo.processInfo.processIdentifier) size=\(Int(W))x\(Int(H))pt move=\(move)")
fflush(stdout)
app.run()
