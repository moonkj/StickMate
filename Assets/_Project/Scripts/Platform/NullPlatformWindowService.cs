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
        private const float DummyFootholdWidthMultiplier = 4f;

        public NullPlatformWindowService()
        {
            // 화면 하단을 가로지르는 가상의 "작업표시줄" 발판 하나. 폭은 화면 폭의
            // DummyFootholdWidthMultiplier배로, 화면 중심(=world x=0, 카메라가 x=0에 위치)을 기준으로
            // 좌우 대칭 확장한다 — 배회 AI가 카메라 뷰포트보다 훨씬 넓은 범위를 돌아다닐 수 있다.
            // 에디터 테스트용 스텁이므로 해상도 변경 시 재계산하지 않고 생성 시점 값으로 고정한다.
            float baseWidth = Screen.width > 0 ? Screen.width : 1920f;
            float widenedWidth = baseWidth * DummyFootholdWidthMultiplier;
            float widenedX = (baseWidth - widenedWidth) / 2f;
            const float dummyTaskbarHeight = 40f;
            var dummyRect = new Rect(widenedX, 0f, widenedWidth, dummyTaskbarHeight);

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
