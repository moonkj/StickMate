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
    ///   <item><s>새 캐릭터로 켜면 시드가 실제로 지갑에 들어오는가</s>(§1 — 2026-09-29 폐지).</item>
    ///   <item><s>저장하고 다시 켜면 두 번째 시드가 안 나오는가</s>(§2 — 폐지).</item>
    ///   <item>정중앙 명중 이벤트가 <b>실제로 관문을 통과하는가</b>, 그리고 빗나감·조준 시점·쿨다운이
    ///     <b>실제로 막는가</b>(§3-보안). ★ 2026-09-29 이후 이 관문이 여는 것은 동전이 아니라 <b>XP</b>다.</item>
    ///   <item><s>집중 세션 중 유휴가 멈추고, 끝난 뒤에 그 시간을 몰아주지 않는가</s>(§4 — 폐지).</item>
    ///   <item>장비를 <b>실제로 갈아입어</b> H-8 등급 눈금이 새겨지고, 벗어도 안 내려가는가(§5).</item>
    ///   <item><s>「유휴 수급이 멈췄다」가 정상 동작을 고장으로 신고하지 않는가</s>(§6 — 폐지).</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★★★ 2026-09-29 DLC·재화 폐지 R5 — <b>여섯 절 중 넷을 뗐다</b>
    /// ============================================================================
    /// 뗀 것: §1·§2(첫 실행 시드) · §3의 동전 판(정중앙 명중이 <b>동전</b>을 늘린다) ·
    /// §4(유휴 수급 I-7′ 실주행) · §6(정지 로그 줄 수 세기)와 그 로그 감시 장치.
    /// <b>전부 대상 소멸</b>이다 — 배선이 삭제됐으므로 실주행으로 관측할 것이 없다.
    ///
    /// <para>★ <b>남긴 둘이 더 중요해졌다</b>: §3-보안(활쏘기 관문이 XP를 실제로 막는가)은
    /// 2026-09-07 보안 결함의 회귀 시험이고, 동전이 사라진 뒤에도 <b>그 결함은 그대로 재현 가능</b>하다.
    /// §5(등급 눈금)는 재화와 무관한 축이라 한 줄도 바뀌지 않았다.</para>
    ///
    /// <para>★★ §4·§6이 남긴 <b>실주행 방법론</b>은 기록으로 남긴다(다음 상시 적립 기능에서 필요하다):
    /// ① 시간 예산은 <b>벽시계(초)</b>로 잡는다 — 배치모드 PlayMode는 수천 fps라 프레임 예산이
    /// 밀리초가 된다. ② 「0이어야 한다」는 단언에는 <b>같은 실행의 양성 대조</b>(그 값이 실제로 늘기는
    /// 하는가)를 반드시 붙인다 — 없으면 «계약을 지켰다»와 «기능이 죽었다»가 똑같이 생긴다.
    /// ③ 로그 문구는 베끼지 않고 <b>프로덕션 상수를 참조</b>한다.</para>
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
                "이 컴포넌트가 없으면 활쏘기 XP도 집중 모드 XP도 <b>영원히</b> 지급되지 않습니다.");
        }

        // ====================================================================
        // §1·§2. 첫 실행 시드 — ★★★ 2026-09-29 <b>폐지</b>
        // ====================================================================
        //
        // 뗀 것: <c>새_캐릭터로_켜면_시드가_실제로_지갑에_들어온다</c> ·
        //   <c>저장하고_다시_켜도_시드는_두_번_나오지_않는다</c>.
        //
        // ★ 두 번째가 잡던 <b>실주행 형태</b>는 기록으로 남긴다: 「다시 켜기」를 재현할 때 정적 모델을
        //   <b>일부러 초기화</b>해야 한다 — 그러지 않으면 프로세스에 남은 정적 상태가 «디스크가 말한 것»을
        //   가려서, 저장이 아예 안 되는 구현도 초록이 된다.

        // ====================================================================
        // §3. 활쏘기 — ★ 동전 판 폐지, <b>관문 판(§3-보안)은 유지</b>
        // ====================================================================
        //
        // 뗀 것: <c>정중앙_명중이_실제로_동전을_늘린다</c>. 그 시나리오 다섯 단계
        // (빗나감 · 조준 시점 · 정중앙 지급 · 같은 발 재발행 · 쿨다운)는 아래 §3-보안이
        // <b>XP로 그대로</b> 재생하므로 관문 자체의 실주행 검증은 하나도 잃지 않았다.

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

            // 깨끗한 카운터·깨끗한 XP로 시작한다(이전 테스트 잔여가 증가분을 흐리지 않도록).
            CurrencyModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
            Assert.AreEqual(0, CurrencyModel.ArcheryCoinsToday, "전제 — 관문 누계가 0이어야 증가분을 잰다.");
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

            // ── (다) 정중앙 + Release = 관문 통과 + XP 지급.
            RaiseShot(11, ArcheryShotPhase.Release, ArcheryShotResult.Bullseye);
            yield return null;
            Assert.AreEqual(CurrencyRules.ArcheryCoinsPerAward, CurrencyModel.ArcheryCoinsToday,
                $"{LogPrefix} 전제 — 첫 명중은 관문을 통과해야 아래 XP 대조가 의미를 갖습니다.");
            Assert.AreEqual(0, CurrencyModel.CoinBalance,
                $"{LogPrefix} ★ 활쏘기가 다시 동전을 냈습니다(잔액 {CurrencyModel.CoinBalance}) — " +
                "2026-09-29에 사용자가 닫은 문(재화)을 되열었습니다.");
            Assert.AreEqual(bullseyeXp, CharacterProgressionModel.TotalXpEarned, 0.001f,
                $"{LogPrefix} 첫 정중앙 명중인데 누적 XP가 {CharacterProgressionModel.TotalXpEarned}입니다 " +
                $"(기대 {bullseyeXp}). 0이면 배선이 죽은 것이고, 다른 값이면 지급량이 두 곳에 있습니다.");

            // ── (라) 같은 발 재발행 — 코인도 XP도 다시 늘지 않는다(기존 방어, §3-(라)와 대칭).
            RaiseShot(11, ArcheryShotPhase.Release, ArcheryShotResult.Bullseye);
            yield return null;
            Assert.AreEqual(bullseyeXp, CharacterProgressionModel.TotalXpEarned, 0.001f,
                $"{LogPrefix} 같은 발이 XP를 두 번 줬습니다.");

            // ── (마) ★★ 회귀의 핵심 — 새 발(다른 shotIndex)이 또 정중앙이어도 관문이 쿨다운에
            //    막히면 XP도 함께 막혀야 한다. 수정 전에는 관문은 막혔는데 XP만 계속 나갔다 —
            //    그것이 이 보안 결함의 실체였다.
            RaiseShot(12, ArcheryShotPhase.Release, ArcheryShotResult.Bullseye);
            yield return null;
            Assert.AreEqual(CurrencyRules.ArcheryCoinsPerAward, CurrencyModel.ArcheryCoinsToday,
                $"{LogPrefix} 전제 — 쿨다운({CurrencyRules.ArcheryAwardCooldownSeconds:F0}초) 중이라 " +
                $"관문 누계는 여전히 {CurrencyRules.ArcheryCoinsPerAward}이어야 합니다.");
            Assert.AreEqual(bullseyeXp, CharacterProgressionModel.TotalXpEarned, 0.001f,
                $"{LogPrefix} ★★ 관문은 쿨다운에 막혔는데 누적 XP가 {CharacterProgressionModel.TotalXpEarned}로 " +
                $"늘었습니다(기대 {bullseyeXp}, 변화 없음) — 쿨다운 없는 XP 무한 파밍 결함이 되돌아왔습니다 " +
                "(docs/DESIGN_SYSTEMS_LEVEL_STAT_GROWTH_PROPOSAL.md §3-3, 시간당 ~6,478XP 이론치로 " +
                "Lv50 요구량을 22.4시간에 채울 수 있었던 그 구멍입니다). " +
                "★ 2026-09-29 이후 이 관문은 <b>XP 전용 방어선</b>이라 이 단언의 무게가 더 커졌습니다.");
        }

        // ====================================================================
        // §4. 유휴 수급 I-7′ 실주행 — ★★★ 2026-09-29 <b>폐지</b>
        // ====================================================================
        //
        // 뗀 것: <c>집중_세션_중에는_유휴가_멈추고_끝난_뒤에_몰아주지_않는다</c>와 그 시간 예산 헬퍼
        //   (<c>IdleProbeSeconds</c> · <c>WaitRealSeconds</c>).
        //
        // ★★ <b>이 테스트가 가장 값졌던 이유를 남긴다</b>: I-7′ 파손(같은 시간이 두 축에서 보상됨)은
        //   화면에도 로그에도 흔적을 남기지 않고, <b>정상보다 오히려 후해 보여서</b> 아무도 신고하지
        //   않는다. 잡는 방법은 하나였다 — 실제 세션을 켜고 벽시계로 기다린 뒤 «세션이 끝난 첫 틱에
        //   버킷이 훌쩍 뛰는가»를 보는 것. 두 번째 적립 축을 만드는 라운드는 이 형태를 복제하라.
        //
        // ★ 그리고 관측 대상을 «지갑»이 아니라 «그 축 전용 카운터»로 잡아야 했다 — 지갑은 여러 채널이
        //   함께 쓰는 칸이라, 기다리는 동안 캐릭터가 스스로 활쏘기를 시작하면 잔액이 흔들리고
        //   그 흔들림은 «샜다»와 똑같이 생긴다.

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
        // §6. 「멈췄다」 로그 줄 수 세기 — ★★★ 2026-09-29 <b>절 전체 폐지</b>
        // ====================================================================
        //
        // 뗀 것: 로그 감시 장치(<c>_stallLines</c> · <c>StartWatchingLogs</c> · <c>StopWatchingLogs</c> ·
        //   <c>OnLogMessage</c>)와 테스트 둘(<c>정상_수급_중에는_정지_로그가_한_줄도_안_찍힌다</c> ·
        //   <c>상한에_도달하면_정지_로그가_한_줄만_그리고_상한만_말한다</c>).
        //   정지 로그와 그 표지 상수 4개가 프로덕션에서 삭제됐다(유휴 수급 폐지).
        //
        // ★★ <b>이 절이 잡은 실제 결함과 방법을 남긴다</b>(다음에 「조용히 멈추는 기능」을 만들 때
        //   그대로 필요하다):
        //   ① 결함: 정지 판정이 «이번 틱의 지급액이 0인가»였는데, 요율이 초당 0.2라 <b>정상 상태에서도
        //      프레임의 대부분이 0</b>이었다 ⇒ 5초에 한 줄, 실측 <b>720줄/시간</b>. 게다가 그 720줄이
        //      <b>전부</b> «지급 가능 시간(480분)을 다 썼다»고 적혀 있었다 — 그때 창은 60분 썼다.
        //   ② 방법: 실제 씬을 <b>벽시계로</b> 돌려 <b>줄 수를 센다</b>. 소스 스캔은 «어떤 조건으로
        //      찍는가»만 보고, 모델 테스트는 로그를 아예 안 본다 — 줄 수를 세는 자가 따로 있어야 했다.
        //   ③ 「0줄」 단언에는 <b>같은 실행의 양성 대조</b>를 붙였다(그 사이 값이 실제로 늘었는가).
        //   ④ 문구는 베끼지 않고 프로덕션 상수를 참조했다 — 문구를 다듬는 라운드에 조용히 초록이
        //      되지 않게 하는 유일한 방법이다.
    }
}
