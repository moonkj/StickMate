using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using StickMate.Core;
using StickMate.Platform;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 2026-09-03 라운드의 회귀 잠금 — <b>두 가지</b>를 잠근다.
    ///
    /// <list type="number">
    ///   <item><b>마디 병합</b>(리더 판정 CH-8 채택) — 팔다리 하나 = 선 하나. 몸의 선 11 → 7.
    ///     조형이 안 바뀐다는 것이 채택 근거이므로, 여기서 잠그는 것은 "합쳐졌다"가 아니라
    ///     <b>"합쳐졌는데 모양이 그대로다"</b>이다.</item>
    ///   <item><b>하한 계약을 잉크 코어 기준으로</b> — 그리고 <b>게이트도 함께</b>.
    ///     ★ 이쪽이 더 위험하다. 오늘은 막이 0이라 옳은 식과 금지 형태가 <b>같은 그림</b>을 내고,
    ///     그래서 막을 켜는 라운드가 순서를 검사하지 않는다. 그 순서를 <b>지금</b> 못박는다.</item>
    /// </list>
    ///
    /// <para><b>기대값의 출처</b>(TEAM.md — 기대값을 프로덕션 함수로 만들지 마라):
    /// 병합 쪽은 <b>프리팹의 디스크 비트</b>와 <b>테스트가 직접 적은 역변환</b>에서 오고,
    /// 하한 쪽은 <b>배포 상수(StickConfig)로부터의 독립 계산</b>에서 온다.</para>
    /// </summary>
    public sealed class LimbMergeInkFloorTests
    {
        private const string LogPrefix = "[병합·잉크하한-TEST]";
        private const string PrefabAssetPath = "Assets/_Project/Prefabs/Stickman.prefab";

        private static GameObject LoadPrefab()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAssetPath);
            Assert.IsNotNull(prefab, $"{LogPrefix} 프리팹을 찾지 못했습니다: {PrefabAssetPath}");
            return prefab;
        }

        // ====================================================================
        // (1) 마디 병합 — 개수
        // ====================================================================

        /// <summary>몸의 선이 <b>7개</b>다. 숫자를 베끼지 않고 <b>구성으로 유도</b>한다:
        /// 머리 2(채움 + 링) + 몸통 1 + 팔다리 4 = 7.</summary>
        [Test]
        public void 몸의_선이_팔다리당_하나로_합쳐져_일곱_개다()
        {
            GameObject prefab = LoadPrefab();
            string[] limbs = { "LeftLeg", "RightLeg", "LeftArm", "RightArm" };

            int total = 0, limbLines = 0, lowerLines = 0;
            foreach (LineRenderer lr in prefab.GetComponentsInChildren<LineRenderer>(true))
            {
                total++;
                if (Array.IndexOf(limbs, lr.gameObject.name) >= 0) limbLines++;
                foreach (string n in limbs)
                    if (lr.gameObject.name == n + "Lower") lowerLines++;
            }

            Assert.AreEqual(0, lowerLines,
                $"{LogPrefix} ★ 아래 마디에 선이 {lowerLines}개 남아 있습니다 — 마디 병합 이전 프리팹이거나 " +
                "합치고 나서 아래를 안 지운 중간 상태입니다. 그 상태는 <b>같은 그림을 두 번</b> 그립니다.");
            Assert.AreEqual(limbs.Length, limbLines,
                $"{LogPrefix} 팔다리 선이 {limbLines}개입니다 — 팔다리 하나에 정확히 하나여야 합니다.");

            // 7 = 머리 채움 1 + 머리 링 1 + 몸통 1 + 팔다리 4. 숫자가 아니라 이 합으로 적는다.
            const int headLines = 2, torsoLines = 1;
            Assert.AreEqual(headLines + torsoLines + limbs.Length, total,
                $"{LogPrefix} 몸의 LineRenderer가 {total}개입니다(기대 {headLines + torsoLines + limbs.Length}개 = " +
                $"머리 {headLines} + 몸통 {torsoLines} + 팔다리 {limbs.Length}). " +
                "이 수를 세는 문서·계측이 여럿 있으니 바뀌면 그쪽도 함께 갱신해야 합니다.");

            Debug.Log($"{LogPrefix} 몸의 선 {total}개 — 팔다리 {limbLines}개(각 " +
                $"{LimbCurveRenderer.PolylinePointCount}점) · 아래 마디 선 {lowerLines}개.");
        }

        // ====================================================================
        // (2) 마디 병합 — <b>조형이 안 바뀐다</b>
        // ====================================================================

        /// <summary>
        /// ★ design-character R9 §14-2의 실측을 <b>프로덕션 좌표로</b> 재현한다:
        /// 병합 폴리라인의 <b>관절 꼭짓점 회전각이 다른 다섯 꼭짓점과 구별되지 않는다</b>.
        /// 그것이 "병합은 새 코너를 만들지 않는다"의 기하학적 내용 전부다.
        ///
        /// <para>왜 이 단언인가: 코너가 새로 생겼다면 <b>그 한 꼭짓점만</b> 회전각이 튄다.
        /// 프리팹 그림을 눈으로 보는 것보다 이쪽이 훨씬 민감하다.</para>
        /// </summary>
        [Test]
        public void 병합_폴리라인의_관절_꼭짓점이_다른_꼭짓점과_구별되지_않는다()
        {
            GameObject prefab = LoadPrefab();
            foreach (string name in new[] { "LeftLeg", "RightLeg", "LeftArm", "RightArm" })
            {
                Transform upper = prefab.transform.Find(name);
                Assert.IsNotNull(upper, $"{LogPrefix} 프리팹에 '{name}'이 없습니다.");
                var lr = upper.GetComponent<LineRenderer>();
                Assert.AreEqual(LimbCurveRenderer.PolylinePointCount, lr.positionCount);

                var turns = new float[lr.positionCount - 2];
                for (int i = 1; i < lr.positionCount - 1; i++)
                {
                    Vector3 a = lr.GetPosition(i - 1), b = lr.GetPosition(i), c = lr.GetPosition(i + 1);
                    Vector2 u = new Vector2(b.x - a.x, b.y - a.y);
                    Vector2 v = new Vector2(c.x - b.x, c.y - b.y);
                    if (u.sqrMagnitude < 1e-14f || v.sqrMagnitude < 1e-14f)
                        Assert.Fail($"{LogPrefix} '{name}'의 꼭짓점 {i} 주변에 <b>길이 0인 선분</b>이 있습니다 — " +
                            "관절 칸을 두 번 담았다는 뜻이고, 두꺼운 폴리라인의 퇴화 선분은 2026-09-01 '발' 실패와 " +
                            "같은 계열입니다(코너 조인 자기교차).");
                    turns[i - 1] = Mathf.Abs(Vector2.SignedAngle(u, v));
                }

                // 관절 꼭짓점의 회전각이 나머지 다섯의 <b>최대치를 넘지 않는다</b>.
                // (직선 구간의 회전각 0은 필렛 밖이므로 비교에서 뺀다.)
                int jointVertex = LimbCurveRenderer.PolylineJointIndex - 1;   // turns 배열 기준
                float jointTurn = turns[jointVertex];
                float maxOther = 0f;
                for (int i = 0; i < turns.Length; i++)
                    if (i != jointVertex && turns[i] > maxOther) maxOther = turns[i];

                Assert.LessOrEqual(jointTurn, maxOther + 1e-3f,
                    $"{LogPrefix} ★ '{name}'의 관절 꼭짓점 회전각 {jointTurn:F4}도가 나머지 최대 " +
                    $"{maxOther:F4}도를 넘습니다 — 병합이 <b>새 코너를 만들었습니다</b>. " +
                    "design-character R9 §14-2의 채택 근거가 무너진 것이므로 병합을 되돌려야 합니다. " +
                    $"(전체: {string.Join(" / ", Array.ConvertAll(turns, t => t.ToString("F3")))})");

                Debug.Log($"{LogPrefix} {name} 꼭짓점 회전각(도) — " +
                    $"{string.Join(" / ", Array.ConvertAll(turns, t => t.ToString("F3")))} " +
                    $"[관절 = 인덱스 {jointVertex}]");
            }
        }

        /// <summary>옛 세대 프리팹/리그처럼 아래 마디에 선이 <b>남아 있으면</b> 렌더러가 그것을 끈다.
        /// 안 끄면 같은 그림을 두 번 그린다(획이 겹쳐 굵어 보이고 선 수도 11개로 남는다).</summary>
        [Test]
        public void 옛_세대의_아래마디_선은_꺼진다()
        {
            var root = new GameObject("LegacyRig");
            try
            {
                LineRenderer legacyLower = null;
                foreach (string name in new[] { "LeftLeg", "RightLeg", "LeftArm", "RightArm" })
                {
                    var upper = new GameObject(name);
                    upper.transform.SetParent(root.transform, false);
                    MakeLine(upper, 0.5f, 0.08f);

                    var lower = new GameObject(name + "Lower");
                    lower.transform.SetParent(upper.transform, false);
                    lower.transform.localPosition = new Vector3(0f, -0.5f, 0f);
                    lower.transform.localRotation = Quaternion.Euler(0f, 0f, 30f);
                    LineRenderer ll = MakeLine(lower, 0.45f, 0.08f);
                    if (legacyLower == null) legacyLower = ll;
                }

                Assert.IsTrue(legacyLower.enabled,
                    $"{LogPrefix} 양성 대조 실패 — 리그를 만든 직후부터 아래 마디 선이 꺼져 있습니다. " +
                    "그러면 아래 단언은 아무것도 증명하지 못합니다.");

                root.AddComponent<LimbCurveRenderer>().BakeEditorPreview();

                Assert.IsFalse(legacyLower.enabled,
                    $"{LogPrefix} ★ 옛 세대의 아래 마디 선이 켜진 채로 남았습니다 — " +
                    "병합 폴리라인과 <b>같은 그림을 두 번</b> 그립니다.");
                Debug.Log($"{LogPrefix} 옛 세대 아래 마디 선 — 굽기 후 enabled=False 확인.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static LineRenderer MakeLine(GameObject go, float length, float width)
        {
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.positionCount = 2;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, new Vector3(0f, -length, 0f));
            return lr;
        }

        // ====================================================================
        // (3) 하한 계약 — <b>순서</b>
        // ====================================================================

        /// <summary>★ <b>되돌릴 문</b>: 막이 0이면 오늘과 <b>비트 동일</b>이다.
        /// 부동소수 근사가 아니라 비트로 비교한다 — <c>+ 0f</c>조차 하지 않는다는 계약이다.</summary>
        [Test]
        public void 막이_0이면_그려진_두께는_잉크_코어와_비트_동일이다()
        {
            float[] samples =
            {
                0f, 1e-8f, 0.0488798f, 0.078375f, 0.09405f, 0.165f, 1f, 12345.6789f,
                StickConfig.MinStrokeScreenPoints / StickConfig.ReferencePointsPerWorldUnitApprox,
                StickConfig.MinFillOutlineScreenPoints / StickConfig.ReferencePointsPerWorldUnitApprox,
            };

            foreach (float ink in samples)
            {
                foreach (int sides in new[] { 0, InkMembraneStroke.FillBoundarySides, InkMembraneStroke.StandaloneSides })
                {
                    float drawn = InkMembraneStroke.Drawn(ink, 0f, sides);
                    Assert.AreEqual(BitConverter.SingleToInt32Bits(ink), BitConverter.SingleToInt32Bits(drawn),
                        $"{LogPrefix} ★ 막 0 · 겹 {sides}에서 그려진 두께가 잉크 코어와 비트 동일이 아닙니다 " +
                        $"({ink:R} → {drawn:R}). 이 라운드가 「오늘과 같은 그림」이라고 말할 근거가 사라집니다.");
                    Assert.AreEqual(BitConverter.SingleToInt32Bits(ink),
                        BitConverter.SingleToInt32Bits(InkMembraneStroke.InkCore(drawn, 0f, sides)),
                        $"{LogPrefix} 되빼기도 비트 동일이어야 합니다.");
                }
            }

            Assert.AreEqual(0f, InkMembraneStroke.MembranePhysicalPixels,
                $"{LogPrefix} 막 두께가 0이 아닙니다({InkMembraneStroke.MembranePhysicalPixels}) — " +
                "막을 켜는 판정(game-architect 경유 CH-9)이 끝났다면 이 테스트를 <b>켜진 값 기준</b>으로 " +
                "다시 써야 합니다. 그대로 두면 위 비트 동일 단언이 막의 존재를 가립니다.");
        }

        /// <summary>막이 걸린 상태에서 <see cref="InkMembraneStroke.Drawn"/> ↔
        /// <see cref="InkMembraneStroke.InkCore"/>가 정확한 역이다(게이트가 되빼는 근거).</summary>
        [Test]
        public void 막을_더하고_되빼면_잉크_코어로_돌아온다()
        {
            foreach (float ink in new[] { 0.0488798f, 0.078375f, 0.165f })
            foreach (float m in new[] { 0.001f, 0.0122f, 0.05f })
            foreach (int sides in new[] { InkMembraneStroke.FillBoundarySides, InkMembraneStroke.StandaloneSides })
            {
                float drawn = InkMembraneStroke.Drawn(ink, m, sides);
                Assert.Greater(drawn, ink, $"{LogPrefix} 막이 두께를 늘리지 않았습니다.");
                Assert.AreEqual(ink, InkMembraneStroke.InkCore(drawn, m, sides), 1e-6f,
                    $"{LogPrefix} 되빼기가 잉크 코어로 돌아오지 않습니다(ink={ink} m={m} sides={sides}).");
            }
        }

        /// <summary>
        /// ★★ <b>네거티브 컨트롤 — 금지 형태가 실제로 파국을 낸다.</b>
        /// 리더 지시의 그 식(<c>Max(baked×ratio + sides×막, floor)</c>)을 직접 계산해
        /// <b>Windows 100%에서 잉크 코어가 하한을 못 지킨다</b>는 것을 보인다.
        ///
        /// <para>★ <b>자기 정정(이 테스트를 쓰다가 내가 틀렸다)</b>: 처음에 "금지 형태 = 잉크 0px"라고
        /// 한 줄로 적었는데 <b>거짓</b>이다. 금지 형태의 잉크는
        /// <c>max(비례, 하한 − 겹수×막)</c>이라 비례 두께가 남아 있으면 0이 아니다
        /// (실측: 배율 0.35에서 팔 1.289px / 다리 1.547px — 하한 2.00px 대비 <b>결손 0.45~0.71px</b>).
        /// <b>정확히 0이 되는 곳은 따로 있다</b> — 액세서리/펫의 <b>되올리기</b> 경로다.
        /// 거기서는 그려진 두께가 <c>하한 그 자체</c>로 고정되므로
        /// <c>잉크 = 하한 − 겹수×막 = 2 − 2×1 = 0.000px</c>다. <c>perf-doc</c>의 T6가 잰 것이 이 경로다.
        /// 두 경로를 <b>따로</b> 잰다 — 한 숫자로 뭉뚱그리면 어느 쪽이 고쳐졌는지 알 수 없다.</para>
        /// </summary>
        [Test]
        public void 금지_형태는_Windows100퍼센트에서_잉크가_하한을_못_지키게_한다()
        {
            // Windows 표시배율 100% ⇒ 1 OS 포인트 = 1 물리픽셀. 이 축이 파국의 유일한 무대다.
            const float windowsHundredPercentPointsPerPixel = 1f;
            float floorPx = StickConfig.MinStrokeScreenPoints / windowsHundredPercentPointsPerPixel;

            // 막 설계값(design-art) 1 물리픽셀 — 프로덕션 상수는 아직 0이므로 여기서는 <b>가정값</b>으로 잰다.
            //   0을 쓰면 금지 형태와 옳은 식이 같아져 네거티브 컨트롤이 성립하지 않는다.
            const float designMembranePx = 1f;
            const int sides = InkMembraneStroke.StandaloneSides;

            // ── (가) 되올리기 경로(액세서리/펫 안전망) — 그려진 두께가 <b>하한 그 자체</b>다 ──
            float raiseForbiddenInk = floorPx - sides * designMembranePx;   // 옛 코드: startWidth = floor
            float raiseCorrectDrawn = InkMembraneStroke.Drawn(floorPx, designMembranePx, sides);
            float raiseCorrectInk = InkMembraneStroke.InkCore(raiseCorrectDrawn, designMembranePx, sides);

            Assert.LessOrEqual(raiseForbiddenInk, 0f,
                $"{LogPrefix} ★ 되올리기 경로의 잉크 코어가 {raiseForbiddenInk:F3}px입니다 — 0 이하가 아니라면 " +
                $"하한({StickConfig.MinStrokeScreenPoints}pt)이나 막 설계값이 바뀐 것이고, " +
                "그렇다면 「잉크 소멸」 판정 자체를 다시 해야 합니다.");
            Assert.AreEqual(floorPx, raiseCorrectInk, 1e-5f,
                $"{LogPrefix} 옳은 순서에서는 되올린 뒤에도 잉크 코어가 정확히 하한이어야 합니다.");

            // ── (나) 구워진 선 경로 — 비례 두께가 하한에 눌리는 전 구간에서 <b>하한 미달</b>이 된다 ──
            //    비례 두께를 하한의 0 / 25 / 50 / 90%로 훑는다(배율 0.35~0.509 구간이 여기에 해당한다).
            foreach (float fraction in new[] { 0f, 0.25f, 0.5f, 0.9f })
            {
                float proportionalPx = fraction * floorPx;
                float forbiddenDrawn = Mathf.Max(proportionalPx + sides * designMembranePx, floorPx);
                float forbiddenInk = forbiddenDrawn - sides * designMembranePx;

                float correctInk = Mathf.Max(proportionalPx, floorPx);
                float correctDrawn = InkMembraneStroke.Drawn(correctInk, designMembranePx, sides);

                Assert.Less(forbiddenInk, floorPx - 1e-4f,
                    $"{LogPrefix} ★ 비례 {fraction:P0}에서 금지 형태의 잉크가 {forbiddenInk:F3}px로 " +
                    $"하한 {floorPx:F2}px를 지켜 버립니다 — 그렇다면 이 라운드의 전제가 틀렸으니 다시 재십시오.");
                Assert.AreEqual(floorPx, correctInk, 1e-5f,
                    $"{LogPrefix} 옳은 순서에서는 잉크 코어가 정확히 하한이어야 합니다(비례 {fraction:P0}).");
                Assert.AreEqual(floorPx + sides * designMembranePx, correctDrawn, 1e-5f,
                    $"{LogPrefix} 옳은 순서의 그려진 두께는 하한 + 겹수×막이어야 합니다(비례 {fraction:P0}).");

                Debug.Log($"{LogPrefix} 구워진 선 · 비례 {proportionalPx:F3}px — " +
                    $"금지 잉크 {forbiddenInk:F3}px / 옳은 잉크 {correctInk:F3}px " +
                    $"(결손 {correctInk - forbiddenInk:F3}px).");
            }

            // ★ 양성 대조 — 하한이 안 물리는 구간(비례 > 하한)에서는 두 형태가 <b>같다</b>.
            //   그래서 이 결함은 "낮은 배율 전용"이고, 그것이 macOS Retina에서 안 보이는 이유이기도 하다.
            float highProportional = 3f * floorPx;
            float highForbiddenInk = Mathf.Max(highProportional + sides * designMembranePx, floorPx)
                                     - sides * designMembranePx;
            Assert.AreEqual(highProportional, highForbiddenInk, 1e-5f,
                $"{LogPrefix} 대조 실패 — 하한이 안 물리는 구간에서도 두 형태가 다릅니다. " +
                "그렇다면 이 결함의 범위 판정(낮은 배율 전용)이 틀린 것입니다.");

            Debug.Log($"{LogPrefix} Windows 100% · 하한 {floorPx:F2}px · 막 {designMembranePx:F2}px×{sides} — " +
                $"되올리기 경로 금지 잉크 {raiseForbiddenInk:F3}px(= 소멸) / 옳은 순서 {raiseCorrectInk:F3}px. " +
                $"★ 파국은 Windows 100% 전용이다 — Retina에서는 같은 하한이 {floorPx * 2f:F2} 물리픽셀이라 " +
                $"잉크가 {floorPx * 2f - sides * designMembranePx:F2}px 남는다.");
        }

        /// <summary>
        /// ★ 프로덕션 본문이 <b>그 순서로</b> 적혀 있는가. 위 계산은 산술이 옳다는 것만 말하고,
        /// 에이전트가 실제로 그 순서를 쓰는지는 말하지 않는다 — 그 틈이 이 저장소의 단골 사고다.
        /// </summary>
        [Test]
        public void 에이전트_본문이_하한을_먼저_적용하고_막을_나중에_더한다()
        {
            string body = ApplyStrokeWidthsBody();

            StringAssert.Contains("Mathf.Max(_bakedStrokeWidths", body,
                $"{LogPrefix} 하한 되올리기(Mathf.Max)를 찾지 못했습니다 — 잘라낸 범위가 틀렸거나 " +
                "하한이 사라졌습니다.");
            StringAssert.Contains(nameof(InkMembraneStroke) + "." + nameof(InkMembraneStroke.Drawn), body,
                $"{LogPrefix} ★ 막을 더하는 단일 창구를 부르지 않습니다 — 막을 인라인으로 더하면 " +
                "게이트가 그 사실을 알 수 없습니다.");

            int max = body.IndexOf("Mathf.Max(_bakedStrokeWidths", StringComparison.Ordinal);
            int drawn = body.IndexOf(nameof(InkMembraneStroke) + "." + nameof(InkMembraneStroke.Drawn),
                StringComparison.Ordinal);
            Assert.Less(max, drawn,
                $"{LogPrefix} ★ 막을 더하는 줄이 하한보다 <b>앞</b>에 있습니다 — 그것이 금지 형태입니다. " +
                "Windows 100% 최저 배율에서 잉크 코어가 0px가 되고, 그런데도 게이트는 「하한 지킴」을 찍습니다.");

            // 금지 형태의 문자 그대로의 흔적 — 하한 안에 막이 들어간 Max.
            Assert.IsFalse(body.Contains("Mathf.Max(_bakedStrokeWidths[i] * ratio + "),
                $"{LogPrefix} ★ 하한 계산 <b>안에</b> 막이 더해져 있습니다(금지 형태).");

            Debug.Log($"{LogPrefix} 에이전트 본문 — Mathf.Max(위치 {max}) → InkMembraneStroke.Drawn(위치 {drawn}). 순서 확인.");
        }

        /// <summary>ApplyStrokeWidthsForScale의 본문만 잘라낸다(중괄호 균형).
        /// 파일 전체를 훑으면 문서 주석의 인용문만으로도 통과하는 거짓 초록이 된다.</summary>
        private static string ApplyStrokeWidthsBody()
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts", "Core", "StickmanAgent.cs");
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 소스를 찾지 못했습니다: {path}");
            string src = File.ReadAllText(path);
            int sig = src.IndexOf("private void ApplyStrokeWidthsForScale(", StringComparison.Ordinal);
            Assert.Greater(sig, 0, $"{LogPrefix} ApplyStrokeWidthsForScale 정의를 찾지 못했습니다.");
            int open = src.IndexOf('{', sig);
            int depth = 0;
            for (int i = open; i < src.Length; i++)
            {
                if (src[i] == '{') depth++;
                else if (src[i] == '}' && --depth == 0) return src.Substring(open, i - open + 1);
            }
            Assert.Fail($"{LogPrefix} 본문 끝을 찾지 못했습니다.");
            return string.Empty;
        }

        // ====================================================================
        // (4) 게이트 — <b>잉크가 0px인데 「하한 지킴」</b>을 못 찍게 한다
        // ====================================================================

        /// <summary>
        /// ★★ 리더가 지목한 그 구멍. <see cref="StrokeWidthDiagnostics.Report"/>를 <b>Windows 100% ·
        /// 배율 0.35의 숫자로 직접 조립</b>해서, 옛 판정(그려진 두께 비교)이 <b>참</b>이었을 상황에서
        /// 새 판정이 <b>거짓</b>을 내는지 본다.
        ///
        /// <para>왜 Report를 손으로 조립하는가: 막 상수가 오늘 0이라 씬을 만들어서는 이 상황을
        /// <b>재현할 수 없다</b>. 재현 불가를 이유로 안 재면, 막을 켜는 날 아무도 이 구멍을 못 본다.</para>
        /// </summary>
        [Test]
        public void 게이트는_그려진_두께가_아니라_잉크_코어로_하한을_판정한다()
        {
            const float membranePx = 1f;                    // design-art 설계값(물리픽셀)
            const int sides = InkMembraneStroke.StandaloneSides;
            float floorPt = StickConfig.MinStrokeScreenPoints;
            float fillFloorPt = StickConfig.MinFillOutlineScreenPoints;

            // Windows 100%: 1pt = 1 물리픽셀. 그려진 두께가 정확히 하한이면 잉크는 0이다.
            float drawnPt = floorPt;
            float inkPt = drawnPt - sides * membranePx;
            Assert.LessOrEqual(inkPt, 0f, $"{LogPrefix} 전제 확인 — 이 배치에서 잉크는 0 이하여야 합니다.");

            var broken = new StrokeWidthDiagnostics.Report(
                lineCount: 7, fillOutlineCount: 1, pixelsPerWorldUnit: 40.9167f,
                minPixels: drawnPt, maxPixels: drawnPt,
                minPoints: drawnPt, maxPoints: drawnPt,
                minLineInkCorePoints: inkPt, minFillOutlineInkCorePoints: fillFloorPt,
                minLineDrawnPoints: drawnPt, minFillOutlineDrawnPoints: fillFloorPt + membranePx,
                membranePixels: membranePx, membraneLineCount: 7,
                floorPoints: floorPt, fillOutlineFloorPoints: fillFloorPt);

            Assert.IsFalse(broken.FloorHonored,
                $"{LogPrefix} ★★ 잉크 코어가 {inkPt:F2}pt인데 게이트가 「하한 지킴」을 돌려줍니다. " +
                "이것이 이 저장소의 서명 사고입니다 — 실패한 측정과 성공한 측정이 똑같이 생겼습니다.");

            // 네거티브 컨트롤 — 옛 판정(그려진 두께 비교)이었다면 <b>참</b>이었다.
            Assert.GreaterOrEqual(broken.MinLineDrawnPoints, floorPt - 0.01f,
                $"{LogPrefix} 네거티브 컨트롤 실패 — 그려진 두께조차 하한 미달이면 옛 게이트도 " +
                "빨갛게 났을 것이고, 이 테스트가 잡으려는 <b>조용한</b> 형태가 아닙니다.");

            // 양성 대조 — 잉크가 하한을 지키면 통과해야 한다(항상 빨간 게이트는 게이트가 아니다).
            var healthy = new StrokeWidthDiagnostics.Report(
                lineCount: 7, fillOutlineCount: 1, pixelsPerWorldUnit: 40.9167f,
                minPixels: floorPt + sides * membranePx, maxPixels: floorPt + sides * membranePx,
                minPoints: floorPt + sides * membranePx, maxPoints: floorPt + sides * membranePx,
                minLineInkCorePoints: floorPt, minFillOutlineInkCorePoints: fillFloorPt,
                minLineDrawnPoints: floorPt + sides * membranePx,
                minFillOutlineDrawnPoints: fillFloorPt + membranePx,
                membranePixels: membranePx, membraneLineCount: 7,
                floorPoints: floorPt, fillOutlineFloorPoints: fillFloorPt);
            Assert.IsTrue(healthy.FloorHonored,
                $"{LogPrefix} 양성 대조 실패 — 잉크가 하한을 지키는데 결함으로 읽힙니다.");

            StringAssert.Contains("잉크", StrokeWidthDiagnostics.Describe(broken),
                $"{LogPrefix} 로그 한 줄이 「잉크」와 「그려진 두께」를 구분해 적지 않습니다 — " +
                "읽는 사람이 두 숫자를 구분할 방법이 없으면 이 수정의 절반이 사라집니다.");

            Debug.Log($"{LogPrefix} 게이트 판정 — 잉크 {inkPt:F2}pt/하한 {floorPt:F1}pt → " +
                $"{broken.FloorHonored} · 정상 배치 → {healthy.FloorHonored}\n" +
                $"  {StrokeWidthDiagnostics.Describe(broken)}");
        }

        /// <summary>계측기가 <b>선 자신에게</b> 막 여부를 묻는가(이름/역할로 추측하면 제외된 선이 샌다).</summary>
        [Test]
        public void 계측기가_막_여부를_선_자신에게_묻는다()
        {
            string path = Path.Combine(Application.dataPath, "_Project", "Scripts", "Platform",
                "StrokeWidthDiagnostics.cs");
            Assert.IsTrue(File.Exists(path), $"{LogPrefix} 소스를 찾지 못했습니다: {path}");
            string src = File.ReadAllText(path);

            StringAssert.Contains(nameof(InkMembraneStroke) + "." + nameof(InkMembraneStroke.AppliedSides), src,
                $"{LogPrefix} ★ 계측기가 「이 선에 막이 걸렸는가」를 선에게 묻지 않습니다 — " +
                "역할(FillOutlineStroke)로 추측하면 <b>막에서 제외된 선</b>까지 되빼서 잉크를 과소평가합니다.");
            StringAssert.Contains(nameof(InkMembraneStroke) + "." + nameof(InkMembraneStroke.InkCore), src,
                $"{LogPrefix} ★ 계측기가 되빼기 단일 창구를 쓰지 않습니다 — 산술을 두 곳에 적으면 갈라집니다.");

            // 부재 단언은 썩으면 조용히 초록이 된다(CLAUDE.md). 그래서 <b>실재 대조</b>를 함께 건다.
            Assert.IsTrue(src.Contains("FillOutlineStroke.Is"),
                $"{LogPrefix} 대조 실패 — 역할 조회조차 없습니다. 위 니들들이 무엇을 재고 있는지 알 수 없습니다.");

            Debug.Log($"{LogPrefix} 계측기 — 막 조회 + 되빼기 단일 창구 확인.");
        }
    }
}
