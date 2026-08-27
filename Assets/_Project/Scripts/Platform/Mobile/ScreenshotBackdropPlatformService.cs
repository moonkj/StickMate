using System.Collections.Generic;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;

namespace StickMate.Platform.Mobile
{
    /// <summary>
    /// 모바일(iPad/iPhone) "스크린샷 백드롭 모드"의 IPlatformWindowService 구현체 (아키텍처 0-1절).
    /// iOS/iPadOS 샌드박스 정책상 실오버레이/타 앱 창 열거가 불가능하므로, 유저가 불러온 홈 화면
    /// 스크린샷을 정적 배경으로 표시하고, 유저가 직접 탭으로 지정한 아이콘 줄/Dock 좌표를
    /// "발판"으로 사용한다. 상태머신/이펙트/전투 로직은 데스크톱과 100% 동일 코드를 그대로 쓰고
    /// 이 서비스 구현체만 교체하면 된다 (IPlatformWindowService 다형성).
    ///
    /// UNITY_IOS/UNITY_ANDROID 가드를 걸지 않은 이유: 아키텍처 요구사항에 따라 에디터에서도
    /// 이 서비스를 강제로 선택해 스크린샷 백드롭 모드를 미리보기/테스트할 수 있어야 하기 때문
    /// (범용 모바일 서비스). 실제 플랫폼별 자동 선택 로직(UNITY_IOS || UNITY_ANDROID 분기)은
    /// 이 클래스가 아니라 추후 별도 팩토리/부트스트랩 코드의 책임이다.
    /// </summary>
    public sealed class ScreenshotBackdropPlatformService : IPlatformWindowService
    {
        /// <summary>
        /// 유저가 불러온 홈 화면 스크린샷. 배경 렌더링용으로만 쓰이며 발판 판정 로직 자체에는
        /// 관여하지 않는다 (발판은 아래 유저 지정 사각형 리스트로만 결정 — 이미지 픽셀 분석 기반
        /// 아이콘 그리드 자동 감지는 2차 고도화 항목, 아키텍처 0-1절 참고).
        /// </summary>
        public Texture2D BackdropScreenshot { get; private set; }

        // 유저가 탭으로 지정한 발판 사각형 목록(아이콘 줄/Dock). 런타임에 추가/삭제 가능한 정적 리스트.
        private readonly List<PlatformFoothold> _userDefinedFootholds = new List<PlatformFoothold>(16);

        // 다음에 추가될 발판에 부여할 id 시퀀스. 모바일에는 OS 핸들 개념이 없어 단순 증가 id로 대체한다.
        private long _nextFootholdId = 1L;

        /// <summary>
        /// 코어 루프를 시작해도 되는지("발판이 최소 1개는 지정되어 있는지") 판정하는 가드.
        /// UX_FLOW.md 6-1절(최초 빈 상태)/9절-7 요구사항: 발판 0개 상태로 코어 루프가 그냥 시작되면
        /// 캐릭터가 설 곳이 없는 상태가 되므로, 상위 부트스트랩 코드는 이 값이 false인 동안
        /// 발판 탭 지정 온보딩(3절)을 계속 노출해야 한다.
        /// </summary>
        public bool IsConfigured => _userDefinedFootholds.Count > 0;

        /// <summary>
        /// 유저가 사진첩에서 고른(또는 앱이 제안한) 홈 화면 스크린샷을 배경으로 지정한다.
        /// 원본 사진첩 파일은 읽기 전용으로 로드할 뿐 절대 수정/삭제하지 않는다 (유저 자산 불변 원칙).
        ///
        /// 배경 교체 시 기존 발판 좌표는 옛 이미지 기준이라 더 이상 유효하지 않으므로, 배경 갱신과
        /// 발판 무효화를 같은 호출(트랜잭션) 안에서 함께 처리한다 — "배경은 새 이미지인데 발판 좌표는
        /// 옛 이미지 기준"인 불일치 상태를 허용하지 않는다 (UX_FLOW.md 3절/7절, 9절-8).
        /// </summary>
        public void SetBackdropScreenshot(Texture2D screenshot)
        {
            BackdropScreenshot = screenshot;
            ClearUserDefinedFootholds(); // 배경 교체와 같은 트랜잭션으로 발판 좌표 무효화 (재온보딩 유도)
        }

        /// <summary>
        /// 유저가 화면을 탭해 새 발판(아이콘 줄/Dock 영역)을 지정할 때 호출.
        /// screenRect는 이 서비스 기준 스크린 좌표계(좌상단 원점) — UX 입력 레이어가 터치 좌표를
        /// 이 좌표계로 변환해 전달한다. 반환된 id로 나중에 RemoveUserDefinedFoothold를 호출할 수 있다.
        /// </summary>
        public long AddUserDefinedFoothold(Rect screenRect)
        {
            long id = _nextFootholdId++;
            // 모바일은 "최상단(포그라운드) 창" 개념이 없음 — 유저가 지정한 정적 사각형은 모두 동급이므로 항상 true.
            _userDefinedFootholds.Add(new PlatformFoothold(id, screenRect, isTopmost: true));
            StickmanEventBus.RaiseFootholdsChanged(); // 발판 목록 변경을 알려 지형 재계산을 트리거
            return id;
        }

        /// <summary>유저가 잘못 지정한 발판을 삭제할 때 호출. 성공 시 true.</summary>
        public bool RemoveUserDefinedFoothold(long footholdId)
        {
            for (int i = 0; i < _userDefinedFootholds.Count; i++)
            {
                if (_userDefinedFootholds[i].Handle == footholdId)
                {
                    _userDefinedFootholds.RemoveAt(i);
                    StickmanEventBus.RaiseFootholdsChanged();
                    return true;
                }
            }
            return false;
        }

        /// <summary>온보딩을 다시 시작하거나 배경을 교체할 때 기존 발판 지정을 모두 초기화.</summary>
        public void ClearUserDefinedFootholds()
        {
            _userDefinedFootholds.Clear();
            StickmanEventBus.RaiseFootholdsChanged();
        }

        public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => _userDefinedFootholds;

        // 모바일은 오버레이가 아니라 이 앱 자체가 포그라운드 앱 — 별도로 "생성"할 오버레이 창 개념이 없으므로
        // 이미 존재하는 것으로 간주해 항상 성공(true) 취급.
        public bool CreateOverlayWindow() => true;

        // 클릭관통 개념 없음 (비침해 원칙의 클릭관통은 "타 앱 위 오버레이"를 전제로 함).
        // 모바일은 유저가 탭하면 캐릭터가 반응하는 것이 기본 상호작용이므로 no-op.
        public void SetClickThrough(bool enabled) { /* no-op: 모바일에는 클릭관통 개념이 없음 */ }

        // 창을 띄우는 개념이 없으므로 "항상 위" 개념도 없음.
        public void SetAlwaysOnTop(bool enabled) { /* no-op: 모바일에는 창 Z-order 개념이 없음 */ }

        // 전체화면 게임 감지는 "타 앱 위에 떠 있는 오버레이"를 전제로 한 개념 — 모바일에서는 이 앱이 곧
        // 포그라운드 앱이므로 "다른 전체화면 앱"이라는 개념이 성립하지 않아 항상 false.
        public bool IsFullscreenAppActive() => false;
    }
}
