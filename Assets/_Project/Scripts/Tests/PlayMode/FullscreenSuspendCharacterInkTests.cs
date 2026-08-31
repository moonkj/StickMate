using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.Platform;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 절대 불변 원칙 2("전체화면 게임 감지 시 자동 숨김")를 <b>캐릭터가 그리는 잉크 전부</b>에 대해
    /// 프레임 단위로 잠근다 — 2026-08-31 능동 탐색이 찾은 Major 1의 회귀 잠금.
    ///
    /// ============================================================================
    /// 무엇이 깨져 있었나 (실측)
    /// ============================================================================
    /// <c>Suspend()</c>는 <b>Awake에서 캐시한 몸 렌더러 12개</b>만 그 프레임에 껐다. 액세서리/펫/FX는
    /// 캐시 배열에 영원히 들어오지 않는 런타임 생성물이라(펫/FX는 캐릭터의 자식조차 아니다) 자기
    /// <c>HeadOutline.enabled</c>를 관찰해 <b>0.18~0.25초에 걸쳐 페이드아웃</b>했다:
    /// <code>
    /// SUSPEND+0f   suspended=True bodyVis=0 accVis=12 petVis=12 petAlpha=1.00 fxVis=12
    /// SUSPEND+0.1s suspended=True bodyVis=0 accVis=12 petVis=12 petAlpha=0.59 fxVis=12
    /// SUSPEND+0.3s suspended=True bodyVis=0 accVis=0  petVis=0  petAlpha=0.00 fxVis=0
    /// </code>
    /// 사용자가 방금 켠 전체화면 게임 위에 <b>몸 없는 모자·망토·펫 공·반짝임</b>이 떠 있었다.
    /// 가출 은신도 같은 통로(<c>SetCharacterVisible</c> → <c>SetRenderersEnabled</c>)라 동일했고,
    /// 그쪽에서는 남은 잉크가 <b>숨은 자리를 그대로 가리켰다</b>.
    ///
    /// ============================================================================
    /// 왜 기존 테스트가 못 잡았나 — <b>Suspend() 본체를 한 번도 실행한 적이 없다</b>
    /// ============================================================================
    /// <see cref="FullscreenSuspendUiHidingTests"/>는 리플렉션으로 <c>_isSuspended</c> <b>필드만</b>
    /// 세운다(그 파일의 소비자들은 그 플래그만 읽으므로 그 테스트에서는 등가다). 그래서
    /// <c>Suspend()</c> 안의 <c>SetRenderersEnabled(false)</c>는 실행된 적이 없었다.
    /// 이 파일은 반대로 간다 — <b>플랫폼 서비스를 갈아끼워 진짜 감지 경로로</b> Suspend()를 부르고,
    /// 그 프레임에 화면에 남는 잉크를 실측한다.
    ///
    /// ============================================================================
    /// 네거티브 컨트롤
    /// ============================================================================
    /// "0이 나온 게 원래 아무것도 없어서"가 아님을 두 겹으로 막는다:
    ///  ① 숨기기 <b>직전</b>에 액세서리/펫/FX가 실제로 그려지고 있음을 절대 조건으로 확인한다.
    ///  ② <b>수정 전 규칙을 같은 실행에서 계산</b>한다 — 실제 <c>Time.deltaTime</c>과 각 렌더러의
    ///     실제 <c>FadeSeconds</c> 상수(리플렉션으로 읽는다)로 "페이드였다면 이 프레임의 알파가
    ///     얼마였는가"를 구해, 그 값이 <b>또렷하게 보이는 수준</b>임을 단언한다. 즉 이 테스트가 잡는
    ///     것은 "항상 참인 단언"이 아니라 실제로 존재했던 노출 구간이다.
    /// </summary>
    public sealed class FullscreenSuspendCharacterInkTests
    {
        private const string LogPrefix = "[전체화면잉크]";

        /// <summary>페이드였다면 이 프레임에 남았을 알파가 이 값보다 크면 "육안으로 또렷하다"로 본다.</summary>
        private const float VisibleAlphaThreshold = 0.5f;

        /// <summary>감지가 걸릴 때까지 기다리는 <b>게임 시간</b>(초). 감지 주기(0.1초)의 넉넉한 배수다.</summary>
        private const float SuspendWaitSeconds = 3f;

        private StickmanAgent _agent;
        private StickConfig _originalConfig;
        private StickConfig _clonedConfig;
        private IPlatformWindowService _originalService;
        private FullscreenSpoofService _spoof;

        private static readonly FieldInfo PlatformServiceField =
            typeof(StickmanAgent).GetField("_platformService", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo AgentConfigField =
            typeof(StickmanAgent).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// 진짜 감지 경로를 타기 위한 데코레이터 — <b>전체화면 여부 한 가지만</b> 가로채고 나머지는
        /// 원래 구현에 그대로 넘긴다. 이렇게 해야 발판 열거/클릭관통 같은 다른 동작이 변하지 않아,
        /// 관측된 차이가 오직 "전체화면이 감지됐다"에서만 온다.
        /// </summary>
        private sealed class FullscreenSpoofService : IPlatformWindowService
        {
            private readonly IPlatformWindowService _inner;
            public bool Fullscreen;

            public FullscreenSpoofService(IPlatformWindowService inner) { _inner = inner; }

            public IReadOnlyList<PlatformFoothold> EnumerateFootholds() => _inner.EnumerateFootholds();
            public bool CreateOverlayWindow() => _inner.CreateOverlayWindow();
            public void SetClickThrough(bool enabled) => _inner.SetClickThrough(enabled);
            public void SetAlwaysOnTop(bool enabled) => _inner.SetAlwaysOnTop(enabled);
            public bool IsFullscreenAppActive() => Fullscreen;
        }

        // ====================================================================
        // 셋업/정리
        // ====================================================================

        private IEnumerator SetUp()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(_agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다 — Main.unity 배선 확인.");
            Assert.IsNotNull(PlatformServiceField, $"{LogPrefix} StickmanAgent._platformService 필드를 찾지 못했습니다.");
            Assert.IsNotNull(AgentConfigField, $"{LogPrefix} StickmanAgent._config 필드를 찾지 못했습니다.");
            Assert.IsNotNull(BodyRenderersField, $"{LogPrefix} StickmanAgent._renderers 필드를 찾지 못했습니다.");

            // 배포 에셋(DefaultStickConfig.asset)을 절대 건드리지 않는다(불변 원칙 3) — 폴링 주기를
            // 줄여야 하므로 복제본으로 갈아끼운다(CharacterScaleRuntimeTests와 같은 관례).
            _originalConfig = _agent.Config;
            _clonedConfig = Object.Instantiate(_originalConfig);
            _clonedConfig.fullscreenPollInterval = 0.1f;
            _agent.Blackboard.Config = _clonedConfig;
            AgentConfigField.SetValue(_agent, _clonedConfig);

            yield return new WaitForSeconds(1.0f);   // 낙하 정착.

            yield return EquipEverything();
        }

        [TearDown]
        public void TearDown()
        {
            if (_spoof != null) _spoof.Fullscreen = false;
            if (_agent != null && _originalService != null) PlatformServiceField.SetValue(_agent, _originalService);
            if (_agent != null && _originalConfig != null)
            {
                AgentConfigField.SetValue(_agent, _originalConfig);
                if (_agent.Blackboard != null) _agent.Blackboard.Config = _originalConfig;
            }
            if (_clonedConfig != null) Object.Destroy(_clonedConfig);
            _spoof = null;
            _originalService = null;
            _clonedConfig = null;
            _originalConfig = null;
            _agent = null;
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
        }

        /// <summary>FX 슬롯의 <b>0번은 "없음"</b>이다(AppearanceShapeBuilder.FxNone). 0번을 입히면
        /// 이펙트가 하나도 안 생겨 이 테스트의 전제가 조용히 무너지므로 발자국(1번)을 명시한다.</summary>
        private const int FxFootprintItem = 1;

        /// <summary>7슬롯을 전부 착용시킨다 — "가장 많이 그리고 있는 상태"가 최악의 경우다.</summary>
        private IEnumerator EquipEverything()
        {
            CharacterProgressionModel.AddXp(1000000f, _clonedConfig);
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                var slot = (EquipmentSlot)i;
                if (!EquipmentModel.IsUnlocked(slot)) continue;
                int item = slot == EquipmentSlot.Fx ? FxFootprintItem : 0;
                if (EquipmentModel.WornIndex(slot) != item) EquipmentModel.TryWear(slot, item, _clonedConfig);
            }
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                Assert.IsTrue(EquipmentModel.IsEquipped((EquipmentSlot)i),
                    $"{LogPrefix} 슬롯 {(EquipmentSlot)i}을 착용시키지 못했습니다 — 이 테스트의 전제가 성립하지 않습니다.");
            }
            Assert.AreEqual(FxFootprintItem, EquipmentModel.WornIndex(EquipmentSlot.Fx),
                $"{LogPrefix} FX가 '없음'(0번)으로 남아 있습니다 — 이펙트가 하나도 생기지 않습니다.");

            Assert.IsNotNull(_agent.GetComponent<CharacterFxRenderer>(), $"{LogPrefix} CharacterFxRenderer가 없습니다.");
            yield return WaitUntilEverythingIsDrawn();
        }

        /// <summary>
        /// 액세서리/펫/FX가 <b>동시에</b> 실제로 그려지는 순간까지 기다린다. 이것이 곧 이 테스트의
        /// 전제이므로 대리 지표(살아 있는 조각 수)가 아니라 <b>관측 함수 그대로</b>를 조건으로 쓴다 —
        /// 갓 생성된 조각은 그 프레임에 알파가 0이라 "살아 있지만 아직 안 보이는" 구간이 있고,
        /// 실제로 그 구간에서 스냅샷을 떠 전제가 무너진 적이 있다.
        /// <para>FX 발자국은 <b>걸어야</b> 찍히고 수명이 2.4초라, 자율 배회가 걷는 구간을 기다린다.</para>
        /// </summary>
        private IEnumerator WaitUntilEverythingIsDrawn()
        {
            float deadline = Time.time + 40f;
            Snapshot last = default;
            while (Time.time < deadline)
            {
                yield return null;
                last = Observe();
                if (last.Body > 0 && last.Accessory > 0 && last.Pet > 0 && last.Fx > 0) yield break;
            }
            Assert.Fail($"{LogPrefix} 40초 안에 몸/액세서리/펫/FX가 동시에 그려지는 프레임이 없었습니다 " +
                $"(마지막 관측 {last}) — 이 테스트의 전제가 성립하지 않습니다.");
        }

        // ====================================================================
        // 관측 — "지금 이 프레임에 실제로 픽셀을 그리는 렌더러"만 센다
        // ====================================================================

        private Transform AccessoryRoot() => _agent != null ? _agent.transform.Find("EquipmentAccessories") : null;

        private static Transform FindDetachedRoot(string name)
        {
            GameObject go = GameObject.Find(name);
            return go != null ? go.transform : null;
        }

        private static Transform PetRoot() => FindDetachedRoot("CharacterPet");
        private static Transform FxRoot() => FindDetachedRoot("CharacterFx");

        /// <summary>
        /// 이 하위 트리에서 <b>실제로 화면에 잉크를 얹는</b> 렌더러 수.
        /// 플래그가 아니라 <c>enabled</c> / <c>activeInHierarchy</c> / 알파를 전부 읽는다 —
        /// 셋 중 하나라도 놓치면 "안 보이는데 통과"나 "보이는데 통과" 둘 다 만들 수 있다.
        /// </summary>
        private static int CountVisibleInk(Transform root)
        {
            if (root == null) return 0;
            int n = 0;
            Renderer[] all = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Renderer r = all[i];
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (r is LineRenderer lr && lr.startColor.a <= 0.01f && lr.endColor.a <= 0.01f) continue;
                n++;
            }
            return n;
        }

        private static readonly FieldInfo BodyRenderersField =
            typeof(StickmanAgent).GetField("_renderers", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// 몸 — <b>StickmanAgent가 Awake에서 캐시한 바로 그 배열</b>을 읽는다.
        /// 계층을 다시 훑지 않는 이유: 캐릭터 계층에는 말풍선/게이지처럼 이 라운드의 관심사가 아닌
        /// 런타임 잉크도 붙을 수 있어, 그것들이 섞이면 이 테스트가 엉뚱한 이유로 깜빡인다.
        /// Suspend()가 제어하는 대상을 정확히 그대로 세는 것이 이 단언의 의미다.
        /// </summary>
        private int CountVisibleBody()
        {
            var body = (Renderer[])BodyRenderersField.GetValue(_agent);
            if (body == null) return 0;
            int n = 0;
            for (int i = 0; i < body.Length; i++)
            {
                Renderer r = body[i];
                if (r != null && r.enabled && r.gameObject.activeInHierarchy) n++;
            }
            return n;
        }

        private struct Snapshot
        {
            public int Body, Accessory, Pet, Fx;
            public override string ToString() => $"body={Body} acc={Accessory} pet={Pet} fx={Fx}";
        }

        private Snapshot Observe()
        {
            return new Snapshot
            {
                Body = CountVisibleBody(),
                Accessory = CountVisibleInk(AccessoryRoot()),
                Pet = CountVisibleInk(PetRoot()),
                Fx = CountVisibleInk(FxRoot()),
            };
        }

        /// <summary>수정 전 규칙(페이드)이었다면 이 프레임에 남았을 알파. 실제 dt와 실제 상수로 계산한다.</summary>
        private static float PreFixAlphaAfterOneFrame(System.Type rendererType, float dt)
        {
            FieldInfo f = rendererType.GetField("FadeSeconds", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(f, $"{LogPrefix} {rendererType.Name}.FadeSeconds 상수를 찾지 못했습니다 — " +
                "네거티브 컨트롤이 실제 값 대신 추측을 쓰게 되므로 이름이 바뀌면 여기도 고쳐야 합니다.");
            float fade = (float)f.GetRawConstantValue();
            return Mathf.Max(0f, 1f - dt / Mathf.Max(0.01f, fade));
        }

        // ====================================================================
        // (1) 전체화면 감지 — 진짜 Suspend() 경로
        // ====================================================================

        [UnityTest]
        public IEnumerator 전체화면_감지_프레임에_액세서리_펫_FX가_한_개도_남지_않는다()
        {
            yield return SetUp();
            yield return WaitUntilEverythingIsDrawn();

            Snapshot before = Observe();
            Debug.Log($"{LogPrefix} BEFORE  suspended={_agent.IsSuspended} {before}");

            // ① 네거티브 컨트롤 1 — 원래 아무것도 없어서 0이 나오는 것이 아님을 절대 조건으로 못박는다.
            Assert.Greater(before.Body, 0, $"{LogPrefix} 숨기기 전 몸이 이미 안 보입니다 — 전제 불성립.");
            Assert.Greater(before.Accessory, 0, $"{LogPrefix} 숨기기 전 액세서리가 이미 안 보입니다 — 전제 불성립.");
            Assert.Greater(before.Pet, 0, $"{LogPrefix} 숨기기 전 펫이 이미 안 보입니다 — 전제 불성립.");
            Assert.Greater(before.Fx, 0, $"{LogPrefix} 숨기기 전 FX가 이미 안 보입니다 — 전제 불성립.");

            // 진짜 경로로 전체화면을 감지시킨다(플래그 주입이 아니다).
            _originalService = (IPlatformWindowService)PlatformServiceField.GetValue(_agent);
            _spoof = new FullscreenSpoofService(_originalService) { Fullscreen = true };
            PlatformServiceField.SetValue(_agent, _spoof);

            // ★ 프레임 수가 아니라 <b>게임 시간</b>으로 기다린다. 감지 주기는 초 단위(최소 0.1초)인데
            //   배치모드(-nographics)는 프레임이 극단적으로 짧아(실측 dt≈0.0001초) 300프레임이
            //   0.03초밖에 안 된다 — 프레임 수로 기다리면 감지가 <b>영원히</b> 오지 않는다.
            //   그렇다고 WaitForSeconds로 건너뛰면 "감지된 그 프레임"을 놓치므로, 한 프레임씩 돌되
            //   한계를 시간으로 둔다.
            float suspendDeadline = Time.time + SuspendWaitSeconds;
            while (!_agent.IsSuspended && Time.time < suspendDeadline) yield return null;
            Assert.IsTrue(_agent.IsSuspended,
                $"{LogPrefix} {SuspendWaitSeconds:F1}초(게임 시간) 안에 Suspend()가 걸리지 않았습니다 — " +
                $"감지 경로 배선을 확인하세요(감지 주기 {_agent.Config.fullscreenPollInterval:F2}초).");

            float dt = Time.deltaTime;
            Snapshot at0 = Observe();
            Debug.Log($"{LogPrefix} SUSPEND+0f suspended={_agent.IsSuspended} {at0} (dt={dt:F4})");

            // ② 네거티브 컨트롤 2 — <b>수정 전 규칙</b>을 같은 실행에서 계산한다.
            float accPreFix = PreFixAlphaAfterOneFrame(typeof(CharacterAccessoryRenderer), dt);
            float petPreFix = PreFixAlphaAfterOneFrame(typeof(CharacterPetRenderer), dt);
            Debug.Log($"{LogPrefix} 네거티브 컨트롤 — 수정 전(페이드) 규칙이었다면 이 프레임의 알파는 " +
                $"액세서리 {accPreFix:F2} / 펫 {petPreFix:F2}였다(FX는 자기 수명대로 계속 그려졌다).");
            Assert.Greater(accPreFix, VisibleAlphaThreshold,
                $"{LogPrefix} 이 실행에서는 페이드 규칙이어도 액세서리가 이미 안 보였을 것입니다(알파 {accPreFix:F2}) — " +
                "그러면 아래 단언이 결함을 잡고 있다고 말할 수 없습니다(프레임 시간이 비정상적으로 긴 환경).");
            Assert.Greater(petPreFix, VisibleAlphaThreshold,
                $"{LogPrefix} 이 실행에서는 페이드 규칙이어도 펫이 이미 안 보였을 것입니다(알파 {petPreFix:F2}).");

            // ③ 본 단언 — 감지된 그 프레임에 <b>하나도</b> 남지 않는다.
            Assert.AreEqual(0, at0.Body, $"{LogPrefix} 감지 프레임에 몸이 {at0.Body}개 남았습니다.");
            Assert.AreEqual(0, at0.Accessory,
                $"{LogPrefix} 감지 프레임에 액세서리가 {at0.Accessory}개 남았습니다 — 전체화면 게임 위에 " +
                "'몸 없는 모자·망토'가 뜹니다(절대 불변 원칙 2 위반).");
            Assert.AreEqual(0, at0.Pet,
                $"{LogPrefix} 감지 프레임에 펫이 {at0.Pet}개 남았습니다 — 주인 없는 공/종이비행기가 게임 위에 뜹니다.");
            Assert.AreEqual(0, at0.Fx,
                $"{LogPrefix} 감지 프레임에 FX 조각이 {at0.Fx}개 남았습니다 — 이미 떠 있던 발자국/반짝임이 " +
                "자기 수명(수 초)만큼 게임 위에 계속 그려집니다.");

            // ④ 그 뒤로도 다시 켜지지 않는다 — 소유자의 LateUpdate가 되살리지 않는가.
            for (int i = 1; i <= 3; i++)
            {
                yield return null;
                Snapshot s = Observe();
                Debug.Log($"{LogPrefix} SUSPEND+{i}f {s}");
                Assert.AreEqual(0, s.Accessory + s.Pet + s.Fx + s.Body,
                    $"{LogPrefix} 감지 {i}프레임 뒤에 잉크가 되살아났습니다({s}) — 소유자의 LateUpdate가 다시 켰습니다.");
            }

            yield return new WaitForSeconds(0.3f);
            Snapshot late = Observe();
            Debug.Log($"{LogPrefix} SUSPEND+0.3s {late}");
            Assert.AreEqual(0, late.Accessory + late.Pet + late.Fx + late.Body,
                $"{LogPrefix} 감지 0.3초 뒤에 잉크가 남아 있습니다({late}).");
        }

        // ====================================================================
        // (2) 같은 통로를 쓰는 가출(Runaway) 은신
        // ====================================================================

        /// <summary>
        /// 가출 은신은 <see cref="StickmanBlackboard.SetCharacterVisible"/>(= 같은
        /// <c>SetRenderersEnabled</c>)를 부른다. RunawayState가 실제로 부르는 델리게이트를 그대로
        /// 호출하므로 private 필드 주입이 아니다 — 상태 전이를 기다리지 않고도 같은 코드가 실행된다.
        /// </summary>
        [UnityTest]
        public IEnumerator 가출_은신_프레임에도_액세서리_펫_FX가_함께_사라진다()
        {
            yield return SetUp();
            yield return WaitUntilEverythingIsDrawn();

            Snapshot before = Observe();
            Debug.Log($"{LogPrefix} [가출] BEFORE {before}");
            Assert.Greater(before.Accessory, 0, $"{LogPrefix} [가출] 전제 불성립 — 액세서리가 이미 안 보입니다.");
            Assert.Greater(before.Pet, 0, $"{LogPrefix} [가출] 전제 불성립 — 펫이 이미 안 보입니다.");
            Assert.Greater(before.Fx, 0, $"{LogPrefix} [가출] 전제 불성립 — FX가 이미 안 보입니다.");

            Assert.IsNotNull(_agent.Blackboard.SetCharacterVisible,
                $"{LogPrefix} [가출] SetCharacterVisible이 배선되지 않았습니다.");
            _agent.Blackboard.SetCharacterVisible.Invoke(false);
            _agent.Blackboard.IsCharacterHiddenByRunaway = true;

            yield return null;   // 소유자들의 LateUpdate가 한 바퀴 도는 그 프레임.

            Snapshot hidden = Observe();
            Debug.Log($"{LogPrefix} [가출] HIDE+1f {hidden}");
            Assert.AreEqual(0, hidden.Accessory + hidden.Pet + hidden.Fx + hidden.Body,
                $"{LogPrefix} [가출] 은신 중인데 잉크가 {hidden} 남아 있습니다 — 모자와 펫이 숨은 자리를 " +
                "그대로 가리킵니다(숨바꼭질이 성립하지 않는다).");

            // 되돌린다(다음 테스트로 은신 상태가 새지 않게).
            _agent.Blackboard.IsCharacterHiddenByRunaway = false;
            _agent.Blackboard.SetCharacterVisible.Invoke(true);
            for (int i = 0; i < 30; i++) yield return null;

            Snapshot back = Observe();
            Debug.Log($"{LogPrefix} [가출] 발견 후 복귀 {back}");
            Assert.Greater(back.Body, 0, $"{LogPrefix} [가출] 발견됐는데 몸이 돌아오지 않았습니다.");
            Assert.Greater(back.Accessory, 0,
                $"{LogPrefix} [가출] 발견됐는데 액세서리가 돌아오지 않았습니다 — 숨기기만 고치고 " +
                "되살아나는 경로를 끊어버린 것입니다.");
        }
    }
}
