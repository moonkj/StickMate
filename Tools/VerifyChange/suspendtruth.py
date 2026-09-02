#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""verify-change 독립 진리표 — "등급 2 동작이 예전과 전수 동치"를 내가 다시 만든다.
소스를 읽어 만든 것이 아니라, 읽은 뒤 **내 말로 다시 적은** 두 모델을 전수 대조한다.
교정: 알려진 두 값(설정 OFF면 절대 안 숨는다 / 전체화면 게임 + 설정 ON이면 숨는다)."""
import itertools, sys

# ---- 예전(HEAD eca8c58, StickmanAgent.TickFullscreenSuspend) ----
#   bool fullscreenActive = _platformService.IsFullscreenAppActive() && AppSettingsModel.AutoHideOnFullscreen;
#   if (fullscreenActive && !_isSuspended) Suspend(); else if (!fullscreenActive && _isSuspended) Resume();
#   IsFullscreenAppActive() (HEAD Mac/Win) = debounce(EvaluateFullscreen) 이고
#   EvaluateFullscreen 는 기하불일치/조회실패면 false, 일치하면 isGame 을 돌려준다 = covers AND game.
def old_suspend(auto_hide, covers, game):
    raw = covers and game               # HEAD EvaluateFullscreen 의 반환값
    verdict = raw                       # 디바운스 안정 상태(양쪽 판이 같은 디바운서·같은 입력)
    return verdict and auto_hide

# ---- 지금(작업트리) ----
#   tier = Resolve(coversVerdict, verdict);  verdict = debounce(rawCovers && rawGame)
#   _fullscreenAutoHide = AutoHideOnFullscreen && SuspendsCharacter(tier)
#   _fullscreenPanelRetreat = AutoHideOnFullscreen && RetreatsPanels(tier)
#   shouldSuspend = _fullscreenAutoHide || _userHidden
#   ArePanelsSuppressed = _isSuspended || _fullscreenPanelRetreat
def resolve(covers, game):
    if not covers: return "None"
    return "Full" if game else "PanelsOnly"
def suspends_character(t): return t == "Full"
def retreats_panels(t):    return t != "None"

def new_state(auto_hide, covers, game, user_hidden):
    tier = resolve(covers, covers and game)      # verdict = rawCovers and rawGame
    axis1 = auto_hide and suspends_character(tier)
    axis3 = auto_hide and retreats_panels(tier)
    is_susp = axis1 or user_hidden
    return is_susp, (is_susp or axis3), tier

def calibrate():
    f=[]
    if old_suspend(False, True, True):      f.append("설정 OFF인데 예전 판이 숨는다")
    if not old_suspend(True, True, True):   f.append("전체화면 게임 + 설정 ON인데 예전 판이 안 숨는다")
    if new_state(False, True, True, False)[0]:      f.append("설정 OFF인데 새 판이 숨는다")
    if not new_state(True, True, True, False)[0]:   f.append("전체화면 게임 + 설정 ON인데 새 판이 안 숨는다")
    # 2026-08-31 신고 회귀 가드: 전체화면 '엑셀'(게임 아님)에서 캐릭터가 사라지면 안 된다
    if new_state(True, True, False, False)[0]:      f.append("★ 등급1에서 캐릭터가 숨는다 = 2026-08-31 신고 회귀")
    return f

if __name__ == "__main__":
    fails = calibrate()
    if fails:
        print("교정 실패 — 이후 표 폐기:"); [print("  ", x) for x in fails]; sys.exit(2)
    print("교정 통과 (설정OFF / 게임전체화면 / 등급1 캐릭터 유지)\n")
    print(f"{'AutoHide':>8} {'덮음':>4} {'게임':>4} {'사용자숨김':>8} | {'예전 숨김':>8} {'지금 숨김':>8} {'동치':>4} | {'등급':>10} {'표면걷기':>7}")
    print("-"*88)
    bad = 0
    for a, c, g, u in itertools.product([False,True],repeat=4):
        o = old_suspend(a,c,g)
        n, panels, tier = new_state(a,c,g,u)
        # 예전에는 사용자숨김 축 자체가 없었다 -> u=False 인 행만 동치 비교 대상이다
        same = "-" if u else ("OK" if o==n else "★불일치")
        if u is False and o != n: bad += 1
        print(f"{str(a):>8} {str(c):>4} {str(g):>4} {str(u):>8} | {str(o):>8} {str(n):>8} {same:>4} | {tier:>10} {str(panels):>7}")
    print("-"*88)
    print(f"사용자숨김=False 8개 조합 중 불일치 {bad}건")
    print(f"(AutoHide, 덮음, 게임) 4조합만 보면: " +
          ", ".join(f"{(a,c)}->{old_suspend(a,c,True)}" for a,c in itertools.product([False,True],repeat=2)))
