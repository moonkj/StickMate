using System.Collections.Generic;
using UnityEngine;

namespace StickMate.Interaction
{
    /// <summary>
    /// ★ <b>한 프레임에 들어온 클릭 하나는 표면 하나만 먹는다.</b>
    /// (2026-09-08 사용자 신고 P0 — <c>[추가]</c>를 눌렀는데 그 아래 포스트잇 체크박스까지 함께
    /// 반응해 항목이 자동 완료되고 하루 1회 300동전이 소진됐다.)
    ///
    /// ============================================================================
    /// 왜 창마다 있는 <c>TryClaimAction</c>으로는 못 막았나
    /// ============================================================================
    /// 이 저장소에는 <c>TryClaimAction</c>이 <b>4벌</b> 있다(<see cref="PopoverPanel"/> ·
    /// <see cref="TodoPostItWidget"/> · <c>CharacterInfoWindow.Input</c> · <c>SettingsWindow</c>).
    /// 넷 다 «같은 클릭을 uGUI 경로와 전역 폴링 경로가 두 번 처리하는 것»을 막지만, 넷 다
    /// <b>자기 안에서만</b> 막는다. 두 표면이 서로의 존재를 모르므로 <b>서로 다른 두 표면</b>이
    /// 같은 물리적 클릭을 각자 정상 처리하는 것은 어느 벌도 막지 못했다.
    ///
    /// <para>그리고 그 사고는 «전역 폴링 경로»에서만 난다. uGUI 경로는 <c>GraphicRaycaster</c>가
    /// 캔버스 <c>sortingOrder</c>로 이미 z-순서를 풀어 주지만(위 캔버스가 아래 캔버스의 클릭을
    /// 가린다), 전역 폴링은 <b>생 사각형 히트테스트</b>라 z-순서를 모른다. 팝오버(31700)와
    /// 포스트잇(30000)이 화면에서 겹치면 둘 다 «내 사각형 안이다»라고 판정한다.</para>
    ///
    /// ============================================================================
    /// 판정은 <b>순서에 의존하지 않는다</b> — 그게 이 클래스의 핵심 계약이다
    /// ============================================================================
    /// 단순한 «먼저 잡는 쪽이 이긴다» 프레임 게이트만 두면 승자가 <c>Update</c> 실행 순서에
    /// 좌우된다. 포스트잇이 먼저 돌면 <c>[추가]</c>가 <b>삼켜져</b> 「추가가 안 된다」는 다른
    /// 버그가 된다 — 증상만 바뀐다. 그래서 두 축으로 판정한다:
    /// <list type="number">
    /// <item><b>z-순서(공간)</b>: 클릭 지점을 <b>더 위에 있는 표면</b>이 덮고 있으면 아래 표면은
    ///   먹지 않는다. 이건 우연이 아니라 화면에서 실제로 보이는 것과 같은 답이다.</item>
    /// <item><b>프레임(시간)</b>: 그 프레임에 이미 다른 표면이 먹었으면 두 번째는 먹지 않는다.
    ///   같은 표면이 다시 부르는 것은 허용한다 — 그건 «uGUI와 전역 폴링이 겹친» 경우이고,
    ///   그 중복은 각 창의 <c>TryClaimAction</c>(0.35초)이 이미 막고 있다.</item>
    /// </list>
    ///
    /// ============================================================================
    /// 등록은 «지금 클릭을 먹을 수 있는 사각형»이다 — 그리기 사각형이 아니다
    /// ============================================================================
    /// 팝오버는 자기 차단막(<c>BoxCollider2D</c>)이 켜져 있는 동안에만
    /// <see cref="PublishSurface"/>한다. 그 사각형이 곧 «이 앱이 클릭관통을 해제한 영역»이고,
    /// 이 클래스가 답해야 하는 질문(«이 클릭은 누구 것인가»)과 정확히 같은 범위다.
    ///
    /// <para>등록을 <b>거두는 것을 잊는</b> 경로(창이 파괴됐다 / 예외로 <c>Update</c>가 끊겼다)를
    /// 대비해 <see cref="StaleFrames"/>가 지난 등록은 스스로 무효가 된다. 「닫혔는데 그 자리가
    /// 영원히 클릭을 삼킨다」가 이 기구가 만들 수 있는 최악이고, 그건 원래 고치려던 것보다 나쁘다.</para>
    ///
    /// ============================================================================
    /// 프레임 번호를 <b>인자로 받는</b> 오버로드가 있는 이유
    /// ============================================================================
    /// <c>Time.frameCount</c>는 EditMode에서 <b>전진하지 않는다</b>. 프레임 게이트를 EditMode로
    /// 검증하려면 시간을 밖에서 넣을 수 있어야 한다 —
    /// <see cref="PopoverPanel.ResolvePanelSizePoints"/>가 화면 인셋을 인자로 받는 것과 같은 이유이고,
    /// 같은 이유로 여기에도 <c>Screen</c>·<c>#if</c>가 없다.
    /// </summary>
    public static class UiClickArbiter
    {
        /// <summary>등록이 이만큼 지난 프레임의 것이면 무시한다.
        /// <para>표면들은 자기 <c>Update</c>에서 매 프레임 등록하므로 정상 상태에서는 항상 0이다.
        /// 2가 아니라 4인 이유: <c>Update</c> 순서에 따라 «질문이 등록보다 먼저» 오는 프레임이
        /// 정상적으로 존재하고(포스트잇이 팝오버보다 먼저 도는 프레임), 그때 답을 잃으면
        /// 이 기구가 <b>가끔만</b> 동작한다 — 이 저장소가 제일 싫어하는 형태다.</para></summary>
        public const int StaleFrames = 4;

        // ==================== 층 — <b>숫자를 베끼지 않는다</b> ====================
        //
        // 여기 있는 것은 캔버스 sortingOrder의 사본이 아니다(그 상수들은 각 표면의 private이다).
        // 이 클래스가 답해야 하는 질문은 «누가 위인가»라는 <b>순서</b>뿐이고, 절댓값은 필요 없다.
        // 모형과 실제가 갈라지는 것은 두 표면의 CanvasSortingOrderForTests를 대조하는 테스트가 막는다
        // — 숫자를 베꼈다면 그 대조가 «같은 숫자를 두 번 읽는» 무의미한 확인이 됐을 것이다.

        /// <summary>사용자가 열지 않은 <b>상시</b> 표면(포스트잇 카드). 가장 아래다.</summary>
        public const int LayerAmbientCard = 100;

        /// <summary>사용자가 <b>직접 연</b> 창·팝오버. 상시 표면을 덮고, 덮은 자리의 클릭도 가져간다.</summary>
        public const int LayerOpenedPanel = 200;

        private struct SurfaceEntry
        {
            public string Id;
            public int SortingOrder;
            public Rect ScreenRect;
            public int Frame;
        }

        private static readonly List<SurfaceEntry> Surfaces = new List<SurfaceEntry>(4);

        private static int _claimFrame = int.MinValue;
        private static string _claimSurfaceId;

        // ==================== 등록 ====================

        /// <summary>이 표면이 <paramref name="screenRect"/>(Unity 스크린 픽셀) 안의 클릭을 먹는다고 알린다.
        /// 열려 있는 동안 <b>매 프레임</b> 부른다(값이 같아도 프레임 도장을 새로 찍어야 한다).</summary>
        public static void PublishSurface(string surfaceId, int sortingOrder, Rect screenRect, int frame)
        {
            if (string.IsNullOrEmpty(surfaceId)) return;

            for (int i = 0; i < Surfaces.Count; i++)
            {
                if (!string.Equals(Surfaces[i].Id, surfaceId, System.StringComparison.Ordinal)) continue;
                Surfaces[i] = new SurfaceEntry
                {
                    Id = surfaceId, SortingOrder = sortingOrder, ScreenRect = screenRect, Frame = frame,
                };
                return;
            }

            Surfaces.Add(new SurfaceEntry
            {
                Id = surfaceId, SortingOrder = sortingOrder, ScreenRect = screenRect, Frame = frame,
            });
        }

        /// <inheritdoc cref="PublishSurface(string,int,UnityEngine.Rect,int)"/>
        public static void PublishSurface(string surfaceId, int sortingOrder, Rect screenRect)
            => PublishSurface(surfaceId, sortingOrder, screenRect, Time.frameCount);

        /// <summary>이 표면은 더 이상 클릭을 먹지 않는다(닫힘 · 비활성 · 파괴).
        /// <b>등록을 지우는 것이지 클릭을 포기하는 것이 아니다</b> — 이미 잡은 프레임 도장은 그대로 둔다.</summary>
        public static void WithdrawSurface(string surfaceId)
        {
            if (string.IsNullOrEmpty(surfaceId)) return;
            for (int i = Surfaces.Count - 1; i >= 0; i--)
            {
                if (string.Equals(Surfaces[i].Id, surfaceId, System.StringComparison.Ordinal)) Surfaces.RemoveAt(i);
            }
        }

        // ==================== 판정 ====================

        /// <summary>
        /// 이 클릭을 <paramref name="surfaceId"/>가 먹어도 되는가 — <b>지점까지 보는</b> 판정
        /// (전역 폴링 경로용). 다음 둘 중 하나라도 걸리면 false다:
        /// <list type="bullet">
        /// <item>더 높은 <c>sortingOrder</c>의 다른 표면이 <paramref name="screenPoint"/>를 덮고 있다</item>
        /// <item>이 프레임에 이미 <b>다른</b> 표면이 클릭을 먹었다</item>
        /// </list>
        /// </summary>
        public static bool TryClaimClick(string surfaceId, int sortingOrder, Vector2 screenPoint, int frame)
        {
            if (IsCoveredByHigherSurface(surfaceId, sortingOrder, screenPoint, frame)) return false;
            return TryClaimFrame(surfaceId, frame);
        }

        /// <inheritdoc cref="TryClaimClick(string,int,UnityEngine.Vector2,int)"/>
        public static bool TryClaimClick(string surfaceId, int sortingOrder, Vector2 screenPoint)
            => TryClaimClick(surfaceId, sortingOrder, screenPoint, Time.frameCount);

        /// <summary>
        /// 이 프레임의 클릭을 <paramref name="surfaceId"/>가 잡는다 — <b>지점을 모르는</b> 경로용
        /// (uGUI <c>Button.onClick</c>. 그쪽은 <c>GraphicRaycaster</c>가 z-순서를 이미 풀었으므로
        /// 공간 판정이 필요 없고, 시간 판정만 더한다).
        /// <para>같은 표면이 같은 프레임에 다시 부르면 <b>true</b>다 — 한 표면 안의 두 입력 경로가
        /// 겹친 경우이고, 그 중복은 그 창의 <c>TryClaimAction</c>이 막는다. 여기서 막으면
        /// 이 클래스가 남의 일까지 하게 되고 두 판정이 서로를 모르는 채 겹친다.</para>
        /// </summary>
        public static bool TryClaimFrame(string surfaceId, int frame)
        {
            if (string.IsNullOrEmpty(surfaceId)) return true;

            if (_claimFrame == frame)
            {
                return string.Equals(_claimSurfaceId, surfaceId, System.StringComparison.Ordinal);
            }

            _claimFrame = frame;
            _claimSurfaceId = surfaceId;
            return true;
        }

        /// <inheritdoc cref="TryClaimFrame(string,int)"/>
        public static bool TryClaimFrame(string surfaceId) => TryClaimFrame(surfaceId, Time.frameCount);

        /// <summary>
        /// <paramref name="screenPoint"/>를 <b>더 위에 있는 다른 표면</b>이 덮고 있는가.
        /// <b>순수 조회다</b> — 아무것도 잡지 않는다(테스트가 «왜 거절됐는가»를 따로 물을 수 있게).
        /// </summary>
        public static bool IsCoveredByHigherSurface(string surfaceId, int sortingOrder,
            Vector2 screenPoint, int frame)
        {
            for (int i = 0; i < Surfaces.Count; i++)
            {
                SurfaceEntry entry = Surfaces[i];
                if (string.Equals(entry.Id, surfaceId, System.StringComparison.Ordinal)) continue;
                if (frame - entry.Frame > StaleFrames) continue;      // 거두는 것을 잊은 등록.
                if (entry.SortingOrder <= sortingOrder) continue;     // 나보다 아래거나 같은 층.
                if (!entry.ScreenRect.Contains(screenPoint)) continue;
                return true;
            }
            return false;
        }

        /// <inheritdoc cref="IsCoveredByHigherSurface(string,int,UnityEngine.Vector2,int)"/>
        public static bool IsCoveredByHigherSurface(string surfaceId, int sortingOrder, Vector2 screenPoint)
            => IsCoveredByHigherSurface(surfaceId, sortingOrder, screenPoint, Time.frameCount);

        // ==================== 진단/테스트 창구 ====================

        /// <summary>지금 등록돼 있는 표면 수 — <b>0이면 이 기구는 아무것도 안 막는다</b>.
        /// 그 공허한 통과를 테스트가 단언으로 막는다.</summary>
        public static int PublishedSurfaceCountForTests => Surfaces.Count;

        /// <summary>이번 프레임의 클릭을 잡은 표면(없으면 null).</summary>
        public static string LastClaimSurfaceIdForTests => _claimSurfaceId;

        /// <summary>그 도장이 찍힌 프레임 번호.</summary>
        public static int LastClaimFrameForTests => _claimFrame;

        /// <summary>정적 상태를 비운다. 이 클래스는 <c>static</c>이라 테스트 사이에 샌다 —
        /// 앞 테스트의 등록이 남아 뒤 테스트를 조용히 거절하면 그게 곧 거짓 빨강이다.</summary>
        public static void ResetForTests()
        {
            Surfaces.Clear();
            _claimFrame = int.MinValue;
            _claimSurfaceId = null;
        }
    }
}
