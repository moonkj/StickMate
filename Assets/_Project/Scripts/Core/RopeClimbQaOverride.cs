using System;
using System.Globalization;
using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// QA 전용 스위치 2종 — 밧줄 등반(RopeClimb)을 배회 AI의 자연 발생을 기다리지 않고 실기에서
    /// 즉시 검증하기 위한 탈출구. docs/DESIGN_ROPE_CLIMB_ARCHITECTURE.md §9-4-C의 처방을 그대로 구현한다.
    ///
    /// 1. <see cref="ChanceEnvironmentVariableName"/>(<c>STICKMATE_QA_ROPE_CLIMB_CHANCE=&lt;0..1&gt;</c>)
    ///    — <see cref="StickConfig.ropeClimbChance"/>를 런타임에서만 오버라이드한다(에셋은 절대 안 건드림).
    ///    "배회 AI 확률" 우회이고, <see cref="EquipmentDebugUnlock"/>(STICKMATE_UNLOCK_ALL, "보유 판정"
    ///    우회)와 성격이 달라 별도 환경변수를 쓴다 — 같은 스위치에 묶으면 장비 QA를 하려던 사람이
    ///    뜻하지 않게 로프등반 확률까지 건드리게 된다(EquipmentDebugUnlock.cs 문서와 같은 논지).
    /// 2. <see cref="TestWallEnvironmentVariableName"/>(<c>STICKMATE_QA_ROPE_CLIMB_TEST_WALL=1</c>)
    ///    — 실제 창 배치와 무관하게 밧줄등반 대역의 가상 시험벽 1개를 삽입할지 여부(판정만 여기서 하고,
    ///    실제 삽입은 <see cref="Platform.FallbackPlatformWindowService"/>가 이 값을 읽어 담당한다).
    ///
    /// ============================================================================
    /// ★ EquipmentDebugUnlock과 구조가 다른 지점 — 왜 "에디터/개발빌드는 무조건 열림" 가지가 없는가
    /// ============================================================================
    /// EquipmentDebugUnlock은 불리언 "보유 우회"라 값이 하나뿐이고, 그래서 "에디터/개발빌드는 무조건
    /// 켜짐"이 안전하다(둘 중 하나를 강제해도 잃을 게 없다). 여기는 숫자(확률) 오버라이드라 "에디터에서
    /// 무조건 어떤 값으로 강제"할 기본값 자체가 없다 — 그래서 <b>개발/릴리스 구분 없이</b> 환경변수가
    /// 실제로 설정된 경우에만 오버라이드가 켜진다. 이쪽이 오히려 더 안전한 형태다: 에디터에서도 "값을
    /// 명시하지 않았는데 조용히 다른 값이 적용되는" 사고가 구조적으로 불가능하다(환경변수 미설정 =
    /// 모든 빌드 구성에서 100% 동일하게 StickConfig 원래 값 그대로).
    /// <see cref="IsDevelopmentConfiguration"/>은 그래서 게이트가 아니라 순수 진단 정보로만 남는다.
    /// </summary>
    public static class RopeClimbQaOverride
    {
        /// <summary>배회 AI의 ropeClimbChance를 오버라이드하는 환경변수 이름.
        /// (<see cref="EquipmentDebugUnlock.EnvironmentVariableName"/>과 <b>일부러</b> 다르다 — 위 클래스 문서 참고.)</summary>
        public const string ChanceEnvironmentVariableName = "STICKMATE_QA_ROPE_CLIMB_CHANCE";

        /// <summary>QA 전용 합성 시험벽을 켜는 환경변수 이름.</summary>
        public const string TestWallEnvironmentVariableName = "STICKMATE_QA_ROPE_CLIMB_TEST_WALL";

        /// <summary>진단 정보 — 게이트로는 쓰이지 않는다(위 클래스 문서 참고).</summary>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static readonly bool IsDevelopmentConfiguration = true;
#else
        public static readonly bool IsDevelopmentConfiguration = false;
#endif

        private static float? _chanceTestOverride;
        private static bool? _wallTestOverride;

        private static bool _chanceCached;
        private static float? _chanceResolved;
        private static string _chanceSource = "미해석";
        private static bool _chanceWarned;

        private static bool _wallCached;
        private static bool _wallResolved;
        private static string _wallSource = "미해석";
        private static bool _wallWarned;

        /// <summary>
        /// 확률 오버라이드 판정 <b>순수 함수</b> — 회귀 테스트가 실제 프로세스 환경을 건드리지 않고 이
        /// 규칙만 잠글 수 있다. 파싱 실패 / 범위(0..1) 밖 / 미설정이면 <c>null</c>(=오버라이드 없음,
        /// StickConfig.ropeClimbChance를 그대로 쓴다).
        /// </summary>
        public static float? ResolveChanceOverride(string environmentRaw)
        {
            if (string.IsNullOrWhiteSpace(environmentRaw)) return null;
            if (!float.TryParse(environmentRaw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                return null;
            }
            if (value < 0f || value > 1f) return null;
            return value;
        }

        /// <summary>
        /// 합성 시험벽 판정 <b>순수 함수</b>. 값 해석은 <see cref="StickMateDevTools.ResolveFromEnvironmentValue"/>와
        /// 완전히 같은 관례("1/true/on/yes")를 재사용한다 — 이 앱 안에서 진/부 문자열 해석 규칙이 스위치마다
        /// 갈리면 그게 더 비싼 함정이다(EquipmentDebugUnlock.cs와 같은 논지).
        /// </summary>
        public static bool ResolveTestWallEnabled(string environmentRaw) =>
            StickMateDevTools.ResolveFromEnvironmentValue(environmentRaw);

        /// <summary>지금 적용해야 할 확률 오버라이드. <c>null</c>이면 오버라이드가 꺼져 있다는 뜻 —
        /// 호출부는 <c>RopeClimbQaOverride.ChanceOverride ?? config.ropeClimbChance</c> 형태로 쓴다.</summary>
        public static float? ChanceOverride
        {
            get
            {
                if (_chanceTestOverride.HasValue) return _chanceTestOverride.Value;
                if (!_chanceCached) ResolveChance();

                if (_chanceResolved.HasValue && !_chanceWarned)
                {
                    _chanceWarned = true;
                    Debug.LogWarning($"[StickMate] QA 밧줄등반 확률 오버라이드가 켜져 있습니다({_chanceSource}): " +
                        $"ropeClimbChance={_chanceResolved.Value:F2}로 강제합니다(StickConfig 원래 값은 무시). " +
                        "QA 전용 절차로만 쓰십시오 — 이 환경변수를 지우면 다음 실행부터 원래 값으로 돌아갑니다.");
                }
                return _chanceResolved;
            }
        }

        /// <summary>지금 QA 합성 시험벽을 켜야 하는가.</summary>
        public static bool TestWallEnabled
        {
            get
            {
                if (_wallTestOverride.HasValue) return _wallTestOverride.Value;
                if (!_wallCached) ResolveWall();

                if (_wallResolved && !_wallWarned)
                {
                    _wallWarned = true;
                    Debug.LogWarning($"[StickMate] QA 밧줄등반 합성 시험벽이 켜져 있습니다({_wallSource}): " +
                        "실제 창 배치와 무관한 가상의 높은 벽이 발판 목록에 추가됩니다. 세이브/에셋에는 " +
                        "절대 남지 않고, 이 환경변수를 지우면 다음 폴링부터 즉시 사라집니다.");
                }
                return _wallResolved;
            }
        }

        /// <summary>왜 켜졌는지/왜 꺼졌는지 — 진단 로그가 그대로 인쇄한다.</summary>
        public static string ChanceSourceLabel
        {
            get
            {
                if (_chanceTestOverride.HasValue) return $"테스트 강제 {_chanceTestOverride.Value:F2}";
                if (!_chanceCached) ResolveChance();
                return _chanceSource;
            }
        }

        /// <summary>왜 켜졌는지/왜 꺼졌는지 — 진단 로그가 그대로 인쇄한다.</summary>
        public static string WallSourceLabel
        {
            get
            {
                if (_wallTestOverride.HasValue) return _wallTestOverride.Value ? "테스트 강제 ON" : "테스트 강제 OFF";
                if (!_wallCached) ResolveWall();
                return _wallSource;
            }
        }

        /// <summary>테스트 전용 강제값. <c>null</c>을 넣으면 실제 판정으로 되돌아간다.
        /// <para>★ <c>public</c>인 이유: PlayMode 테스트 어셈블리(<c>StickMate.Tests.PlayMode</c>)는
        /// <c>InternalsVisibleTo</c> 대상이 아니다(<c>AssemblyInfo.cs</c>는 EditMode만 허용 —
        /// <see cref="States.AutoWanderController.ResolveStepUpMaxHeightStatic"/>과 같은 이유).</para>
        ///
        /// <para>★★ <b>보안 판정 (security, 2026-09-07 — 근거는 단정이 아니라 실측이다).</b>
        /// 초판 주석은 «부작용이 테스트 격리 목적 하나뿐이라 노출해도 위험이 없다»고 <b>단정</b>했다.
        /// 그 문장은 검증할 수 없는 형태라 아래 <b>도달 범위</b>로 바꾼다.
        /// <list type="bullet">
        ///   <item><see cref="ChanceOverride"/>의 유일한 소비자는 배회 AI의 로프 추첨 확률이고,</item>
        ///   <item><see cref="TestWallEnabled"/>의 유일한 소비자는 발판 목록에 합성 벽 1개를 더하는 자리다.</item>
        /// </list>
        /// 두 종점 어디에서도 XP·동전·아이템 보유·엔타이틀먼트 모델로 가는 화살표가 없다
        /// (밧줄 등반은 어떤 재화도 지급하지 않는다). 즉 이 강제값은
        /// <c>docs/security/ENTITLEMENT_CONTRACT.md</c> §E-6-c가 겨눈 <b>C층(유료 권한) 강제값이 아니다</b>.
        /// 그 사실을 <c>UnlockSwitchScopeAuditTests</c>가 매 실행 다시 잰다 — 누군가 이 값을 보유 판정
        /// 쪽으로 배선하는 날 그 감사가 <b>먼저</b> 빨개진다.</para>
        ///
        /// <para>★ <b>이 판정이 뒤집히는 조건</b>(적어 두지 않으면 다음 사람이 모른다):
        /// (가) 밧줄 등반이 <b>XP·동전·아이템 해금</b> 중 하나라도 지급하게 되는 날,
        /// (나) 이 프로세스가 <b>제3자 어셈블리를 적재</b>하게 되는 날(원칙 4 플러그인 통로가
        /// 코드까지 열리는 경우 — 오늘은 <c>Assembly.Load</c> 계열이 프로덕션에 0건이라
        /// <c>public</c>을 바깥에서 부를 주체 자체가 없다). 둘 중 하나라도 참이 되면
        /// <c>internal</c> 전환 또는 릴리스 무력화를 다시 검토해야 한다.</para></summary>
        public static void SetChanceTestOverride(float? value) => _chanceTestOverride = value;

        /// <summary>테스트 전용 강제값. <c>null</c>을 넣으면 실제 판정으로 되돌아간다. <c>public</c>인
        /// 이유와 <b>보안 판정</b>은 <see cref="SetChanceTestOverride"/>와 같다.</summary>
        public static void SetWallTestOverride(bool? value) => _wallTestOverride = value;

        private static void ResolveChance()
        {
            string raw = null;
            try
            {
                raw = Environment.GetEnvironmentVariable(ChanceEnvironmentVariableName);
            }
            catch (Exception e)
            {
                // 샌드박스/보안 정책으로 환경변수 조회가 막힌 플랫폼 — 오버라이드를 닫는 쪽이 안전하다.
                Debug.LogWarning($"[StickMate] 환경변수 {ChanceEnvironmentVariableName} 조회 실패" +
                                 $"({e.GetType().Name}) — QA 확률 오버라이드를 닫습니다.");
            }

            _chanceResolved = ResolveChanceOverride(raw);
            _chanceCached = true;
            _chanceSource = _chanceResolved.HasValue
                ? $"{ChanceEnvironmentVariableName}={raw}"
                : $"닫힘({ChanceEnvironmentVariableName} 미설정 또는 0..1 범위를 벗어난 값)";
        }

        private static void ResolveWall()
        {
            string raw = null;
            try
            {
                raw = Environment.GetEnvironmentVariable(TestWallEnvironmentVariableName);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[StickMate] 환경변수 {TestWallEnvironmentVariableName} 조회 실패" +
                                 $"({e.GetType().Name}) — QA 합성 시험벽을 닫습니다.");
            }

            _wallResolved = ResolveTestWallEnabled(raw);
            _wallCached = true;
            _wallSource = _wallResolved
                ? $"{TestWallEnvironmentVariableName}={raw}"
                : $"닫힘({TestWallEnvironmentVariableName} 미설정)";
        }
    }
}
