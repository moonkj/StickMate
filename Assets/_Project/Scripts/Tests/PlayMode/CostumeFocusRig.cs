using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 코스튬 × 집중 세션(PART2) PlayMode 계측의 <b>공용 리그</b>.
    /// 설계 정본은 <c>docs/DESIGN_COSTUME_FOCUS_ARCHITECTURE.md</c> 5-5절(L-1~L-5) ·
    /// 6-5절(P-1~P-6) · 9-3절(Phase 판정)이다.
    ///
    /// ============================================================================
    /// 이 파일이 존재하는 이유
    /// ============================================================================
    /// L·P 합격선을 재려면 매번 <b>같은 다섯 가지</b>가 성립해야 한다: 씬 로드 → 오피스 4부위 착용 →
    /// 코스튬 해석 성공 → 세션 시작 → 몰입기 진입. 그 다섯을 각 픽스처가 따로 적으면 한 곳만
    /// 낡아도 <b>다른 것을 재면서 초록</b>이 된다 — 이 저장소가 반복해서 당한 형태다.
    ///
    /// ============================================================================
    /// ★★ 하드코딩하지 않는 것 (CLAUDE.md 「테스트에 프로덕션 상수·식별자를 베끼지 않는다」)
    /// ============================================================================
    /// <list type="bullet">
    ///   <item><b>아이템 id 4개를 적지 않는다</b> — 스탯 4슬롯에서 <see cref="ItemCatalog.ThemeOffice"/>와
    ///     같은 테마를 가진 항목을 <b>찾아서</b> 쓴다. 번호가 재배치돼도, 이름이 바뀌어도 낡지 않는다.</item>
    ///   <item><b>요구 레벨(9/6/8/22)을 적지 않는다</b> — 고른 넷의 <see cref="EquipmentModel.RequiredLevel"/>
    ///     최댓값까지 <b>실제 성장 경로</b>(<c>AddXp</c>)로 올린다.
    ///     ★ <c>EquipmentDebugUnlock</c>은 <b>건드리지 않는다</b>: 에디터에서는 이미 켜져 있지만
    ///     그 스위치에 기대면 «릴리스에서 닫히는 경로»와 다른 것을 재게 되고, 같은 날 보안 라운드가
    ///     그 스위치의 감사를 정밀화했다. 레벨 경로는 출하 경로 그 자체다.</item>
    ///   <item><b>코스튬 키 문자열을 적지 않는다</b> — <see cref="CostumeCatalog.FindByBaseTheme"/>가
    ///     돌려준 서술자의 <c>CostumeKey</c>를 기대값으로 쓴다(에셋이 골든이다).</item>
    ///   <item><b>프롭 조각 수(4 또는 10)를 적지 않는다</b> — <see cref="ExpectedBuildPointWrites"/>가
    ///     매니페스트 에셋에서 <b>독립으로</b> 센다. 프로덕션의 <c>ResolveStageShapes</c>를 부르지
    ///     않는 것이 핵심이다(기대값을 프로덕션 함수로 만들면 둘이 같이 틀려도 아무도 모른다 —
    ///     <c>docs/TEAM.md</c> 「생성기와 검사기가 같이 틀린다」).</item>
    /// </list>
    ///
    /// ============================================================================
    /// ★ 알려진 배선 결함 1건 — 이 리그가 <b>보상</b>하고 있다 (2026-09-08 test-engineer 실측)
    /// ============================================================================
    /// <c>Interaction/CostumePropRenderer</c>가 <b>출하 프리팹
    /// (<c>Assets/_Project/Prefabs/Stickman.prefab</c>)에 붙어 있지 않다</b>. 형제 렌더러
    /// (<c>GraffitiRenderer</c>·<c>ArcheryRenderer</c>·<c>RopeClimbRenderer</c>·<c>FocusWatchDirector</c>)는
    /// 전부 붙어 있다. 그래서 <b>지금 이 트리를 빌드하면 코스튬 연출 전체가 조용히 죽는다</b>
    /// (프롭이 안 뜨고 → <c>IsCostumeImmersionActive</c>가 영원히 거짓 → LFVS가 한 프레임도 안 돈다).
    ///
    /// <para>이 리그는 없으면 <see cref="EnsureCostumeProp"/>에서 <b>붙이고 경고를 남긴다</b> —
    /// 그렇게 하지 않으면 L·P 계측을 <b>한 줄도</b> 할 수 없기 때문이다. 대신 그 회차의 숫자가
    /// «출하 배선 위에서 잰 값»이 아니라는 사실이 러너 로그에 <b>반드시</b> 남는다.
    /// 배선 자체의 빨간불은 <c>Tests/EditMode/BootstrapPrefabParityAuditTests</c>가 이미 낸다
    /// (부트스트래퍼 소스에는 <c>root.AddComponent&lt;CostumePropRenderer&gt;()</c>가 있고 프리팹에는 없다)
    /// — 여기서 두 번째 빨간불을 만들지 않는 이유는 같은 사실을 두 곳에서 신고하면 고친 뒤에
    /// 한쪽이 남기 때문이다.</para>
    /// </summary>
    public static class CostumeFocusRig
    {
        public const string LogPrefix = "[코스튬계측]";

        /// <summary>씬 로드 후 캐릭터가 <c>Idle</c>로 안정되기를 기다리는 <b>벽시계</b> 상한(초).</summary>
        public const float SettleBudgetSeconds = 20f;

        /// <summary>세션 다이얼에 넣는 분. 디렉터 하한(60초)에 걸려 실제 길이는 60초다 —
        /// 그 60초를 <see cref="TimeCompression"/>으로 압축한다.</summary>
        public const float SessionMinutes = 1f;

        /// <summary>스탯 4슬롯. <see cref="CostumeResolver"/>가 보는 <b>바로 그 집합</b>이고,
        /// 여기서 새로 정의하는 것이 아니라 «외형 슬롯이 아닌 것»으로 판정한다면 두 곳이 갈라진다 —
        /// 그래서 <see cref="EquipmentModel.IsAppearanceSlot"/>로 <b>파생</b>시킨다.</summary>
        public static IReadOnlyList<EquipmentSlot> StatSlots()
        {
            var slots = new List<EquipmentSlot>(4);
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                var slot = (EquipmentSlot)i;
                if (EquipmentModel.IsAppearanceSlot(slot)) continue;
                if (EquipmentModel.IsRetiredSlot(slot)) continue;
                slots.Add(slot);
            }
            Assert.AreEqual(4, slots.Count,
                $"{LogPrefix} 스탯 슬롯이 {slots.Count}개입니다 — CostumeResolver가 보는 집합은 4개입니다. " +
                "슬롯 구성이 바뀌었다면 이 리그와 CostumeResolver.StatSlots가 함께 갈라졌는지 먼저 보십시오.");
            return slots;
        }

        // ====================================================================
        // 씬 · 캐릭터
        // ====================================================================

        /// <summary>씬을 싣고 캐릭터가 <c>Idle</c>로 안정될 때까지 <b>벽시계</b>로 기다린다.
        /// <para>★ <c>IntentSource</c>를 갈아끼우지 <b>않는다</b>: 이 라운드가 재려는 것 중 하나가
        /// «몰입기에는 배회 확률이 0이라 캐릭터가 안 걷는다»(<c>AutoWanderController</c>)이고,
        /// 의도 소스를 정지 더미로 바꾸면 그 사실을 <b>테스트가 대신 만들어</b> 아무것도 증명하지 못한다.</para></summary>
        public static IEnumerator LoadSceneAndSettle()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            StickmanAgent agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} 씬에 StickmanAgent가 없습니다.");
            Assert.IsNotNull(agent.Blackboard, $"{LogPrefix} 블랙보드가 없습니다.");
            Assert.IsNotNull(agent.Config, $"{LogPrefix} StickConfig가 없습니다.");

            FocusWatchDirector director = Object.FindFirstObjectByType<FocusWatchDirector>();
            Assert.IsNotNull(director, $"{LogPrefix} 씬에 FocusWatchDirector가 없습니다.");
            if (director.IsSessionActive) director.StopFocusSession();

            // 낙하 → 착지 → Idle. 프레임 수가 아니라 벽시계로 기다린다(배치모드 2,000fps 규약).
            yield return TestClock.WaitUntil(
                () => agent.Blackboard.Machine != null
                      && agent.Blackboard.Machine.CurrentStateId == StickmanStateId.Idle
                      && agent.Blackboard.SenseGround().Grounded,
                SettleBudgetSeconds,
                "캐릭터가 바닥에 서서 Idle로 안정되기");
        }

        public static StickmanAgent Agent()
        {
            StickmanAgent agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} StickmanAgent를 찾지 못했습니다.");
            return agent;
        }

        public static FocusWatchDirector Director()
        {
            FocusWatchDirector d = Object.FindFirstObjectByType<FocusWatchDirector>();
            Assert.IsNotNull(d, $"{LogPrefix} FocusWatchDirector를 찾지 못했습니다.");
            return d;
        }

        /// <summary>
        /// 캐릭터의 <see cref="CostumePropRenderer"/>. <b>없으면 붙이고 크게 경고한다</b>
        /// (클래스 문서의 「알려진 배선 결함 1건」).
        /// </summary>
        public static CostumePropRenderer EnsureCostumeProp(StickmanAgent agent)
        {
            CostumePropRenderer prop = agent.GetComponent<CostumePropRenderer>();
            if (prop != null)
            {
                Debug.Log($"{LogPrefix} 프롭 렌더러가 출하 프리팹에 이미 붙어 있습니다 — " +
                    "이 회차의 숫자는 출하 배선 위에서 잰 값입니다.");
                return prop;
            }

            Debug.LogWarning($"{LogPrefix} ★ 배선 결함 보상 — 출하 프리팹(Stickman.prefab)에 " +
                "CostumePropRenderer가 없어서 테스트가 런타임에 붙였습니다. " +
                "형제 렌더러(GraffitiRenderer/ArcheryRenderer/RopeClimbRenderer/FocusWatchDirector)는 전부 " +
                "프리팹에 있습니다. 이 상태로 빌드하면 코스튬 연출 전체가 «코드는 있는데 실행되지 않는» " +
                "형태로 출하됩니다(프롭 미배치 → IsCostumeImmersionActive 영구 거짓 → LFVS 0프레임). " +
                "고치는 법: SceneBootstrapper.EnsurePrefabComponents 목록에 EnsureComponent<CostumePropRenderer>(root)를 " +
                "더한 뒤 그 메뉴를 실행하고 프리팹을 커밋하십시오 — 지금은 BuildStickmanPrefab(전체 재생성) " +
                "경로에만 있어서, 프리팹 파일이 이미 있으면(BUG-SW-M3 조기 반환) 영원히 안 붙습니다. " +
                "★ 아래 계측 숫자는 «렌더러가 돌 때 어떻게 동작하는가»이지 «출하물이 도는가»가 아닙니다.");
            return agent.gameObject.AddComponent<CostumePropRenderer>();
        }

        // ====================================================================
        // 오피스 코스튬 착용
        // ====================================================================

        /// <summary>이 슬롯에서 <paramref name="theme"/> 테마를 가진 <b>첫</b> 아이템의 번호.
        /// 없으면 −1(호출부가 존재 단언으로 붙잡는다).</summary>
        public static int FirstItemWithTheme(EquipmentSlot slot, string theme)
        {
            int count = ItemCatalog.ItemCountIn(slot);
            for (int i = 0; i < count; i++)
            {
                ItemCatalogEntry e = ItemCatalog.Item(slot, i);
                if (e != null && string.Equals(e.Theme, theme, System.StringComparison.Ordinal)) return i;
            }
            return -1;
        }

        /// <summary>
        /// 오피스 4부위를 실제로 걸치고 <see cref="CostumeResolver"/>가 그 코스튬을 <b>해석하는지</b>까지
        /// 확인한다. 반환값은 «화면에 뜰 코스튬»의 서술자다.
        /// </summary>
        public static CostumeDescriptor WearOfficeCostume(StickConfig config)
        {
            CostumeDescriptor expected = CostumeCatalog.FindByBaseTheme(ItemCatalog.ThemeOffice);
            Assert.IsNotNull(expected,
                $"{LogPrefix} 기본 코호트 테마 '{ItemCatalog.ThemeOffice}'의 코스튬 매니페스트를 못 찾았습니다 " +
                $"(실린 코스튬 {CostumeCatalog.Count}개). Resources/Items 아래 CostumeManifest 에셋이 " +
                "빠졌거나 CostumeCatalog가 그것을 거부했습니다(거부 사유는 [코스튬] 접두 로그에 있습니다).");

            IReadOnlyList<EquipmentSlot> slots = StatSlots();

            // ---- 1. 넷을 고르고, 그 넷의 요구 레벨 최댓값을 구한다(숫자를 베끼지 않는다) ----
            var picked = new int[slots.Count];
            int needLevel = 1;
            for (int i = 0; i < slots.Count; i++)
            {
                int index = FirstItemWithTheme(slots[i], ItemCatalog.ThemeOffice);
                Assert.GreaterOrEqual(index, 0,
                    $"{LogPrefix} {slots[i]} 슬롯에 테마 '{ItemCatalog.ThemeOffice}' 아이템이 하나도 없습니다 — " +
                    "오피스 세트가 스탯 4슬롯을 다 못 채우면 CostumeResolver가 구조적으로 null을 냅니다.");
                picked[i] = index;
                int required = EquipmentModel.RequiredLevel(slots[i], index);
                if (required != int.MaxValue && required > needLevel) needLevel = required;
            }

            // ---- 2. 그 레벨까지 실제 성장 경로로 올린다 ----
            for (int guard = 0; guard < 8192 && CharacterProgressionModel.Level < needLevel; guard++)
            {
                CharacterProgressionModel.AddXp(CharacterProgressionModel.XpToNextLevel(config) + 1f, config);
            }
            Assert.GreaterOrEqual(CharacterProgressionModel.Level, needLevel,
                $"{LogPrefix} 레벨을 {needLevel}까지 못 올렸습니다(지금 {CharacterProgressionModel.Level}). " +
                "레벨 상한이 요구 레벨보다 낮아졌다면 이 코스튬은 아무도 못 입습니다 — 그건 계측 문제가 아니라 결함입니다.");

            // ---- 3. 먼저 전부 벗고(다른 픽스처의 잔재 제거) 넷을 건다 ----
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                EquipmentModel.TryWear((EquipmentSlot)i, EquipmentModel.NotWorn, config);
            }
            for (int i = 0; i < slots.Count; i++)
            {
                EquipmentModel.TryWear(slots[i], picked[i], config);
                Assert.AreEqual(picked[i], EquipmentModel.WornIndex(slots[i]),
                    $"{LogPrefix} {slots[i]}에 {picked[i]}번을 걸치지 못했습니다 " +
                    $"(요구 레벨 {EquipmentModel.RequiredLevel(slots[i], picked[i])}, 지금 Lv.{CharacterProgressionModel.Level}).");
            }

            // ---- 4. 해석기가 실제로 그 코스튬을 내는가 ----
            string resolved = CostumeResolver.ResolveKey();
            Assert.AreEqual(expected.CostumeKey, resolved,
                $"{LogPrefix} 오피스 4부위를 걸쳤는데 CostumeResolver가 '{resolved ?? "없음"}'을 냈습니다.\n" +
                DescribeLoadout(slots));

            Debug.Log($"{LogPrefix} 오피스 착용 완료 — Lv.{CharacterProgressionModel.Level}(요구 최대 {needLevel}), " +
                $"코스튬 '{expected.CostumeKey}', 키포즈표 {(expected.Keyposes != null ? "있음" : "없음")}, " +
                $"기본 조각 {expected.PropShapes.Count}개 / 단계 오버라이드 {expected.StageShapes.Count}개.");
            return expected;
        }

        /// <summary>실패 메시지에 넣을 «지금 네 자리가 어떤 상태인가» — 어느 조건에서 걸렸는지를
        /// 한 번에 보여준다(Worn / Theme / Cohort 셋 중 무엇이 틀어졌는지 눈으로 갈린다).</summary>
        public static string DescribeLoadout(IReadOnlyList<EquipmentSlot> slots)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < slots.Count; i++)
            {
                EquipmentSlot slot = slots[i];
                StatSlotLoadout lo = EquipmentStatRules.SlotLoadout(slot);
                int worn = EquipmentModel.WornIndex(slot);
                // ★ 코호트(ItemCatalogEntry.CohortId)는 internal이라 PlayMode 어셈블리가 볼 수 없다
                //   (AssemblyInfo의 InternalsVisibleTo는 EditMode 하나뿐). 대신 아이템 id를 적는다 —
                //   코호트 불일치로 해석이 죽으면 그 id를 보고 EditMode 감사로 넘기면 된다.
                sb.Append("    · ").Append(slot).Append(": worn=").Append(worn)
                  .Append(" 착용판정=").Append(lo.Worn)
                  .Append(" 테마='").Append(lo.Theme ?? "null").Append('\'')
                  .Append(" id='").Append(worn >= 0 ? EquipmentModel.ItemId(slot, worn) : "-").Append('\'')
                  .Append(" 요구Lv=").Append(worn >= 0 ? EquipmentModel.RequiredLevel(slot, worn).ToString() : "-")
                  .Append('\n');
            }
            sb.Append("    (지금 Lv.").Append(CharacterProgressionModel.Level).Append(')');
            return sb.ToString();
        }

        // ====================================================================
        // 기대 점 쓰기 수 — ★ 에셋에서 독립으로 센다
        // ====================================================================

        /// <summary>이 단계에서 그릴 조각. <b>프로덕션의 <c>ResolveStageShapes</c>를 부르지 않는다</b> —
        /// 같은 함수를 쓰면 그 함수가 틀어지는 날 기대값도 함께 틀어져 아무것도 못 잰다
        /// (<c>docs/TEAM.md</c> 「기대값을 프로덕션 함수로 만들지 마라」).</summary>
        public static IReadOnlyList<AccessoryWornShapeData> StageShapesOf(CostumeDescriptor costume, int stage)
        {
            IReadOnlyList<CostumeStageOverride> overrides = costume.StageShapes;
            for (int i = 0; i < overrides.Count; i++)
            {
                CostumeStageOverride o = overrides[i];
                if (o.stage == stage && o.shapes != null && o.shapes.Length > 0) return o.shapes;
            }
            return costume.PropShapes;
        }

        /// <summary>
        /// 빌드 프레임에 <c>LineRenderer</c> 점 배열이 <b>몇 번</b> 쓰일 것인가.
        /// 규칙은 렌더러와 같다: 채운 도형이면 채움 선 1개 + (획을 끄지 않았으면) 외곽선 1개.
        /// </summary>
        public static int ExpectedBuildPointWrites(CostumeDescriptor costume, int stage)
        {
            IReadOnlyList<AccessoryWornShapeData> shapes = StageShapesOf(costume, stage);
            int writes = 0;
            for (int i = 0; i < shapes.Count; i++)
            {
                if (shapes[i].filled) writes++;
                if (!shapes[i].noStroke) writes++;
            }
            return writes;
        }

        /// <summary>지금 세이브 기준의 진화 단계(새 세이브에서는 0).</summary>
        public static int CurrentStageOf(CostumeDescriptor costume)
            => CostumeEvolutionRules.StageOf(CostumeProgressModel.MinutesOf(costume.CostumeKey));

        // ====================================================================
        // 세션 · 몰입기
        // ====================================================================

        /// <summary>몰입기의 <b>벽시계</b> 길이(초) — 세션 시간을 <paramref name="timeCompression"/>배로
        /// 압축했을 때. 예산을 프레임이 아니라 여기서 파생시킨다.</summary>
        public static float ImmersionWallSeconds(float sessionDurationSeconds, float timeCompression)
            => FocusSessionPhases.ImmersionSeconds(sessionDurationSeconds) / Mathf.Max(0.0001f, timeCompression);

        /// <summary>적응기 + 몰입기 도달까지의 <b>벽시계</b> 상한(초). 여유 3배.</summary>
        public static float ImmersionArrivalBudget(float sessionDurationSeconds, float timeCompression)
            => Mathf.Max(5f, 3f * FocusSessionPhases.EdgeSeconds(sessionDurationSeconds)
                             / Mathf.Max(0.0001f, timeCompression));

        /// <summary>
        /// 세션을 시작하고 <b>몰입기 + 프롭 배치</b>까지 간다. 둘 다 성립해야 코스튬 층이 산다
        /// (<c>PropPlaced</c>가 P-GHOST-1의 그 값이다).
        /// </summary>
        public static IEnumerator StartAndReachImmersion(FocusWatchDirector director,
            CostumePropRenderer prop, float timeCompression)
        {
            // ★ 세션을 걸기 전에 «서 있고 땅에 붙어 있는» 상태를 만든다. 레벨업 연출·낙하가 끼면
            //   구간 경계 프레임에 공중에 있을 수 있고, 그러면 프롭 빌드가 «바닥 없음»으로 조용히
            //   건너뛰어진다(설계 6-3 ①이 접지를 전제한다).
            StickmanAgent agent = Agent();
            yield return WaitForGroundedIdle(agent, SettleBudgetSeconds);

            director.StartFocusSession(SessionMinutes);
            Assert.AreEqual(FocusSessionPhase.Adapt, director.CurrentPhase,
                $"{LogPrefix} 세션 시작 직후는 적응기여야 합니다.");

            float budget = ImmersionArrivalBudget(director.SessionDurationSeconds, timeCompression);
            yield return TestClock.WaitUntil(() => director.CurrentPhase == FocusSessionPhase.Immersion,
                budget, $"몰입기 진입(세션 {director.SessionDurationSeconds:F0}초 × 압축 {timeCompression:F0}배)");

            // 프롭은 몰입기 진입을 알아챈 LateUpdate에서 지어진다 — 다음 프레임이면 충분하지만,
            // 폴백 사다리가 F2/F3를 거치는 경우까지 덮도록 짧은 벽시계 예산을 준다.
            yield return TestClock.WaitUntil(() => prop.PropPlaced, 2f,
                "프롭 배치(PropPlaced). 거짓으로 남으면 폴백 F4(자리 없음)이거나 코스튬 해석이 null입니다 " +
                "— 그 경우 [코스튬프롭] 로그에 사유가 한 줄 남습니다");
        }

        /// <summary>
        /// 캐릭터가 <b>땅에 붙은 채 Idle</b>이 될 때까지 벽시계로 기다린다.
        /// <para>코스튬 층(<c>TickFocusWatchStance</c>)은 <c>Idle</c>에서만 돌고, 프롭 빌드는 접지를
        /// 요구한다. 두 조건이 아직 아닌 프레임을 표본에 넣으면 «계기가 안 돌았다»를
        /// «계기가 0이다»로 잘못 읽는다.</para>
        /// </summary>
        public static IEnumerator WaitForGroundedIdle(StickmanAgent agent, float budgetSeconds)
        {
            yield return TestClock.WaitUntil(
                () => agent.Blackboard != null && agent.Blackboard.Machine != null
                      && agent.Blackboard.Machine.CurrentStateId == StickmanStateId.Idle
                      && agent.Blackboard.SenseGround().Grounded,
                budgetSeconds,
                "캐릭터가 땅에 선 채 Idle이 되기");
        }

        /// <summary>세션·전역 모델·시간 배율을 원래대로. 모든 픽스처의 <c>[UnityTearDown]</c>이 부른다.</summary>
        public static void RestoreGlobals(float savedTimeScale)
        {
            if (savedTimeScale > 0f) Time.timeScale = savedTimeScale;

            FocusWatchDirector director = Object.FindFirstObjectByType<FocusWatchDirector>();
            if (director != null && director.IsSessionActive) director.StopFocusSession();

            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();
        }
    }
}
