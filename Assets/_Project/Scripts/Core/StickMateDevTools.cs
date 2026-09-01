using System;
using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// ★ 개발자 전용 기능의 <b>단일 게이트</b> — docs/UX_FLOW.md <b>36-2</b> 확정 규정.
    ///
    /// ============================================================================
    /// 무엇을 잠그는가
    /// ============================================================================
    /// 36-1이 전수 분류한 (다) "순수 개발자용" 5개 + 가출 <b>발동</b>측이다:
    /// 진단 로그 토글(D) / 하드웨어 반응 미리보기(H) / 스트레스 게이지 순환(S) /
    /// 할일 알림 데모(J) / 집중 모드 90초 데모(F) / 가출 강제 발동(N의 발동 분기).
    ///
    /// 이들의 공통점은 <b>"표시된 것과 실제가 다르다"를 만든다</b>는 것이다 — 배터리가 90%인데 배터리
    /// 경고 연기를 보여주고, 사용자가 적지 않은 할일을 목록에 넣고, 스트레스 게이지 표시값을 조작하고,
    /// 25분이라 적힌 집중 세션을 90초로 만든다. 전부 CLAUDE.md 원칙 1의 정면 위반이라 사용자 UI에
    /// 상설 설치될 수 없다. 기능을 끄는 것이 아니라 <b>미리보기 경로</b>만 막는 것이다
    /// (<c>StickConfig</c>의 자동 발동 확률과는 무관하다 — 36-2 규칙 4).
    ///
    /// ============================================================================
    /// ★ 왜 환경변수 경로가 반드시 있어야 하는가
    /// ============================================================================
    /// <c>DEVELOPMENT_BUILD</c>만으로 잠그면 <b>검증 수단을 같이 죽인다</b>. 이 팀의 표준 절차는
    /// "릴리스 빌드를 실제로 켜고 <c>Player.log</c>로 확인한다"이고(debugger/test-engineer가 오늘까지
    /// 그렇게 일했다), 개발 빌드는 프레임 페이싱·로그 밀도·검증 스크립트가 달라 같은 실행이 아니다.
    /// 그래서 릴리스 빌드에서도 <c>STICKMATE_DEVTOOLS=1</c>로 열 수 있게 남긴다. 일반 사용자가 셸에서
    /// 환경변수를 세운 뒤 앱을 켜는 일은 없으므로 사용자 노출 위험은 사실상 0이다.
    ///
    /// ============================================================================
    /// 값은 <b>한 번만</b> 읽는다
    /// ============================================================================
    /// <see cref="Environment.GetEnvironmentVariable"/>는 플랫폼에 따라 네이티브 조회 + 문자열 할당이다.
    /// 이 앱은 하루 종일 켜져 있고 단축키 폴링은 20Hz라, 매 폴링마다 읽으면 초당 20회의 무의미한 할당이
    /// 된다. 프로세스 수명 동안 환경변수가 바뀔 일도 없으므로 최초 1회 캐시가 정확하기도 하다.
    /// </summary>
    public static class StickMateDevTools
    {
        /// <summary>릴리스 빌드에서도 개발 경로를 여는 환경변수 이름.</summary>
        public const string EnvironmentVariableName = "STICKMATE_DEVTOOLS";

        private static bool? _cached;
        private static bool? _testOverride;
        private static string _source = "미해석";

        /// <summary>
        /// 개발자 전용 경로가 열려 있는가. 컴파일 심볼(<c>UNITY_EDITOR</c> / <c>DEVELOPMENT_BUILD</c>)
        /// <b>또는</b> 환경변수 <c>STICKMATE_DEVTOOLS=1</c>.
        /// </summary>
        public static bool Enabled
        {
            get
            {
                if (_testOverride.HasValue) return _testOverride.Value;
                if (!_cached.HasValue) Resolve();
                return _cached.Value;
            }
        }

        /// <summary>왜 열렸는지/왜 닫혔는지 — 시작 배너와 진단 로그가 그대로 인쇄한다.</summary>
        public static string SourceLabel
        {
            get
            {
                if (_testOverride.HasValue) return _testOverride.Value ? "테스트 강제 ON" : "테스트 강제 OFF";
                if (!_cached.HasValue) Resolve();
                return _source;
            }
        }

        /// <summary>
        /// 환경변수 문자열 하나를 판정하는 <b>순수 함수</b> — 회귀 테스트가 실제 프로세스 환경을 건드리지
        /// 않고 이 규칙만 잠글 수 있게 분리해 두었다(에디터에서는 <c>UNITY_EDITOR</c> 때문에 게이트가
        /// 언제나 열려 있어 <see cref="Enabled"/>만으로는 이 규칙을 검증할 수 없다).
        ///
        /// "1"만이 아니라 true/on/yes도 받는다: 이 값을 세우는 사람은 셸에 익숙한 팀원이고, 관례가
        /// 갈리는 곳에서 오타 한 번에 "안 켜지는데 이유를 모르는" 30분이 날아가는 쪽이 훨씬 비싸다.
        /// </summary>
        public static bool ResolveFromEnvironmentValue(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            string v = raw.Trim();
            return v == "1"
                || v.Equals("true", StringComparison.OrdinalIgnoreCase)
                || v.Equals("on", StringComparison.OrdinalIgnoreCase)
                || v.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 테스트 전용 강제 — (다) 격리가 "기본 빌드에서 실제로 안 보이는지"를 확인하려면 에디터에서도
        /// 게이트를 닫아볼 수 있어야 한다. <c>null</c>을 넣으면 실제 판정으로 되돌아간다.
        /// </summary>
        public static void SetTestOverride(bool? value) => _testOverride = value;

        private static void Resolve()
        {
#if UNITY_EDITOR
            _cached = true;
            _source = "UNITY_EDITOR";
            return;
#elif DEVELOPMENT_BUILD
            _cached = true;
            _source = "DEVELOPMENT_BUILD";
            return;
#else
            string raw = null;
            try
            {
                raw = Environment.GetEnvironmentVariable(EnvironmentVariableName);
            }
            catch (Exception e)
            {
                // 샌드박스/보안 정책으로 환경변수 조회가 막힌 플랫폼 — 게이트를 닫는 쪽이 안전하다.
                Debug.LogWarning($"[개발도구] 환경변수 {EnvironmentVariableName} 조회 실패({e.GetType().Name}) — 게이트를 닫습니다.");
            }

            bool on = ResolveFromEnvironmentValue(raw);
            _cached = on;
            _source = on
                ? $"{EnvironmentVariableName}={raw}"
                : $"닫힘(릴리스 빌드, {EnvironmentVariableName} 미설정)";
#endif
        }
    }
}
