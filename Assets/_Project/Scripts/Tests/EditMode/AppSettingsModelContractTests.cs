using NUnit.Framework;
using UnityEngine;
using StickMate.Core;
using StickMate.Dialogue;

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
            Assert.AreEqual(DialogueVisibleLength.Default, AppSettingsModel.DialogueVisibleLength,
                "대사 표시 시간의 배포 기본값은 `기본`이어야 한다(세그먼트 첫 칸).");
            Assert.AreEqual(DialogueBudget.MinVisibleScale, AppSettingsModel.ResolveDialogueVisibleScale(), 1e-4f,
                "고른 적이 없는데 노출 배율이 100%가 아니다 — 그러면 이 라운드가 2026-09-01에 착륙한 " +
                "화면 거동을 조용히 되돌린 것이다.");
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
        public void 배포_최소가_최대보다_길면_최소가_받쳐진다()
        {
            // 배포 기본 최소가 최대보다 큰 조합(에셋 튜닝으로 충분히 가능하다)에서는 최소가 받쳐진다.
            _probe.dialogueMinVisibleSeconds = 3f;
            _probe.dialogueMaxVisibleSeconds = 1.5f;
            Assert.AreEqual(1.5f, AppSettingsModel.ResolveDialogueMinVisibleSeconds(_probe), 1e-4f,
                "최소가 최대보다 길면 '최소 보장'과 '최대 제한'이 동시에 참일 수 없다 — 말풍선이 " +
                "규칙 4를 조용히 어기게 된다.");

            // 상한 없음(0 이하)이면 받칠 것이 없다 — 배포 최소가 그대로 나온다.
            _probe.dialogueMaxVisibleSeconds = 0f;
            Assert.AreEqual(3f, AppSettingsModel.ResolveDialogueMinVisibleSeconds(_probe), 1e-4f);
        }

        /// <summary>
        /// ★ 2026-09-02 — 초 슬라이더가 폐기되고 3단 세그먼트가 됐다(UX_FLOW.md 42-4).
        /// 여기서 잠그는 것은 <b>"고른 적 없음 = 기본"</b>과 <b>범위 밖 값이 기본으로 떨어진다</b> 둘이다.
        /// 배율의 <b>숫자</b>는 이 파일이 아니라 <c>DialogueVisibleScaleContractTests</c>가 유도식으로 잰다.
        /// </summary>
        [Test]
        public void 대사_표시_시간은_세_칸이고_고른_값이_그대로_남는다()
        {
            AppSettingsModel.SetDialogueVisibleLength(DialogueVisibleLength.VeryLong);
            Assert.IsTrue(AppSettingsModel.HasDialogueVisibleLength);
            Assert.AreEqual(DialogueVisibleLength.VeryLong, AppSettingsModel.DialogueVisibleLength);
            Assert.AreEqual(AppSettingsModel.ScaleOf(DialogueVisibleLength.VeryLong),
                AppSettingsModel.ResolveDialogueVisibleScale(), 1e-4f);

            // 열거형 밖의 값(저장 파일 손상/미래 버전)은 기본으로 떨어진다 — 죽은 값을 사용자의
            // 선택으로 오해하는 것보다 배포 기본값이 언제나 안전하다.
            AppSettingsModel.SetDialogueVisibleLength((DialogueVisibleLength)99);
            Assert.AreEqual(DialogueVisibleLength.Default, AppSettingsModel.DialogueVisibleLength);
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
                hasVisibleLength: false, visibleLengthName: null,
                hasChatterPercent: false, chatterPercent: 0,
                hasBubbleEnabled: false, bubbleEnabled: false);

            Assert.IsTrue(AppSettingsModel.AutoHideOnFullscreen);
            Assert.IsTrue(AppSettingsModel.GearIconVisible);
            Assert.AreEqual(16, AppSettingsModel.ResolveDialogueFontSize(_probe),
                "고른 적 없음(has*=false)인데 저장된 0이 새어 나왔습니다.");
            Assert.IsTrue(AppSettingsModel.ResolveDialogueBubbleEnabled(_probe),
                "고른 적 없음인데 저장된 false가 새어 나왔습니다 — 말풍선이 이유 없이 꺼집니다.");
            Assert.IsFalse(AppSettingsModel.HasDialogueVisibleLength,
                "고른 적 없음인데 대사 표시 시간이 '사용자가 고른 값'으로 복원됐습니다.");
            Assert.IsFalse(AppSettingsModel.IsDirty, "복원은 변화가 아니라 초기 상태 확정이다(다른 모델과 같은 규약).");
        }

        /// <summary>
        /// ★ 저장 파일에는 숫자가 아니라 <b>이름 문자열</b>이 적힌다(잉크색과 같은 관례). 그래서 모르는
        /// 이름을 만나는 경로가 실재한다 — 파일 손상, 그리고 <b>미래 버전이 추가한 칸</b>.
        /// 그때 조용히 첫 칸으로 떨어지는 것이 계약이다.
        /// </summary>
        [Test]
        public void 저장된_이름이_모르는_값이면_고른_적_없음으로_떨어진다()
        {
            AppSettingsModel.RestoreFromSave(
                autoHideOnFullscreen: true, gearIconVisible: true,
                hasFontSize: false, fontSize: 0,
                hasVisibleLength: true, visibleLengthName: "Eternal",
                hasChatterPercent: false, chatterPercent: 0,
                hasBubbleEnabled: false, bubbleEnabled: true);

            Assert.IsFalse(AppSettingsModel.HasDialogueVisibleLength,
                "모르는 이름이 '사용자가 고른 값'으로 복원됐습니다 — 그 값은 화면에서 아무 뜻도 없습니다.");
            Assert.AreEqual(DialogueBudget.MinVisibleScale, AppSettingsModel.ResolveDialogueVisibleScale(), 1e-4f);

            // 아는 이름은 그대로 살아남는다(위 단언이 '전부 떨어뜨려서' 통과한 것이 아님을 보인다).
            AppSettingsModel.RestoreFromSave(
                autoHideOnFullscreen: true, gearIconVisible: true,
                hasFontSize: false, fontSize: 0,
                hasVisibleLength: true, visibleLengthName: DialogueVisibleLength.Long.ToString(),
                hasChatterPercent: false, chatterPercent: 0,
                hasBubbleEnabled: false, bubbleEnabled: true);
            Assert.IsTrue(AppSettingsModel.HasDialogueVisibleLength);
            Assert.AreEqual(DialogueVisibleLength.Long, AppSettingsModel.DialogueVisibleLength);
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
