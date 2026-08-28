using System.Collections.Generic;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// 에디터/미지원 플랫폼 폴백 구현체. 실제 OS 열거를 하지 않고 더미 발판 1개(작업표시줄/Dock 역할)를
    /// 반환해, 상태머신·발판 인식 로직을 어떤 플랫폼 API 없이도 크래시 없이 에디터에서 테스트할 수 있게 한다.
    /// </summary>
    public sealed class NullPlatformWindowService : IPlatformWindowService, ICursorPositionService, ILocalClickCaptureService, IDesktopIconLayoutService
    {
        // 더미 발판 목록. GC 압박 방지를 위해 생성자에서 1회만 만들고 매 호출마다 동일 인스턴스를 재사용한다.
        private readonly List<PlatformFoothold> _dummyFootholds;

        // ILocalClickCaptureService(UX_FLOW.md 15절) — 에디터에는 애초에 클릭관통을 흉내낼 네이티브
        // 창이 없으므로(SetClickThrough가 no-op) 여기도 순수 소유권/영역 부기만 제공한다. 이 부기만으로도
        // Phase 3 컨트롤러(Interaction/*)가 "동시에 두 이벤트가 캐릭터 클릭을 다투는 상황"을 에디터에서
        // 그대로 재현/검증할 수 있다는 실익이 있다(ILocalClickCaptureService.cs 핵심 한계 문서 참고).
        private readonly LocalClickCaptureGate _clickCaptureGate = new LocalClickCaptureGate();

        public bool RequestLocalClickCapture(Rect hitboxOsScreen, object owner)
            => _clickCaptureGate.TryRequestCapture(hitboxOsScreen, owner);

        public void UpdateLocalClickCaptureRegion(Rect hitboxOsScreen, object owner)
            => _clickCaptureGate.UpdateRegion(hitboxOsScreen, owner);

        public void ReleaseLocalClickCapture(object owner)
            => _clickCaptureGate.ReleaseCapture(owner);

        public bool IsLocalClickCaptureOwnedBy(object owner)
            => _clickCaptureGate.IsOwnedBy(owner);

        // BUG-SW-M2 대응(Architect 결정, 2026-08-28, docs/BUG_REPORT_SCENE_WIRING.md): 이 더미 발판의
        // OS-px 폭을 화면 폭(Screen.width) 그대로 쓰면, Platform/ScreenCoordinateConverter.cs의 변환이
        // 정확히 카메라 뷰포트 폭과 1:1로 대응하므로(px=0/Screen.width가 각각 뷰포트의 왼쪽/오른쪽
        // 가장자리 world X로 그대로 역변환됨) 발판의 월드 폭이 항상 "카메라에 보이는 만큼"으로 orthographicSize에
        // 종속되어버린다. 이전 라운드는 "자율 배회 AI가 15초 관찰 구간 안에 화면 끝에 닿는다"는 문제를
        // 해결하려고 SceneBootstrapper.cs의 카메라 orthographicSize를 5→20으로 키웠는데, 그 방식은
        // px/world-unit 변환 비율 자체를 바꿔버려 StickConfig.groundSnapTolerance 및 다른 7개 OS-px
        // 단위 필드(wanderCursorReactionRadiusPx 등)의 유효 월드 크기까지 의도치 않게 조용히 4배
        // 넓혀버리는 부작용을 냈다(BUG-SW-M2). 카메라 크기는 건드리지 않고 이 더미 발판의 OS-px 폭만
        // 화면 폭의 배수로 독립적으로 넓혀 배회 관찰 범위 문제를 해결한다 — 이렇게 하면 px/world-unit
        // 스케일은 groundSnapTolerance 등이 가정하는 값 그대로 유지된다.
        //
        // ★ 2026-08-28 되돌림(리더 지시, 사용자 피드백 "캐릭터가 화면 벗어나서 잘 안 보임"):
        // 배율을 4 -> 1(=화면 폭과 정확히 일치)로 되돌린다. 아래 두 문단은 이 배율이 왜 4였는지에 대한
        // 이력이며, 그 이유는 이미 사라졌다:
        //   - 4배로 넓힌 목적은 "자율 배회 AI가 관찰 구간 안에 발판 끝에 닿아 Fall로 떨어져 고착된다"는
        //     문제의 우회였는데, 그 진짜 원인(발판이 하나도 없거나 경계를 벗어났을 때의 낙하 고착)은
        //     FallbackPlatformWindowService가 항상 안전망 발판을 제공하도록 고치면서 해결됐다.
        //   - 남은 것은 부작용뿐이었다: 발판이 화면보다 4배 넓으니 AutoWanderController의 경계 판정
        //     (경계 도달 시 정지 후 방향 전환)이 **화면 밖 한참 먼 곳**에서야 걸려, 캐릭터가 화면
        //     바깥으로 걸어나가 보이지 않는 시간이 길었다(사용자 신고 증상 그 자체).
        // 배율 1이면 그 경계 판정이 곧 화면 가장자리 판정이 되어 캐릭터가 항상 화면 안에 머문다.
        // 또한 AutoWanderController는 이 경계가 "화면 자체의 끝"일 때(isTrueScreenEdge) 점프 시도
        // 확률을 0으로 두므로, 가장자리에서 점프로 발판 밖에 착지하는 경로도 열리지 않는다.
        //
        // public인 이유(BUG-P1-R5-B3 대응, Coder 실측 발견, 2026-08-28): 이 폭 넓히기는 원래 "에디터
        // 테스트에서 배회 관찰 범위가 좁다"는 문제만 풀려고 만들었는데, 실제 Standalone .app을 60초+
        // 실행해보니 `Platform/FallbackPlatformWindowService.cs`(macOS/Windows 실제 빌드가 실제 창을
        // 하나도 못 찾을 때 쓰는 안전망 발판)는 이 폭 넓히기를 전혀 적용하지 않고 있어서, 실제 배포
        // 환경의 "안전한 배회 범위"가 에디터 테스트보다 4배나 좁았다(뷰포트 폭 그대로, 절반폭 약
        // 8유닛). `AutoWanderController`의 한 Walk 페이즈 최대 이동거리(walkSpeed×wanderWalkDurationMax×
        // 지터, 기본값 기준 약 11.75유닛)가 이 좁은 절반폭을 초과할 수 있어, 에디터에서는 몇 시간을
        // 돌려도 거의 안 걸리던 "발판 가장자리 이탈 후 영원히 낙하 고착" 경로가 실제 배포 환경에서는
        // 수십 초 안에 실제로 재현됐다(제자리 점프가 하필 가장자리 근처에서 발동해 착지 시 발판 밖으로
        // 벗어나는 경로로 추정). Editor/SceneBootstrapper.cs와 이 값을 단일 소스로 공유해야 어긋나지
        // 않는다는 `DummyFootholdHeightFraction`과 동일한 원칙에 따라, 이제 이 폭 배율도
        // FallbackPlatformWindowService.cs가 재사용할 수 있도록 public으로 승격한다.
        public const float DummyFootholdWidthMultiplier = 1f;

        // BUG-P1-R4-B1 핫픽스(2026-08-28, Architect 진단 — 사용자가 GUI 에디터에서 Main.unity를 직접
        // Play시켜 육안으로 "화면 제일 상단에서 뭔가 걸려 잘려 보인다"고 보고, 캐릭터가 카메라 뷰포트
        // 최상단 가장자리에 걸쳐 정착하는 것으로 진단됨): 이 발판의 세로 위치를 더 이상 고정 픽셀
        // 두께(예전 40f)로 화면 맨 아래에서 잡지 않고, 화면 세로 길이(Screen.height)에 대한 "비율"로
        // 잡는다. 두 가지 독립적인 이유가 있다.
        //
        // (1) [근본 원인 자체] 예전 코드는 `new Rect(widenedX, 0f, ...)`로 이 발판을 만들었다.
        //     Platform/ScreenCoordinateConverter.cs 문서의 좌표계(좌상단 원점, y 아래로 갈수록 증가)에서
        //     y=0은 화면의 "맨 위"다 — 즉 주석은 "작업표시줄"이라 해놓고 실제로는 화면 최상단에 배치한
        //     반대 버그였다. Platform/FallbackPlatformWindowService.cs가 예전에(BUG-P1-R3-B1) 정확히
        //     같은 종류의 실수를 고친 적이 있는데(그때는 이 클래스를 건드리지 않아 여기 남아 있었다),
        //     그 클래스와 동일한 패턴 — Rect의 y를 `Screen.height - 발판두께`로 잡아 화면 진짜 하단
        //     근처에 둔다 — 을 그대로 따른다.
        // (2) [단순히 위/아래만 뒤집으면 안 되는 이유] Editor/SceneBootstrapper.cs는 이 발판의 상단
        //     가장자리가 변환되는 월드 Y를 기준으로 캐릭터/지면을 배치한다. 발판 두께를 예전처럼 고정
        //     픽셀값(40f)으로 두면, 이 상단 가장자리가 "화면 맨 아래에서 몇 유닛 위"인지가 실제
        //     Screen.height에 반비례해 달라진다 — 해상도가 클수록(예: GUI 에디터의 큰 Game View)
        //     발판이 화면 맨 아래에 더 바짝 붙어, 이번에는 캐릭터가 화면 "하단" 가장자리에 걸려 잘리는
        //     동일 계열 버그가 반대쪽에서 재발할 위험이 있다. 두께를 Screen.height의 고정 "비율"로
        //     잡으면 이 상단 가장자리의 월드 Y가 (Editor/SceneBootstrapper.cs의 ComputeGroundTopWorldY
        //     유도 과정 참고) Screen.height 실측값과 무관하게 항상 `cam.y - orthographicSize*(1-2*fraction)`
        //     라는 카메라 설정만의 폐쇄형 값으로 귀결되어, 어떤 해상도(배치모드 640x480이든 GUI의 임의
        //     Game View 크기든)에서도 캐릭터 전신이 뷰포트 안에 여유 있게 들어오도록 보장할 수 있다.
        //
        // public인 이유: Editor/SceneBootstrapper.cs가 groundTopWorldY를 계산할 때 이 비율을 직접
        // 재사용해야 한다(매직 넘버로 각자 따로 계산하면, 두 파일의 가정이 서로 어긋나 버린 것 자체가
        // 이번 버그의 근본 원인 중 하나였다 — 재발 방지를 위해 단일 소스로 강제).
        public const float DummyFootholdHeightFraction = 0.2f;

        public NullPlatformWindowService()
        {
            // 화면 하단 근처를 가로지르는 가상의 "작업표시줄" 발판 하나. 폭은 화면 폭의
            // DummyFootholdWidthMultiplier배로, 화면 중심(=world x=0, 카메라가 x=0에 위치)을 기준으로
            // 좌우 대칭 확장한다 — 배회 AI가 카메라 뷰포트보다 훨씬 넓은 범위를 돌아다닐 수 있다.
            // 에디터 테스트용 스텁이므로 해상도 변경 시 재계산하지 않고 생성 시점 값으로 고정한다.
            float baseWidth = Screen.width > 0 ? Screen.width : 1920f;
            float widenedWidth = baseWidth * DummyFootholdWidthMultiplier;
            float widenedX = (baseWidth - widenedWidth) / 2f;

            // 위 DummyFootholdHeightFraction 문서 참고 — 화면 진짜 하단에서 위로 이 비율만큼의 두께를
            // 갖는 발판. FallbackPlatformWindowService.GetFallbackFoothold()와 동일한 "y = height - 두께"
            // 패턴으로 화면 하단 근처에 둔다(예전처럼 y=0에 두지 않음).
            float baseHeight = Screen.height > 0 ? Screen.height : 1080f;
            float dummyTaskbarHeight = baseHeight * DummyFootholdHeightFraction;
            var dummyRect = new Rect(widenedX, baseHeight - dummyTaskbarHeight, widenedWidth, dummyTaskbarHeight);

            _dummyFootholds = new List<PlatformFoothold>(1)
            {
                new PlatformFoothold(handle: 1L, screenRect: dummyRect, isTopmost: true)
            };
        }

        public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => _dummyFootholds;

        // 에디터에서는 Game View 자체가 창 역할을 하므로 별도 오버레이를 만들 필요 없이 항상 성공 취급.
        public bool CreateOverlayWindow() => true;

        // 에디터/미지원 플랫폼에서는 클릭관통을 흉내낼 네이티브 창이 없으므로 no-op.
        public void SetClickThrough(bool enabled) { }

        // 동일한 이유로 no-op.
        public void SetAlwaysOnTop(bool enabled) { }

        // "다른 전체화면 앱"을 감지할 수단이 없으므로 항상 false (자동 숨김을 트리거하지 않음).
        public bool IsFullscreenAppActive() => false;

        // ICursorPositionService — 에디터에는 실제 OS 전역 커서 API가 없으므로 Unity가 아는 마우스
        // 좌표(Game View 기준)로 대체한다. Input.mousePosition은 Unity 스크린 좌표(좌하단 원점)이므로
        // PlatformFoothold와 동일한 좌상단 원점 OS 좌표계로 변환해서 반환한다
        // (Platform/ScreenCoordinateConverter.cs의 좌표계 설명과 동일한 y 반전 규칙).
        public bool TryGetGlobalCursorPosition(out Vector2 osScreenPosition)
        {
            Vector3 mouse = Input.mousePosition;
            osScreenPosition = new Vector2(mouse.x, Screen.height - mouse.y);
            return true;
        }

        // IDesktopIconLayoutService(UX_FLOW.md 27-2/27-5절) — 에디터에는 실제 OS 아이콘 조회 API가
        // 없으므로(IDesktopIconLayoutService.cs 문서 상단 "알려진 한계" 참고), 화면 좌상단에 합성
        // 아이콘 그리드를 반환해 청소부/블랙홀의 오버레이 파이프라인·취소 판정 로직을 에디터에서 검증할
        // 수 있게 한다. 실제 아이콘이 아니므로 클릭해도 실행되는 앱이 없지만, "좌표 조회 → 오버레이 →
        // 취소 판정" 구조 자체를 테스트하는 데는 충분하다. 해상도 변경 시 재계산하지 않고 생성 시점
        // 값으로 고정(다른 더미 값들과 동일한 컨벤션).
        private const int IconGridColumns = 4;
        private const int IconGridRows = 3;
        private const float IconCellSize = 64f;
        private const float IconCellSpacing = 16f;
        private readonly List<Rect> _dummyIconRects = new List<Rect>(IconGridColumns * IconGridRows);
        private Rect _dummyIconRegion;
        private bool _dummyIconGridBuilt;

        private void EnsureDummyIconGridBuilt()
        {
            if (_dummyIconGridBuilt) return;
            _dummyIconGridBuilt = true;

            const float originX = 24f;
            const float originY = 24f;
            float step = IconCellSize + IconCellSpacing;

            for (int row = 0; row < IconGridRows; row++)
            {
                for (int col = 0; col < IconGridColumns; col++)
                {
                    _dummyIconRects.Add(new Rect(originX + col * step, originY + row * step, IconCellSize, IconCellSize));
                }
            }

            float width = (IconGridColumns - 1) * step + IconCellSize;
            float height = (IconGridRows - 1) * step + IconCellSize;
            _dummyIconRegion = new Rect(originX, originY, width, height);
        }

        public bool TryGetIconRegion(out Rect osScreenRegion)
        {
            EnsureDummyIconGridBuilt();
            osScreenRegion = _dummyIconRegion;
            return true;
        }

        public IReadOnlyList<Rect> EnumerateIconRects()
        {
            EnsureDummyIconGridBuilt();
            return _dummyIconRects;
        }
    }
}
