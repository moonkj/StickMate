# StickMate 산출물·시험결과 추적

> **리더(Architect) 소유.** 팀원 읽기 전용. 사용자 지시로 신설(2026-09-02):
> *"총합시험결과 및 각 산출물 추적관리하는 문서 만들어서 지속 업데이트"*
>
> ★ **`MILESTONES.md`는 「얼마나 왔나」, 이 문서는 「무엇이 나왔고 검증됐나」다.**
> ★ **`docs/TEAM.md` 상시 의무**: 여기 적을 때도 **판정과 코드 반영을 갈라 적는다.**

## 총합 시험 결과 (최신)

| 항목 | 값 | 시각 | 타깃 |
|---|---:|---|---|
| EditMode | **1517건 / 실패 0 / 건너뜀 12** | 15:22 | `UNITY_STANDALONE_WIN` |
| PlayMode | 570건 / **실패 5** (14:46엔 9) | 15:44 | 〃 |
| 크로스컴파일 `xcheck win` | 5개 어셈블리 errors=0 | 상시 | — |
| 크로스컴파일 `xcheck osx` | 5개 어셈블리 errors=0 | 상시 | — |

★ **오늘의 모든 배치 초록은 macOS 전용 파일을 한 줄도 컴파일하지 않은 상태에서 나왔다.**
컴파일된 DLL 실측: `CGGetActiveDisplayList`·`NSWindow` **0건** / `SHAppBarMessage`·`EnumDisplayMonitors` 존재.
**타깃 전환 후 재측정 전까지 macOS 초록을 초록이라 부르지 않는다.**

### PlayMode 실패 9건의 귀속
| 건수 | 정체 | 상태 |
|---:|---|---|
| ~~4~~ | ~~캐러셀 폭 미반영~~ | ✅ **초록**(15:44). 헤더 592 잔존 + 잡는 지점 |
| ~~1~~ | ~~`SuspendAbsoluteDeadlineTests` 매처 눈멂~~ | ✅ **초록** |
| 1 | `ManualHideUserAxisTests` — 설정창 안 열림 | 설정창 배선 라운드 대기 |
| 2 | 펫(달팽이·풍선) | **HEAD부터 빨강**, 별건 |
| 1 | `DialogueComicTextPlacementTests` 기울기 | ★ **12:01부터 빨강, 미배정** |
| 1 | `LiveObjectGrowthGuardTests.G1` | ★ **신규.** 격리 통과·전량 실패 → **순서 의존 누적 의심.** `qa-regression` 배정 필요 |

## 산출물 — 설계 (구현 대기)

| 문서 | 담당 | 상태 |
|---|---|---|
| `design/systems/ECONOMY_SPEC.md` | design-systems | 교정 4/4. **42종 전제로 재검산 중** |
| `design/art/PALETTE_SPEC.md` §12~15 | design-art | 등급 4단계 + 6팩 12색 확정 |
| `design/equipment/` 털모자·안경·망토 처방 | design-equipment | 양성 대조 6종 |
| `design/motion/` R4 | design-motion | 기동 전이 + 뒷걸음 포즈 |
| `design/narrative/` R2·R3 + `SPEC_가독예산_언어인식.md` | design-narrative | **N=24 자기 정정** |
| `docs/UX_FLOW.md` §48~50 | ux-designer | 입구 안내 · 모니터 행 · 상점 탭 |
| `docs/UX_WIDGETS.md` | ux-widgets | 팝오버 3종 고도화(진행) |
| `docs/localization/PLAN_1.0.md` | localization | **부채 485건 확정** |
| `docs/security/SECURITY_MODEL.md` | security | 위협 3층 분리 |
| `docs/GAME_ARCHITECTURE_REVIEW.md` §7 | game-architect | 착수 순서 확정판 |
| `docs/SCREEN_SHARE_DETECTION.md` | dev-platform | S1 2등급 분할 |

## 검증 도구 (교정 통과한 것만)

| 도구 | 교정 |
|---|---|
| `design/art/verify/colorlab.py` | 16건 |
| `design/art/verify/cvd.py` | 13건(무게중심 C1 무채축 불변) |
| `Tools/PaletteVerify/*.py` 4종 | 대역/ΔE/골든/글리프, 각 4~8건 |
| `design/narrative/verify/*.py` 6종 | 6/6 |
| `docs/localization/verify/{census,ship}.py` | 각 10/10 |
| `design/equipment/verify/wornfix.py` | 양성 대조 6종 |

## ★ 미반영 (판정만 됨)

| 판정 | 코드 | 배정 |
|---|---|---|
| 「이벤트」 탭 폐지 | ❌ `Tab.Event`·`TabCount=5` 그대로 | **미배정** |
| 판매 범위 42종 | ❌ | design-systems 재검산 중 |
| DLC 1.0 포함 | ❌ 순서만 갱신 | 트랙 4개로 분할됨 |
| 등급 색 처방 C | ❌ | coder 진행 중 |

## 갱신 이력
- **2026-09-02** 신설.
