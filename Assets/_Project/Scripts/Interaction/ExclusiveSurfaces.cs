using System.Collections.Generic;
using UnityEngine;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ <b>배타 표면</b> — 열려 있는 동안 화면을 점유하고, 다른 배타 표면과 <b>동시에 떠 있으면
    /// 안 되는</b> UI. 캐릭터 창 / 설정창 / 부채꼴 메뉴 / 팝오버 3종이 여기 속한다.
    ///
    /// <para>★★ 왜 인터페이스인가 (2026-09-01 사용자 신고 "케릭터창도 겹쳐서보이는 문제있고")
    /// ============================================================================
    /// 종전에는 <c>CharacterInfoWindow</c>와 <c>SettingsWindow</c>가 각자
    /// <c>CloseOverlappingSurfaces()</c>에 <b>닫을 대상을 손으로 나열</b>하고 있었다. 그 목록에
    /// 설정창이 빠져 있어서 <c>I</c>(정보창)를 눌러도 설정창이 남았다 — 720x560 설정창이 880x861
    /// 정보창 위에 겹쳐 떠서 초상화·스탯·장비 그리드를 가렸다.
    ///
    /// 그런데 <b>진짜 문제는 빠진 한 줄이 아니라 목록 자체</b>였다. 그 함수에는
    /// <code>if (_menu != null) { _menu.ForceCloseAll(reason); return; }</code>
    /// 라는 조기 반환이 있어서, 부채꼴이 있는 <b>정식 조립에서는 그 아래 줄이 영원히 실행되지
    /// 않는다.</b> 즉 "설정창을 목록 아래에 추가"하는 가장 자연스러운 수정은 프로덕션에서 조용히
    /// 건너뛰어졌을 것이다. 실제로 그 함수의 주석은 "새 표면은 부채꼴 분기 <b>위에</b> 넣어야
    /// 한다"고 경고하고 있었다 — <b>주석으로 지켜야 하는 규약은 언젠가 반드시 깨진다.</b>
    ///
    /// 그래서 목록을 없앴다. 표면이 <see cref="IExclusiveSurface"/>를 구현하면
    /// <see cref="ExclusiveSurfaces.CloseAllExcept"/>가 <b>자동으로</b> 찾아 닫는다.
    /// 새 표면을 추가하는 사람은 "어느 줄 위에 넣어야 하는지"를 알 필요가 없고, 잊을 자리도 없다.
    /// 잊더라도 <c>Tests/EditMode/ExclusiveSurfaceRegistryTests</c>가 리플렉션 감사로 잡는다.</para>
    ///
    /// <para>구현은 <b>명시적 인터페이스 구현</b>을 쓴다 — 각 창의 공개 API(<c>Open</c>/<c>Close</c>/
    /// <c>IsOpen</c>)는 한 톨도 바뀌지 않고, 배타 규칙은 이 인터페이스를 통해서만 보인다.</para>
    /// </summary>
    public interface IExclusiveSurface
    {
        /// <summary>지금 화면을 점유하고 있는가. <b>플래그가 아니라 "보이는가"</b>가 기준이다 —
        /// 부채꼴처럼 접히는 중(Collapsing)인 표면도 아직 화면에 있으면 true여야 한다.</summary>
        bool IsSurfaceOpen { get; }

        /// <summary>배타 규칙에 의해 거둬진다. <b>이미 닫혀 있으면 아무 일도 하지 않아야 한다</b>
        /// (중복 호출 안전) — 부채꼴처럼 자기 팝오버를 함께 닫는 표면이 있어서 이중 호출이 정상이다.</summary>
        void CloseSurface(string reason);
    }

    /// <summary>
    /// 배타 규칙의 <b>단 하나의 집행 지점</b>. 표면을 여는 쪽이 여기 한 줄만 부르면 된다.
    /// </summary>
    public static class ExclusiveSurfaces
    {
        /// <summary>
        /// <paramref name="opener"/>와 <b>같은 GameObject에 붙은</b> 다른 모든 배타 표면을 닫는다.
        /// 이 저장소의 배타 표면은 전부 스틱맨 프리팹 루트의 형제 컴포넌트다(각 창이
        /// <c>GetComponent&lt;...&gt;()</c>로 서로를 찾는 기존 관례와 같은 전제).
        ///
        /// <para><b>조기 반환이 없다.</b> 어떤 표면이 없거나(null) 이미 닫혀 있어도 나머지는 전부
        /// 순회한다 — 이 함수가 고치는 버그가 정확히 "중간에 return해서 뒤쪽을 건너뛴 것"이다.</para>
        ///
        /// <para>배열 하나를 할당한다. <b>매 프레임 경로가 아니다</b> — 창을 여는 사용자 동작에서만
        /// 불린다(CLAUDE.md의 할당 금지는 <c>Update()</c>를 겨눈 규칙이다). 정적 버퍼를 재사용하면
        /// 재진입(설정창을 닫다가 정보창이 되살아나는 시트 복귀 경로)에서 순회 중인 목록이
        /// 지워지므로, 여기서는 지역 스냅샷이 <b>더 안전하고 더 싸다</b>.</para>
        /// </summary>
        /// <returns>실제로 닫은 표면 수(진단/테스트용).</returns>
        public static int CloseAllExcept(Component opener, string reason)
        {
            if (opener == null) return 0;

            IExclusiveSurface[] surfaces = opener.GetComponents<IExclusiveSurface>();
            int closed = 0;
            for (int i = 0; i < surfaces.Length; i++)
            {
                IExclusiveSurface surface = surfaces[i];
                if (surface == null) continue;
                if (ReferenceEquals(surface, opener)) continue;   // 여는 표면 자신은 건드리지 않는다.
                if (!surface.IsSurfaceOpen) continue;
                surface.CloseSurface(reason);
                closed++;
            }
            return closed;
        }

        /// <summary>지금 열려 있는 배타 표면 수(진단/테스트용). 배타 규칙이 성립하면 0 또는 1이다.</summary>
        public static int CountOpen(Component host)
        {
            if (host == null) return 0;
            IExclusiveSurface[] surfaces = host.GetComponents<IExclusiveSurface>();
            int open = 0;
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (surfaces[i] != null && surfaces[i].IsSurfaceOpen) open++;
            }
            return open;
        }

        /// <summary>열려 있는 표면들의 이름을 채운다(진단 로그/테스트 실패 메시지 전용).</summary>
        public static void CollectOpenNames(Component host, List<string> into)
        {
            if (into == null) return;
            into.Clear();
            if (host == null) return;
            IExclusiveSurface[] surfaces = host.GetComponents<IExclusiveSurface>();
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (surfaces[i] != null && surfaces[i].IsSurfaceOpen) into.Add(surfaces[i].GetType().Name);
            }
        }
    }
}
