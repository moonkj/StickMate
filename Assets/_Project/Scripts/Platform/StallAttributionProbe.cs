using System;
using System.Diagnostics;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace StickMate.Platform
{
    /// <summary>
    /// <see cref="StallAttribution"/>의 <b>배선</b> — 프레임 경계 두 개와 로그 핸들러 래퍼를 건다.
    ///
    /// <para><b>왜 씬을 건드리지 않고 자기 자신을 심는가</b>: 이 라운드는 다른 두 라운드와 동시에
    /// 돌고 있어 씬/프리팹/부트스트래퍼를 만지면 충돌한다. 또 이 장치는 <b>진단</b>이므로 씬 배선의
    /// 일부가 되어서는 안 된다 — 원인이 확정되면 파일 하나만 지우면 흔적 없이 사라지는 것이 맞다.
    /// 같은 이유로 <c>Platform/RenderQualityTuner.cs</c>도 이미
    /// <see cref="RuntimeInitializeOnLoadMethod"/> 방식을 쓰고 있다(선례 있음).</para>
    ///
    /// <para><b>왜 컴포넌트가 두 개인가</b>: 프레임을 "로직 구간"과 "그 밖"으로 가르려면 <b>모든
    /// Update보다 먼저</b>와 <b>모든 LateUpdate보다 나중</b> 두 시점이 필요하다. 한 컴포넌트의
    /// Update/LateUpdate로는 안 된다 — 실행 순서는 컴포넌트 단위라 그 컴포넌트의 LateUpdate도
    /// 똑같이 맨 앞에서 돌기 때문이다. 이 프로젝트에는 커스텀 실행 순서를 쓰는 스크립트가 하나도
    /// 없으므로(전수 검색 확인) ±30000이면 모든 것을 감싼다.</para>
    /// </summary>
    public static class StallAttributionProbe
    {
        private const string HostName = "StickMate.StallAttributionProbe";
        private static GameObject _host;
        private static bool _logHandlerInstalled;
        private static ILogHandler _originalHandler;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInstall()
        {
            // 에디터(Play 모드/테스트)에서는 로그 핸들러를 갈아끼우지 않는다 — Test Framework의
            // 로그 검사와 콘솔 스택 점프에 불필요한 위험을 만들 이유가 없다. 프레임 경계 계측만
            // 건다(그쪽은 Debug.Log를 대체하지 않고 얹기만 한다).
            Install(installLogHandler: !Application.isEditor);
        }

        /// <summary>테스트/수동 설치용. 여러 번 불러도 안전하다.</summary>
        public static void Install(bool installLogHandler)
        {
            if (_host == null)
            {
                _host = new GameObject(HostName);
                _host.hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave;
                Object.DontDestroyOnLoad(_host);
                _host.AddComponent<StallFrameBeginProbe>();
                _host.AddComponent<StallFrameEndProbe>();
            }

            InstallFontAtlasHook();

            if (!installLogHandler || _logHandlerInstalled) return;

            try
            {
                ILogHandler inner = Debug.unityLogger.logHandler;
                if (inner is StallProfilingLogHandler) return;
                _originalHandler = inner;
                Debug.unityLogger.logHandler = new StallProfilingLogHandler(inner);
                _logHandlerInstalled = true;
            }
            catch (Exception ex)
            {
                // 계측 때문에 앱이 죽으면 안 된다. 실패하면 로그 비용 항목만 0으로 남는다.
                Debug.LogWarning("[스톨귀인] 로그 핸들러 계측을 걸지 못했습니다(계측 없이 계속 진행): " + ex.Message);
            }
        }

        // ------------------------------------------------------------------------------------
        // ★ 폰트 아틀라스 재구성 감시 (2026-09-01 2차 라운드)
        // ------------------------------------------------------------------------------------
        // 사용자 확정 조건: "켜놓을수록 렉이 심해짐" — 평균/p50은 그대로인데 p99와 최대만 커진다.
        // 그 형태(상시 비용은 그대로, 간헐적 큰 멈춤만 악화)에 정확히 맞는 후보가 uGUI 동적 폰트의
        // 아틀라스 재구성이다: 새 글리프가 들어올 때마다 아틀라스가 차오르고, 넘치면 <b>전체를 다시
        // 굽고 그 폰트를 쓰는 모든 Text를 다시 만든다</b>. 한글은 글리프가 수천 자라 오래 켜 둘수록
        // 재구성이 커지고 잦아진다. 이 앱은 대사/정보창/설정창/할일이 전부 한글 Text다.
        //
        // 비용: 재구성이 <b>실제로 일어날 때만</b> 콜백 1회(정상 프레임에는 아무 일도 하지 않는다).
        // 여기서 텍스처 크기를 읽는 것도 그 순간뿐이다.
        private static bool _fontHookInstalled;

        private static void InstallFontAtlasHook()
        {
            if (_fontHookInstalled) return;
            try
            {
                Font.textureRebuilt += OnFontTextureRebuilt;
                _fontHookInstalled = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[스톨귀인] 폰트 아틀라스 감시를 걸지 못했습니다(계측 없이 계속 진행): " + ex.Message);
            }
        }

        private static void OnFontTextureRebuilt(Font font)
        {
            Texture tex = font != null && font.material != null ? font.material.mainTexture : null;
            StallAttribution.RecordFontAtlasRebuild(tex != null ? tex.width : 0, tex != null ? tex.height : 0);
        }

        /// <summary>테스트가 원상 복구할 때 쓴다.</summary>
        public static void Uninstall()
        {
            if (_fontHookInstalled)
            {
                Font.textureRebuilt -= OnFontTextureRebuilt;
                _fontHookInstalled = false;
            }

            if (_logHandlerInstalled && _originalHandler != null)
            {
                Debug.unityLogger.logHandler = _originalHandler;
            }
            _logHandlerInstalled = false;
            _originalHandler = null;

            if (_host != null)
            {
                if (Application.isPlaying) Object.Destroy(_host);
                else Object.DestroyImmediate(_host);
                _host = null;
            }
        }
    }

    /// <summary>
    /// 모든 Update/LateUpdate/FixedUpdate보다 <b>먼저</b> 도는 단계 시작 표식.
    ///
    /// <para>★ 2026-09-01 2차 라운드에서 <c>FixedUpdate</c>가 추가됐다. Unity의 프레임 순서는
    /// <c>[FixedUpdate x K] -> Update -> LateUpdate -> 렌더</c>이므로, 1차 계측(Update 시작 ~ LateUpdate 끝)은
    /// <b>물리를 통째로 놓치고 있었다</b> — 그 시간은 전부 "로직밖(렌더/프레젠트/합성)"으로 잘못 귀속됐다.
    /// 랙돌 관절 + 던지기가 상시 도는 앱에서 이건 결코 작은 구멍이 아니다.</para>
    ///
    /// <para>한 컴포넌트가 세 메시지를 다 받는 이유: 실행 순서는 <b>컴포넌트 단위</b>라
    /// -30000이면 이 컴포넌트의 Update·LateUpdate·FixedUpdate가 각 단계에서 모두 맨 앞에 선다.</para>
    /// </summary>
    [DefaultExecutionOrder(-30000)]
    internal sealed class StallFrameBeginProbe : MonoBehaviour
    {
        private void FixedUpdate() => StallAttribution.BeginFixedStep();
        private void Update() => StallAttribution.BeginFrame();
        private void LateUpdate() => StallAttribution.BeginLatePhase();
    }

    /// <summary>모든 Update/LateUpdate/FixedUpdate보다 <b>나중에</b> 도는 단계 종료 표식.</summary>
    [DefaultExecutionOrder(30000)]
    internal sealed class StallFrameEndProbe : MonoBehaviour
    {
        private void FixedUpdate() => StallAttribution.EndFixedStep();
        private void Update() => StallAttribution.EndUpdatePhase();
        private void LateUpdate() => StallAttribution.EndLogicPhase();
    }

    /// <summary>
    /// <see cref="Debug.unityLogger"/>의 실제 핸들러를 감싸 <b>한 줄이 실제로 걸린 시간</b>을 잰다.
    /// 이 안쪽 호출이 곧 (a) 스택트레이스 캡처 + (b) Player.log 동기 쓰기이므로, 이 숫자가 바로
    /// "후보 B(파일 IO)"의 실측치다.
    ///
    /// <para><b>안전 규칙</b>: (1) 어떤 경우에도 원래 핸들러 호출을 건너뛰지 않는다(로그가 사라지면
    /// 진단이 아니라 사고다) — <c>finally</c>에서만 기록한다. (2) 메인 스레드가 아니면 계측만
    /// 건너뛴다(<see cref="StallAttribution"/>의 프레임 버킷은 메인 스레드 전용이다).
    /// (3) 태그 추출은 문자열을 <b>새로 만들지 않는다</b>(태그 종류당 최초 1회 제외).</para>
    /// </summary>
    internal sealed class StallProfilingLogHandler : ILogHandler
    {
        private readonly ILogHandler _inner;
        private readonly int _mainThreadId;

        internal StallProfilingLogHandler(ILogHandler inner)
        {
            _inner = inner;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public void LogFormat(LogType logType, Object context, string format, params object[] args)
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                _inner.LogFormat(logType, context, format, args);
                return;
            }

            long start = Stopwatch.GetTimestamp();
            try
            {
                _inner.LogFormat(logType, context, format, args);
            }
            finally
            {
                StallAttribution.RecordLogWrite(Stopwatch.GetTimestamp() - start, ResolveMessage(format, args));
            }
        }

        public void LogException(Exception exception, Object context)
        {
            if (Thread.CurrentThread.ManagedThreadId != _mainThreadId)
            {
                _inner.LogException(exception, context);
                return;
            }

            long start = Stopwatch.GetTimestamp();
            try
            {
                _inner.LogException(exception, context);
            }
            finally
            {
                StallAttribution.RecordLogWrite(Stopwatch.GetTimestamp() - start, "[예외]");
            }
        }

        /// <summary>
        /// 태그 집계에 쓸 <b>원문</b>을 고른다.
        ///
        /// <para><c>Debug.Log(object)</c>는 내부적으로 <c>LogFormat(type, ctx, "{0}", message)</c>로
        /// 오므로 그때는 args[0]이 우리가 찍은 문자열이다. 반면 <c>Debug.LogFormat("...{0}...", a)</c>는
        /// format 쪽이 원문이고 args[0]은 <b>인자</b>다 — 그걸 원문으로 착각하면 태그 집계가 엉뚱한
        /// 값을 센다. 두 경우를 format 리터럴로 구분한다(3글자 비교, 수 ns).</para>
        ///
        /// <para><c>ToString()</c>은 절대 부르지 않는다: 할당이 생기고, 그건 계측이 증상을 만드는
        /// 짓이다(이 라운드가 고치려는 바로 그 실수).</para>
        /// </summary>
        private static string ResolveMessage(string format, object[] args)
        {
            if (format == "{0}" && args != null && args.Length > 0) return args[0] as string;
            return format;
        }
    }
}
