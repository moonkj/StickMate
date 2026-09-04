using System;
using System.Collections.Generic;

namespace StickMate.Core
{
    /// <summary>
    /// ★ 춤 7종의 <b>안정적인 식별자 표</b>와 장착 집합 정규화 규칙.
    /// 정본: <c>docs/MOTION_SPEC.md</c> §26-8-1(무료 2종/유료 후보 5종) · §26-4-1(장착 ≥ 1) ·
    /// <c>docs/DESIGN_SYSTEMS_STATS.md</c> §20-2-b(정규화 3줄).
    ///
    /// ============================================================================
    /// 여기 있는 것과 없는 것 — 경계를 먼저 못박는다
    /// ============================================================================
    /// <list type="bullet">
    ///  <item><b>있다</b>: 아이디 문자열, 무료 2종이 무엇인가, 저장된 장착 집합을 어떻게 정규화하는가.
    ///    이 셋은 <b>세이브 배선</b>에 필요하고, v10 스키마와 함께 오늘 굳어야 한다.</item>
    ///  <item><b>없다</b>: 자세·박자·재생·감지. 그건 <c>StickmanPoseAnimator</c> 쪽 라운드다.
    ///    여기에 재생 로직을 얹지 마라 — 이 파일은 <b>식별자와 집합 규칙</b>만 안다.</item>
    ///  <item><b>없다</b>: <c>ownedDanceIds</c>. 보유 = 무료 2종(항상 파생) ∪ 엔타이틀먼트(C층)이고
    ///    <b>C층은 세이브에 두지 않는다</b>(§20-2 · ENTITLEMENT_CONTRACT §E-4-a).
    ///    저장할 것이 남지 않으므로 필드가 없다.</item>
    /// </list>
    ///
    /// <para>★ <b>아이디를 숫자가 아니라 문자열로 두는 이유</b>는 장비 아이디와 같다
    /// (<c>Core/EquipmentModel.cs</c>의 "인덱스 vs 문자열 아이디" 문단) — 표 중간에 한 종을
    /// 끼워 넣는 날 전원의 장착이 한 칸씩 밀리는 사고를 구조적으로 없앤다.</para>
    /// </summary>
    public static class DanceIds
    {
        /// <summary>D1 발레 피루엣 — 제자리, 발판 0.45 H. <b>기본 무료</b>(사용자가 직접 지정).</summary>
        public const string Pirouette = "dance.pirouette";

        /// <summary>D2 스타점프 — 발판 2.70 H. <b>기본 무료</b>(사용자가 직접 지정).</summary>
        public const string StarJump = "dance.star_jump";

        /// <summary>D3 문워크 — 유료 후보.</summary>
        public const string Moonwalk = "dance.moonwalk";

        /// <summary>D4 러닝맨 — 유료 후보.</summary>
        public const string RunningMan = "dance.running_man";

        /// <summary>D5 로봇 — 유료 후보.</summary>
        public const string Robot = "dance.robot";

        /// <summary>D6 프리샤트카 — 유료 후보.</summary>
        public const string Prisyadka = "dance.prisyadka";

        /// <summary>D7 말춤 — 유료 후보.</summary>
        public const string HorseDance = "dance.horse";

        /// <summary>
        /// ★ 기본 장착 = 무료 2종. <b>사용자가 직접 요청한 두 동작</b>이라 유료 뒤에 두지 않는다는
        /// 규칙(§26-8-1)의 코드 표현이다.
        /// <para>배열을 그대로 내주지 않고 매번 새로 만드는 이유: 정적 배열을 반환하면 호출부가
        /// 그 자리에서 원소를 갈아 끼울 수 있고, 그러면 "기본값"이 실행 중에 달라진다.</para>
        /// </summary>
        public static string[] CreateFreeDefaults() => new[] { Pirouette, StarJump };

        /// <summary>표에 있는 7종 전부(선언 순서 = D1~D7).</summary>
        public static string[] CreateAll() => new[]
        {
            Pirouette, StarJump, Moonwalk, RunningMan, Robot, Prisyadka, HorseDance,
        };

        /// <summary>아는 아이디인가. 모르는 아이디는 정규화가 조용히 버린다 —
        /// 손상된 파일이나 지워진 팩의 잔재가 장착 집합에 남아 있으면 재생 쪽이 매번 헛돈다.</summary>
        public static bool IsKnown(string danceId)
        {
            if (string.IsNullOrEmpty(danceId)) return false;
            string[] all = CreateAll();
            for (int i = 0; i < all.Length; i++)
            {
                if (string.Equals(all[i], danceId, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>
        /// 저장된 장착 집합을 <b>합법 상태</b>로 정규화한다(§20-2-b).
        ///
        /// <para>세 단계이고, <b>세 번째가 이 함수의 존재 이유</b>다:</para>
        /// <list type="number">
        ///  <item><c>null</c>과 <c>빈 배열</c>을 <b>같은 분기</b>에서 무료 2종으로 채운다 —
        ///    §26-4-1이 <c>|장착| ≥ 1</c>을 요구해 빈 집합이 <b>합법 상태 공간에 없기</b> 때문에
        ///    두 값의 뜻이 같아진다. (그래서 이 필드는 「없음 ≠ 0」이 아니고, v10을 강제하지 않는다.)</item>
        ///  <item>보유하지 않은 아이디를 걸러 낸다(불변식 (a) 장착 ⊆ 보유).</item>
        ///  <item>★ <b>거른 뒤 다시 빈 집합인지 확인한다.</b> 유료 팩 환불·엔타이틀먼트 조회 실패로
        ///    필터가 전부 털어 갈 수 있고, 그때 <c>|장착| = 0</c>이면 음악이 나와도 아무 일이
        ///    일어나지 않는다 — §26-4-1이 그걸 <b>"고장으로 읽힌다"</b>고 명시했다.</item>
        /// </list>
        ///
        /// <param name="isAvailable">보유 판정. <b>null이면 "아는 아이디는 전부 허용"</b>으로 떨어진다 —
        /// 세이브 배선만 있는 지금의 기본값이다. 보유/장착 화면 라운드가 여기에
        /// 엔타이틀먼트 조회를 꽂는다. ★ 조회가 실패할 때 <b>거짓</b>을 돌려주면 3단계가 받아 내므로
        /// 사용자는 어떤 경우에도 춤을 잃지 않는다.</param>
        /// </summary>
        public static string[] Normalize(string[] saved, Func<string, bool> isAvailable)
        {
            var kept = new List<string>(2);
            if (saved != null)
            {
                for (int i = 0; i < saved.Length; i++)
                {
                    string id = saved[i];
                    if (!IsKnown(id)) continue;
                    if (isAvailable != null && !isAvailable(id)) continue;
                    if (kept.Contains(id)) continue;
                    kept.Add(id);
                }
            }

            // 1단계(없음/빈)와 3단계(필터 후 빈)가 여기서 만난다 — 한 줄로 처리하는 것이 §20-2-b의 요지다.
            if (kept.Count == 0) return CreateFreeDefaults();
            return kept.ToArray();
        }
    }
}
