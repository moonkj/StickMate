using NUnit.Framework;
using StickMate.Core;
using UnityEditor;

namespace StickMate.Tests.EditMode
{
    /// <summary>
    /// ★ <b><see cref="EquipmentModel.ResetForTesting"/>가 착용 변경 통지를 정확히 한 번 흘린다</b> —
    /// 2026-09-05 <c>coder</c> 수정(EquipmentModel.cs:362)의 회귀 잠금.
    ///
    /// <para>============================================================================<br/>
    /// 왜 이 테스트가 없으면 안 되는가 — <b>결함이 조용했다</b><br/>
    /// ============================================================================</para>
    ///
    /// <para><c>ResetForTesting()</c>은 <c>_worn</c> 배열을 통째로 갈아치우면서
    /// <see cref="EquipmentModel.TryWear"/>가 하던 <see cref="StickmanEventBus.RaiseCharacterEquipmentChanged"/>를
    /// 빼먹고 있었다. 그 결과 <b>모델은 기본 차림인데 계층에는 직전 차림의 장비 선이 그대로 남는</b>
    /// 상태가 만들어졌다. 그런데 <c>coder</c> 실측상 <b>PlayMode 호출자 24개가 전부 씬을 무조건 다시
    /// 로드</b>했기 때문에, 렌더러가 어차피 새로 생기면서 결함을 우연히 덮었다 —
    /// <b>그 우연이 유일한 방어였고, 아무 테스트도 빨개지지 않았다.</b></para>
    ///
    /// <para>그래서 이 검증은 <b>씬을 쓰지 않는 EditMode</b>에 둔다. 씬 재로드가 개입하는 순간
    /// 같은 사각지대가 그대로 재현된다(PlayMode에 두면 이 테스트조차 결함을 못 본다).</para>
    ///
    /// <para>============================================================================<br/>
    /// 입력 → 기대 결과<br/>
    /// ============================================================================</para>
    /// <list type="table">
    ///  <item><term>차림을 바꿔 놓고 <c>ResetForTesting()</c> 1회</term>
    ///        <description>통지 <b>정확히 1회</b> + 착용 상태가 기본 차림으로 복귀</description></item>
    ///  <item><term>이미 기본 차림인 상태에서 <c>ResetForTesting()</c> 1회</term>
    ///        <description>통지 <b>정확히 1회</b>(변화 감지로 삼키지 <b>않는다</b>)</description></item>
    ///  <item><term>구독자가 통지 안에서 모델을 읽는다</term>
    ///        <description>이미 <b>기본 차림</b>이 보인다(쓰기 → 통지 순서)</description></item>
    ///  <item><term>구독을 끊고 통지 1회</term>
    ///        <description>계수기가 <b>안 움직인다</b>(프로브 음성 대조)</description></item>
    /// </list>
    ///
    /// <para>============================================================================<br/>
    /// 돌연변이(양성 대조) — 이 테스트가 실제로 무엇을 잡는가<br/>
    /// ============================================================================</para>
    /// <list type="bullet">
    ///  <item><c>EquipmentModel.ResetForTesting()</c>의 <c>RaiseCharacterEquipmentChanged()</c> 한 줄을
    ///        지우면 → 통지 0회. <c>되돌리기는_통지를_정확히_한_번_흘린다</c>와
    ///        <c>바뀐_것이_없어도_되돌리기는_통지를_흘린다</c>가 <b>동시에</b> 빨개진다.</item>
    ///  <item>그 줄을 <c>if (변화 있음)</c>으로 감싸면 → 앞 테스트는 살아남고
    ///        <c>바뀐_것이_없어도…</c>만 빨개진다. 이 구분이 이 파일을 두 테스트로 나눈 이유다.
    ///        프로덕션 문서가 <b>"변화가 있을 때만이 아니라 항상"</b>을 명시적 설계로 못박고 있고,
    ///        그 근거는 <see cref="EquipmentModel.RestoreFromSave(EquipmentSlot,string)"/>이
    ///        <b>조용히</b> <c>_worn</c>을 바꾸기 때문이다 — 복원이 중간에 끊기면 "모델은 이미
    ///        기본값인데 계층만 낡은" 상태가 <b>실재</b>하고, 변화 감지는 정확히 그 상태를 못 고친다.</item>
    ///  <item>통지를 <c>_worn</c> 갱신 <b>앞</b>으로 옮기면 → 통지 횟수는 1로 그대로인데
    ///        <c>통지는_모델이_이미_기본_차림이_된_뒤에_도착한다</c>만 빨개진다.
    ///        (횟수만 세면 이 형태를 구조적으로 못 본다: 구독자가 <b>낡은 상태</b>로 서명을 다시 굽고
    ///         그 값을 캐시하면, 통지가 왔는데도 화면은 영영 안 맞는다.)</item>
    /// </list>
    ///
    /// <para>============================================================================<br/>
    /// 프로브 교정 — "0회"를 근거로 쓰기 전에 계수기가 살아 있음을 먼저 보인다<br/>
    /// ============================================================================</para>
    ///
    /// <para>CLAUDE.md: <i>"계산기·검사기를 만들면 알려진 값으로 먼저 교정한다."</i>
    /// 각 테스트는 <b>알려진 발행자</b>(<see cref="EquipmentModel.TryWear"/> 또는 버스 직접 호출)로
    /// 1회를 먼저 확인한 뒤에야 <c>ResetForTesting()</c>을 잰다. 이게 없으면 구독이 죽었을 때의 0회와
    /// 결함이 재발했을 때의 0회를 <b>구분할 수 없다</b>(이 저장소가 반복해 당한 형태 —
    /// "실패한 측정과 성공한 측정이 똑같이 생겼다").</para>
    ///
    /// <para>식별자는 전부 <b>실제 심볼</b>로 참조한다(문자열 니들 0건) — 이벤트 이름이 바뀌면
    /// 조용한 초록이 아니라 <b>컴파일 에러</b>가 난다.</para>
    /// </summary>
    public sealed class EquipmentResetNotificationTests
    {
        private const string DefaultConfigPath = "Assets/_Project/Data/DefaultStickConfig.asset";

        /// <summary>구독 이후 받은 통지 수. 우리 델리게이트만 세므로 다른 구독자(정보창/렌더러)가
        /// 살아 있어도 오염되지 않는다.</summary>
        private int _notifications;

        /// <summary>통지가 도착한 <b>그 순간</b> 모델이 기본 차림이었는가 — 쓰기/통지 순서 검증용.
        /// null이면 아직 통지를 한 번도 안 받았다는 뜻이다.</summary>
        private bool? _headEquippedInsideNotification;

        private StickConfig _config;

        private void OnEquipmentChanged()
        {
            _notifications++;
            // ★ 구독자가 통지 안에서 보는 것이 곧 화면에 나갈 사실이다. 여기서 낡은 값이 보이면
            //   렌더러는 낡은 서명을 캐시하고, 두 번째 통지는 오지 않는다.
            _headEquippedInsideNotification = EquipmentModel.IsEquipped(EquipmentSlot.Head);
        }

        [SetUp]
        public void SubscribeProbe()
        {
            _config = AssetDatabase.LoadAssetAtPath<StickConfig>(DefaultConfigPath);
            Assert.IsNotNull(_config, $"기본 설정 자산을 찾지 못했습니다: {DefaultConfigPath}");

            // ★ 구독 <b>전에</b> 상태를 정규화한다 — 앞선 테스트 클래스가 남긴 차림이 새어 들어오면
            //   "바뀐 것이 없는 상태"를 만들 수 없다. 이 호출의 통지는 세지 않는다(아직 미구독).
            EquipmentModel.ResetForTesting();

            StickmanEventBus.CharacterEquipmentChanged += OnEquipmentChanged;
            _notifications = 0;
            _headEquippedInsideNotification = null;
        }

        [TearDown]
        public void UnsubscribeProbe()
        {
            StickmanEventBus.CharacterEquipmentChanged -= OnEquipmentChanged;
            // 정적 상태를 다음 테스트로 흘리지 않는다(구독을 끊은 뒤라 계수기에 영향이 없다).
            EquipmentModel.ResetForTesting();
        }

        /// <summary>차림을 어긋나게 만들어 둔다 — 되돌릴 <b>거리</b>가 없으면 아래 단언들이 공허해진다.
        /// 벗기(<see cref="EquipmentModel.NotWorn"/>)를 쓰는 이유: 잠금/레벨을 전혀 타지 않아
        /// 요구 레벨 표가 바뀌어도 이 준비 단계가 흔들리지 않는다.</summary>
        private void DirtyOutfitAndCalibrateProbe()
        {
            Assert.IsTrue(EquipmentModel.IsEquipped(EquipmentSlot.Head),
                "전제 붕괴 — 기본 차림에 모자가 걸쳐져 있어야 이 준비 단계가 성립합니다.");

            Assert.IsTrue(EquipmentModel.TryWear(EquipmentSlot.Head, EquipmentModel.NotWorn, _config),
                "전제 붕괴 — 걸치고 있던 모자를 벗지 못했습니다.");

            // ★ 프로브 교정: 알려진 발행자가 정확히 1회를 낸다. 여기가 0이면 아래 0회는
            //   "결함 재발"이 아니라 "구독이 죽었다"는 뜻이다 — 둘을 여기서 갈라 둔다.
            Assert.AreEqual(1, _notifications,
                "프로브 교정 실패 — 알려진 발행자(TryWear)의 통지조차 세지 못했습니다. " +
                "이 상태에서는 ResetForTesting의 '0회'가 무엇을 뜻하는지 판정할 수 없습니다.");

            Assert.IsFalse(EquipmentModel.IsEquipped(EquipmentSlot.Head),
                "전제 붕괴 — 벗었는데 여전히 착용 중입니다.");

            _notifications = 0;
            _headEquippedInsideNotification = null;
        }

        /// <summary>
        /// <b>입력</b>: 기본 차림에서 모자를 벗어 상태를 어긋나게 만든 뒤 <c>ResetForTesting()</c> 1회.<br/>
        /// <b>기대</b>: 착용 변경 통지가 <b>정확히 1회</b> 발행되고, 착용 상태가 기본 차림으로 돌아온다.
        /// <para>0회면 2026-09-05에 고친 결함의 재발이고, 2회 이상이면 슬롯마다 통지가 나가는 다른
        /// 문제다(구독자 셋이 각각 서명을 다시 굽는 비용이 카테고리 수만큼 곱해진다).</para>
        /// </summary>
        [Test]
        public void 되돌리기는_통지를_정확히_한_번_흘린다()
        {
            DirtyOutfitAndCalibrateProbe();

            EquipmentModel.ResetForTesting();

            Assert.AreEqual(1, _notifications,
                "ResetForTesting()이 착용 변경 통지를 정확히 1회 흘리지 않았습니다. " +
                "0회라면 2026-09-05에 고친 결함(EquipmentModel.cs의 RaiseCharacterEquipmentChanged 한 줄)이 " +
                "다시 사라진 것입니다 — 모델은 기본 차림인데 계층에는 직전 차림의 장비 선이 남습니다. " +
                "2회 이상이라면 카테고리마다 통지가 새어 나가는 것입니다(구독자 셋이 그만큼 다시 굽습니다).");

            // ★ 위 단언이 "아무 일도 안 하고 통지만 흘려서" 통과한 것이 아님을 보인다(음성 대조).
            Assert.IsTrue(EquipmentModel.IsEquipped(EquipmentSlot.Head),
                "통지는 왔는데 착용 상태가 기본 차림으로 돌아오지 않았습니다 — 통지가 거짓말을 한 것입니다.");
        }

        /// <summary>
        /// <b>입력</b>: <b>이미 기본 차림인</b> 상태에서 <c>ResetForTesting()</c> 1회.<br/>
        /// <b>기대</b>: 그래도 통지가 <b>정확히 1회</b> 발행된다.
        /// <para>이 테스트가 잠그는 것은 "쓸데없는 통지"가 아니라 <b>설계 결정</b>이다 —
        /// <see cref="EquipmentModel.RestoreFromSave(EquipmentSlot,string)"/>이 조용히 <c>_worn</c>을
        /// 바꾸므로, "모델은 이미 기본값인데 계층만 낡은" 상태가 실재한다. 변화 감지로 통지를 아끼면
        /// <b>정확히 그 상태를 못 고친다.</b> 여기는 Update 경로가 아니라 여분의 통지 한 번이 비용이 아니다.</para>
        /// </summary>
        [Test]
        public void 바뀐_것이_없어도_되돌리기는_통지를_흘린다()
        {
            // ★ 프로브 교정 — 상태를 건드리지 않고 계수기가 살아 있음만 확인한다
            //   (여기서 차림을 바꾸면 "바뀐 것이 없는 상태"라는 이 테스트의 전제가 무너진다).
            StickmanEventBus.RaiseCharacterEquipmentChanged();
            Assert.AreEqual(1, _notifications,
                "프로브 교정 실패 — 버스에 직접 흘린 통지조차 세지 못했습니다.");
            _notifications = 0;

            // SetUp이 이미 기본 차림으로 맞춰 두었다. 그 사실을 단언으로 못박는다.
            Assert.IsTrue(EquipmentModel.IsEquipped(EquipmentSlot.Head),
                "전제 붕괴 — 이 테스트는 '바뀔 것이 없는 상태'에서 출발해야 합니다.");
            int signatureBefore = EquipmentModel.WornStateSignature;

            EquipmentModel.ResetForTesting();

            Assert.AreEqual(signatureBefore, EquipmentModel.WornStateSignature,
                "전제 붕괴 — 이 테스트는 착용 상태가 한 톨도 바뀌지 않는 경우를 재야 합니다.");
            Assert.AreEqual(1, _notifications,
                "착용 상태가 그대로일 때 통지가 삼켜졌습니다. 통지를 '변화가 있을 때만'으로 좁히면 " +
                "저장 복원(RestoreFromSave)이 중간에 끊겨 '모델은 기본값인데 계층만 낡은' 상태가 됐을 때 " +
                "ResetForTesting이 그것을 되돌릴 수 없습니다 — 프로덕션 문서가 '항상 흘린다'를 " +
                "명시적 설계로 못박은 이유가 그것입니다.");
        }

        /// <summary>
        /// <b>입력</b>: 통지 핸들러 안에서 <see cref="EquipmentModel.IsEquipped(EquipmentSlot)"/>를 읽는다.<br/>
        /// <b>기대</b>: 이미 <b>기본 차림</b>이 보인다(<c>_worn</c> 갱신이 통지보다 먼저다).
        /// <para>횟수만 세는 단언은 이 형태를 구조적으로 못 본다 — 통지가 <b>먼저</b> 나가면 구독자는
        /// 낡은 상태로 서명을 다시 구워 캐시하고, 두 번째 통지는 오지 않아 화면이 영영 안 맞는다.</para>
        /// </summary>
        [Test]
        public void 통지는_모델이_이미_기본_차림이_된_뒤에_도착한다()
        {
            DirtyOutfitAndCalibrateProbe();

            EquipmentModel.ResetForTesting();

            Assert.IsTrue(_headEquippedInsideNotification.HasValue,
                "통지가 아예 오지 않아 순서를 잴 수 없습니다(앞의 '정확히 한 번' 테스트를 먼저 보십시오).");
            Assert.IsTrue(_headEquippedInsideNotification.Value,
                "통지가 착용 상태 갱신보다 <b>먼저</b> 도착했습니다 — 구독자가 직전 차림을 읽고 그 서명을 " +
                "캐시하면, 통지는 왔는데도 장비 선이 영영 갱신되지 않습니다.");
        }

        /// <summary>
        /// <b>입력</b>: 구독을 끊은 뒤 통지 1회.<br/>
        /// <b>기대</b>: 계수기가 움직이지 않는다.
        /// <para>계수기의 <b>음성 대조</b>다. 이게 없으면 위 단언들이 "계수기가 어떤 이유로든 항상 1을
        /// 낸다"는 가능성과 구분되지 않는다. 끝에서 다시 구독해 TearDown의 <c>-=</c>와 짝을 맞춘다.</para>
        /// </summary>
        [Test]
        public void 구독을_끊으면_계수기가_더_이상_세지_않는다()
        {
            StickmanEventBus.RaiseCharacterEquipmentChanged();
            Assert.AreEqual(1, _notifications, "프로브 교정 실패 — 구독 중인데 세지 못했습니다.");

            StickmanEventBus.CharacterEquipmentChanged -= OnEquipmentChanged;
            StickmanEventBus.RaiseCharacterEquipmentChanged();
            EquipmentModel.ResetForTesting();

            Assert.AreEqual(1, _notifications,
                "구독을 끊었는데도 계수기가 올라갔습니다 — 이 계수기는 구독과 무관하게 움직이고 있으며, " +
                "그러면 이 파일의 모든 '정확히 1회' 단언이 아무것도 증명하지 못합니다.");

            StickmanEventBus.CharacterEquipmentChanged += OnEquipmentChanged;   // TearDown과 짝 맞추기
        }
    }
}
