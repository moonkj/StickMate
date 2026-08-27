using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Platform
{
    /// <summary>
    /// IPlatformWindowService.EnumerateFootholds()를 감싸는 절제된 폴러.
    /// (Debugger BUG-M3 대응: "매 프레임 호출 금지" 계약을 강제하는 코드가 전혀 없었음 — 이 클래스가
    /// 그 계약을 코드 레벨로 강제한다.)
    ///
    /// 왜 필요한가: IPlatformWindowService.EnumerateFootholds()는 데스크톱 구현체(Win32WindowService)에서
    /// EnumWindows P/Invoke + 창마다 IsWindowVisible/GetWindowTextLength/GetWindowRect 3회 호출을
    /// 수행하는 상대적으로 무거운 작업이다. 상태머신 Tick()이 이걸 매 프레임 직접 호출하면 24시간
    /// 상주 앱에서 불필요한 OS 호출 부하가 누적된다. 이 클래스는 StickConfig.footholdPollInterval
    /// 주기로만 실제 열거를 수행하고, 그 사이 프레임에는 마지막으로 열거한 결과를 캐시로 재사용한다.
    ///
    /// 컨벤션: States/*.cs는 IPlatformWindowService.EnumerateFootholds()를 절대 직접 호출하지 않고
    /// 반드시 이 클래스의 CachedFootholds(또는 이를 감싼 StickmanBlackboard.SenseGround())만 읽는다.
    ///
    /// 알림 방식: 캐시가 이전 폴링과 달라졌을 때만 StickmanEventBus.FootholdsChanged를 발생시켜
    /// 구독자(향후 렌더링/디버그 오버레이 등)가 "변경 있을 때만" 반응하게 한다. 상태머신 자체는
    /// 매 프레임 CachedFootholds를 읽어 접지 판정을 하되(메모리 읽기일 뿐 OS 호출이 아니므로 저렴하다),
    /// 실제 OS 재열거 빈도만 이 클래스가 통제한다.
    ///
    /// 모바일(ScreenshotBackdropPlatformService)에서는 유저가 발판을 추가/삭제할 때 그 서비스가 스스로
    /// StickmanEventBus.RaiseFootholdsChanged()를 즉시 호출해 UX_FLOW.md 3절이 요구하는 "탭 즉시 피드백"을
    /// 보장한다. 이 폴러가 그 위에 추가로 주기 폴링을 얹는 것은 중복이지만 무해하다(캐시가 이미
    /// 최신이면 변경 감지 로직이 이벤트를 재발행하지 않음) — 데스크톱과 동일한 코드 경로를 유지하기
    /// 위한 의도적 트레이드오프(Coder→Debugger 확인 요청 항목, Tasklist.md 참고).
    /// </summary>
    public sealed class FootholdPoller
    {
        private readonly IPlatformWindowService _service;
        private readonly StickConfig _config;

        // 마지막으로 확정된 발판 스냅샷. 서비스 구현체가 내부적으로 재사용하는 List를 그대로 들고
        // 있으면(Null/ScreenshotBackdrop/Win32 구현체 모두 내부 버퍼 재사용 컨벤션을 따름) 다음
        // EnumerateFootholds() 호출 때 같은 참조가 제자리에서 바뀌어 "이전 값과 비교"가 불가능해진다.
        // 그래서 별도의 안정된 캐시 리스트에 값(구조체)을 복사해 보관한다. 폴링 주기(0.2~0.5초)마다
        // 한 번만 복사하므로 매 프레임 할당 금지 규칙과 충돌하지 않는다.
        private readonly List<PlatformFoothold> _cache = new List<PlatformFoothold>(64);

        // BUG-P1-M4 대응(Major, docs/BUG_REPORT_PHASE1.md, Phase 0 Minor m2 재발): IReadOnlyList<T>로
        // 노출해도 List<T>가 그 인터페이스를 구현하므로 호출부가 캐스팅해 Add/Clear/Sort로 변형할 수
        // 있었다. _cache.AsReadOnly()는 같은 List를 그대로 감싸는 "살아있는" 뷰이므로(내부 리스트가
        // 바뀌면 이 래퍼를 통한 조회에도 즉시 반영됨) 생성자에서 1회만 감싸두면 된다 — Poll()이 캐시
        // 내용을 갱신할 때마다 매번 새로 감쌀 필요가 없다(그러면 매 프레임 호출되는 프로퍼티에서
        // 할당이 생겨 24시간 상주 앱 GC 압박 방지 컨벤션과 충돌한다).
        private readonly ReadOnlyCollection<PlatformFoothold> _readOnlyCache;

        private float _timer;

        public IReadOnlyList<PlatformFoothold> CachedFootholds => _readOnlyCache;

        public FootholdPoller(IPlatformWindowService service, StickConfig config)
        {
            _service = service;
            _config = config;
            _timer = 0f;
            _readOnlyCache = _cache.AsReadOnly();
            Poll(); // 첫 프레임부터 발판 정보가 있어야 "빈 화면에 멈춰 보임"을 피할 수 있다 (UX_FLOW.md 6-1절).
        }

        /// <summary>매 프레임 호출해도 안전 — 내부적으로 StickConfig.footholdPollInterval 주기를 스스로 지킨다.</summary>
        public void Tick(float deltaTime)
        {
            _timer += deltaTime;
            float interval = _config != null ? Mathf.Max(0.05f, _config.footholdPollInterval) : 0.5f;
            if (_timer < interval) return;
            _timer = 0f;
            Poll();
        }

        /// <summary>다음 Tick을 기다리지 않고 즉시 재열거하고 싶을 때(예: 모바일 온보딩 완료 직후) 사용.</summary>
        public void PollImmediately()
        {
            _timer = 0f;
            Poll();
        }

        private void Poll()
        {
            IReadOnlyList<PlatformFoothold> latest = _service.EnumerateFootholds();
            if (!HasChanged(latest)) return;

            _cache.Clear();
            for (int i = 0; i < latest.Count; i++)
            {
                _cache.Add(latest[i]);
            }
            StickmanEventBus.RaiseFootholdsChanged();
        }

        private bool HasChanged(IReadOnlyList<PlatformFoothold> latest)
        {
            if (latest.Count != _cache.Count) return true;
            for (int i = 0; i < latest.Count; i++)
            {
                PlatformFoothold a = _cache[i];
                PlatformFoothold b = latest[i];
                if (a.Handle != b.Handle || a.IsTopmost != b.IsTopmost || a.ScreenRect != b.ScreenRect)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
