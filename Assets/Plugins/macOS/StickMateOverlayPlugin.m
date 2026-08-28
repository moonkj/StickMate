// StickMateOverlayPlugin.m
//
// StickMate macOS 네이티브 오버레이 플러그인 — Unity Standalone Mac Player의 실제 NSWindow(게임뷰가
// 아니라 진짜 OS 창)를 Cocoa API로 조작하기 위한 최소 C ABI 브릿지. Platform/MacOS/MacWindowService.cs
// 헤더 주석이 설명하듯, CoreGraphics/CoreFoundation의 공개 C ABI만으로는 다른 프로세스는 물론 우리
// 자신의 NSWindow조차 클릭관통(ignoresMouseEvents)/레벨(level)을 "쓰기"로 바꿀 수 있는 수단이 없다 —
// 그건 오직 Cocoa(AppKit)의 Objective-C 런타임을 통해서만 가능하고, 그래서 별도의 네이티브 플러그인이
// 필요하다(이번 라운드 전까지는 Architect 지시로 범위 밖이었음).
//
// 빌드 방법(Xcode 프로젝트 없이 clang 직접 컴파일 — 이 파일과 나란히 있는 build.sh 참고):
//   clang -dynamiclib -arch arm64 -arch x86_64 -mmacosx-version-min=11.0 -framework Cocoa \
//     -o StickMateOverlayPlugin.bundle/Contents/MacOS/StickMateOverlayPlugin StickMateOverlayPlugin.m
//
// 절대 원칙(CLAUDE.md / docs/ARCHITECTURE.md 3절 "유저 자산 불변"): 이 플러그인이 조작하는 창은
// 오직 "우리 자신의 프로세스가 소유한 Unity Player 메인 창" 단 하나뿐이다. 다른 프로세스의 창을
// 열거/조회/조작하는 코드는 이 파일에 존재하지 않는다(그건 MacWindowService.cs의 CoreGraphics
// 읽기 전용 열거 영역의 몫이며, 여기서는 아예 접근 대상이 아니다).
#import <Cocoa/Cocoa.h>

// ============================================================================
// 우리 자신의 메인 창을 찾는 공용 헬퍼.
//
// 정책: [NSApplication sharedApplication].windows를 순회하며 "보이는(isVisible) 창" 중
// isMainWindow==YES인 것을 최우선으로 선택한다. 앱이 아직 완전히 활성화되지 않은 극초반 타이밍(예:
// Start() 프레임)에는 어떤 창도 아직 isMainWindow가 아닐 수 있으므로, 그럴 때는 "처음 발견한
// 보이는 창"을 폴백으로 채택한다(작업 지시서 "보통 첫 번째 또는 isMainWindow인 것" 요구사항 그대로).
// 이 폴백조차 없으면(즉 보이는 창이 하나도 없으면) NULL을 반환하고, 호출부(SM_IsMainWindowFound)가
// 그 사실을 C# 쪽에 정직하게 알린다 — 조용히 no-op하지 않는다(이전 라운드들의 컨벤션과 동일).
// ============================================================================
static NSWindow *StickMate_FindMainWindow(void) {
    NSArray<NSWindow *> *windows = [[NSApplication sharedApplication] windows];
    NSWindow *firstVisibleFallback = nil;

    for (NSWindow *w in windows) {
        if (![w isVisible]) continue;
        if (firstVisibleFallback == nil) {
            firstVisibleFallback = w;
        }
        if ([w isMainWindow]) {
            return w;
        }
    }
    return firstVisibleFallback;
}

// ============================================================================
// extern "C" C ABI 익스포트 — MacWindowService.cs가 [DllImport("StickMateOverlayPlugin")]로 호출한다.
// ============================================================================
#ifdef __cplusplus
extern "C" {
#endif

/// 우리 자신의 Unity Player 메인 창을 실제로 찾을 수 있는지 여부(0/1). MacWindowService의 나머지
/// 함수들이 대상 창을 못 찾은 채 조용히 no-op되지 않도록, 호출부가 이 값으로 실패를 명시적으로
/// 감지할 수 있게 한다.
int SM_IsMainWindowFound(void) {
    return (StickMate_FindMainWindow() != nil) ? 1 : 0;
}

/// 현재 우리 창의 NSWindow.level 원시값을 반환한다(디버그/외부 검증용 — CGWindowListCopyWindowInfo의
/// kCGWindowLayer와 비교 대조하기 위해 필요). 창을 못 찾으면 0(NSNormalWindowLevel과 우연히 같은 값)을
/// 반환하는데, 호출부는 반드시 먼저 SM_IsMainWindowFound()로 존재 여부를 확인한 뒤에 이 값을 신뢰해야
/// 한다(이 함수 자체는 "못 찾음"과 "정상 레벨 0"을 구분해 알려줄 별도 채널이 없다).
int SM_GetOverlayWindowLevel(void) {
    NSWindow *window = StickMate_FindMainWindow();
    if (window == nil) {
        return 0;
    }
    return (int)[window level];
}

/// 우리 자신의 Unity Player 메인 창 하나에만 적용되는 오버레이 설정. 절대 다른 프로세스의 창을
/// 건드리지 않는다(위 StickMate_FindMainWindow가 우리 프로세스 내부의 NSApplication.windows만
/// 순회하므로 애초에 다른 프로세스 창에 접근할 방법 자체가 없다).
///
/// makeClickThrough != 0  -> [window setIgnoresMouseEvents:YES]  (마우스 입력이 아래 창으로 그대로 통과)
/// alwaysOnTop != 0       -> [window setLevel:NSFloatingWindowLevel] (그 외에는 NSNormalWindowLevel로 복귀)
/// transparent != 0       -> 창을 불투명 해제 + 배경을 완전 투명색으로 + 타이틀바 텍스트만 숨김
///                            (신호등 버튼/타이틀바 구조는 보존 — 2026-08-28 보수적 조정, 아래 참고).
///
/// 정직한 한계(작업 지시서가 요구한 대로 기록): Unity Standalone Mac Player의 렌더 서페이스(콘텐츠
/// 뷰 내부의 Metal/OpenGL 레이어)는 엔진이 기본적으로 불투명(opaque)하게 그리도록 되어 있어, 여기서
/// NSWindow 레벨의 setOpaque:NO/backgroundColor=clearColor만으로는 "스틱맨 뒤 데스크톱 배경이 실제로
/// 완전히 비쳐 보이는" 진짜 투명 렌더링까지는 보장하지 못할 수 있다(엔진 렌더 파이프라인을 건드리는
/// 별도 작업이 필요할 수 있음 — Main Camera의 Clear Flags=Solid Color/alpha=0 설정은
/// Assets/Editor/SceneBootstrapper.cs가 담당하지만, 그 알파가 최종 창 콘텐츠 뷰의 CALayer까지
/// 투과되는지는 이 라운드에서 100% 검증되지 않았다). 창 레벨(altitude, 클릭관통)은 순수 AppKit
/// 창 속성이라 이 함수만으로 확실하게 동작한다.
void SM_ConfigureOverlayWindow(int makeClickThrough, int alwaysOnTop, int transparent) {
    NSWindow *window = StickMate_FindMainWindow();
    if (window == nil) {
        NSLog(@"[StickMateOverlayPlugin] SM_ConfigureOverlayWindow: 우리 자신의 창을 찾지 못해 아무 것도 적용하지 않았습니다(no-op). "
              @"NSApplication.windows에 보이는 창이 아직 없는 매우 이른 타이밍일 수 있습니다.");
        return;
    }

    [window setIgnoresMouseEvents:(makeClickThrough != 0)];
    [window setLevel:(alwaysOnTop != 0 ? NSFloatingWindowLevel : NSNormalWindowLevel)];

    if (transparent != 0) {
        [window setOpaque:NO];
        [window setBackgroundColor:[NSColor clearColor]];
        [window setHasShadow:NO];

        // 타이틀바 텍스트만 숨긴다 — 보수적 조정(사용자가 실제로 실행되는 앱을 보고 "이상하게
        // 나온다"고 지적한 뒤, 2026-08-28 재검토). 이전에는 여기서 NSWindowStyleMaskFullSizeContentView도
        // 함께 켜서 콘텐츠 뷰를 타이틀바 영역까지 확장시켰는데, 그 결과 불투명하게 그려지는 Unity
        // 게임 렌더 서페이스가 타이틀바 신호등 버튼(빨강/노랑/초록, close/miniaturize/zoom) 바로
        // 아래까지 파고들게 된다 — 신호등 버튼 자체가 사라지거나 클릭 불가능해지는 것은 아니지만,
        // 버튼이 정상적인 타이틀바 배경 스트립 위가 아니라 게임 화면 바로 위에 붕 뜬 것처럼 보이는
        // 부자연스러운 경계가 생길 수 있다 — 사용자가 지적한 "창이 이상해 보인다"는 증상과 정확히
        // 들어맞을 수 있는 원인이라고 판단해 제거했다.
        //
        // FullSizeContentView 없이 titlebarAppearsTransparent만 켜면: 타이틀바 영역은 원래 표준
        // 레이아웃(신호등이 놓이는 그 스트립)을 그대로 유지한 채 배경만 창 배경색(투명 설정 시
        // clearColor)에 맞춰지고, 콘텐츠 뷰는 여전히 타이틀바 아래에서 시작한다 — 즉 타이틀 "텍스트"만
        // 사라지고 창의 나머지 테두리/버튼 배치는 정상 윈도우와 동일하게 보인다. 완전 Borderless
        // (styleMask에서 NSWindowStyleMaskTitled 자체를 제거)는 더 과감한 변경이라 이번에는 시도하지
        // 않았다 — 필요하면 사용자 육안 확인 후 다음 라운드에서 재검토할 것.
        [window setTitlebarAppearsTransparent:YES];
        [window setTitleVisibility:NSWindowTitleHidden];

        // 콘텐츠 뷰 레이어까지 비-불투명으로 시도(엔진 렌더 서페이스가 매 프레임 자신의 불투명
        // 설정을 되돌릴 수 있어 완전한 보장은 아니다 — 위 함수 문서 주석의 "정직한 한계" 참고).
        NSView *contentView = [window contentView];
        if (contentView != nil) {
            [contentView setWantsLayer:YES];
            contentView.layer.opaque = NO;
            contentView.layer.backgroundColor = [[NSColor clearColor] CGColor];
        }
    }

    NSLog(@"[StickMateOverlayPlugin] SM_ConfigureOverlayWindow 적용 완료: clickThrough=%d alwaysOnTop=%d transparent=%d level=%ld",
          makeClickThrough, alwaysOnTop, transparent, (long)[window level]);
}

#ifdef __cplusplus
}
#endif
