using System.Collections.Generic;
using UnityEngine;

namespace StickMate.Platform
{
    /// <summary>
    /// ★ 사용자 신고 버그(2026-08-31): "창이 겹쳐있을때 창이 뒤에 있음에도 그 경계면을 따라 걸음."
    ///
    /// ============================================================================
    /// 이 클래스가 생긴 이유 — 같은 결함을 플랫폼마다 두 번 고치지 않기 위해서
    /// ============================================================================
    /// 2026-08-28 라운드에서 macOS는 이미 이 버그를 고쳤다(가려진 창의 상단 테두리를 발판에서
    /// 제거). 그런데 그 수정은 <b>MacWindowService.BuildVisibleTopEdgeFootholds()라는 private
    /// 메서드 안에 통째로 갇혀 있었다.</b> 그 클래스는 파일 전체가 macOS 전용 P/Invoke라
    /// (1) Windows 구현이 재사용할 수 없었고 (2) macOS가 아닌 환경에서는 컴파일조차 되지 않아
    /// 테스트로 겨냥할 수도 없었다. 그 결과:
    ///   · Win32WindowService.EnumerateFootholds()에는 가려짐 계산이 <b>한 줄도 없다</b> —
    ///     EnumWindows가 돌려준 창 전체 사각형이 그대로 발판이 된다.
    ///   · 그래서 Windows에서는 앞 창에 완전히 덮인 뒤 창의 상단선이 계속 유효한 발판으로 남고,
    ///     캐릭터가 사용자 눈에 보이지 않는 경계를 따라 걷는다 = 이번 신고 그대로다.
    ///
    /// 그래서 알고리즘을 <b>플랫폼 중립 순수 계산</b>으로 끌어내 한 곳에 둔다. 이제:
    ///   · macOS/Windows 두 구현체가 같은 코드를 부른다(한쪽만 고쳐지는 재발 경로가 사라진다).
    ///   · OS 호출이 전혀 없으므로 <b>이 개발 환경(macOS)에서도 Windows 시나리오를 실측 검증</b>할 수
    ///     있다 — Tests/EditMode/VisibleTopEdgeSolverTests.cs가 그 실측이다.
    ///
    /// ============================================================================
    /// 알고리즘 — "z-order를 실제로 활용해 눈에 보이는 상단 테두리 조각만 남긴다"
    /// ============================================================================
    /// 입력은 z-order <b>앞→뒤</b> 순서의 창 사각형 목록이다(이 순서가 계약의 전부다. macOS는
    /// CGWindowListCopyWindowInfo(OnScreenOnly), Windows는 EnumWindows가 그 순서를 보장한다).
    /// 창 i의 상단선은 높이 r.y의 가로 구간 [r.x, r.x+r.width]이고, 여기서 <b>i보다 앞에 있는
    /// 창 j 중 그 높이를 세로로 품는 것들의 가로 구간</b>을 빼면 남는 것이 곧 "실제로 눈에 보이는
    /// 상단 테두리"다. 남은 조각이 여럿이면 조각마다 발판을 하나씩 낸다(핸들은 원본 창 그대로 —
    /// ParkourClimb의 핸들 추적/진단 로그가 그대로 동작한다). 조각이 하나도 없으면 그 창은 발판을
    /// 내지 않는다 = 그 위에 서 있던 캐릭터는 낙하한다(의도된 동작).
    ///
    /// 비용: 창 수 n에 대해 O(n^2) 사각형 연산. n은 보통 수십이고 폴링 주기(0.3초)마다 한 번만
    /// 도므로 무시 가능하다. 모든 버퍼를 재사용하므로 워밍업 이후 <b>할당 0</b>이다
    /// (24시간 상주 앱 GC 압박 방지 컨벤션).
    ///
    /// 읽기 전용 원칙(CLAUDE.md 3): 이 클래스는 순수 사각형 산술만 한다. OS 호출도, 타 프로세스
    /// 창에 대한 어떤 조작도 없다 — 애초에 그럴 수단(핸들)을 입력으로 받지도 않는다.
    /// </summary>
    public sealed class VisibleTopEdgeSolver
    {
        // 입력: z-order 앞->뒤 창 사각형(좌상단 원점, OS 좌표).
        private readonly List<Rect> _windows = new List<Rect>(64);

        // 출력 1: 창별로 "가려짐 계산 후 남은 상단 테두리 총 폭". 0이면 완전히 가려진 창이다.
        private readonly List<float> _visibleWidth = new List<float>(64);

        // 출력 2: 채택된 조각들. 창 순서(바깥 루프) -> 조각 순서(안쪽 루프)로 쌓이므로,
        // 목록의 첫 항목이 곧 "가장 앞에서 실제로 보이는" 발판이다(IsTopmost 판정 근거).
        private readonly List<int> _segWindowIndex = new List<int>(64);
        private readonly List<float> _segStart = new List<float>(64);
        private readonly List<float> _segWidth = new List<float>(64);

        // 작업용 버퍼(창 하나를 푸는 동안의 구간 목록). 재사용 — 매 폴링 할당 금지.
        private readonly List<float> _workStarts = new List<float>(16);
        private readonly List<float> _workEnds = new List<float>(16);
        private readonly List<float> _tmpStarts = new List<float>(16);
        private readonly List<float> _tmpEnds = new List<float>(16);

        /// <summary>Solve()에 넘긴 창 개수(= AddWindow 호출 횟수).</summary>
        public int WindowCount => _windows.Count;

        /// <summary>Solve() 결과로 채택된 "보이는 상단 테두리 조각" 개수.</summary>
        public int SegmentCount => _segWindowIndex.Count;

        /// <summary>조각 s가 어느 입력 창(AddWindow 순서 인덱스)에서 나왔는지.</summary>
        public int GetSegmentWindowIndex(int s) => _segWindowIndex[s];

        /// <summary>조각 s의 왼쪽 X(OS 좌표).</summary>
        public float GetSegmentStartX(int s) => _segStart[s];

        /// <summary>조각 s의 폭(OS 좌표).</summary>
        public float GetSegmentWidth(int s) => _segWidth[s];

        /// <summary>
        /// 창 w의 "보이는 상단 테두리" 총 폭. <b>0이면 그 창은 다른 창에 완전히 가려져 발판을
        /// 하나도 내지 못했다</b>는 뜻이다(진단 로그와 "완전히 가려짐" 집계의 유일한 판별 근거).
        /// </summary>
        public float GetVisibleWidth(int w) => _visibleWidth[w];

        /// <summary>새 열거 패스를 시작한다. 이전 입력/출력을 모두 비운다.</summary>
        public void Begin()
        {
            _windows.Clear();
            _visibleWidth.Clear();
            _segWindowIndex.Clear();
            _segStart.Clear();
            _segWidth.Clear();
        }

        /// <summary>
        /// 창 하나를 입력에 추가한다. <b>반드시 z-order 앞→뒤 순서로</b> 넣어야 한다 —
        /// 이 순서가 뒤집히면 "앞 창이 뒤 창에 가려지는" 정반대 결과가 나온다.
        /// 발판 후보에서 이미 탈락한 창(투명/너무 작음/최소화 등)은 넣지 않는다: 이 클래스는
        /// 가리는 쪽과 가려지는 쪽을 같은 목록으로 취급하므로, 넣으면 그 창도 발판이 된다.
        /// </summary>
        public void AddWindow(Rect screenRect)
        {
            _windows.Add(screenRect);
            _visibleWidth.Add(0f);
        }

        /// <summary>
        /// 가려짐을 계산해 "보이는 상단 테두리 조각" 목록을 만든다.
        /// </summary>
        /// <param name="minVisibleWidth">
        /// 남은 조각이 이보다 좁으면 버린다. 캐릭터 몸통 폭보다 훨씬 좁은 조각 위에 서 있게 하면
        /// "허공에 떠 있다"는 사용자 인식이 그대로 재발하기 때문이다.
        /// </param>
        /// <param name="hasClipBounds">
        /// 발판을 화면 안쪽으로 잘라낼지. true면 clipBounds의 좌우 밖으로 뻗은 부분을 버려서
        /// 배회 AI가 인식하는 "발판 끝"이 항상 화면 안이 되게 한다(캐릭터가 걸어서 화면 밖으로
        /// 나가는 경로 자체를 없앤다).
        /// </param>
        /// <param name="clipBounds">화면(디스플레이) 사각형. hasClipBounds가 false면 무시된다.</param>
        public void Solve(float minVisibleWidth, bool hasClipBounds, Rect clipBounds)
        {
            _segWindowIndex.Clear();
            _segStart.Clear();
            _segWidth.Clear();

            for (int i = 0; i < _windows.Count; i++)
            {
                Rect r = _windows[i];
                _visibleWidth[i] = 0f;

                float left = r.x;
                float right = r.x + r.width;
                if (hasClipBounds)
                {
                    left = Mathf.Max(left, clipBounds.x);
                    right = Mathf.Min(right, clipBounds.x + clipBounds.width);
                }
                // 화면 밖으로 완전히 나갔거나 애초에 폭이 0 이하 — 조각이 있을 수 없다.
                if (right - left < minVisibleWidth) continue;

                _workStarts.Clear();
                _workEnds.Clear();
                _workStarts.Add(left);
                _workEnds.Add(right);

                // 나보다 앞(작은 인덱스)에 있는 창만 나를 가릴 수 있다.
                for (int j = 0; j < i && _workStarts.Count > 0; j++)
                {
                    Rect o = _windows[j];

                    // 가리는 창이 내 상단선 "높이"를 세로로 품지 않으면 내 상단 테두리에 영향이 없다.
                    // (창 내부를 덮는 것과 상단선을 덮는 것은 다르다 — 우리가 발판으로 쓰는 것은
                    //  오직 상단선 하나뿐이므로 그 한 줄만 판정하면 충분하고, 그래야 정확하다.)
                    if (r.y < o.y || r.y > o.y + o.height) continue;

                    float oL = o.x;
                    float oR = o.x + o.width;

                    _tmpStarts.Clear();
                    _tmpEnds.Clear();
                    for (int k = 0; k < _workStarts.Count; k++)
                    {
                        float sx = _workStarts[k];
                        float ex = _workEnds[k];
                        if (oR <= sx || oL >= ex)
                        {
                            _tmpStarts.Add(sx); _tmpEnds.Add(ex); // 겹치지 않음 — 그대로 통과
                            continue;
                        }
                        if (sx < oL) { _tmpStarts.Add(sx); _tmpEnds.Add(oL); } // 왼쪽 잔여
                        if (oR < ex) { _tmpStarts.Add(oR); _tmpEnds.Add(ex); } // 오른쪽 잔여
                    }

                    _workStarts.Clear(); _workEnds.Clear();
                    for (int k = 0; k < _tmpStarts.Count; k++)
                    {
                        _workStarts.Add(_tmpStarts[k]); _workEnds.Add(_tmpEnds[k]);
                    }
                }

                float total = 0f;
                for (int k = 0; k < _workStarts.Count; k++)
                {
                    float width = _workEnds[k] - _workStarts[k];
                    if (width < minVisibleWidth) continue;
                    total += width;
                    _segWindowIndex.Add(i);
                    _segStart.Add(_workStarts[k]);
                    _segWidth.Add(width);
                }
                _visibleWidth[i] = total;
            }
        }
    }
}
