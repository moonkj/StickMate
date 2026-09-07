using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★★ <b>배선이 실제로 도는가</b> — 소스 스캔이 구조적으로 못 보는 절반을 여기서 잰다
    /// (2026-09-06 재화 배선 2차: 첫 실행 시드 · 활쏘기 상금 / 3차: 유휴 수급 · H-8 등급 눈금).
    ///
    /// ============================================================================
    /// EditMode 감사와 이 파일의 분업
    /// ============================================================================
    /// <c>Tests/EditMode/CurrencySeedAndArcheryWiringTests</c>는 <b>소스에 호출부가 있는가</b>와
    /// <b>순서·관문이 맞는가</b>를 잰다. 그것만으로는 «컴포넌트가 씬에 실제로 있고, 그 <c>Start</c>가
    /// 실제로 돌고, 이벤트가 실제로 그 훅에 닿는가»를 알 수 없다 — 이 저장소는 그 갭에서
    /// «코드는 있는데 아무도 안 부른다»를 이미 여러 번 겪었다.
    ///
    /// <para>그래서 이 파일은 <b>실제 씬을 띄우고</b>:</para>
    /// <list type="number">
    ///   <item>새 캐릭터로 켜면 <b>시드가 실제로 지갑에 들어오는가</b>(§1).</item>
    ///   <item>저장하고 <b>다시 켜면 두 번째 시드가 안 나오는가</b>(§2 — 사용자 요구 그대로).</item>
    ///   <item>정중앙 명중 이벤트가 <b>실제로 동전을 늘리는가</b>, 그리고 빗나감·조준 시점·쿨다운이
    ///     <b>실제로 막는가</b>(§3).</item>
    ///   <item>★★ <b>집중 세션 중 유휴가 멈추고, 끝난 뒤에 그 시간을 몰아주지 않는가</b>(§4).
    ///     실제 세션을 켜고 벽시계로 재는, 이 라운드에서 가장 값진 단언이다 —
    ///     I-7′ 파손은 화면에도 로그에도 «두 번 받았다»는 흔적을 남기지 않는다.</item>
    ///   <item>장비를 <b>실제로 갈아입어</b> H-8 등급 눈금이 새겨지고, 벗어도 안 내려가는가(§5).</item>
    ///   <item>★★ <b>「유휴 수급이 멈췄다」가 정상 동작을 고장으로 신고하지 않는가</b>(§6, 2026-09-06).
    ///     정지 판정이 «0동전»이던 동안 <b>5초에 한 줄(실측 720줄/시간)</b>이 쌓였다. 실제 씬을
    ///     벽시계로 돌려 <b>줄 수를 세는</b> 것 말고는 이 결함을 잡을 자가 없다 — 소스 스캔은
    ///     «어떤 조건으로 찍는가»만 보고, 모델 테스트는 로그를 아예 안 본다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 격리 — 개발자의 실제 저장 파일을 건드리지 않는다
    /// ============================================================================
    /// <c>GlobalPlayModeTestIsolation</c>이 저장 경로를 임시 폴더로 옮기고,
    /// <c>PlayModeSaveIsolationGate</c>가 <b>리프 테스트마다</b> 그 폴더를 비운다. 이 픽스처는 그 위에서
    /// <b>정적 모델까지</b> 초기화한다 — <c>CharacterSaveStore.Load()</c>는 파일이 없으면 <b>아무것도
    /// 하지 않고 돌아가므로</b>(그것이 «새 캐릭터»의 정의다) 앞 테스트가 남긴 정적 <c>SeedGranted=true</c>가
    /// 그대로 살아남는다. 그 상태로 재면 «시드가 안 나왔다»는 <b>거짓 빨강</b>이 난다.
    ///
    /// ============================================================================
    /// ★ 숫자를 베끼지 않는다 / 시간은 벽시계로
    /// ============================================================================
    /// 1200·20은 <see cref="CurrencyRules"/>에서 읽는다. 프레임 수로 예산을 잡지 않는다
    /// (이 저장소의 배치모드 PlayMode는 수천 fps로 돈다 — CLAUDE.md).
    ///
    /// <para><b>플랫폼</b>: 플랫폼 중립. 창 열거·좌표계·네이티브를 하나도 건드리지 않는다.</para>
    /// </summary>
    public sealed class CurrencyWiringRuntimeTests
    {
        private const string LogPrefix = "[재화배선실주행]";

        private CharacterProgressionDirector _director;

        [SetUp]
        public void ResetStaticModels()
        {
            // ★ 반드시 씬 로드 <b>전</b>이다. 씬의 Start가 이 값들을 읽는다.
            CurrencyModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
        }

        [TearDown]
        public void Clean()
        {
            StopWatchingLogs();
            _director = null;
            CurrencyModel.ResetForTesting();
        }

        /// <summary>씬을 띄우고 성장 디렉터를 찾는다. <c>Start()</c>가 이미 돈 뒤다
        /// (프레임을 두 번 넘긴다 — 씬 로드가 끝나는 프레임과 그다음 프레임).</summary>
        private IEnumerator LoadSceneAndFindDirector()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _director = Object.FindFirstObjectByType<CharacterProgressionDirector>();
            Assert.IsNotNull(_director,
                $"{LogPrefix} 씬에 {nameof(CharacterProgressionDirector)}가 없습니다 — " +
                "이 컴포넌트가 없으면 시드도 활쏘기 상금도 <b>영원히</b> 지급되지 않습니다.");
        }

        // ====================================================================
        // §1. 새 캐릭터로 켜면 시드가 실제로 들어온다
        // ====================================================================

        [UnityTest]
        public IEnumerator 새_캐릭터로_켜면_시드가_실제로_지갑에_들어온다()
        {
            Assert.IsTrue(CharacterSaveStore.IsRedirectedForTesting,
                $"{LogPrefix} 저장 경로가 격리돼 있지 않습니다 — 개발자의 실제 파일을 읽게 되므로 여기서 멈춥니다.");
            Assert.IsFalse(CurrencyModel.SeedGranted, "전제 — 초기화 직후에는 시드를 받은 적이 없어야 합니다.");

            yield return LoadSceneAndFindDirector();

            Assert.IsFalse(CharacterSaveStore.LoadedFromFile,
                $"{LogPrefix} 격리 폴더에 앞선 테스트의 저장 파일이 남아 있습니다 — " +
                "이 테스트는 «새 캐릭터»를 전제로 하므로 지금 재는 것은 그것이 아닙니다.");

            Assert.IsTrue(CurrencyModel.SeedGranted,
                $"{LogPrefix} 앱을 켰는데 시드 플래그가 서지 않았습니다 — 지급 배선이 돌지 않았습니다.");
            Assert.AreEqual(CurrencyRules.SeedCoins, CurrencyModel.CoinBalance,
                $"{LogPrefix} 첫 실행 잔액이 시드({CurrencyRules.SeedCoins})와 다릅니다. " +
                "0이면 배선이 안 돌았거나 로드가 지급을 덮어쓴 것이고, 그보다 크면 다른 경로가 " +
                "같은 프레임에 지급한 것입니다 — 어느 쪽이든 원인을 찾아야 합니다.");

            Assert.IsTrue(CurrencyModel.IsDirty,
                $"{LogPrefix} 시드를 지급했는데 저장 대상으로 표시되지 않았습니다 — " +
                "주기/종료 저장이 이 지급을 싣지 못합니다.");
        }

        // ====================================================================
        // §2. 저장하고 다시 켜면 두 번째 시드는 없다 (사용자 요구 그대로)
        // ====================================================================

        [UnityTest]
        public IEnumerator 저장하고_다시_켜도_시드는_두_번_나오지_않는다()
        {
            yield return LoadSceneAndFindDirector();
            Assert.AreEqual(CurrencyRules.SeedCoins, CurrencyModel.CoinBalance, "전제 — 첫 실행 시드.");

            // 주기 저장(60초)을 기다리는 대신 같은 경로를 직접 부른다 — 재는 것은 «시드가 디스크에
            // 남는가»이지 «타이머가 도는가»가 아니다(타이머는 다른 테스트의 주제다).
            Assert.IsTrue(CharacterSaveStore.Save(), $"{LogPrefix} 저장이 실패/보류됐습니다.");

            // ── 「다시 켜기」. 정적 모델은 프로세스가 살아 있는 동안 남으므로 <b>일부러 지운다</b> —
            //    그래야 두 번째 실행이 «디스크가 말한 것»만 보고 판단한다.
            CurrencyModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            Assert.IsFalse(CurrencyModel.SeedGranted, "전제 — 초기화가 안 먹으면 아래 단언이 공허합니다.");

            yield return LoadSceneAndFindDirector();

            Assert.IsTrue(CharacterSaveStore.LoadedFromFile,
                $"{LogPrefix} 두 번째 실행이 저장 파일을 읽지 못했습니다 — 「다시 켜기」가 재현되지 않았습니다.");
            Assert.IsTrue(CurrencyModel.SeedGranted, $"{LogPrefix} seedGranted가 디스크를 왕복하지 못했습니다.");
            Assert.AreEqual(CurrencyRules.SeedCoins, CurrencyModel.CoinBalance,
                $"{LogPrefix} 다시 켰더니 잔액이 {CurrencyModel.CoinBalance}입니다 — " +
                $"시드({CurrencyRules.SeedCoins})의 2배라면 <b>켤 때마다 시드가 나오는 상태</b>이고, " +
                "0이라면 시드가 저장되지 않은 것입니다.");
        }

        // ====================================================================
        // §3. 활쏘기 — 정중앙 명중이 실제로 동전을 늘린다
        // ====================================================================

        /// <summary>
        /// ★ 이벤트를 <b>버스에 직접 발행</b>한다. 실제 활쏘기 사이클을 기다리지 않는 이유는
        /// 그 사이클이 «걸어가서 · 과녁을 세우고 · 세 발을 쏘는» 십수 초짜리 연출이고, 결과가
        /// <b>확률로 뽑히기 때문</b>이다(<c>archeryHitChance</c>/<c>archeryBullseyeChance</c>).
        /// 여기서 재려는 것은 «정중앙이 나왔을 때 동전이 들어오는가»이지 «정중앙이 얼마나 자주
        /// 나오는가»가 아니다 — 뒤쪽은 <c>ArcheryShotProbabilityTests</c>의 주제다.
        /// </summary>
        [UnityTest]
        public IEnumerator 정중앙_명중이_실제로_동전을_늘린다()
        {
            yield return LoadSceneAndFindDirector();

            // 시드가 들어온 판 위에서 재면 «얼마가 늘었는가»가 흐려진다 — 깨끗한 지갑으로 시작한다.
            CurrencyModel.ResetForTesting();
            Assert.AreEqual(0, CurrencyModel.CoinBalance, "전제 — 지갑이 비어 있어야 증가분을 잰다.");

            // ── (가) 빗나간 발은 한 푼도 만들지 않는다.
            RaiseShot(0, ArcheryShotPhase.Release, ArcheryShotResult.Miss);
            yield return null;
            Assert.AreEqual(0, CurrencyModel.CoinBalance,
                $"{LogPrefix} 빗나갔는데 동전이 나왔습니다 — «명중 = 동전» 규칙이 깨졌습니다.");

            // ── (나) 조준(Aim) 시점도 아니다. Release에서만 한 번이다.
            RaiseShot(1, ArcheryShotPhase.Aim, ArcheryShotResult.Bullseye);
            yield return null;
            Assert.AreEqual(0, CurrencyModel.CoinBalance,
                $"{LogPrefix} 시위를 당긴 것만으로 동전이 나왔습니다 — 한 발에 두 번 지급되는 형태입니다.");

            // ── (다) 정중앙 + Release = 지급.
            RaiseShot(1, ArcheryShotPhase.Release, ArcheryShotResult.Bullseye);
            yield return null;
            Assert.AreEqual(CurrencyRules.ArcheryCoinsPerAward, CurrencyModel.CoinBalance,
                $"{LogPrefix} 정중앙에 맞았는데 지급액이 {CurrencyModel.CoinBalance}입니다 " +
                $"(기대 {CurrencyRules.ArcheryCoinsPerAward}). 0이면 배선이 안 돌았고, " +
                "다른 값이면 지급 상수가 두 곳에 있습니다.");
            Assert.AreEqual(CurrencyRules.ArcheryCoinsPerAward, CurrencyModel.ArcheryCoinsToday,
                $"{LogPrefix} 오늘 활쏘기 누계가 지급액과 다릅니다 — 일일 상한이 잘못 세어집니다.");

            // ── (라) 같은 발이 다시 발행돼도 두 번 지급하지 않는다.
            RaiseShot(1, ArcheryShotPhase.Release, ArcheryShotResult.Bullseye);
            yield return null;
            Assert.AreEqual(CurrencyRules.ArcheryCoinsPerAward, CurrencyModel.CoinBalance,
                $"{LogPrefix} 같은 발이 두 번 지급됐습니다.");

            // ── (마) 다음 발이 또 정중앙이어도 <b>쿨다운</b>에 막힌다(§20-3-b).
            //    쿨다운은 단조 시계 600초라 이 테스트 안에서는 절대 풀리지 않는다 — 결정론적이다.
            RaiseShot(2, ArcheryShotPhase.Release, ArcheryShotResult.Bullseye);
            yield return null;
            Assert.AreEqual(CurrencyRules.ArcheryCoinsPerAward, CurrencyModel.CoinBalance,
                $"{LogPrefix} 쿨다운({CurrencyRules.ArcheryAwardCooldownSeconds:F0}초) 중인데 상금이 또 나왔습니다 — " +
                "연속 명중만으로 무제한 파밍이 됩니다.");

            Assert.IsTrue(CurrencyModel.IsDirty,
                $"{LogPrefix} 활쏘기 상금이 저장 대상으로 표시되지 않았습니다 — 그날 번 동전이 디스크에 안 남습니다.");
        }

        private static void RaiseShot(int shotIndex, ArcheryShotPhase phase, ArcheryShotResult result)
            => StickmanEventBus.RaiseArcheryShotChanged(shotIndex, phase, result, Vector2.zero, 0.5f);

        // ====================================================================
        // §3-보안. ★★ 활쏘기 XP 채널이 동전과 같은 쿨다운/일일상한을 공유하는가
        //    (design-systems 발견 2026-09-06, coder-systems 수정 2026-09-07)
        // ====================================================================
        //
        // 위 §3(동전)과 <b>대칭되는 XP 버전</b>이다 — 같은 시나리오를 그대로 재생하되 잔액이 아니라
        // CharacterProgressionModel.TotalXpEarned를 본다.
        //
        // ★★ 이 파일이 존재하는 이유(수정 전 실측): OnArcheryShotChanged는 동전에는 이미 쿨다운
        //    (단조 600초)·일일 상한(72회)을 걸었으면서 <b>XP는 같은 발 재발행 방어 하나만 걸고
        //    무조건 지급</b>했다. 그래서 §3의 (마)와 똑같은 시나리오(쿨다운 중 새 발이 또 정중앙)에서
        //    <b>동전은 안 늘고 XP만 늘었다</b> — 연속 도배 시 시간당 ~6,478XP(패시브의 72배)로
        //    Lv50 전체 요구량(design-systems 곡선 기준)을 22.4시간 만에 채우는 익스플로잇이었다
        //    (docs/DESIGN_SYSTEMS_LEVEL_STAT_GROWTH_PROPOSAL.md §3-3). 아래 (마)가 그 회귀를 정확히
        //    재현한다 — 수정 전에는 여기서 빨갛다.
        //
        // ★ 지급량(progressionBullseyeXp)은 리터럴로 베끼지 않고 씬의 배포 설정에서 읽는다.

        /// <summary>
        /// ★★ 보안 결함 회귀 — 코인이 쿨다운/일일상한에 막히면 XP도 함께 막히는가.
        /// </summary>
        [UnityTest]
        public IEnumerator 정중앙_명중이_실제로_XP를_늘리고_코인과_같은_쿨다운을_공유한다()
        {
            yield return LoadSceneAndFindDirector();

            // 깨끗한 지갑·깨끗한 XP로 시작한다(시드/이전 테스트 잔여가 증가분을 흐리지 않도록).
            CurrencyModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            Assert.AreEqual(0, CurrencyModel.CoinBalance, "전제 — 지갑이 비어 있어야 증가분을 잰다.");
            Assert.AreEqual(0f, CharacterProgressionModel.TotalXpEarned, "전제 — 누적 XP가 0이어야 증가분을 잰다.");

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} 씬에 {nameof(StickmanAgent)}가 없습니다.");
            float bullseyeXp = agent.Config != null ? agent.Config.progressionBullseyeXp : 0f;
            Assert.Greater(bullseyeXp, 0f,
                $"{LogPrefix} 배포 설정의 progressionBullseyeXp가 0입니다 — 아래 증가분 판정이 공허해집니다.");

            // ── (가) 빗나간 발은 XP도 늘리지 않는다.
            RaiseShot(10, ArcheryShotPhase.Release, ArcheryShotResult.Miss);
            yield return null;
            Assert.AreEqual(0f, CharacterProgressionModel.TotalXpEarned,
                $"{LogPrefix} 빗나갔는데 XP가 나왔습니다.");

            // ── (나) 조준(Aim) 시점도 아니다.
            RaiseShot(11, ArcheryShotPhase.Aim, ArcheryShotResult.Bullseye);
            yield return null;
            Assert.AreEqual(0f, CharacterProgressionModel.TotalXpEarned,
                $"{LogPrefix} 시위를 당긴 것만으로 XP가 나왔습니다.");

            // ── (다) 정중앙 + Release = 코인과 XP가 함께 지급된다.
            RaiseShot(11, ArcheryShotPhase.Release, ArcheryShotResult.Bullseye);
            yield return null;
            Assert.AreEqual(CurrencyRules.ArcheryCoinsPerAward, CurrencyModel.CoinBalance,
                $"{LogPrefix} 전제 — 첫 명중은 코인이 나와야 아래 XP 대조가 의미를 갖습니다.");
            Assert.AreEqual(bullseyeXp, CharacterProgressionModel.TotalXpEarned, 0.001f,
                $"{LogPrefix} 첫 정중앙 명중인데 누적 XP가 {CharacterProgressionModel.TotalXpEarned}입니다 " +
                $"(기대 {bullseyeXp}). 0이면 배선이 죽은 것이고, 다른 값이면 지급량이 두 곳에 있습니다.");

            // ── (라) 같은 발 재발행 — 코인도 XP도 다시 늘지 않는다(기존 방어, §3-(라)와 대칭).
            RaiseShot(11, ArcheryShotPhase.Release, ArcheryShotResult.Bullseye);
            yield return null;
            Assert.AreEqual(bullseyeXp, CharacterProgressionModel.TotalXpEarned, 0.001f,
                $"{LogPrefix} 같은 발이 XP를 두 번 줬습니다.");

            // ── (마) ★★ 회귀의 핵심 — 새 발(다른 shotIndex)이 또 정중앙이어도 코인이 쿨다운에
            //    막히면 XP도 함께 막혀야 한다(§3-(마)와 대칭). 수정 전에는 코인은 0인데 XP만
            //    계속 나갔다 — 그것이 이 보안 결함의 실체였다.
            RaiseShot(12, ArcheryShotPhase.Release, ArcheryShotResult.Bullseye);
            yield return null;
            Assert.AreEqual(CurrencyRules.ArcheryCoinsPerAward, CurrencyModel.CoinBalance,
                $"{LogPrefix} 전제 — 쿨다운({CurrencyRules.ArcheryAwardCooldownSeconds:F0}초) 중이라 " +
                $"코인은 여전히 {CurrencyRules.ArcheryCoinsPerAward}이어야 합니다.");
            Assert.AreEqual(bullseyeXp, CharacterProgressionModel.TotalXpEarned, 0.001f,
                $"{LogPrefix} ★★ 코인은 쿨다운에 막혔는데 누적 XP가 {CharacterProgressionModel.TotalXpEarned}로 " +
                $"늘었습니다(기대 {bullseyeXp}, 변화 없음) — 쿨다운 없는 XP 무한 파밍 결함이 되돌아왔습니다 " +
                "(docs/DESIGN_SYSTEMS_LEVEL_STAT_GROWTH_PROPOSAL.md §3-3, 시간당 ~6,478XP 이론치로 " +
                "Lv50 요구량을 22.4시간에 채울 수 있었던 그 구멍입니다).");
        }

        // ====================================================================
        // §4. ★★ 유휴 수급 — I-7′를 <b>실제 세션을 돌려</b> 잰다
        // ====================================================================
        //
        // ★ <b>왜 CoinBalance가 아니라 TodayGrantedCoins를 보는가.</b> 잔액은 활쏘기·집중·시드가
        //   함께 쓰는 칸이라, 20초를 기다리는 동안 캐릭터가 스스로 활쏘기를 시작하면 잔액이 흔들린다
        //   (그 흔들림은 «유휴가 샜다»와 똑같이 생겼다). <c>todayGrantedCoins</c>는 정의상
        //   <b>«오늘 유휴로 지급된 동전»</b>이고 다른 채널은 그 칸을 건드리지 않는다 —
        //   집중 지급이 일부러 이 칸을 안 지나는 것이 그 증거다(CurrencyModel 「집중 모드 지급」 문단).
        //   ⇒ 다른 연출이 무엇을 하든 이 테스트는 <b>유휴만</b> 본다.

        /// <summary>1동전이 확실히 쌓이는 관측 시간(초). ★ 상수를 베끼지 않고
        /// <see cref="CurrencyRules.IdleCoinsPerSecond"/>에서 유도한다 — 요율이 바뀌면 이 예산이
        /// 저절로 따라간다. 2를 곱해 여유를 둔다(경계에서 <c>floor</c>에 걸려 0이 나오지 않게).</summary>
        private static float IdleProbeSeconds => (float)(2.0 / CurrencyRules.IdleCoinsPerSecond);

        /// <summary>벽시계(초) 대기. ★ 프레임 수로 예산을 잡지 않는다 —
        /// 이 저장소의 배치모드 PlayMode는 수천 fps로 돌아서 프레임 예산이 밀리초가 된다(CLAUDE.md).</summary>
        private static IEnumerator WaitRealSeconds(float seconds)
        {
            double until = Time.realtimeSinceStartupAsDouble + seconds;
            while (Time.realtimeSinceStartupAsDouble < until) yield return null;
        }

        [UnityTest]
        public IEnumerator 집중_세션_중에는_유휴가_멈추고_끝난_뒤에_몰아주지_않는다()
        {
            yield return LoadSceneAndFindDirector();

            var focus = Object.FindFirstObjectByType<FocusWatchDirector>();
            Assert.IsNotNull(focus, $"{LogPrefix} 씬에 {nameof(FocusWatchDirector)}가 없습니다.");
            Assert.IsFalse(focus.IsSessionActive, "전제 — 세션이 꺼져 있어야 합니다.");

            CurrencyModel.ResetForTesting();
            Assert.AreEqual(0, CurrencyModel.TodayGrantedCoins, "전제 — 유휴 버킷이 비어 있어야 합니다.");

            // ── 세션을 켜고 관측 시간만큼 기다린다. 이 동안 유휴는 <b>한 푼도</b> 나오면 안 된다.
            focus.StartFocusSession(5f);
            Assert.IsTrue(focus.IsSessionActive, "집중 세션이 시작되지 않았습니다.");

            yield return WaitRealSeconds(IdleProbeSeconds);

            Assert.AreEqual(0, CurrencyModel.TodayGrantedCoins,
                $"{LogPrefix} 집중 세션 중에 유휴 수급이 들어왔습니다 — 같은 1초가 집중과 유휴 " +
                "양쪽에서 지급되고 있습니다(I-7′ 파손).");
            Assert.AreEqual(0.0, CurrencyModel.IdleWindowUsedSeconds, 1e-6,
                $"{LogPrefix} 지급도 없이 8시간 창만 닳았습니다 — 그건 방어가 아니라 버그입니다(T-15-1-a).");

            // ── 세션을 끈다. 1분 미만 경과라 취소 지급은 0이고(§22-12), 유휴 버킷도 그대로여야 한다.
            focus.StopFocusSession();
            Assert.IsFalse(focus.IsSessionActive);

            // ★★ 여기가 이 테스트의 본론이다. 기산점이 세션 동안 멈춰 있었다면, 세션이 끝난
            //    <b>첫 틱의 델타에 세션 전체 길이가 실려</b> 유휴 버킷이 그 자리에서 훌쩍 뛴다.
            yield return null;
            yield return null;
            Assert.AreEqual(0, CurrencyModel.TodayGrantedCoins,
                $"{LogPrefix} ★ 세션이 끝나자마자 유휴 버킷이 {CurrencyModel.TodayGrantedCoins}동전으로 " +
                "뛰었습니다 — 집중 세션 동안 멈춰 뒀던 기산점이 그 시간을 <b>유휴로 한 번 더</b> " +
                "지급한 것입니다. 사용자는 25분 세션마다 300동전을 덤으로 받게 됩니다.");

            // ── 양성 대조 — 유휴가 <b>실제로 벌기는 하는가</b>. 이게 없으면 위 두 개의 «0»은
            //    «계약을 지켰다»가 아니라 «유휴 수급이 아예 안 돈다»와 구분되지 않는다.
            yield return WaitRealSeconds(IdleProbeSeconds);
            Assert.Greater(CurrencyModel.TodayGrantedCoins, 0,
                $"{LogPrefix} 세션이 끝난 뒤에도 유휴 수급이 0입니다 — 위의 «집중 중 0동전»은 " +
                "계약이 아니라 <b>기능 부재</b>입니다. 배선이 죽었는지 확인하십시오.");
            Assert.Greater(CurrencyModel.IdleWindowUsedSeconds, 0.0,
                $"{LogPrefix} 동전은 늘었는데 8시간 창이 안 닳았습니다 — 두 값이 한 사건이어야 합니다.");
        }

        // ====================================================================
        // §5. H-8 등급 눈금 — 장비를 갈아입으면 실제로 새겨지는가
        // ====================================================================

        [UnityTest]
        public IEnumerator 장비를_갈아입으면_등급_눈금이_실제로_새겨진다()
        {
            yield return LoadSceneAndFindDirector();

            var stats = Object.FindFirstObjectByType<CharacterStatsDirector>();
            Assert.IsNotNull(stats, $"{LogPrefix} 씬에 {nameof(CharacterStatsDirector)}가 없습니다 — " +
                "등급 눈금을 기록하는 컴포넌트가 없으면 정보창 눈금은 영원히 0단계입니다.");

            CurrencyModel.ResetForTesting();
            for (int i = 0; i < EquipmentStatRules.StatCount; i++)
            {
                Assert.AreEqual(0, CurrencyModel.StatTierReached(i), "전제 — 눈금이 비어 있어야 합니다.");
            }

            // 레벨 해금이 유일한 보유 경로다 — 실제 카탈로그에서 고르려면 레벨이 있어야 한다.
            // ★ 목표 레벨을 손으로 적지 않는다: <b>카탈로그가 요구하는 최대 레벨</b>을 읽어서 거기까지
            //   올린다. 아이템이 늘거나 요구 레벨이 바뀌어도 이 테스트는 저절로 따라간다.
            //   (PlayMode 어셈블리에는 InternalsVisibleTo가 없어 RestoreFromSave를 못 쓴다 —
            //    공개 경로인 AddXp로 올린다. 그게 실제 게임이 레벨을 올리는 방법이기도 하다.)
            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} 씬에 StickmanAgent가 없습니다.");
            RaiseLevelTo(HighestRequiredLevelInStatSlots(), agent.Config);

            // ★ 갈아입기는 <b>프로덕션 경로</b>로 한다. EquipmentModel.TryWear가 스스로
            //   CharacterEquipmentChanged를 발행하므로, 이 아래는 우리가 이벤트를 흉내 내는 것이
            //   아니라 <b>실제로 일어나는 일</b>이다.
            foreach (EquipmentSlot slot in StatSlots)
            {
                int best = BestOwnedItemIndex(slot);
                if (best >= 0) EquipmentModel.TryWear(slot, best, null);
            }
            yield return null;

            StatBuild build = EquipmentStatRules.CurrentBuild();

            int highest = 0;
            for (int i = 0; i < EquipmentStatRules.StatCount; i++)
            {
                int tier = build.TierReached((CharacterStat)i);
                if (tier > highest) highest = tier;
            }

            if (highest <= 0)
            {
                Assert.Ignore($"{LogPrefix} 지금 카탈로그에서 고를 수 있는 최고 조합으로도 임계 1단계에 " +
                    "닿지 않아, 이 실행은 «눈금이 올랐다»를 잴 수 없습니다(0 == 0으로 공허하게 통과할 " +
                    "자리라 일부러 건너뜁니다). 임계값이나 아이템 수치가 바뀌면 이 건너뜀이 사라집니다.");
            }

            for (int i = 0; i < EquipmentStatRules.StatCount; i++)
            {
                Assert.AreEqual(build.TierReached((CharacterStat)i), CurrencyModel.StatTierReached(i),
                    $"{LogPrefix} {EquipmentStatRules.StatName((CharacterStat)i)}의 눈금이 지금 차림과 " +
                    "다릅니다. 0이면 배선이 안 돌았고(정보창이 영구히 0단계로 보인다), 다른 칸의 값이면 " +
                    "스탯 번호와 저장 배열 자리가 어긋난 것입니다.");
            }

            Assert.IsTrue(CurrencyModel.IsDirty,
                $"{LogPrefix} 눈금이 올랐는데 저장 대상으로 표시되지 않았습니다 — 앱을 끄면 사라집니다.");

            // ★ 래칫 — 전부 벗어도 내려가지 않는다(영구 해금). 이것까지 봐야 «기록»이다.
            foreach (EquipmentSlot slot in StatSlots)
            {
                EquipmentModel.TryWear(slot, EquipmentModel.NotWorn, null);
            }
            yield return null;

            for (int i = 0; i < EquipmentStatRules.StatCount; i++)
            {
                Assert.AreEqual(build.TierReached((CharacterStat)i), CurrencyModel.StatTierReached(i),
                    $"{LogPrefix} 장비를 벗었더니 {EquipmentStatRules.StatName((CharacterStat)i)}의 " +
                    "영구 해금이 내려갔습니다 — high-water mark가 아니라 현재값이 되었습니다.");
            }
        }

        /// <summary>스탯 4슬롯의 아이템이 요구하는 <b>가장 높은 레벨</b>. 요구 레벨이 없는 항목
        /// (<c>int.MaxValue</c>로 오는 것 — «행동»처럼 잠기지 않는 것들)은 세지 않는다.</summary>
        private static int HighestRequiredLevelInStatSlots()
        {
            int highest = 1;
            foreach (EquipmentSlot slot in StatSlots)
            {
                int count = EquipmentModel.ItemCount(slot);
                for (int i = 0; i < count; i++)
                {
                    int need = EquipmentModel.RequiredLevel(slot, i);
                    if (need == int.MaxValue) continue;
                    if (need > highest) highest = need;
                }
            }
            return highest;
        }

        /// <summary>공개 경로(<see cref="CharacterProgressionModel.AddXp"/>)로 레벨을 올린다.
        /// 한 번에 «다음 레벨 필요치의 2배»를 넣으므로 매 반복이 최소 한 레벨을 보장한다.</summary>
        private static void RaiseLevelTo(int targetLevel, StickConfig config)
        {
            for (int guard = 0; guard < 128 && CharacterProgressionModel.Level < targetLevel; guard++)
            {
                CharacterProgressionModel.AddXp(CharacterProgressionModel.XpToNextLevel(config) * 2f, config);
            }
            Assert.GreaterOrEqual(CharacterProgressionModel.Level, targetLevel,
                $"{LogPrefix} 레벨을 {targetLevel}까지 올리지 못했습니다(지금 " +
                $"{CharacterProgressionModel.Level}) — 아래 «장비를 걸친다»가 성립하지 않습니다.");
        }

        /// <summary>스탯이 걸리는 4슬롯. <c>EquipmentStatRules.CurrentBuild()</c>가 읽는 것과 같은 넷이다.</summary>
        private static readonly EquipmentSlot[] StatSlots =
        {
            EquipmentSlot.Head, EquipmentSlot.Eyes, EquipmentSlot.Neck, EquipmentSlot.Shoulders,
        };

        /// <summary>이 슬롯에서 <b>보유한</b> 것 중 등급이 가장 높은 자리(없으면 −1).
        /// 아이디를 손으로 적지 않는다 — 카탈로그가 바뀌어도 «가장 센 것»이라는 뜻이 유지된다.</summary>
        private static int BestOwnedItemIndex(EquipmentSlot slot)
        {
            int best = -1;
            int bestRarity = -1;
            int count = EquipmentModel.ItemCount(slot);
            for (int i = 0; i < count; i++)
            {
                if (!EquipmentModel.IsItemOwned(slot, i)) continue;
                int rarity = (int)ItemCatalog.Rarity(slot, i);
                if (rarity <= bestRarity) continue;
                bestRarity = rarity;
                best = i;
            }
            return best;
        }
        // ====================================================================
        // §6. ★★ 「멈췄다」 로그 — 정상 동작을 고장으로 신고하지 않는가
        // ====================================================================
        //
        // ★ 무슨 일이 있었나(2026-09-06 debugger 규명): 정지 판정이 <b>«이번 틱의 지급액이 0인가»</b>
        //   였다. 그런데 요율이 CurrencyRules.IdleCoinsPerMinute(분당 12 = 초당 0.2)라
        //   <b>정상 상태에서도 프레임의 대부분이 0동전</b>이다 — 소수분은 다음 틱으로 넘어간다.
        //   결과: 5초에 한 줄(동전 1개마다 플래그가 풀리고 다음 프레임에 다시 찍힌다).
//   실측(CurrencyRules.IdleTick을 그대로 컴파일해 60fps 1시간): 옛 기준 720줄, 새 기준 0줄.
//   그 720줄은 <b>전부</b> «지급 가능 시간(480분)을 다 썼다»고 적혔다 — 그때 창은 60분 썼다.
        //   그 함수 바로 옆 주석이 «매 프레임 찍지 않는다(24시간 상주 앱)»라고 금지한 바로 그 상황이다.
        //
        // ★ <b>여기가 이 결함을 잡는 유일한 자</b>다. EditMode 소스 스캔은 «어떤 조건으로 찍는가»를
        //   구조로만 볼 수 있고, 모델 단위 테스트는 로그를 보지 않는다. 실제 씬을 벽시계로 돌려
        //   <b>줄 수를 세는</b> 것만이 «정상인데 시끄러운가»를 잰다.
        //
        // ★ 문구를 베끼지 않는다 — 표지와 사유 낱말은 프로덕션 상수를 <b>참조</b>한다
        //   (CharacterProgressionDirector.IdleStallLogMarker 등). 문구를 다듬는 라운드에 이 테스트가
        //   조용히 초록이 되지 않게 하는 유일한 방법이다(CLAUDE.md — 부재 단언용 니들은 썩어도 안 빨개진다).

        private readonly List<string> _stallLines = new List<string>();
        private bool _watchingLogs;

        private void StartWatchingLogs()
        {
            _stallLines.Clear();
            if (_watchingLogs) return;
            Application.logMessageReceived += OnLogMessage;
            _watchingLogs = true;
        }

        private void StopWatchingLogs()
        {
            if (!_watchingLogs) return;
            Application.logMessageReceived -= OnLogMessage;
            _watchingLogs = false;
        }

        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (condition != null
                && condition.IndexOf(CharacterProgressionDirector.IdleStallLogMarker,
                    System.StringComparison.Ordinal) >= 0)
            {
                _stallLines.Add(condition);
            }
        }

        /// <summary>
        /// ★★ <b>정상 수급 중에는 «멈췄다»가 한 줄도 안 나온다.</b>
        /// <para>양성 대조를 같은 실행에 붙인다 — 관측 시간 동안 유휴 버킷이 <b>실제로 늘어야</b>
        /// 이 «0줄»이 «수급이 아예 안 돈다»와 구분된다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator 정상_수급_중에는_정지_로그가_한_줄도_안_찍힌다()
        {
            yield return LoadSceneAndFindDirector();

            // 깨끗한 하루로 되돌린다 — 상한도 창도 남아 있으니 «멈출» 이유가 하나도 없는 상태다.
            CurrencyModel.ResetForTesting();
            Assert.Greater(CurrencyModel.RemainingDailyRoomCoins(), 0, "전제 — 오늘 상한이 남아 있어야 합니다.");
            Assert.Greater(CurrencyModel.RemainingIdleWindowSeconds(), 0.0, "전제 — 창이 남아 있어야 합니다.");

            StartWatchingLogs();
            yield return WaitRealSeconds(IdleProbeSeconds);
            StopWatchingLogs();

            // ── 양성 대조 먼저. 이게 0이면 아래 «정지 로그 0줄»은 계약이 아니라 기능 부재다.
            Assert.Greater(CurrencyModel.TodayGrantedCoins, 0,
                $"{LogPrefix} 관측 시간({IdleProbeSeconds:F0}초) 동안 유휴 수급이 한 푼도 안 들어왔습니다 — " +
                "아래 «정지 로그 0줄»은 «조용하다»가 아니라 <b>아무것도 안 돈다</b>는 뜻이 됩니다.");

            Assert.AreEqual(0, _stallLines.Count,
                $"{LogPrefix} ★ 정상 수급 중인데 «{CharacterProgressionDirector.IdleStallLogMarker}»가 " +
                $"{_stallLines.Count}줄 찍혔습니다(관측 {IdleProbeSeconds:F0}초, 그동안 유휴 " +
                $"{CurrencyModel.TodayGrantedCoins}동전이 실제로 들어왔습니다). 요율이 초당 " +
                $"{CurrencyRules.IdleCoinsPerSecond}동전이라 <b>0동전 프레임은 정상</b>입니다 — " +
                "정지 판정이 지급액이 아니라 «창이 갉혔는가»를 봐야 합니다. " +
                "첫 줄: " + (_stallLines.Count > 0 ? _stallLines[0] : "(없음)"));
        }

        /// <summary>
        /// ★ 진짜로 멈췄을 때는 <b>정확히 한 줄</b>, 그리고 그 줄은 <b>맞는 이유 하나만</b> 말한다.
        /// <para>실제 로그에 «8시간 창을 0으로 리셋했다»와 «480분을 다 썼다»가 11줄 간격으로 함께
        /// 찍힌 적이 있다. 창은 설계상 상한보다 <b>2.3배 넉넉</b>해서(<c>WindowToCeilingRatio</c>)
        /// 거의 도달할 수 없는 쪽인데, 옛 구현이 그것을 <b>기본 분기</b>로 적었기 때문이다.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator 상한에_도달하면_정지_로그가_한_줄만_그리고_상한만_말한다()
        {
            yield return LoadSceneAndFindDirector();

            CurrencyModel.ResetForTesting();

            // 오늘 상한까지 한 번에 채운다. 숫자를 베끼지 않고 상한/요율에서 <b>유도</b>한다 —
            // 상한이나 요율이 바뀌어도 이 테스트는 저절로 따라간다.
            double secondsToCap = CurrencyModel.DailyCapCoins() / CurrencyRules.IdleCoinsPerSecond;
            CurrencyModel.TickIdleIncome(secondsToCap, true, out _);

            Assert.AreEqual(0, CurrencyModel.RemainingDailyRoomCoins(),
                $"{LogPrefix} 상한을 채우지 못했습니다(오늘 유휴 {CurrencyModel.TodayGrantedCoins}/" +
                $"{CurrencyModel.DailyCapCoins()}) — 아래 단언이 재는 것은 «정지»가 아닙니다.");
            Assert.Greater(CurrencyModel.RemainingIdleWindowSeconds(), 0.0,
                $"{LogPrefix} 창까지 함께 소진됐습니다 — 이 실행은 «상한만 말하는가»를 가릴 수 없습니다. " +
                "설계상 창은 상한보다 " +
                $"{CurrencyRules.WindowToCeilingRatio:F1}배 넉넉해야 합니다(WindowToCeilingRatio).");

            StartWatchingLogs();
            yield return WaitRealSeconds(1.0f);   // 수백~수천 프레임. 반복해서 찍히면 여기서 드러난다.
            StopWatchingLogs();

            Assert.AreEqual(1, _stallLines.Count,
                $"{LogPrefix} 정지 로그가 {_stallLines.Count}줄입니다(1이어야 합니다). " +
                "0이면 상한에 걸린 상태가 <b>화면에서 고장과 똑같이 생긴 채</b> 아무 흔적도 안 남고, " +
                "2 이상이면 24시간 상주 앱의 로그를 매 프레임 채웁니다.");

            string line = _stallLines[0];
            StringAssert.Contains(CharacterProgressionDirector.IdleStallCapPhrase, line,
                $"{LogPrefix} 상한에 걸렸는데 그 사실이 로그에 없습니다: {line}");
            Assert.IsFalse(
                line.IndexOf(CharacterProgressionDirector.IdleStallWindowPhrase,
                    System.StringComparison.Ordinal) >= 0,
                $"{LogPrefix} ★ 상한 정지인데 «창을 다 썼다»까지 같이 주장합니다 — 같은 로그가 서로 " +
                $"모순되는 두 이유를 말합니다(남은 창 {CurrencyModel.RemainingIdleWindowSeconds() / 60.0:F0}분): {line}");
            Assert.IsFalse(
                line.IndexOf(CharacterProgressionDirector.IdleStallUnknownPhrase,
                    System.StringComparison.Ordinal) >= 0,
                $"{LogPrefix} 상한 도달을 «원인 불명»으로 적었습니다 — 사유 판정이 정지 판정과 " +
                $"어긋났습니다: {line}");

            TestContext.WriteLine($"{LogPrefix} 정지 로그 1줄 — {line}");
        }
    }
}
