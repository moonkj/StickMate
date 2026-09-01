using UnityEngine;

namespace StickMate.Core
{
    /// <summary>
    /// ★ <b>임시</b> QA 스위치 — 장비 28종의 요구 레벨 게이트를 통째로 우회한다.
    /// 사용자 요청(2026-08-31): "장비창은 일단 전부다 잠금없이 열어줘(임시로) 전체 동작하는지 확인해야함".
    ///
    /// ============================================================================
    /// 되돌리는 법 — 한 줄이다
    /// ============================================================================
    /// 아래 <see cref="DefaultUnlockAll"/>을 <c>false</c>로 바꾸면 끝이다. <b>요구 레벨 로직 자체는
    /// 한 줄도 지우지 않았다</b> — <see cref="ItemCatalogEntry.RequiredLevel"/>도,
    /// <see cref="EquipmentModel.RequiredLevel"/>도, 레벨업 해제 안내도 그대로 살아 있고
    /// 이 스위치가 꺼지는 순간 원래대로 돌아온다(그 사실은 EditMode 테스트가 지킨다).
    ///
    /// ============================================================================
    /// 왜 <see cref="StickMateDevTools"/>에 얹지 않았는가
    /// ============================================================================
    /// 그쪽 게이트는 <c>UNITY_EDITOR / DEVELOPMENT_BUILD / STICKMATE_DEVTOOLS=1</c>이다. 이번 요청은
    /// <b>사용자가 평소 쓰는 릴리스 빌드에서 28종을 직접 눌러 보는 것</b>이 목적이라, 그 게이트에 얹으면
    /// "환경변수를 세우고 앱을 켜라"는 단계가 하나 붙어 요청이 그대로 이행되지 않는다. 성격도 다르다 —
    /// DevTools가 막는 것은 "표시된 것과 실제가 다른" 연출 경로이고, 이건 <b>보유 판정 하나</b>다.
    ///
    /// ============================================================================
    /// 거짓말은 하지 않는다 (원칙 1 계열)
    /// ============================================================================
    /// 우회는 <b>보유 판정 두 곳</b>(<see cref="ItemCatalogEntry.IsOwned"/> /
    /// <see cref="EquipmentModel.IsItemOwned"/>)에서만 한다. 그 두 곳이 카드 색·상태 문구·착용 가능
    /// 여부의 <b>공통 뿌리</b>라, 켜는 순간 "Lv.20에 열림"이라 적힌 카드가 몰래 눌리는 상태는 생기지
    /// 않는다 — 문구도 같이 "보유"로 바뀐다. 눌리는 것과 적힌 것이 어긋나면 그게 곧 버그다.
    /// </summary>
    public static class EquipmentDebugUnlock
    {
        /// <summary>
        /// ★ <b>지금 켜져 있다(true).</b> QA용 임시값이며, 검증이 끝나면 <c>false</c>로 되돌린다.
        /// (되돌릴 때 다른 파일을 건드릴 필요는 없다.)
        /// </summary>
        private const bool DefaultUnlockAll = true;

        private static bool? _override;
        private static bool _warned;

        /// <summary>요구 레벨을 무시하고 전부 보유로 칠 것인가.</summary>
        public static bool UnlockAll
        {
            get
            {
                if (_override.HasValue) return _override.Value;

                // 로그를 뒤질 사람이 "왜 Lv.1인데 왕관이 열려 있지?"를 5초 안에 알 수 있어야 한다.
                // 프로세스당 한 번만 — 이 프로퍼티는 보관함을 그릴 때마다 수십 번 불린다.
                if (DefaultUnlockAll && !_warned)
                {
                    _warned = true;
                    Debug.LogWarning("[StickMate] 임시 QA 스위치가 켜져 있습니다: 장비 요구 레벨을 무시하고 " +
                        "28종을 전부 보유로 취급합니다(Core/EquipmentDebugUnlock.DefaultUnlockAll). " +
                        "출시 전에 false로 되돌리십시오.");
                }
                return DefaultUnlockAll;
            }
        }

        /// <summary>테스트 전용 강제값. <c>null</c>이면 기본값으로 돌아간다 —
        /// 스위치가 켜진 상태와 꺼진 상태를 <b>둘 다</b> 검증할 수 있어야 하기 때문에 있다.</summary>
        internal static void SetTestOverride(bool? value) => _override = value;
    }
}
