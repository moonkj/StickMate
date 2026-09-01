using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Interaction;
using StickMate.States;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★★ 2026-09-02 사용자 신고 — <b>"캐릭터창에서 보이는 캐릭터는 장비 착용 모습만 적용되서
    /// 보여줘야하는데 가끔 움직임"</b>. 판정과 실측은 docs/UX_FLOW.md §45.
    ///
    /// ============================================================================
    /// 이 파일이 잠그는 것 — 【액자 불변식】
    /// ============================================================================
    /// 액자 속 그림은 <b>착용 장비 · 해금 상태 · 잉크색 · 캐릭터 키</b> 네 가지가 바뀔 때만 다시
    /// 그려진다. <b>캐릭터의 상태(무엇을 하고 있는가)는 액자에 도달하지 않는다.</b>
    ///
    /// 신고의 원인은 <c>ComputeSignature()</c>의 <c>hash = hash * 31 + (int)_pose;</c> 한 줄이었다.
    /// 그 줄이 있으면 상태 버킷이 바뀌는 순간 그림을 다시 굽고, 실측으로 <b>뒷팔 끝이 1프레임에
    /// 70.9pt</b>(액자 세로 180pt의 39%) 솟았다가 1.2초 뒤 되돌아왔다(§45-0-3).
    ///
    /// ============================================================================
    /// 왜 EditMode인가 / 왜 이 방식인가
    /// ============================================================================
    /// 서명은 <b>순수 계산</b>이라 화면도 시간도 필요 없다. 그리고 이 파일은 <b>네거티브 컨트롤</b>을
    /// 함께 갖는다 — 서명이 "항상 같은 값"이면 (1)은 아무것도 증명하지 못하므로,
    /// (2)에서 <b>바뀌어야 하는 입력(잉크색)에는 실제로 반응하는지</b>를 같은 창구로 확인한다.
    /// 숫자는 하나도 베끼지 않는다: 비교 대상은 전부 <see cref="CharacterPortraitStage.SignatureForTests"/>가
    /// 돌려주는 값끼리다.
    /// </summary>
    public sealed class PortraitFrameInvariantTests
    {
        private const string LogPrefix = "[액자불변식-TEST]";

        private CharacterPortraitStage _stage;
        private StickConfig _config;

        [SetUp]
        public void SetUp()
        {
            EquipmentModel.ResetForTesting();
            _config = ScriptableObject.CreateInstance<StickConfig>();
            _stage = CharacterPortraitStage.Create(_config, null, null);
            Assert.IsNotNull(_stage, $"{LogPrefix} 촬영장을 만들지 못했습니다.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_stage != null) Object.DestroyImmediate(_stage.gameObject);
            if (_config != null) Object.DestroyImmediate(_config);
            _stage = null;
            _config = null;
            EquipmentModel.ResetForTesting();
        }

        // ============================================================================
        // (1) 핵심 — 포즈는 서명에 <b>도달하지 않는다</b>
        // ============================================================================

        [Test]
        public void SignatureIgnoresEveryPose()
        {
            int baseline = _stage.SignatureForTests;

            foreach (PortraitPose pose in System.Enum.GetValues(typeof(PortraitPose)))
            {
                _stage.SetPose(pose);
                int now = _stage.SignatureForTests;
                Assert.AreEqual(baseline, now,
                    $"{LogPrefix} 포즈를 {pose}로 바꾸자 액자 서명이 {baseline} -> {now}로 달라졌습니다. " +
                    "서명이 달라지면 그림을 다시 굽고, 그 순간 뒷팔 끝이 1프레임에 70.9pt 튑니다 " +
                    "(사용자 신고 \"가끔 움직임\"의 실체 — docs/UX_FLOW.md 45-0-3). " +
                    "ComputeSignature()에 (int)_pose가 되살아났는지 확인하십시오.");
            }
        }

        /// <summary>
        /// 상태 ID <b>전수</b>로 한 번 더 — 버킷이 늘어나도(새 PortraitPose가 생겨도) 이 파일이
        /// 자동으로 그 값을 훑는다. <see cref="CharacterPortraitStage.PoseForState"/>는 남아 있지만
        /// <b>프로덕션 호출자가 없다</b>는 사실 자체는 PlayMode 쪽(PortraitPaperDollTests)이 잠근다.
        /// </summary>
        [Test]
        public void SignatureIgnoresEveryStateIdMappedPose()
        {
            int baseline = _stage.SignatureForTests;
            int probed = 0;

            foreach (StickmanStateId id in System.Enum.GetValues(typeof(StickmanStateId)))
            {
                _stage.SetPose(CharacterPortraitStage.PoseForState(id));
                Assert.AreEqual(baseline, _stage.SignatureForTests,
                    $"{LogPrefix} 상태 {id}에서 액자 서명이 달라졌습니다.");
                probed++;
            }

            Assert.Greater(probed, 20,
                $"{LogPrefix} 훑은 상태가 {probed}개뿐입니다 — StickmanStateId 열거를 못 읽었다는 뜻이라 " +
                "이 테스트는 아무것도 증명하지 못했습니다(전수 대조 전제가 깨졌습니다).");
        }

        // ============================================================================
        // (2) ★ 네거티브 컨트롤 — 서명이 "무엇에도 반응하지 않는" 상수가 아님을 증명한다
        // ============================================================================
        //
        // 이 테스트가 없으면 (1)은 "ComputeSignature가 상수 0을 돌려주는" 구현에서도 초록이다.
        // 사용자가 요구한 네 입력 중 <b>가장 싸게 흔들 수 있는 것</b>(잉크색)으로 관측 채널이 살아
        // 있음을 보인다 — 나머지 셋(장비/해금/키)은 PlayMode 쪽에서 실제 화면으로 확인된다.

        [Test]
        public void SignatureStillReactsToInkColour()
        {
            _config.SetRuntimeInkColor(StickmanInkColor.Black);
            int black = _stage.SignatureForTests;

            _config.SetRuntimeInkColor(StickmanInkColor.White);
            int white = _stage.SignatureForTests;

            Assert.AreNotEqual(black, white,
                $"{LogPrefix} 잉크색을 검정 -> 흰색으로 바꿨는데 서명이 {black} 그대로입니다 — " +
                "서명이 아무것에도 반응하지 않는다는 뜻이고, 그러면 위 (1)의 초록은 거짓입니다. " +
                "사용자가 요구한 네 입력(장비/해금/잉크/키)은 계속 살아 있어야 합니다.");

            Debug.Log($"{LogPrefix} 관측 채널 정상 — 잉크색 검정 {black} / 흰색 {white}. " +
                "포즈 4종과 상태 전수에는 반응하지 않음(위 두 테스트).");
        }

        // ============================================================================
        // (3) 숨쉬기 게이트 — 값을 베끼지 않고 <b>프로덕션 게이트를 읽어</b> 확인한다
        // ============================================================================

        [Test]
        public void BreathingGateIsOff()
        {
            Assert.IsFalse(CharacterPortraitStage.BreathingEnabledForTests,
                $"{LogPrefix} 숨쉬기가 켜져 있습니다. 실측(45-0-1)으로 창이 열려 있는 내내 " +
                "주기 2.004초 / peak-to-peak 1.898pt로 그림 전체가 오르내렸고, 그러면 " +
                "[천모자]와 [털모자]를 겹쳐 비교할 수 없습니다.");

            Assert.Greater(CharacterPortraitStage.BreathPeriodSecondsForTests, 0f,
                $"{LogPrefix} 숨쉬기 주기를 읽지 못했습니다 — PlayMode 관찰 예산이 이 값에서 나옵니다.");
        }
    }
}
