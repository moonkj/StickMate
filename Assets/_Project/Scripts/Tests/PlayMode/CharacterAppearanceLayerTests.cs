using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.States;

namespace StickMate.Tests.PlayMode
{
    /// <summary>
    /// ★ 외형 32종 이식 라운드(2026-08-30)의 <b>런타임</b> 회귀 — docs/UX_FLOW.md 33-2 ~ 33-6.
    ///
    /// ============================================================================
    /// 이 파일이 잡으려는 세 가지 실패
    /// ============================================================================
    /// (A) <b>재구성 서명이 아이템 교체를 놓친다</b>(직전 라운드 데이터 모델 코더가 발견한 함정).
    ///     확장 전 서명은 "카테고리 비트마스크"라 <b>천 모자 -> 왕관</b>처럼 같은 카테고리 안에서
    ///     아이템만 바뀌면 값이 그대로다 -> 도형이 영영 갱신되지 않는다. 화면에는
    ///     "착용은 됐다는데 그림이 그대로"로 나타난다. 실제 씬에서 <b>그려진 선의 이름</b>으로 잠근다.
    ///     네거티브 컨트롤(<see cref="OldCategoryMaskWouldNotSeeTheSwap"/>)이 "옛 방식이었다면 실제로
    ///     못 잡는다"를 같은 파일에서 증명한다.
    /// (B) <b>신규 20종이 배율을 따라가지 않는다</b> — 이 프로젝트가 반복해 온 유형이다. 아이템별로
    ///     잉크 사각형을 재서 배율 1.0/0.75/0.5에 <b>정확히 비례</b>하는지 본다.
    /// (C) <b>신규 컴포넌트 3종이 씬에 두 벌 배치돼 있다</b> — 전역 상태를 읽는 컴포넌트라 두 벌이면
    ///     같은 연출이 두 번 그려진다. 이 프로젝트가 여러 번 사고를 겪은 항목이라 씬에서 직접 센다.
    /// </summary>
    public sealed class CharacterAppearanceLayerTests
    {
        private const float Tol = 1e-4f;
        private const string LogPrefix = "[외형32-TEST]";

        // 배율 1.0 프리팹의 실측 치수(Editor/SceneBootstrapper.cs가 굽는 값 그대로).
        private const float BaseHeight = StickConfig.BaselineCharacterTotalHeight;
        private const float BaseHeadRadius = 0.22f;
        private const float BaseShoulderY = 1.7646944f;
        private const float BaseHipY = 0.9346944f;
        private const float BaseHeadCenterY = BaseHeight - BaseHeadRadius;

        // 카테고리 안의 아이템 자리(0~3). 이 숫자가 카탈로그 표와 어긋나면
        // Tests/EditMode/AccessoryShapeCatalogTests가 아이디 문자열로 먼저 잡는다.
        private const int Cap = 0, Beanie = 1, Fedora = 2, Crown = 3;

        /// <summary>BACK 카테고리의 긴 망토 자리(AccessoryShapeBuilder.BackLongCape와 같은 값).</summary>
        private const int LongCape = 1;

        /// <summary>FX 카테고리의 발자국 자리 / 발자국 원형 버퍼 칸 수(CharacterFxRenderer와 같은 값).</summary>
        private const int FxFootprint = 1;
        private const int FootprintCapacity = 12;

        /// <summary>PET 카테고리의 작은 공 자리 / 스폰 회전 회귀를 보이게 만드는 원점 이탈 거리(월드 유닛).</summary>
        private const int PetBall = 0;
        private const float SpawnProbeOffsetX = 3f;

        /// <summary>긴 망토 자율 발동 간격을 바꿔 볼 때 쓰는 <b>복제본</b>과 원복 정보.
        /// ★ 배포 에셋(Data/DefaultStickConfig.asset)에는 한 비트도 쓰지 않는다 —
        /// Tests/PlayMode/DeployedConfigAssetImmutabilityTests가 잠근 계약이자 CLAUDE.md 원칙 3이다.</summary>
        private StickmanAgent _tripAgent;
        private StickConfig _tripDeployedConfig;
        private StickConfig _tripCloneConfig;

        /// <summary>StickmanAgent의 StickConfig 참조를 복제본으로 갈아끼우기 위한 주입 지점
        /// (SuspendedField와 같은 관례 — 프리팹 직렬화 필드라 공개 세터가 없다).</summary>
        private static readonly FieldInfo AgentConfigField =
            typeof(StickmanAgent).GetField("_config", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>전체화면 감지 주입 — 플랫폼 서비스에 주입 지점이 없어 이 프로젝트가 쓰는 관례.</summary>
        private static readonly FieldInfo SuspendedField =
            typeof(StickmanAgent).GetField("_isSuspended", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly List<GameObject> _rigs = new List<GameObject>(4);

        private StickConfig _inkConfig;

        /// <summary>정리 진입점은 <b>하나로 유지한다</b> — TearDown이 여러 개면 실행 순서가 정의되지 않는다
        /// (CharacterPortraitStageTests가 확립한 이 프로젝트의 관례).</summary>
        [UnityTearDown]
        public IEnumerator TearDownAll()
        {
            for (int i = 0; i < _rigs.Count; i++)
            {
                if (_rigs[i] != null) Object.DestroyImmediate(_rigs[i]);
            }
            _rigs.Clear();

            // 둘 다 정적 클래스라 씬을 다시 로드해도 값이 살아남는다 — 다음 테스트가 이 테스트의
            // 레벨/차림을 물려받으면 결과가 실행 순서에 좌우된다(이 프로젝트의 표준 정리 관례).
            EquipmentModel.ResetForTesting();
            CharacterProgressionModel.ResetForTesting();

            // 잉크색은 ScriptableObject 자산 위에 얹히는 값이라 다음 테스트로 새어 나간다(폴링 주기를
            // 복원하는 FullscreenSuspendUiHidingTests와 같은 관례). ★ 2026-08-31 R5 — 이제 배포 에셋의
            // 직렬화 필드를 저장/복원하는 것이 아니라 **런타임 오버라이드만** 지운다(이 테스트가
            // 배포 에셋을 오염시킬 능력 자체를 없앴다).
            if (_inkConfig != null)
            {
                _inkConfig.ClearRuntimeInkColor();
                _inkConfig = null;
            }

            // ★ 2026-08-31 — 긴 망토 자율 발동 간격을 켜 본 테스트가 있으면 에이전트의 StickConfig
            // 참조를 배포 에셋으로 되돌리고 복제본을 파괴한다. (배포 에셋 자체는 애초에 만지지
            // 않았으므로 되돌릴 값이 없다 — 그것이 이 방식을 쓰는 이유다.)
            if (_tripAgent != null && _tripDeployedConfig != null && AgentConfigField != null)
            {
                AgentConfigField.SetValue(_tripAgent, _tripDeployedConfig);
            }
            if (_tripCloneConfig != null) Object.DestroyImmediate(_tripCloneConfig);
            _tripAgent = null;
            _tripDeployedConfig = null;
            _tripCloneConfig = null;
            yield return null;
        }

        /// <summary>
        /// 긴 망토 자율 발동 간격을 이 테스트 동안만 바꾼다.
        ///
        /// ★ <b>배포 에셋에 쓰지 않는다.</b> <c>agent.Config</c>는 프리팹에 배선된
        /// <c>Data/DefaultStickConfig.asset</c> <b>그 자체</b>이고, 유니티는 플레이 모드 중 애셋에
        /// 가한 변경을 되돌려 주지 않는다(Tests/PlayMode/DeployedConfigAssetImmutabilityTests의
        /// R3 Blocker 2 기록). 여기서 그 필드에 직접 쓰면, 테스트가 중간에 실패해 TearDown이
        /// 건너뛰어졌을 때 <b>사용자가 없애 달라고 한 연출이 켜진 채로 커밋된다</b> — 이 라운드에서
        /// 가장 피해야 할 결과다. 그래서 <b>복제본</b>을 만들어 에이전트에 갈아끼운다.
        /// </summary>
        private void SetCapeTripMeanSeconds(StickmanAgent agent, float seconds)
        {
            Assert.IsNotNull(agent, $"{LogPrefix} StickmanAgent가 없습니다.");
            Assert.IsNotNull(AgentConfigField,
                $"{LogPrefix} StickmanAgent._config 필드를 찾지 못했습니다 — 주입 지점이 사라졌습니다.");

            if (_tripCloneConfig == null)
            {
                _tripAgent = agent;
                _tripDeployedConfig = agent.Config;
                Assert.IsNotNull(_tripDeployedConfig, $"{LogPrefix} StickConfig가 배선돼 있지 않습니다.");
                _tripCloneConfig = Object.Instantiate(_tripDeployedConfig);
                _tripCloneConfig.name = _tripDeployedConfig.name + " (CapeTripClone)";

                // ★ 복제본은 **직렬화 필드만** 복사한다 — 런타임 오버라이드(배율/잉크색)는 딸려오지
                // 않는다. 그대로 갈아끼우면 저장 파일에서 복원된 배율이 조용히 풀려 캐릭터 크기가
                // 테스트 중간에 바뀐다(그 배율 오염이 바로 R3 Blocker 2의 피해 경로였다).
                // 그래서 실효값을 그대로 옮겨 심는다 — 이 교체가 관측 대상 외에는 아무것도 바꾸지
                // 않는다는 것이 이 헬퍼의 계약이다.
                if (_tripDeployedConfig.HasRuntimeCharacterScale)
                {
                    _tripCloneConfig.SetRuntimeCharacterScale(_tripDeployedConfig.ResolveCharacterScale());
                }
                if (_tripDeployedConfig.HasRuntimeInkColor)
                {
                    _tripCloneConfig.SetRuntimeInkColor(_tripDeployedConfig.ResolveInkPreset());
                }

                AgentConfigField.SetValue(agent, _tripCloneConfig);
            }

            _tripCloneConfig.longCapeTripMeanSeconds = seconds;

            Assert.AreEqual(0f, _tripDeployedConfig.longCapeTripMeanSeconds, Tol,
                $"{LogPrefix} 배포 에셋의 longCapeTripMeanSeconds가 오염됐습니다 — " +
                "테스트가 배포 기본값을 바꿔 사용자 요청을 되돌리고 있습니다(CLAUDE.md 원칙 3).");
        }

        /// <summary>StickmanMetrics가 실측하는 소스만 갖춘 최소 리그(CharacterAccessoryScaleTests와 같은 구성).</summary>
        private CharacterAccessoryRenderer Renderer(float scale, float facing)
        {
            var root = new GameObject($"AppearanceRig_{scale:F2}");
            _rigs.Add(root);

            float height = BaseHeight * scale;

            var capsule = root.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.4f * scale, height);
            capsule.offset = new Vector2(0f, height * 0.5f);

            var head = new GameObject("Head");
            head.transform.SetParent(root.transform, false);
            head.transform.localPosition = new Vector3(0f, BaseHeadCenterY * scale, 0f);
            var outline = new GameObject("HeadOutline");
            outline.transform.SetParent(head.transform, false);
            var lr = outline.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.positionCount = 1;
            lr.SetPosition(0, new Vector3(BaseHeadRadius * scale, 0f, 0f));

            var arm = new GameObject("LeftArm");
            arm.transform.SetParent(root.transform, false);
            arm.transform.localPosition = new Vector3(0f, BaseShoulderY * scale, 0f);

            var leg = new GameObject("LeftLeg");
            leg.transform.SetParent(root.transform, false);
            leg.transform.localPosition = new Vector3(0f, BaseHipY * scale, 0f);

            root.AddComponent<StickmanMetrics>();
            var renderer = root.AddComponent<CharacterAccessoryRenderer>();
            renderer.SetFacingForTests(facing);
            return renderer;
        }

        private static readonly EquipmentSlot[] DrawableSlots =
        {
            EquipmentSlot.Head, EquipmentSlot.Eyes, EquipmentSlot.Neck,
            EquipmentSlot.Shoulders, EquipmentSlot.Hair,
        };

        // ============================================================================
        // (B) 배율 비례 — 20종 신규를 포함해 그릴 수 있는 24종 전부
        // ============================================================================

        [TestCase(0.75f)]
        [TestCase(0.5f)]
        public void 모든_아이템의_도형이_배율에_정확히_비례한다(float scale)
        {
            CharacterAccessoryRenderer full = Renderer(1.0f, +1f);
            CharacterAccessoryRenderer scaled = Renderer(scale, +1f);

            for (int s = 0; s < DrawableSlots.Length; s++)
            {
                EquipmentSlot slot = DrawableSlots[s];
                for (int i = 0; i < 4; i++)
                {
                    Assert.IsTrue(full.TryMeasureItemBounds(slot, i, out Vector2 min1, out Vector2 max1),
                        $"{slot} {i}번이 배율 1.0에서 도형을 만들지 않습니다.");
                    Assert.IsTrue(scaled.TryMeasureItemBounds(slot, i, out Vector2 minS, out Vector2 maxS),
                        $"{slot} {i}번이 배율 {scale:F2}에서 도형을 만들지 않습니다.");

                    string what = $"{slot} {i}번({EquipmentModel.ItemName(slot, i)})";
                    AssertScaled(min1.x, scale, minS.x, what, "잉크 좌측 x");
                    AssertScaled(max1.x, scale, maxS.x, what, "잉크 우측 x");
                    AssertScaled(min1.y, scale, minS.y, what, "잉크 하단 y");
                    AssertScaled(max1.y, scale, maxS.y, what, "잉크 상단 y");
                }
            }
        }

        /// <summary>
        /// 배율과 무관하게 참인 절대 조건 — 모자는 머리 위, 안경은 머리 링 안, 넥타이는 목,
        /// 망토 계열은 어깨 아래로 흘러내리고, 머리 모양은 머리 링 <b>바깥</b>에 얹힌다.
        /// </summary>
        [TestCase(1.0f)]
        [TestCase(0.75f)]
        [TestCase(0.5f)]
        public void 신규_아이템이_모든_배율에서_몸의_제자리에_붙어있다(float scale)
        {
            CharacterAccessoryRenderer r = Renderer(scale, +1f);
            StickmanMetrics m = r.GetComponent<StickmanMetrics>();
            float headCenter = m.HeadCenterLocalY;
            float headTop = m.HeadTopLocalY;
            float headBottom = headCenter - m.HeadRadius;
            string label = $"배율 {scale:F2}";

            for (int i = 0; i < 4; i++)
            {
                // 모자 — 관 꼭대기는 정수리 위, 그러나 신장의 15%를 넘게 솟지 않는다.
                Assert.IsTrue(r.TryMeasureItemBounds(EquipmentSlot.Head, i, out Vector2 hmin, out Vector2 hmax));
                Assert.Greater(hmax.y, headTop, $"{label}: 모자 {i}번이 정수리({headTop:F4}) 위로 솟지 않았습니다.");
                Assert.Less(hmax.y, headTop + m.TotalHeight * 0.15f,
                    $"{label}: 모자 {i}번 꼭대기 {hmax.y:F4}가 정수리 위 신장 15%를 넘어 허공에 떴습니다.");
                Assert.Greater(hmin.y, headCenter - m.HeadRadius,
                    $"{label}: 모자 {i}번이 머리 아래까지 내려왔습니다.");

                // 안경 — 머리 링 안(세로), 좌우로도 머리 반경의 1.6배를 넘지 않는다.
                Assert.IsTrue(r.TryMeasureItemBounds(EquipmentSlot.Eyes, i, out Vector2 emin, out Vector2 emax));
                // 고글 스트랩은 머리 링(반경 R·1.02)을 따라 뒤로 도는 반원이라 정수리 언저리까지 올라간다 —
                // 설계 그대로다. 그래서 상한은 "정수리 + R·0.15"이고, 그 위로 가면 모자 자리를 침범한 것이다.
                Assert.Less(emax.y, headTop + m.HeadRadius * 0.15f,
                    $"{label}: 안경 {i}번이 정수리 위로 R·0.15를 넘게 올라갔습니다(모자 자리 침범).");
                Assert.Greater(emin.y, headBottom - m.HeadRadius * 1.2f,
                    $"{label}: 안경 {i}번이 목 아래까지 늘어졌습니다(외알 안경 체인 허용 범위 초과).");
                Assert.Less(Mathf.Max(Mathf.Abs(emin.x), Mathf.Abs(emax.x)), m.HeadRadius * 1.6f,
                    $"{label}: 안경 {i}번이 머리 반경의 1.6배를 넘어 뻗었습니다.");

                // 넥타이 — 위쪽은 머리 아래(=목), 아래쪽은 고관절 위.
                Assert.IsTrue(r.TryMeasureItemBounds(EquipmentSlot.Neck, i, out Vector2 nmin, out Vector2 nmax));
                // 기준은 머리 **중심**이다. 나비넥타이 날개 윗변(ty + R·0.30)은 머리 아래선을 R·0.15
                // 넘어서는데 확장 전부터 그랬고 그림상 문제도 없다 — 머리 중심을 넘으면 그때가 진짜 침범이다.
                Assert.Less(nmax.y, headCenter, $"{label}: 넥타이 {i}번이 얼굴 한가운데까지 올라왔습니다.");
                Assert.Greater(nmin.y, m.HipLocalY - m.TotalHeight * 0.05f,
                    $"{label}: 넥타이 {i}번이 고관절({m.HipLocalY:F4})보다 한참 아래로 내려갔습니다.");

                // 망토 계열 — 옷깃 언저리에서 시작해 진행 반대쪽(x 음수)으로 뻗는다.
                Assert.IsTrue(r.TryMeasureItemBounds(EquipmentSlot.Shoulders, i, out Vector2 bmin, out Vector2 bmax));
                Assert.Less(bmin.x, 0f, $"{label}: 망토/날개/배낭 {i}번이 진행 반대쪽으로 뻗지 않았습니다.");
                // 날개는 어깨 뒤에서 **위·뒤로** 벌어지므로 어깨보다 위로 올라간다(33-2-4 #3 그대로).
                // 정수리를 넘으면 그때가 모자 자리 침범이다.
                Assert.Less(bmax.y, headTop, $"{label}: 등 아이템 {i}번이 정수리 위로 올라갔습니다.");
                // 긴 망토(TorsoLength × 2.10)는 발목 언저리까지 내려온다 — 바닥을 뚫지만 않으면 된다.
                Assert.Greater(bmin.y, 0f, $"{label}: 등 아이템 {i}번 밑단 {bmin.y:F4}이 바닥을 뚫었습니다.");

                // 머리 모양 — 링 **바깥**에 얹는다(링을 덮어 그리면 두 겹 선이 겹쳐 뭉갠다).
                Assert.IsTrue(r.TryMeasureItemBounds(EquipmentSlot.Hair, i, out _, out Vector2 rmax));
                Assert.Greater(rmax.y, headCenter,
                    $"{label}: 머리 모양 {i}번이 머리 중심 위로 올라가지 않았습니다.");
            }
        }

        /// <summary>비대칭 아이템은 방향을 따라 뒤집히고, 좌우 대칭 아이템(왕관 등)은 그대로다.</summary>
        [Test]
        public void 좌우_반전이_모든_아이템에_일관되게_적용된다()
        {
            CharacterAccessoryRenderer right = Renderer(0.75f, +1f);
            CharacterAccessoryRenderer left = Renderer(0.75f, -1f);

            for (int s = 0; s < DrawableSlots.Length; s++)
            {
                EquipmentSlot slot = DrawableSlots[s];
                for (int i = 0; i < 4; i++)
                {
                    right.TryMeasureItemBounds(slot, i, out Vector2 rmin, out Vector2 rmax);
                    left.TryMeasureItemBounds(slot, i, out Vector2 lmin, out Vector2 lmax);

                    string what = $"{slot} {i}번({EquipmentModel.ItemName(slot, i)})";
                    Assert.AreEqual(-rmax.x, lmin.x, Tol, $"{what}: 좌우 반전 시 x 범위가 거울상이 아닙니다.");
                    Assert.AreEqual(-rmin.x, lmax.x, Tol, $"{what}: 좌우 반전 시 x 범위가 거울상이 아닙니다.");
                    Assert.AreEqual(rmin.y, lmin.y, Tol, $"{what}: 좌우 반전인데 y가 함께 움직였습니다.");
                    Assert.AreEqual(rmax.y, lmax.y, Tol, $"{what}: 좌우 반전인데 y가 함께 움직였습니다.");
                }
            }
        }

        // ============================================================================
        // 33-4-1 모자 + 머리 동시 착용
        // ============================================================================

        /// <summary>
        /// ★ 2026-09-01 — 커버 규칙이 <b>"선 통째로 생략" -> "커버선에서 자르기"</b>로 바뀌었다
        /// (docs/UX_FLOW.md 37-7 #1, 리더 승인). 머리카락이 닫힌 채움 도형이 되면서 옛 규칙은
        /// "모자를 쓰면 머리카락이 <b>전부</b> 사라진다"를 뜻하게 됐기 때문이다.
        ///
        /// <para>그래서 이제 조합표가 요구하는 것은 "선 0개"가 아니라 두 가지다:
        /// ① 커버선 위로 잉크가 한 점도 올라가지 않는다 ② 모자 밑으로 삐져나온 옆머리는 <b>남는다</b>.
        /// 실제로 모자를 써도 귀 옆 머리는 보이고, 그게 옛 그림보다 옳다.</para>
        /// </summary>
        [Test]
        public void 모자를_쓰면_머리카락이_커버선_아래로_잘리고_왕관만_예외다()
        {
            CharacterAccessoryRenderer r = Renderer(0.75f, +1f);

            for (int hair = 0; hair < 4; hair++)
            {
                int bare = r.ItemLineCount(EquipmentSlot.Hair, hair);
                Assert.Greater(bare, 0, $"머리 {hair}번이 모자 없이도 아무것도 그리지 않습니다.");

                foreach (int hat in new[] { Cap, Beanie, Fedora })
                {
                    Assert.IsTrue(r.TryMeasureHairTopUnderHat(hair, hat, out float top, out float cover),
                        $"모자 {hat}번을 썼더니 머리 {hair}번이 통째로 사라졌습니다 — " +
                        "옛 '선 통째로 생략' 규칙이 채움 도형에 그대로 적용되면 나는 그림입니다.");
                    Assert.LessOrEqual(top, cover + 1e-4f,
                        $"모자 {hat}번을 썼는데 머리 {hair}번의 잉크가 커버선({cover:F4}) 위 {top:F4}에 남았습니다.");
                }

                Assert.AreEqual(bare, r.HairLineCountUnderHat(hair, Crown),
                    $"왕관을 썼는데 머리 {hair}번이 사라졌습니다 — 왕관은 밑이 뚫려 있어 머리가 함께 보여야 합니다.");
            }

            Assert.IsTrue(float.IsPositiveInfinity(r.HatCoverLocalYFor(Crown)),
                "왕관의 커버선이 +∞가 아닙니다 — 이 예외는 하드코딩 분기가 아니라 데이터여야 합니다.");
        }

        // ============================================================================
        // (A) ★ 재구성 서명 — 실제 씬에서 아이템을 갈아 끼우면 그림이 바뀐다
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator SwappingItemWithinTheSameCategoryActuallyRedrawsTheShape()
        {
            yield return LoadSceneAndPinIdle();

            var renderer = Object.FindFirstObjectByType<CharacterAccessoryRenderer>();
            Assert.IsNotNull(renderer, $"{LogPrefix} 씬에서 CharacterAccessoryRenderer를 찾지 못했습니다.");
            StickConfig agentConfig = Object.FindFirstObjectByType<StickmanAgent>().Config;

            // 왕관은 Lv.20이라 기본 레벨로는 잠겨 있다 — 잠긴 아이템은 애초에 걸칠 수 없으므로(설계 그대로)
            // 이 관측이 성립하려면 레벨을 올려야 한다. 관측 전제를 만들 뿐 규칙을 우회하지 않는다.
            RaiseLevelTo(20, agentConfig);

            // TryWear는 "이미 그걸 쓰고 있으면" false를 돌려준다(변화 없음). 기본 차림이 천 모자라
            // 반환값으로 단언하면 실행 순서에 따라 흔들린다 — **상태**로 단언한다.
            EquipmentModel.TryWear(EquipmentSlot.Head, Cap, null);
            Assert.AreEqual(Cap, EquipmentModel.WornIndex(EquipmentSlot.Head), "천 모자를 걸치지 못했습니다.");
            yield return null;
            yield return null;

            string[] capLines = AccessoryLineNames(renderer);
            CollectionAssert.Contains(capLines, "HatBrim",
                $"{LogPrefix} 천 모자를 썼는데 챙(HatBrim)이 그려지지 않았습니다: [{string.Join(", ", capLines)}]");

            EquipmentModel.TryWear(EquipmentSlot.Head, Crown, null);
            Assert.AreEqual(Crown, EquipmentModel.WornIndex(EquipmentSlot.Head),
                "왕관을 걸치지 못했습니다 — 레벨 20이 되지 않았거나 잠금 규칙이 바뀌었습니다.");
            yield return null;
            yield return null;

            string[] crownLines = AccessoryLineNames(renderer);
            Debug.Log($"{LogPrefix} 천 모자 [{string.Join(", ", capLines)}] -> 왕관 [{string.Join(", ", crownLines)}]");

            CollectionAssert.Contains(crownLines, "CrownBody",
                $"{LogPrefix} ★ 천 모자 -> 왕관 교체가 화면에 반영되지 않았습니다. 재구성 서명이 " +
                "카테고리 비트마스크로 되돌아갔는지 확인하십시오(같은 카테고리 안의 교체를 못 봅니다).");
            CollectionAssert.DoesNotContain(crownLines, "HatBrim",
                $"{LogPrefix} 왕관을 썼는데 천 모자 챙이 남아 있습니다 — 옛 도형이 지워지지 않았습니다.");

        }

        /// <summary>
        /// ★ 네거티브 컨트롤 — "옛 카테고리 비트마스크였다면 이 교체를 <b>실제로 못 본다</b>".
        /// 이 테스트가 실패하면 위 테스트가 통과하는 이유가 "조건이 헐거워서"라는 뜻이다.
        /// </summary>
        [Test]
        public void OldCategoryMaskWouldNotSeeTheSwap()
        {
            RaiseLevelTo(20, null);
            try
            {
                EquipmentModel.TryWear(EquipmentSlot.Head, Cap, null);
                Assert.AreEqual(Cap, EquipmentModel.WornIndex(EquipmentSlot.Head));
                int oldMaskA = LegacyCategoryMask();
                int newSignatureA = EquipmentModel.WornStateSignature;

                EquipmentModel.TryWear(EquipmentSlot.Head, Crown, null);
                Assert.AreEqual(Crown, EquipmentModel.WornIndex(EquipmentSlot.Head));
                int oldMaskB = LegacyCategoryMask();
                int newSignatureB = EquipmentModel.WornStateSignature;

                Assert.AreEqual(oldMaskA, oldMaskB,
                    "옛 카테고리 비트마스크가 천 모자 -> 왕관 교체에서 값이 달라졌습니다 — 그렇다면 애초에 " +
                    "버그가 없었다는 뜻이므로, 위 회귀 테스트는 아무것도 증명하지 못합니다.");
                Assert.AreNotEqual(newSignatureA, newSignatureB,
                    "새 서명(WornStateSignature)이 같은 카테고리 안의 아이템 교체를 구분하지 못합니다 — " +
                    "이 값으로 갈아탄 의미가 없습니다.");

                Debug.Log($"{LogPrefix} (네거티브 컨트롤) 옛 마스크 {oldMaskA} -> {oldMaskB}(동일, 못 잡음) / " +
                    $"새 서명 {newSignatureA} -> {newSignatureB}(다름, 잡음).");
            }
            finally
            {
                CharacterProgressionModel.ResetForTesting();
                EquipmentModel.ResetForTesting();
            }
        }

        /// <summary>확장 전 <c>ComputeSignature()</c>가 쓰던 계산을 그대로 재현한 것(비교 전용).</summary>
        private static int LegacyCategoryMask()
        {
            int mask = 0;
            for (int i = 0; i < EquipmentModel.SlotCount; i++)
            {
                if (EquipmentModel.IsEquipped((EquipmentSlot)i)) mask |= 1 << i;
            }
            return mask;
        }

        /// <summary>
        /// 액세서리 <b>선</b>의 이름 목록. 2026-08-30부터 컨테이너가 한 겹 깊어졌다 —
        /// 머리에 붙는 것(모자/안경/머리카락)은 "HeadAttached" 자식에 들어간다
        /// (유휴 앰비언트 "주위 살피기"가 머리만 좌우로 밀기 때문. CharacterAccessoryRenderer 문서 (3-1)).
        /// 그래서 직속 자식만 훑으면 모자가 통째로 안 보인다 — <b>LineRenderer를 가진 것</b>만 모은다.
        /// </summary>
        /// <summary>
        /// "이 아이템을 걸친 상태로 만든다". <see cref="EquipmentModel.TryWear"/>를 그냥 쓰면 안 된다 —
        /// <b>이미 그것을 걸치고 있으면 false</b>(변화 없음)를 돌려주기 때문이다. PlayMode 테스트는
        /// 실제 저장 파일(<c>Application.persistentDataPath</c>)을 읽는 씬을 띄우므로, 개발 기기에서
        /// 그 아이템을 이미 착용 중이면 <b>준비 조건 단언이 통과하지 못한다</b>(2026-08-30 실측:
        /// 발자국/작은 공이 저장 파일에 걸려 있어 관련 테스트 2건이 기기 상태 때문에 실패했다).
        /// 검사해야 할 것은 "TryWear가 true를 돌려줬는가"가 아니라 <b>지금 그것을 걸치고 있는가</b>다.
        /// </summary>
        private static bool Wear(EquipmentSlot slot, int itemIndex)
        {
            EquipmentModel.TryWear(slot, itemIndex, null);
            return EquipmentModel.WornIndex(slot) == itemIndex;
        }

        private static string[] AccessoryLineNames(CharacterAccessoryRenderer renderer)
        {
            Transform container = renderer.transform.Find("EquipmentAccessories");
            if (container == null) return System.Array.Empty<string>();
            LineRenderer[] lines = container.GetComponentsInChildren<LineRenderer>(true);
            var names = new List<string>(8);
            for (int i = 0; i < lines.Length; i++) names.Add(lines[i].name);
            return names.ToArray();
        }

        // ============================================================================
        // (C) 신규 컴포넌트 3종 — 정확히 1개씩, 콜라이더 0개
        // ============================================================================

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator NewAppearanceComponentsArePlacedOnceAndAddNoColliders()
        {
            yield return LoadSceneAndPinIdle();

            AssertExactlyOne<CharacterFxRenderer>();
            AssertExactlyOne<CharacterPetRenderer>();
            AssertExactlyOne<LongCapeTripDirector>();

            // 펫을 실제로 걸쳐 개체가 생기게 한 뒤 콜라이더가 0개인지 본다(원칙 2·3).
            // 작은 공은 Lv.1부터 보유라 레벨을 만질 필요가 없다.
            EquipmentModel.TryWear(EquipmentSlot.Pet, 0, null);
            Assert.AreEqual(0, EquipmentModel.WornIndex(EquipmentSlot.Pet), "작은 공을 걸치지 못했습니다.");
            yield return null;
            yield return null;

            GameObject petRoot = GameObject.Find("CharacterPet");
            Assert.IsNotNull(petRoot, $"{LogPrefix} 펫을 걸쳤는데 'CharacterPet' 개체가 씬에 생기지 않았습니다.");
            Assert.Greater(petRoot.GetComponentsInChildren<LineRenderer>(true).Length, 0,
                $"{LogPrefix} 펫 개체에 LineRenderer가 0개입니다(빈 껍데기).");
            Assert.AreEqual(0, petRoot.GetComponentsInChildren<Collider2D>(true).Length,
                $"{LogPrefix} 펫이 콜라이더를 만들었습니다 — 그 자리의 다른 앱이 클릭되지 않게 됩니다(원칙 2·3).");
            Assert.AreEqual(0, petRoot.GetComponentsInChildren<Rigidbody2D>(true).Length,
                $"{LogPrefix} 펫이 Rigidbody2D를 만들었습니다 — 펫은 물리 없이 보간만 합니다(33-6-1).");

            // 벗으면 개체가 사라진다(탈출구가 실제로 동작하는가).
            EquipmentModel.TryWear(EquipmentSlot.Pet, EquipmentModel.NotWorn, null);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline && GameObject.Find("CharacterPet") != null) yield return null;
            Assert.IsNull(GameObject.Find("CharacterPet"),
                $"{LogPrefix} 펫을 벗었는데 개체가 화면에 남아 있습니다 — 탈출구가 동작하지 않습니다.");
        }

        // ============================================================================
        // 33-6 펫 작은 공의 스폰 프레임 회전 (R2 m4)
        // ============================================================================

        /// <summary>
        /// ★ 공은 "미끄러지지 않는 구름" <c>θ −= Δx / r</c>로 회전한다. 스폰 프레임에 <c>Δx</c>를
        /// "0 → 캐릭터의 실제 x"로 계산하면 반지름이 신장의 0.055배뿐이라 <b>수천 도</b>가 한 번에
        /// 튄다(R2 m4). 캐릭터를 원점에서 멀리 옮겨 두고 펫을 붙여야 이 회귀가 보인다 —
        /// x가 0 근처면 버그가 있어도 값이 작아 아무것도 관측하지 못한다.
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator PetBallDoesNotSpinThousandsOfDegreesOnItsFirstFrame()
        {
            yield return LoadSceneAndPinIdle();

            var pet = Object.FindFirstObjectByType<CharacterPetRenderer>();
            Assert.IsNotNull(pet, $"{LogPrefix} CharacterPetRenderer가 씬에 없습니다.");

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            StickmanBlackboard bb = agent.Blackboard;

            // 원점에서 충분히 떨어뜨린다 — 옛 코드의 Δx는 곧 이 x값이었다.
            Vector2 home = bb.Body.position;
            bb.Body.position = new Vector2(home.x + SpawnProbeOffsetX, home.y);
            yield return null;

            Assert.IsTrue(Wear(EquipmentSlot.Pet, PetBall),
                $"{LogPrefix} 작은 공을 걸치지 못했습니다 — 관측 전제가 성립하지 않습니다.");
            yield return null; // 이 프레임에 펫이 처음 나타난다.

            Assert.AreEqual(PetBall, pet.ActivePetItemIndex, $"{LogPrefix} 작은 공이 그려지지 않았습니다.");
            Assert.Less(Mathf.Abs(pet.BallSpinDegrees), 360f,
                $"{LogPrefix} 스폰 프레임의 회전각이 {pet.BallSpinDegrees:F0}도입니다 — " +
                "이전 위치가 없는데 굴러온 것처럼 계산했습니다(스포크가 임의 각도로 시작합니다).");

            EquipmentModel.TryWear(EquipmentSlot.Pet, EquipmentModel.NotWorn, null);
            bb.Body.position = home;
            yield return null;
        }

        // ============================================================================
        // 33-5 FX 조각이 잉크색 전환을 따라오는가 (R2 M2)
        // ============================================================================

        /// <summary>
        /// ★ FX 조각은 <b>원형 버퍼로 재사용</b>된다(발자국 12칸). 생성 시점에만 색을 칠하면
        /// 그 GameObject는 앱 수명 내내 옛 색을 쓴다 — 흰 잉크로 바꾼 사용자에게 검은 발자국이
        /// 계속 찍히는 실제 증상이다(R2 M2). 알파만 만지는 <c>SetGroupAlpha</c>로는 못 고친다.
        ///
        /// 그래서 <b>버퍼를 반드시 한 바퀴 넘긴 뒤</b>(13번째부터 재사용) 색을 바꾼다 — 새로 만들어지는
        /// 조각은 원래도 현재 색으로 칠해지므로, 12칸을 채우지 않으면 이 회귀를 아예 관측할 수 없다.
        /// 검사는 플래그가 아니라 실제 <see cref="LineRenderer.startColor"/>다.
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator FxPiecesRepaintThemselvesWhenTheInkColorChanges()
        {
            yield return LoadSceneAndPinIdle();

            var fx = Object.FindFirstObjectByType<CharacterFxRenderer>();
            Assert.IsNotNull(fx, $"{LogPrefix} CharacterFxRenderer가 씬에 없습니다.");

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            _inkConfig = agent.Config;

            RaiseLevelTo(6, agent.Config); // 발자국 요구 레벨
            Assert.IsTrue(Wear(EquipmentSlot.Fx, FxFootprint),
                $"{LogPrefix} 발자국 FX를 걸치지 못했습니다 — 관측 전제가 성립하지 않습니다.");

            SetInk(agent, StickmanInkColor.Black);
            yield return StampFootprints(agent, fx, FootprintCapacity + 4); // 버퍼를 한 바퀴 넘긴다

            Assert.AreEqual(FootprintCapacity, fx.LiveEffectCount,
                $"{LogPrefix} 발자국이 버퍼를 채우지 못했습니다 — 재사용 구간에 들어가지 못해 이 회귀를 볼 수 없습니다.");
            Assert.AreEqual(0, fx.StalePieceCount, $"{LogPrefix} 검정 잉크인데 검정이 아닌 조각이 있습니다.");

            // ---- 여기서 색을 바꾼다. 재사용되는 조각이 따라와야 한다 ----
            SetInk(agent, StickmanInkColor.White);
            Assert.AreNotEqual(0, fx.StalePieceCount,
                $"{LogPrefix} 색을 바꾼 직후인데 옛 색 조각이 0개입니다 — 관측 전제(재사용 대기 중인 조각)가 " +
                "성립하지 않아 이 테스트가 아무것도 검사하지 못합니다.");

            yield return StampFootprints(agent, fx, FootprintCapacity + 4);

            Assert.AreEqual(FootprintCapacity, fx.LiveEffectCount, $"{LogPrefix} 재사용 후 조각 수가 달라졌습니다.");
            Assert.AreEqual(0, fx.StalePieceCount,
                $"{LogPrefix} 잉크색을 바꿨는데 옛 색 그대로인 FX 조각이 남았습니다 — " +
                "흰 잉크 사용자에게 검은 발자국이 계속 찍힙니다(Revive에서 다시 칠하지 않은 것).");

            EquipmentModel.TryWear(EquipmentSlot.Fx, EquipmentModel.NotWorn, null);
            yield return null;
        }

        private void SetInk(StickmanAgent agent, StickmanInkColor ink)
        {
            // 배포 에셋의 직렬화 필드가 아니라 런타임 오버라이드에 쓴다(프로덕션 경로와 동일).
            agent.Config.SetRuntimeInkColor(ink);
            agent.ApplyInkColorFromConfig();
        }

        /// <summary>Walk로 고정하고 몸을 보폭 이상 앞뒤로 옮겨 발자국을 <b>실제 경로로</b> 찍는다
        /// (한 프레임에 하나씩, 확률 요소 없음).</summary>
        private static IEnumerator StampFootprints(StickmanAgent agent, CharacterFxRenderer fx, int count)
        {
            StickmanBlackboard bb = agent.Blackboard;
            Vector2 home = bb.Body.position;
            float stride = StickmanMetrics.Find(fx).TotalHeight * 0.75f; // 보폭 판정 0.30보다 확실히 크게

            for (int i = 0; i < count; i++)
            {
                bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);
                bb.Body.position = new Vector2(home.x + (i % 2 == 0 ? stride : 0f), home.y);
                yield return null;
            }

            bb.Body.position = home;
            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            yield return null;
        }

        // ============================================================================
        // 33-2-5 (B) 긴 망토 넘어짐 — 세기가 실제로 임계값을 넘는가
        // ============================================================================

        /// <summary>
        /// ★ 33절이 제안한 "최약 피격의 0.6배"를 그대로 쓰면 <b>아무 일도 일어나지 않는다</b>는 것을
        /// 실측으로 남긴다(리더 보고용 근거). 가장 약한 기존 피격은 threshold × 1.25이고,
        /// 그 0.6배 = threshold × 0.75 &lt; threshold라 <c>TryApplyImpact</c>가 그냥 false를 돌려준다.
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator LongCapeTripImpulseMustActuallyExceedTheRagdollThreshold()
        {
            yield return LoadSceneAndPinIdle();

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            StickmanBlackboard bb = agent.Blackboard;
            float threshold = agent.Config.ragdollForceThreshold;
            // 당시 가장 약한 기존 피격은 threshold × 1.25(지금은 삭제된 계수)였고,
            // 33절이 제안한 그 0.6배는 threshold × 0.75다. 그 계수의 출처는 사라졌지만
            // **이 테스트가 잡으려는 사실**(임계 미만은 아무 일도 일어나지 않는다)은 그대로다.
            float belowThresholdSample = threshold * 0.75f;

            Assert.Less(belowThresholdSample, threshold,
                $"{LogPrefix} 표본({belowThresholdSample:F2})이 임계값({threshold:F2})을 넘습니다 — " +
                "이 테스트의 전제가 무너졌습니다.");

            // 실제로도 아무 일이 없는지 확인한다(산술만으로 단언하지 않는다).
            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            yield return null;
            Assert.IsFalse(RagdollImpactResolver.TryApplyImpact(bb, belowThresholdSample),
                $"{LogPrefix} threshold 미만 충격량이 RAGDOLL을 일으켰습니다 — 판정이 바뀌었습니다.");
            Assert.AreNotEqual(StickmanStateId.Ragdoll, bb.Machine.CurrentStateId,
                $"{LogPrefix} threshold 미만인데 상태가 RAGDOLL이 됐습니다.");

            // 이 라운드가 실제로 쓰는 값(threshold × 1.02)은 넘어진다.
            Assert.IsTrue(RagdollImpactResolver.TryApplyImpact(bb, threshold * 1.02f),
                $"{LogPrefix} threshold × 1.02가 RAGDOLL을 일으키지 못했습니다 — 긴 망토가 영영 넘어지지 않습니다.");
            Assert.AreEqual(StickmanStateId.Ragdoll, bb.Machine.CurrentStateId);

            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            yield return null;
        }

        /// <summary>
        /// ★ <b>양성 대조</b>(2026-08-30 R2 m6) — 아래 "벗으면 멈춘다" 테스트는 false만 보기 때문에
        /// 기능이 통째로 죽어도(슬롯 상수 오타 하나면 충분하다) 초록으로 남는다. 여기서
        /// <b>실제로 발동 대기가 걸리는지</b>를 먼저 잠그고, 그 위에서 상호배제 3종을 확인한다:
        /// 스펙터클 락 / 전체화면 감지(<see cref="StickmanAgent.IsSuspended"/>) / Idle.
        ///
        /// <c>IsSuspended</c>를 확인하는 이유: Suspend 중에는 <c>ReportExternalImpact</c>가 조용히
        /// 무시되는데 예전에는 <c>TripCount</c>와 "넘어졌습니다" 로그만 늘어났다 —
        /// 일어나지 않은 일을 로그가 주장하는, 원칙 1의 로그 버전이었다(R2 m1).
        /// 주입은 이 프로젝트의 관례대로 리플렉션이다(FullscreenSuspendUiHidingTests와 같은 이유 —
        /// 플랫폼 서비스는 Awake가 스스로 만들어 private 필드에 넣으므로 주입 지점이 없다).
        /// 프레임을 넘기지 않고 한 프레임 안에서 전부 관측한다(에이전트가 스스로 Resume하지 못하게).
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator LongCapeTripIsArmedWhileWalkingAndYieldsToLockSuspendAndIdle()
        {
            yield return LoadSceneAndPinIdle();

            var director = Object.FindFirstObjectByType<LongCapeTripDirector>();
            Assert.IsNotNull(director, $"{LogPrefix} LongCapeTripDirector가 씬에 없습니다.");

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            RaiseLevelTo(13, agent.Config); // 긴 망토 요구 레벨

            Assert.IsTrue(Wear(EquipmentSlot.Shoulders, LongCape),
                $"{LogPrefix} 긴 망토를 걸치지 못했습니다 — 관측 전제가 성립하지 않습니다.");
            Assert.AreEqual(LongCape, EquipmentModel.WornIndex(EquipmentSlot.Shoulders));

            StickmanBlackboard bb = agent.Blackboard;
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            // ---- ★ 2026-08-31: 기본 설정에서는 **꺼져 있어야 한다**(사용자 명시 요청) ----
            // 이 단언이 이 테스트에서 가장 중요하다 — 아래 상호배제 3종은 전부 "끄는 조건"을 보는
            // 것이라, 기능이 기본으로 켜져 있어도 전부 초록이다. 사용자 요청의 본체는 여기다.
            Assert.AreEqual(0f, agent.Config.longCapeTripMeanSeconds, Tol,
                $"{LogPrefix} longCapeTripMeanSeconds 기본값이 0이 아닙니다 — 사용자가 없애 달라고 " +
                "명시 요청한 '걷다가 갑자기 아픈것처럼 쓰러지는' 연출이 다시 켜졌습니다.");
            Assert.IsFalse(director.IsArmed,
                $"{LogPrefix} 기본 설정인데 긴 망토를 걸치고 걷자 발동 대기가 됐습니다 — " +
                "자율 넘어짐이 되살아났습니다(2026-08-31 사용자 요청 위반).");

            // ---- 양성 대조: 값을 켜면 긴 망토 + Walk이면 반드시 발동 대기 ----
            // 기능을 **지운 것이 아니라 껐다**는 것을 증명한다. 이게 없으면 위 IsFalse는
            // "슬롯 상수 오타로 기능이 통째로 죽은 것"과 구별되지 않는다(원래 이 테스트의 취지).
            SetCapeTripMeanSeconds(agent, 90f); // 원래 기본값 (배포 에셋이 아니라 복제본에 쓴다)
            Assert.IsTrue(director.IsArmed,
                $"{LogPrefix} 긴 망토를 걸치고 걷는데 발동 대기가 아닙니다 — 넘어짐 기능이 죽어 있습니다" +
                "(이 단언이 없으면 '벗으면 멈춘다'만으로는 죽은 기능을 못 잡습니다).");

            // ---- ① 스펙터클 락 상호배제 ----
            object owner = new object();
            Assert.IsTrue(SpectacleEventLock.TryAcquire(SpectacleEventKind.Archery, owner),
                $"{LogPrefix} 락을 잡지 못했습니다 — 다른 스펙터클이 점유 중입니다.");
            Assert.IsFalse(director.IsArmed,
                $"{LogPrefix} 스펙터클 진행 중인데 발동 대기입니다 — 연출 도중 자빠집니다.");
            SpectacleEventLock.Release(owner);
            Assert.IsTrue(director.IsArmed, $"{LogPrefix} 락을 풀었는데 발동 대기로 돌아오지 않습니다.");

            // ---- ② 전체화면 감지(원칙 2) ----
            SuspendedField.SetValue(agent, true);
            Assert.IsFalse(director.IsArmed,
                $"{LogPrefix} 전체화면 감지 중인데 발동 대기입니다 — 임펄스는 무시되면서 " +
                "\"넘어졌습니다\" 로그와 TripCount만 늘어납니다.");
            SuspendedField.SetValue(agent, false);
            Assert.IsTrue(director.IsArmed, $"{LogPrefix} 복귀했는데 발동 대기로 돌아오지 않습니다.");

            // ---- ③ 걷지 않으면 걸려 넘어질 자락도 없다 ----
            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            Assert.IsFalse(director.IsArmed, $"{LogPrefix} 서 있는데 발동 대기입니다.");

            EquipmentModel.TryWear(EquipmentSlot.Shoulders, EquipmentModel.NotWorn, null);
            yield return null;
        }

        /// <summary>긴 망토를 벗으면 넘어짐이 즉시 멈춘다(유일한 탈출구가 실제로 성립하는가).</summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator LongCapeTripStopsImmediatelyWhenTheCapeIsRemoved()
        {
            yield return LoadSceneAndPinIdle();

            var director = Object.FindFirstObjectByType<LongCapeTripDirector>();
            Assert.IsNotNull(director, $"{LogPrefix} LongCapeTripDirector가 씬에 없습니다.");

            RaiseLevelTo(13, Object.FindFirstObjectByType<StickmanAgent>().Config); // 긴 망토 요구 레벨

            EquipmentModel.TryWear(EquipmentSlot.Shoulders, EquipmentModel.NotWorn, null);
            yield return null;
            Assert.IsFalse(director.IsArmed, $"{LogPrefix} 망토를 안 걸쳤는데 발동 대기 상태입니다.");

            EquipmentModel.TryWear(EquipmentSlot.Shoulders, 0, null); // 짧은 망토
            Assert.AreEqual(0, EquipmentModel.WornIndex(EquipmentSlot.Shoulders));
            yield return null;
            Assert.IsFalse(director.IsArmed,
                $"{LogPrefix} 짧은 망토인데 발동 대기 상태입니다 — 넘어지는 것은 **긴** 망토뿐입니다.");
        }

        // ============================================================================
        // ★★ 2026-08-31 사용자 신고 회귀 잠금 — "걷다가 갑자기 아픈것처럼 쓰러지는데 이런건 없애줘"
        // ============================================================================

        /// <summary>
        /// <b>장시간 보행 중 자율 RAGDOLL 0회</b>를 실측으로 잠근다.
        ///
        /// 왜 이 테스트가 필요한가: 위 <c>IsArmed</c> 단언은 <b>한 프레임의 게이트 상태</b>만 본다.
        /// 사용자가 겪은 것은 게이트가 아니라 <b>시간이 흐르는 동안 확률이 누적되는 것</b>이었다
        /// (4시간 18분에 48회 = 약 5.4분에 한 번). 그래서 최악의 조건 — 긴 망토를 걸치고, 매 프레임
        /// Walk로 다시 고정하고, 접지시켜 두는 = <b>발동 조건이 100% 충족된 상태</b> — 로
        /// 여러 프레임을 흘려보내며 RAGDOLL이 <b>한 번도</b> 나오지 않는 것을 본다.
        ///
        /// 세 가지를 동시에 본다(하나만 보면 다른 경로로 새는 것을 놓친다):
        ///  ① 상태가 한 프레임도 Ragdoll이 되지 않는다,
        ///  ② <c>LongCapeTripDirector.TripCount</c>가 0이다,
        ///  ③ 사용자가 실제로 본 지표인 <c>CharacterStatsModel.RagdollFalls</c>("넘어진 횟수")가
        ///     늘지 않는다. 사용자 저장 파일에 48이 찍혀 있던 그 칸이다.
        ///
        /// ★ <b>네거티브 컨트롤이 같은 테스트 안에 있다</b>. 이 프로젝트가 반복해서 데인 유형이
        /// "관측 전제가 조용히 깨져서 아무 일도 안 일어난 것을 초록으로 읽는" 것이다 — 예를 들어
        /// 상태 고정에 실패해 Walk가 아니었다면 위 셋은 전부 자동으로 통과한다. 그래서 마지막에
        /// 발동 간격을 아주 짧게 켜고 <b>같은 루프가 실제로 넘어짐을 관측할 수 있는지</b>를 확인한다.
        /// 그게 실패하면 앞의 초록은 의미가 없다.
        /// </summary>
        [UnityTest]
        [Timeout(180000)]
        public IEnumerator LongCapeNeverTripsByItselfWhileWalkingWithDefaultConfig()
        {
            yield return LoadSceneAndPinIdle();

            var director = Object.FindFirstObjectByType<LongCapeTripDirector>();
            Assert.IsNotNull(director, $"{LogPrefix} LongCapeTripDirector가 씬에 없습니다.");

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            RaiseLevelTo(13, agent.Config);
            Assert.IsTrue(Wear(EquipmentSlot.Shoulders, LongCape),
                $"{LogPrefix} 긴 망토를 걸치지 못했습니다 — 관측 전제가 성립하지 않습니다.");

            Assert.AreEqual(0f, agent.Config.longCapeTripMeanSeconds, Tol,
                $"{LogPrefix} 이 테스트는 **기본 설정**의 거동을 봅니다 — 기본값이 0이 아닙니다.");

            StickmanBlackboard bb = agent.Blackboard;
            int fallsBefore = CharacterStatsModel.RagdollFalls;
            int tripsBefore = director.TripCount;

            // 발동 조건이 100% 충족된 상태로 시간을 흘린다. 매 프레임 Walk로 되돌리는 것은
            // 실사용보다 **엄격한** 조건이다(실사용은 걷다 서다를 반복하므로 노출 시간이 더 짧다).
            //
            // ★ 2026-09-01 — 예전에는 `const int Frames = 900;`이었다. 이 디렉터의 발동 확률은
            //   프레임당 <c>p = Time.deltaTime / 평균간격</c>이라 <b>기대 발동 횟수 = 노출 시간 /
            //   평균간격</b>으로 순수하게 시간에 비례한다. 그런데 배치 모드는 0.11~0.45ms/프레임이라
            //   900프레임은 <b>0.099~0.405초</b>였다 — "정상 보행 중"이라고 적어 놓고 실제로는 0.1초를
            //   봤다. 노출을 초로 잡아야 이 관측이 의미를 갖는다.
            const float ExposureSeconds = 3f;
            int ragdollFrames = 0;
            yield return TestClock.SampleForSeconds(ExposureSeconds, _ =>
            {
                if (bb.Machine.CurrentStateId == StickmanStateId.Ragdoll) ragdollFrames++;
                if (bb.Machine.CurrentStateId != StickmanStateId.Walk)
                {
                    bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);
                }
            });

            Assert.AreEqual(0, ragdollFrames,
                $"{LogPrefix} 정상 보행 중 {ExposureSeconds:F1}초 동안 RAGDOLL 프레임이 {ragdollFrames}개 " +
                "관측됐습니다 — 사용자가 신고한 '걷다가 갑자기 쓰러짐'이 그대로 재발한 것입니다.");
            Assert.AreEqual(tripsBefore, director.TripCount,
                $"{LogPrefix} TripCount가 {tripsBefore} -> {director.TripCount}로 늘었습니다 — " +
                "자율 넘어짐이 발동했습니다.");
            Assert.AreEqual(fallsBefore, CharacterStatsModel.RagdollFalls,
                $"{LogPrefix} 기록의 '넘어진 횟수'가 {fallsBefore} -> {CharacterStatsModel.RagdollFalls}로 " +
                "늘었습니다 — 유저가 정보창에서 보는 바로 그 숫자가 저절로 오르고 있습니다.");

            // ---- 네거티브 컨트롤: 이 관측창이 정말 넘어짐을 볼 수 있는가 ----
            //
            // ★★ 2026-09-01 — 여기 적혀 있던 근거가 <b>틀렸다</b>. 예전 주석은 "간격을 0.01초로 두면
            //    p = dt/0.01 > 1이라 조건이 맞는 첫 프레임에 반드시 발동한다"였는데, p > 1이 되려면
            //    dt > 0.01초(= 100fps 미만)여야 한다. 배치 모드는 0.11~0.45ms/프레임이라 실제
            //    p는 0.011~0.045에 불과했고, 예전 예산 240프레임의 미발동 확률은 프레임이 빠른 쪽에서
            //    <b>(1-0.011)^240 = 약 7%</b>였다. 즉 이 네거티브 컨트롤 자체가 20번에 한 번꼴로
            //    까닭 없이 빨개지는 장치였다(전형적인 "프레임 수 = 시간" 오인).
            //
            //    예산을 <b>1초(벽시계)</b>로 잡으면 기대 발동 횟수 = 1초 / 0.01초 = 100회라
            //    미발동 확률이 e^-100(사실상 0)로 떨어진다 — 프레임률이 무엇이든 같다.
            const float NegativeControlSeconds = 1f;
            SetCapeTripMeanSeconds(agent, 0.01f);
            bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);

            bool sawRagdoll = false;
            yield return TestClock.SampleForSeconds(NegativeControlSeconds, _ =>
            {
                if (bb.Machine.CurrentStateId == StickmanStateId.Ragdoll) sawRagdoll = true;
                if (sawRagdoll) return false;   // 찾았으면 그 자리에서 멈춘다.
                if (bb.Machine.CurrentStateId != StickmanStateId.Walk)
                {
                    bb.Machine.ChangeState(StickmanStateId.Walk, isForcedInterrupt: true);
                }
                return true;
            });

            Assert.IsTrue(sawRagdoll,
                $"{LogPrefix} 네거티브 컨트롤이 실패했습니다 — 발동 간격을 0.01초로 켜고 " +
                $"{NegativeControlSeconds:F1}초(기대 발동 100회)를 봤는데도 넘어지지 " +
                "않았습니다. 즉 위의 '0회' 초록은 기능이 꺼진 증거가 아니라 **관측 전제가 깨진** " +
                "증거입니다(예: 상태 고정 실패, 접지 실패, 씬에 디렉터 없음).");
            Assert.Greater(director.TripCount, tripsBefore,
                $"{LogPrefix} 네거티브 컨트롤에서 TripCount가 오르지 않았습니다.");

            bb.Machine.ChangeState(StickmanStateId.Idle, isForcedInterrupt: true);
            yield return null;
        }

        // ============================================================================
        // 공용
        // ============================================================================

        private sealed class StillIntentSource : IMovementIntentSource
        {
            public float MoveInputX => 0f;
            public bool JumpRequested => false;
            public bool LedgeHangRequested => false;
            public bool HopDownRequested => false;
            public bool StepUpRequested => false;
        }

        /// <summary>씬을 로드하고 자율 배회를 정지 소스로 고정한다(이 프로젝트 PlayMode 표준 관례).</summary>
        private IEnumerator LoadSceneAndPinIdle()
        {
            SceneManager.LoadScene("Main", LoadSceneMode.Single);
            yield return null;
            yield return null;

            var agent = Object.FindFirstObjectByType<StickmanAgent>();
            Assert.IsNotNull(agent, $"{LogPrefix} 씬에서 StickmanAgent를 찾지 못했습니다.");
            Assert.IsNotNull(agent.Blackboard, $"{LogPrefix} 블랙보드가 아직 만들어지지 않았습니다.");
            agent.Blackboard.IntentSource = new StillIntentSource();

            const float TimeoutSeconds = 15f;
            const float RequiredStableSeconds = 0.5f;
            float deadline = Time.realtimeSinceStartup + TimeoutSeconds;
            float idleSince = -1f;
            StickmanStateId last = agent.Blackboard.Machine.CurrentStateId;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                last = agent.Blackboard.Machine.CurrentStateId;
                if (last != StickmanStateId.Idle) { idleSince = -1f; continue; }
                if (idleSince < 0f) idleSince = Time.realtimeSinceStartup;
                if (Time.realtimeSinceStartup - idleSince >= RequiredStableSeconds) break;
            }
            Assert.AreEqual(StickmanStateId.Idle, last,
                $"{LogPrefix} 상태가 Idle로 안정되지 않아 관측 전제가 성립하지 않습니다.");
        }

        /// <summary>
        /// 목표 레벨까지 <b>정상 경로(AddXp)</b>로 올린다. <c>CharacterProgressionModel</c>에는 레벨을
        /// 직접 꽂는 공개 API가 없다 — 그게 옳다(레벨은 XP에서만 파생된다는 규칙을 코드가 강제한다).
        /// 테스트는 그 규칙을 우회하지 않고 <b>관측 전제만</b> 만든다. 뒷정리는 ResetForTesting.
        /// </summary>
        private static void RaiseLevelTo(int level, StickConfig config)
        {
            for (int guard = 0; guard < 4096 && CharacterProgressionModel.Level < level; guard++)
            {
                CharacterProgressionModel.AddXp(CharacterProgressionModel.XpToNextLevel(config) + 1f, config);
            }
            Assert.GreaterOrEqual(CharacterProgressionModel.Level, level,
                $"{LogPrefix} 레벨 {level}까지 올리지 못했습니다 — 관측 전제가 성립하지 않습니다.");
        }

        private static void AssertExactlyOne<T>() where T : Object
        {
            var found = Object.FindObjectsByType<T>(FindObjectsSortMode.None);
            Assert.AreEqual(1, found.Length,
                $"{LogPrefix} 씬의 {typeof(T).Name} 개수가 {found.Length}개입니다 — 1개여야 합니다. " +
                "0개면 SceneBootstrapper 배치 누락, 2개 이상이면 씬에 중복 배치된 것입니다.");
        }

        private static void AssertScaled(float expectedAtScale1, float scale, float actual, string what, string axis)
        {
            Assert.AreEqual(expectedAtScale1 * scale, actual, Tol,
                $"{what}의 {axis}가 {actual:F4}입니다 — 배율 1.0 기대값 {expectedAtScale1:F4} × {scale:F2} = " +
                $"{(expectedAtScale1 * scale):F4}가 나와야 합니다. 절대 월드유닛 상수가 남아 있습니다.");
        }
    }
}
