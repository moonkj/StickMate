using System;
using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// QA 스위치 — 장비 전종의 요구 레벨 게이트를 통째로 우회한다.
    /// 사용자 요청(2026-08-31): "장비창은 일단 전부다 잠금없이 열어줘(임시로) 전체 동작하는지 확인해야함".
    ///
    /// ============================================================================
    /// ★ 2026-09-01 — 상수에서 <b>빌드 구성</b>으로 옮겼다 (릴리스 차단 항목이었다)
    /// ============================================================================
    /// 이전에는 <c>private const bool DefaultUnlockAll = true;</c> 한 줄이었다. 즉 <b>사람이 출시 전에
    /// 손으로 false로 되돌려야</b> 성장 요소가 살아남는 구조였고, 그건 잊히는 종류의 값이다. 잊히면
    /// 첫 실행부터 42종이 전부 열린 채로 나가서 레벨업 보상이 통째로 무의미해진다.
    ///
    /// 이제 판정은 셋으로 갈린다:
    ///  <list type="bullet">
    ///   <item><b>에디터 / 개발 빌드</b>(<c>UNITY_EDITOR</c> · <c>DEVELOPMENT_BUILD</c>) → 열림.
    ///         개발 중에는 계속 필요하다는 요구를 그대로 만족한다.</item>
    ///   <item><b>릴리스 빌드 + <c>STICKMATE_UNLOCK_ALL=1</c></b> → 열림. 이 팀의 표준 QA 절차는
    ///         "릴리스 빌드를 실제로 켜고 확인한다"라, 이 탈출구가 없으면 원래 요청(릴리스에서 전종을
    ///         눌러 본다)을 이행할 방법이 사라진다.</item>
    ///   <item><b>그 외(=사용자에게 나가는 빌드)</b> → <b>닫힘</b>. 사람의 기억이 아니라 컴파일 심볼이
    ///         보장한다.</item>
    ///  </list>
    ///
    /// ============================================================================
    /// 왜 <see cref="StickMateDevTools"/> 게이트에 그냥 얹지 않았는가
    /// ============================================================================
    /// 환경변수를 <b>따로</b> 둔다. 성격이 다르기 때문이다 — DevTools가 여는 것은 "표시된 것과 실제가
    /// 다른" 연출 경로(하드웨어 반응 미리보기 등)이고, 이건 <b>보유 판정 하나</b>다. 한 스위치로 묶으면
    /// 디버거가 배터리 연출을 보려고 DevTools를 켠 순간 장비가 전부 열려, 정작 확인하려던 성장/해금
    /// 버그가 그 아래 숨는다. 값 <b>해석</b>만은 <see cref="StickMateDevTools.ResolveFromEnvironmentValue"/>를
    /// 재사용한다("1/true/on/yes" 관례가 스위치마다 갈리면 그게 더 비싼 함정이다).
    ///
    /// ============================================================================
    /// 거짓말은 하지 않는다 (원칙 1 계열)
    /// ============================================================================
    /// 우회는 <b>보유 판정 두 곳</b>(<see cref="ItemCatalogEntry.IsOwned"/> /
    /// <see cref="EquipmentModel.IsItemOwned"/>)에서만 한다. 그 두 곳이 카드 색·상태 문구·착용 가능
    /// 여부의 <b>공통 뿌리</b>라, 켜는 순간 "Lv.20에 열림"이라 적힌 카드가 몰래 눌리는 상태는 생기지
    /// 않는다 — 문구도 같이 "보유"로 바뀐다. 눌리는 것과 적힌 것이 어긋나면 그게 곧 버그다.
    ///
    /// <para>요구 레벨 로직 자체는 <b>한 줄도 지우지 않았다</b> — <see cref="ItemCatalogEntry.RequiredLevel"/>도,
    /// <see cref="EquipmentModel.RequiredLevel"/>도 그대로 살아 있고 스위치가 꺼지는 순간 원래대로
    /// 돌아온다(그 사실은 <c>EquipmentDebugUnlockTests</c>가 지킨다).</para>
    /// </summary>
    public static class EquipmentDebugUnlock
    {
        /// <summary>릴리스 빌드에서 이 스위치를 여는 환경변수 이름.
        /// (<see cref="StickMateDevTools.EnvironmentVariableName"/>과 <b>일부러</b> 다르다 — 위 주석 참고.)</summary>
        public const string EnvironmentVariableName = "STICKMATE_UNLOCK_ALL";

        /// <summary>
        /// ★ 이 어셈블리가 <b>개발 구성</b>으로 컴파일되었는가. 여기가 컴파일 심볼이 등장하는 <b>유일한
        /// 자리</b>다 — 나머지 판정은 전부 순수 함수라 에디터에서도 릴리스 동작을 그대로 재현해 검증할 수
        /// 있다(에디터는 언제나 <c>UNITY_EDITOR</c>라, 이걸 분리하지 않으면 "릴리스에서 닫히는가"를
        /// 테스트로 잠글 방법이 물리적으로 없다).
        /// <para><c>const</c>가 아니라 <c>static readonly</c>인 이유: <c>const</c>면 아래
        /// <c>if (!IsDevelopmentConfiguration)</c>가 컴파일 시점에 접혀 CS0162(도달 불가 코드) 경고가
        /// 뜨고, 이 저장소는 크로스 컴파일 0경고를 확인 절차로 쓴다.</para>
        /// </summary>
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static readonly bool IsDevelopmentConfiguration = true;
#else
        public static readonly bool IsDevelopmentConfiguration = false;
#endif

        private static bool? _override;
        private static bool? _cached;
        private static string _source = "미해석";
        private static bool _warned;

        /// <summary>
        /// 스위치 판정 <b>규칙 그 자체</b> — 컴파일 심볼도 프로세스 환경도 보지 않는 순수 함수.
        /// <para><c>ResolveUnlockAll(developmentConfiguration: false, environmentRaw: null)</c>이
        /// 곧 <b>사용자에게 나가는 릴리스 빌드의 값</b>이고, 그것이 false라는 사실을 회귀 테스트가 잠근다.</para>
        /// </summary>
        public static bool ResolveUnlockAll(bool developmentConfiguration, string environmentRaw)
            => developmentConfiguration || StickMateDevTools.ResolveFromEnvironmentValue(environmentRaw);

        /// <summary>요구 레벨을 무시하고 전부 보유로 칠 것인가.</summary>
        public static bool UnlockAll
        {
            get
            {
                if (_override.HasValue) return _override.Value;
                if (!_cached.HasValue) Resolve();

                // 로그를 뒤질 사람이 "왜 Lv.1인데 왕관이 열려 있지?"를 5초 안에 알 수 있어야 한다.
                // 프로세스당 한 번만 — 이 프로퍼티는 보관함을 그릴 때마다 수십 번 불린다.
                if (_cached.Value && !_warned)
                {
                    _warned = true;
                    Debug.LogWarning($"[StickMate] QA 해금 스위치가 켜져 있습니다({_source}): 장비 요구 레벨을 " +
                        "무시하고 전종을 보유로 취급합니다(Core/EquipmentDebugUnlock). " +
                        "사용자에게 나가는 릴리스 빌드에서는 자동으로 꺼집니다.");
                }
                return _cached.Value;
            }
        }

        /// <summary>왜 열렸는지/왜 닫혔는지 — 진단 로그가 그대로 인쇄한다.</summary>
        public static string SourceLabel
        {
            get
            {
                if (_override.HasValue) return _override.Value ? "테스트 강제 ON" : "테스트 강제 OFF";
                if (!_cached.HasValue) Resolve();
                return _source;
            }
        }

        /// <summary>테스트 전용 강제값. <c>null</c>이면 실제 판정으로 돌아간다 —
        /// 스위치가 켜진 상태와 꺼진 상태를 <b>둘 다</b> 검증할 수 있어야 하기 때문에 있다.</summary>
        internal static void SetTestOverride(bool? value) => _override = value;

        private static void Resolve()
        {
            string raw = null;
            if (!IsDevelopmentConfiguration)
            {
                try
                {
                    raw = Environment.GetEnvironmentVariable(EnvironmentVariableName);
                }
                catch (Exception e)
                {
                    // 샌드박스/보안 정책으로 환경변수 조회가 막힌 플랫폼 — 스위치를 닫는 쪽이 안전하다.
                    Debug.LogWarning($"[StickMate] 환경변수 {EnvironmentVariableName} 조회 실패" +
                                     $"({e.GetType().Name}) — QA 해금 스위치를 닫습니다.");
                }
            }

            bool on = ResolveUnlockAll(IsDevelopmentConfiguration, raw);
            _cached = on;
            _source = IsDevelopmentConfiguration
                ? "개발 구성(UNITY_EDITOR/DEVELOPMENT_BUILD)"
                : on
                    ? $"{EnvironmentVariableName}={raw}"
                    : $"닫힘(릴리스 빌드, {EnvironmentVariableName} 미설정)";
        }
    }
}
