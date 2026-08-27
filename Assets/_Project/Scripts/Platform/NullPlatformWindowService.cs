using System.Collections.Generic;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// 에디터/미지원 플랫폼 폴백 구현체. 실제 OS 열거를 하지 않고 더미 발판 1개(작업표시줄/Dock 역할)를
    /// 반환해, 상태머신·발판 인식 로직을 어떤 플랫폼 API 없이도 크래시 없이 에디터에서 테스트할 수 있게 한다.
    /// </summary>
    public sealed class NullPlatformWindowService : IPlatformWindowService, ICursorPositionService
    {
        // 더미 발판 목록. GC 압박 방지를 위해 생성자에서 1회만 만들고 매 호출마다 동일 인스턴스를 재사용한다.
        private readonly List<PlatformFoothold> _dummyFootholds;

        public NullPlatformWindowService()
        {
            // 화면 하단을 가로지르는 가상의 "작업표시줄" 발판 하나.
            // 에디터 테스트용 스텁이므로 해상도 변경 시 재계산하지 않고 생성 시점 값으로 고정한다.
            float width = Screen.width > 0 ? Screen.width : 1920f;
            const float dummyTaskbarHeight = 40f;
            var dummyRect = new Rect(0f, 0f, width, dummyTaskbarHeight);

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
    }
}
