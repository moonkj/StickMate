#if UNITY_STANDALONE_WIN || UNITY_STANDALONE_OSX
using UnityEngine;
using StickMate.Core;
#if STICKMATE_STEAMWORKS_INSTALLED
using Steamworks;
#endif

namespace StickMate.Store
{
    /// <summary>
    /// ============================================================================
    /// ★ 스팀 DLC 엔타이틀먼트 조회 — 리더 결재-1로 승인된 니들 예외의 <b>유일한 파일</b>
    /// ============================================================================
    /// 설계 규범: <c>docs/security/STEAMWORKS_ENTITLEMENT_EXCEPTION.md</c>(security, 2026-09-05).
    /// <b>허용 심볼 5개</b>만 쓴다 — <c>Steamworks</c>(using 1회) · <c>SteamAPI.Init/Shutdown</c> ·
    /// <c>SteamApps.BIsDlcInstalled</c> · <c>AppId_t</c> · 이 타입 자신의 이름. 그 밖 전부 금지
    /// (<c>SteamEntitlementAdapterAuditTests</c>가 라인 단위로 잠근다).
    ///
    /// <para>★ <c>RestartAppIfNecessary</c> / <c>RunCallbacks</c> / <c>BIsSubscribed*</c> /
    /// <c>SteamRemoteStorage</c> / <c>SteamInventory</c> / <c>SteamUser</c> 등은 설계 문서 §4-1이
    /// 이 파일 <b>안에서도</b> 명시적으로 금지한다 — 화이트리스트는 파일 통행권이 아니다.</para>
    ///
    /// <para>★ <c>STICKMATE_STEAMWORKS_INSTALLED</c> 스크립팅 정의 심볼이 없으면 이 클래스는
    /// 자가 설치하지 않고(<see cref="PackEntitlements"/>는 <c>NullPackEntitlementSource</c>를 그대로
    /// 쓴다) <see cref="Query"/>는 항상 <see cref="PackEntitlementState.Unknown"/>을 돌려준다 —
    /// Steamworks.NET 패키지(UPM)가 아직 이 저장소에 설치되지 않았기 때문이다(§7-7 "UPM + 태그/커밋
    /// 고정" 권고). 패키지 설치 후 그 심볼을 Player Settings의 Scripting Define Symbols에 추가하면
    /// 아래 실제 구현이 켜진다.</para>
    /// </summary>
    public sealed class SteamPackEntitlementSource : IPackEntitlementSource
    {
#if STICKMATE_STEAMWORKS_INSTALLED
        private const string LogPrefix = "[스팀엔타이틀먼트]";

        private bool _initSucceeded;
        private float _nextInitAttemptTime;

        /// <summary>자가 설치. 이 저장소 선례 3건과 같은 형태
        /// (<c>ReservedBarRevealDirector</c> · <c>RenderQualityTuner</c> · <c>PlayerLogPolicy</c>).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            var source = new SteamPackEntitlementSource();
            PackEntitlements.UseSource(source);
            Application.quitting += source.OnQuitting;
        }

        /// <summary>
        /// 이 팩의 스팀 소유 상태. <b>절대 예외를 던지지 않는다</b> — 어떤 예외든
        /// <see cref="PackEntitlementState.Unknown"/>으로 접는다(부팅 자동 실행 시 스팀 프로세스
        /// 자체가 없는 경로가 실재한다).
        /// </summary>
        public PackEntitlementState Query(string packId)
        {
            if (string.IsNullOrEmpty(packId)) return PackEntitlementState.Unknown;

            try
            {
                PackDescriptor pack = PackRegistry.Find(packId);
                if (pack == null)
                {
                    Debug.LogError($"{LogPrefix} '{packId}' 매니페스트를 찾을 수 없습니다.");
                    return PackEntitlementState.Unknown;
                }

                string entitlementId = FindSteamEntitlementId(pack);
                if (string.IsNullOrEmpty(entitlementId)) return PackEntitlementState.Unknown;

                if (!uint.TryParse(entitlementId, out uint appIdValue))
                {
                    Debug.LogError($"{LogPrefix} '{packId}'의 스팀 appid '{entitlementId}'가 숫자가 아닙니다.");
                    return PackEntitlementState.Unknown;
                }

                if (!EnsureInitialized()) return PackEntitlementState.Unknown;

                bool installed = SteamApps.BIsDlcInstalled(new AppId_t(appIdValue));
                return installed ? PackEntitlementState.Owned : PackEntitlementState.NotOwned;
            }
            catch
            {
                return PackEntitlementState.Unknown;
            }
        }

        private static string FindSteamEntitlementId(PackDescriptor pack)
        {
            for (int i = 0; i < pack.Entitlements.Count; i++)
            {
                if (pack.Entitlements[i].channel == PackStoreChannel.Steam)
                {
                    return pack.Entitlements[i].entitlementId;
                }
            }
            return null;
        }

        /// <summary>§6-2 — 실패하면 최소 60초 뒤 다음 조회에서 재시도. 타이머를 따로 돌리지 않는다
        /// (묻는 쪽이 물을 때 그 자리에서 판단 — 상주 앱에 도는 코드를 안 늘린다).</summary>
        private bool EnsureInitialized()
        {
            if (_initSucceeded) return true;
            if (Time.realtimeSinceStartup < _nextInitAttemptTime) return false;

            _initSucceeded = SteamAPI.Init();
            if (!_initSucceeded) _nextInitAttemptTime = Time.realtimeSinceStartup + 60f;
            return _initSucceeded;
        }

        private void OnQuitting()
        {
            if (_initSucceeded) SteamAPI.Shutdown();
        }
#else
        public PackEntitlementState Query(string packId) => PackEntitlementState.Unknown;
#endif
    }
}
#endif
