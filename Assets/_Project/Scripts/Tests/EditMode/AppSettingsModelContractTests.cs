using NUnit.Framework;
using UnityEngine;
using StickMate.Core;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ 설정창이 만지는 값의 <b>조회 계약</b> — 2026-09-01(docs/UX_FLOW.md 35-1).
    ///
    /// 여기서 지키는 것은 딱 하나다: <b>사용자가 고른 적이 없으면 배포 기본값이 그대로 흘러나온다</b>.
    /// 이 성질이 있어야 설정창을 만들면서 <c>StickConfig</c>의 값을 한 톨도 옮기지 않아도 되고,
    /// 그래서 <b>배포 에셋에 쓸 이유 자체가 사라진다</b>(2026-08-31에 두 번 겪은 오염 사고의 근본 예방).
    ///
    /// 실물 에셋을 열지 않고 <see cref="ScriptableObject.CreateInstance"/>로 만든 <b>깨끗한 사본</b>에
    /// 대고 잰다 — 절대 불변 원칙 3(유저 자산 불변)의 테스트판이며, 배포 에셋의 현재 값에 결과가
    /// 흔들리지도 않는다.
    /// </summary>
    public sealed class AppSettingsModelContractTests
    {
        private StickConfig _probe;

        [SetUp]
        public void SetUp()
        {
            AppSettingsModel.ResetForTesting();
            _probe = ScriptableObject.CreateInstance<StickConfig>();
            _probe.dialogueFontSize = 16;
            _probe.dialogueMinVisibleSeconds = 0.7f;
            _probe.dialogueMaxVisibleSeconds = 4f;
            _probe.idleChatterChance = 0.28f;
            _probe.walkChatterChance = 0.14f;
            _probe.dialogueBubbleEnabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            AppSettingsModel.ResetForTesting();
            if (_probe != null) Object.DestroyImmediate(_probe);
            _probe = null;
        }

        [Test]
        public void 고른_적이_없으면_배포_기본값이_그대로_나온다()
        {
            Assert.AreEqual(16, AppSettingsModel.ResolveDialogueFontSize(_probe));
            Assert.AreEqual(4f, AppSettingsModel.ResolveDialogueMaxVisibleSeconds(_probe), 1e-4f);
            Assert.AreEqual(0.7f, AppSettingsModel.ResolveDialogueMinVisibleSeconds(_probe), 1e-4f);
            Assert.AreEqual(0.28f, AppSettingsModel.ResolveIdleChatterChance(_probe), 1e-4f);
            Assert.AreEqual(0.14f, AppSettingsModel.ResolveWalkChatterChance(_probe), 1e-4f);
            Assert.IsTrue(AppSettingsModel.ResolveDialogueBubbleEnabled(_probe));
            Assert.IsTrue(AppSettingsModel.AutoHideOnFullscreen, "전체화면 자동 숨김의 기본은 켜짐이어야 한다(원칙 2).");
            Assert.IsTrue(AppSettingsModel.GearIconVisible, "톱니 아이콘의 기본은 보임이어야 한다.");
        }

        [Test]
        public void 잡담_빈도는_확률을_덮어쓰지_않고_배율로_곱한다()
        {
            AppSettingsModel.SetChatterPercent(50);
            Assert.AreEqual(0.14f, AppSettingsModel.ResolveIdleChatterChance(_probe), 1e-4f);
            Assert.AreEqual(0.07f, AppSettingsModel.ResolveWalkChatterChance(_probe), 1e-4f);

            AppSettingsModel.SetChatterPercent(0);
            Assert.AreEqual(0f, AppSettingsModel.ResolveIdleChatterChance(_probe), 1e-4f,
                "0%면 혼잣말이 완전히 멈춰야 한다.");

            AppSettingsModel.SetChatterPercent(200);
            Assert.AreEqual(0.56f, AppSettingsModel.ResolveIdleChatterChance(_probe), 1e-4f);
            Assert.AreEqual(0.28f, AppSettingsModel.ResolveWalkChatterChance(_probe), 1e-4f);

            // ★ 원본 확률은 한 번도 바뀌지 않았다 — 배율을 100%로 되돌리면 정확히 제자리다
            //   (35-1-3 ③: 확률값 자체를 0으로 덮어쓰면 원래 값이 사라져 되돌릴 수 없다).
            AppSettingsModel.SetChatterPercent(100);
            Assert.AreEqual(0.28f, AppSettingsModel.ResolveIdleChatterChance(_probe), 1e-4f);
            Assert.AreEqual(0.28f, _probe.idleChatterChance, 1e-4f,
                "설정 모델이 StickConfig의 확률 필드를 건드렸습니다 — 배포 에셋 오염 경로입니다.");
        }

        [Test]
        public void 최대_노출_시간을_최소보다_짧게_고르면_최소가_함께_내려간다()
        {
            // 사용자가 1.5초를 고르면 "최소 0.7초 보장"과 충돌하지 않는다(0.7 < 1.5).
            AppSettingsModel.SetDialogueMaxVisibleSeconds(1.5f);
            Assert.AreEqual(0.7f, AppSettingsModel.ResolveDialogueMinVisibleSeconds(_probe), 1e-4f);

            // 배포 기본 최소가 최대보다 큰 조합(에셋 튜닝으로 충분히 가능하다)에서는 최소가 받쳐진다.
            _probe.dialogueMinVisibleSeconds = 3f;
            Assert.AreEqual(1.5f, AppSettingsModel.ResolveDialogueMinVisibleSeconds(_probe), 1e-4f,
                "최소가 최대보다 길면 '최소 보장'과 '최대 제한'이 동시에 참일 수 없다 — 말풍선이 " +
                "규칙 4를 조용히 어기게 된다.");
        }

        [Test]
        public void 범위를_벗어난_값은_잘려서_들어간다()
        {
            AppSettingsModel.SetDialogueFontSize(999);
            Assert.AreEqual(AppSettingsModel.MaxDialogueFontSize, AppSettingsModel.DialogueFontSize);
            AppSettingsModel.SetDialogueFontSize(-5);
            Assert.AreEqual(AppSettingsModel.MinDialogueFontSize, AppSettingsModel.DialogueFontSize);

            AppSettingsModel.SetChatterPercent(9999);
            Assert.AreEqual(AppSettingsModel.MaxChatterPercent, AppSettingsModel.ChatterPercent);
        }

        [Test]
        public void 옛_저장파일에는_기본이_true인_두_값을_켜진_상태로_복원한다()
        {
            // v7 이하 파일에는 이 키가 없어 JsonUtility가 false로 채운다 — 그대로 읽으면 뜻이 뒤집혀
            // "전체화면에서도 안 숨음 + 톱니 사라짐"이 된다. 호출부(CharacterSaveStore)가 버전을 보고
            // true를 넘기는 계약을 여기서 문서화한다(구석 패널의 cornerPanelEnabled와 같은 함정).
            AppSettingsModel.RestoreFromSave(
                autoHideOnFullscreen: true, gearIconVisible: true,
                hasFontSize: false, fontSize: 0,
                hasVisibleSeconds: false, visibleSeconds: 0f,
                hasChatterPercent: false, chatterPercent: 0,
                hasBubbleEnabled: false, bubbleEnabled: false);

            Assert.IsTrue(AppSettingsModel.AutoHideOnFullscreen);
            Assert.IsTrue(AppSettingsModel.GearIconVisible);
            Assert.AreEqual(16, AppSettingsModel.ResolveDialogueFontSize(_probe),
                "고른 적 없음(has*=false)인데 저장된 0이 새어 나왔습니다.");
            Assert.IsTrue(AppSettingsModel.ResolveDialogueBubbleEnabled(_probe),
                "고른 적 없음인데 저장된 false가 새어 나왔습니다 — 말풍선이 이유 없이 꺼집니다.");
            Assert.IsFalse(AppSettingsModel.IsDirty, "복원은 변화가 아니라 초기 상태 확정이다(다른 모델과 같은 규약).");
        }

        [Test]
        public void 값이_실제로_바뀔_때만_IsDirty가_선다()
        {
            Assert.IsFalse(AppSettingsModel.IsDirty);

            AppSettingsModel.SetGearIconVisible(true);   // 이미 true — 변화 없음.
            Assert.IsFalse(AppSettingsModel.IsDirty,
                "같은 값을 다시 넣었는데 저장이 예약됐습니다 — 하루 종일 켜져 있는 앱에서 주기 저장이 " +
                "이유 없이 디스크를 두드리게 됩니다.");

            AppSettingsModel.SetGearIconVisible(false);
            Assert.IsTrue(AppSettingsModel.IsDirty);

            AppSettingsModel.MarkSaved();
            Assert.IsFalse(AppSettingsModel.IsDirty);
        }
    }
}
