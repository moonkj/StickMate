# 캡처 규율 — 홍보 소재는 이렇게만 찍는다

작성: marketing / 2026-09-02 · **R11 갱신 2026-09-14**(2-3 저장 초기화를 **폴더 단위**로 교체. R5·R6·R9 추가분도 본문에 있다)
· **R12 갱신 2026-09-14**(2-3 R5 `PlayerPrefs` 절차 교체 — 빌드 대상은 `com.Vibelab.StickMate`, macOS는 `defaults`로만, **테이크마다** 다시 비움, Windows 키 통째 삭제·남의 `.reg` 금지)
**★ 아직 한 장도 찍지 않았다.** 이 문서는 **찍기 전에 리더 승인을 받기 위한 절차서**다.
사람이 할 일 / 자동화할 일의 분리와 우선순위는 `ROADMAP.md` §4·§6.

---

## 0. 절대 금지 4가지

| # | 금지 | 이유 |
|---|---|---|
| 1 | **목업을 제품 스크린샷으로 쓰기** | 이 저장소는 "문서가 실재와 갈라진" 사고가 반복됐다. 밖으로 나가면 사기다 |
| 2 | **사용자 실사용 화면 캡처** | 남의 창 제목·파일명·메신저 내용이 그대로 찍힌다 |
| 3 | **되지 않는 기능 연출** | `MOMENTS.md` / `TRUTH_INVENTORY.md`의 A·B등급 밖은 찍지 않는다 |
| 4 | **`driver.sh stop` / 전역 단축키 `Q`** | 전역이라 **사용자가 쓰는 인스턴스까지 죽는다** |

---

## 1. ★★ 지금 있는 캡처 도구는 마케팅에 쓸 수 없다 (실측)

`.claude/skills/run-stickmate/driver.sh`의 `shot` 명령을 읽었다.

```
247:  screencapture -x "$full" || die "전체 캡처 실패(화면 기록 권한 확인)"
249:  screencapture -x -R 0,"$band_y",1512,180 "$band" || die "띠 캡처 실패"
```

- **`screencapture -x`는 화면 전체를 찍는다.** 이 개발 머신에는 사용자의 실제 창·파일명·
  브라우저 탭이 떠 있다. 그 결과물은 **금지 2번에 정면으로 걸린다.**
- 이 도구는 **디버깅·검증용으로는 정확히 옳다**(`design/motion/`, `design/equipment/`의 검증
  캡처들이 그 용도로 잘 쓰이고 있다). **바꾸자는 게 아니라, 마케팅이 그걸 쓰면 안 된다는 것이다.**
- 띠 캡처(`-R 0,band_y,1512,180`)도 안전하지 않다 — 그 띠에 Dock과 다른 앱 창이 들어온다.

**결론: 홍보 캡처는 별도 절차가 필요하고, 그 절차는 사람이 화면을 준비해야 성립한다.**

### 1-1. ★ R2 — 사람만 할 수 있는 것 / 자동화할 수 있는 것

전체 표와 배정 요청은 `ROADMAP.md` §4. 요약만 여기 둔다.

| | 항목 | 왜 |
|---|---|---|
| **사람** | 전용 계정 생성·정리·알림 끄기 / 캡처 도구에 화면 기록 권한 부여 / **소재 세트 창 배치** / Windows 실기 전부 | — |
| **자동** | 사전 청결 검사(창 소유 앱 이름 허용목록) / 영역 한정 캡처 / **테이크 검수(2-5절)** / 빌드 동일성 / 결과물 메모 | 전부 **권한 0**으로 가능하다 |

★ **두 가지를 못박는다.**
1. **화면 기록 권한을 받는 것은 캡처 도구이지 StickMate가 아니다.** 이 구분이 흐려지면
   "어떤 OS 권한도 요구하지 않는다"가 우리 내부에서부터 무너진다.
2. **창 배치는 자동화하지 않는다.** 창을 옮기는 코드는 우리 앱에 존재하지 않고
   (`UserAssetImmutabilityAuditTests`가 자동 검사한다) **만들어서도 안 된다.**
   캡처 편의는 원칙 3보다 싸다.

---

## 2. 깨끗한 캡처 환경 — 체크리스트

**이 목록을 전부 통과하지 않은 캡처는 폐기한다.** 하나라도 미확인이면 그 캡처의 나머지도 무효다
(TEAM.md 4절 "하나라도 깨지면 그 파일의 0건 전부 무효"와 같은 규율).

> ### ★★★ R6 (2026-09-05) — **이 절은 macOS를 전제로 쓰였다. 스토어 컷은 이제 Windows다**
> 아래 2-1~2-4는 macOS 화면을 전제로 한 항목이 섞여 있다(Space·Dock·메뉴바·`~/Library/...`).
> **스토어 슬롯에 들어가는 캡처는 Windows 화면이어야 한다**(`CAPTURE_REQUESTS_R6.md` 1절).
> ⇒ **macOS 항목을 지우지 않는다**(개발일지·내부 판정 캡처에 그대로 쓴다).
> **Windows 판을 2-0으로 새로 얹는다.**

### 2-0. ★★ Windows 캡처 환경 (R6 신설) — **촬영 주체는 사용자다**

이 머신에 Windows 실행 환경이 없다. 아래는 **사용자에게 건네는 체크리스트**이고,
**한 항목이라도 미확인이면 그 회차 전량 폐기**다.

**(가) 화면에서 새는 것 — macOS와 다른 자리**
- [ ] **작업표시줄에 개인 앱이 고정돼 있지 않은가.** ★ 우리 앱은 **작업표시줄 바로 위에 선다** —
      헤드라인 컷의 하단 1/5이 통째로 작업표시줄이다. **거기 찍힌 아이콘·알림 배지가 그대로 나간다**
- [ ] **알림 영역(트레이) 정리.** 백신 아이콘·업무 메신저 배지가 특히 위험하다
- [ ] **작업표시줄 검색창/위젯 끄기** — 날씨 위젯에 **지역명**이 뜬다
- [ ] **알림 끄기(집중 지원)**. 토스트가 뜬 테이크는 폐기
- [ ] 바탕화면 아이콘 0개 · 배경화면 단색 · 브라우저 사용 금지(macOS와 동일)
- [ ] ★ **사용자 계정 이름이 나오는 자리 전부**: 시작 메뉴, 파일 탐색기 주소줄, 창 제목

**(나) Windows에만 있는 촬영 변수 — 하나씩 기록한다**
- [ ] ★★ **표시 배율**(100 / 125 / 150 / 175%)을 **테이크마다 적는다.** 우리 UI는 이 축에서
      아직 실기 0회다(H-9: 리본 y 시작점 반픽셀·글자 번짐). **macOS는 ×1/×2뿐이라 이 축을 구조적으로 못 본다**
- [ ] ★ **한글 폰트 폴백**(H-10) — 두부(□)가 한 글자라도 보이면 **그 컷은 홍보용으로 못 쓴다.**
      `✕`(U+2715)도 함께 본다
- [ ] ★ **작업표시줄 자동 숨김을 쓰는 계정이면** 우리 앱이 **실행 중에만 해제**한다(승인된 예외 1건).
      **그 상태가 그림에 찍힌다** — `STORE_PAGE.md` 4-1 고지와 그림이 어긋나지 않게 한다
- [ ] ★ **다중 모니터면 어느 화면인지 적는다**(H-13: 좌표 규약이 macOS와 다르다)
- [ ] ★★★ **R9 (2026-09-14) — 촬영 중에는 모니터 연결·분리·도킹·언도킹·프로젝터 연결을 하지 않는다.**
      Windows 사용자 신고에서 이 조작 하나로 흰 화면이 뜨고 **PC 전체가 멈춰 리부팅**해야 했다(원인 진단 중).
      촬영 머신이 멀티모니터면 **앱을 켜기 전에 구성을 고정**하고, 바꿔야 하면 **앱을 트레이에서 종료한 뒤** 바꾼다.
      ★ **절전 복귀·잠금 해제도 같은 경로를 탈 수 있다(진단 대기)** — 판정이 나올 때까지 촬영 중 디스플레이 절전을 끈다.
      ★ 정지가 나면 **그 기계의 작업표시줄 자동 숨김 설정부터 확인한다** — 리부팅은 원복 훅이 돌지 않는 경로다
      (`docs/TASKBAR_REVEAL.md` 2-3). ★ **정지 장면은 소재가 아니다.** 판정·진단용으로만 보관하고 파일명에 용도를 적는다

**(다) 앱 상태 — Windows판 함정**
- [ ] ★★ **`PlayerPrefs`가 레지스트리에 있다** — 빌드된 앱은 **`HKCU\Software\Vibelab\StickMate`**
      (〔R12〕 Unity 6 1차 문서로 확인. 에디터는 `HKCU\Software\Unity\UnityEditor\…`로 **다른 자리**다. **Windows 실기 미확인**).
      「첫 실행」을 찍으려면 저장 폴더만으로는 **안 된다** — 온보딩 「봤음」 기록이 거기 있다. 절차는 **2-3 R5 Windows 절차**.
      ★★★ **이 키를 통째로 지우거나 다른 기계의 `.reg`로 덮지 않는다** — 곧 작업표시줄 원복 **소유 표식**이 같은 키에 저장된다.
      지우면 그 설치의 원복 소유 판정이 끊긴다
- [ ] ★★ **빌드 신선도**: `StickMate.exe` 날짜를 보지 마라(그건 Unity 런처 스텁이다).
      **`StickMate_Data\Managed\StickMate.Runtime.dll`의 날짜**를 본다
- [ ] ★★ **09-03 08:32 빌드로 보관함·카드를 찍지 마라** — 그 빌드는 **유령 단축키 `F`/`J`/`H`를
      아직 사용자 단축키로 광고한다**(오늘 제거됨). 누르면 아무 일도 안 일어나는 키를 가르치는 그림이다
- [ ] **인스턴스 1개.** 세이브를 모든 인스턴스가 공유한다
- [ ] ★ **검증 세션과 촬영을 같은 실행에서 섞지 마라.** 검증은 로그를 보고 촬영은 화면을 본다 —
      진단 로깅을 켠 채로 찍으면 화면·로그에 진단 문자열이 남는다.
      **예외 2건**: 「작업표시줄·Alt+Tab에 안 뜬다」와 「자동 숨김 원복」은 **관측 대상이 화면 그 자체**라 같이 해도 된다

### 2-1. 계정·데스크톱
- [ ] **전용 테스트 사용자 계정** 또는 새 macOS "Space"에서. 개인 계정 홈 화면 금지
- [ ] 바탕화면 아이콘 **0개**(또는 무해한 더미만)
- [ ] 배경화면 = 단색 또는 무난한 기본. **개인 사진 금지**
- [ ] 메뉴바 정리: 개인 앱 아이콘·이름·배터리 외 제거. **시계는 그대로 둬도 된다**
- [ ] 알림 **끄기**(집중 모드). 캡처 중 배너가 뜨면 그 테이크는 폐기
- [ ] Dock: 기본 앱만. 개인 앱·최근 문서 제거
- [ ] 브라우저 사용 금지(탭 제목·북마크·프로필 사진이 새어 나온다)

### 2-2. 화면에 띄울 "남의 창" — 소재 세트
`MOMENTS.md` C4가 창을 요구한다. **무엇을 띄울지 미리 정한다.**

| 세트 | 구성 | 쓰는 소재 |
|---|---|---|
| **SET-A 기본** | 텍스트 편집기 1(빈 문서) + 계산기 1 | M7 창 부수기+관통, 창 위 보행, M3 등반 |
| **SET-B 여백** | 화면 아래쪽에만 창 1개, 위쪽 절반 비움 | M4 그라피티(**96px 빈 영역 필요**) |
| **SET-C 작은 창** | 폭 **280~400pt** 창 1개(예: 계산기·시스템 정보) | M5 창 도둑(**폭 ≥280pt 조건**) |
| **SET-D 넓은 발판** | 폭 넓은 창 1개를 화면 중간에 | M1 활쏘기(**사거리 2.6~6.6H**) |

- [ ] 창 제목이 **무해한가**: `제목 없음`, `계산기` 등. 파일 경로가 제목에 없어야 한다
- [ ] 창 내용이 비어 있거나 무해한가(코드·이메일·업무 문서 금지)
- [ ] **회사·타사 로고와 UI가 크게 찍히지 않는가** — 스토어 심사와 상표 문제

### 2-3. 앱 상태
- [ ] **저장 초기화 여부를 정한다.** ★★★ **R11 (2026-09-14) — 파일 하나가 아니라 저장 폴더를 통째로 옮긴다**
      〔R2~R10의 *"`stickmate_character.json` 본체만 초기화"*는 **폐기**. 같은 절차를 옮겨 적은 `CAPTURE_REQUESTS_R5.md` 3절 1번도 함께 고쳤다〕
      - 첫 실행 연출을 찍으려면 초기화 필요. **사용자 진행도는 지우지 않는다 — 옮겨 두고, 촬영 뒤 되돌린다**
      - ★ **본체만 치우면 안 되는 이유**: 곧 들어갈 세이브 복구 규칙(H2′)은 *본체가 없고, 직전 세대(`stickmate_character.prev.json`)와
        같은 캐릭터이면서 내용상 더 새로운 임시 파일(`stickmate_character.json.<번호>.writing`)이 남아 있으면* 교체 중 끊김으로 보고 **복구한다.**
        본체만 치운 폴더는 그 모양과 겹칠 수 있다 → **"지웠는데 캐릭터가 돌아온다"** → 첫 실행 컷이 **조용히 거짓 소재**가 된다
        (근거: `Tasklist.md` 「[security] 세이브 교체 중 끊김 검토」). 같은 폴더 안에서 `.bak`으로 **이름만 바꾸는 것도 같은 모양**이다.
        H2′가 아직 없는 빌드도 **이 절차로** 찍는다 — 빌드마다 절차를 고르면 언젠가 틀린 쪽을 고른다
      - **폴더 위치** — 경로의 회사명 칸은 그 빌드의 `companyName`을 따른다(현행 `ProjectSettings.asset` = `Vibelab`). **추측하지 말고 그 빌드가 찍은 로그 줄로 확인한다**
        - macOS: `~/Library/Application Support/Vibelab/StickMate/` (이 머신에 실재)
        - Windows: `%USERPROFILE%\AppData\LocalLow\Vibelab\StickMate\`
        - 확인 줄: `[동결기록] 활성 — 폴더 <이 폴더>/FreezeForensics`(양 플랫폼, `Platform/FreezeWatchdog.cs:263`) ·
          Windows는 `[작업표시줄] … 흔적 파일=<이 폴더>\stickmate_reserved_bar_restore.json`도(`Platform/ReservedBarRevealDirector.cs:132`)
      - **폴더 안에 있는 것** (코드 실측 — `persistentDataPath`를 쓰는 프로덕션 파일 6개 전수):
        `stickmate_character.json`(본체, `Core/CharacterSaveStore.cs:37`) · `stickmate_character.prev.json`(직전 세대, `:714`) ·
        `stickmate_character.json.<번호>.writing`(쓰다 끊기면 남는 임시 파일, `:1200,1230`) · `character_save.v<N>.backup.json`(다운그레이드 백업, `:764`) ·
        ★ `stickmate_reserved_bar_restore.json`(작업표시줄 원복 흔적 — Windows만 쓴다, `Platform/ReservedBarRestoreLedger.cs:93`) ·
        `FreezeForensics/`(멈춤 원장 · `session-exit-marker.txt` · `previous-abnormal-player-NN.log`, `Platform/FreezeForensicsPolicy.cs:45` · `Platform/SessionExitMarker.cs:53,63`).
        ★ **Windows는 Unity `Player.log` · `Player-prev.log`도 이 폴더에 있다**(`Platform/Windows/WindowsCompositionProbe.cs:34`) — 폴더와 함께 옮겨진다.
        **멈춤 조사용 로그 회수(`docs/verify/WINDOWS_CHECK_SESSION.md` H-3)가 남아 있으면 그것부터 끝낸다.** macOS 로그는 `~/Library/Logs/Vibelab/StickMate/`라 이 폴더 밖이다
      - **① 모든 인스턴스를 앱 안에서 종료** — Windows 트레이 메뉴 「StickMate 종료」(`Platform/SystemTrayPresencePolicy.cs:182`) 또는 캐릭터 우클릭 부채꼴 「앱 종료」(`Interaction/GearRadialMenuWidget.cs:472`, HEAD `6173b6e`).
        〔R12-9: 옛 명칭 「톱니 부채꼴」 · 줄 `:479`는 낡았다. `0229f52`부터 평소 부채꼴은 **캐릭터 우클릭**으로 편다(`Interaction/AppControlDirector.cs:892`). ★ **수정 E-1 전에는 게임이 아닌 전체화면 앱이 떠 있으면 우클릭이 부채꼴을 열지 않는다**(`:854-857` 게이트가 허가 `:886`보다 앞) — 이때는 전체화면 앱을 먼저 닫고 우클릭하거나 Windows 트레이 「StickMate 종료」를 쓴다〕
        ★ `driver.sh stop` · 전역 `Q` 금지(0절 4번). 확인은 **읽기만**: macOS `pgrep -x StickMate` / Windows `Get-Process StickMate -ErrorAction SilentlyContinue` → **0줄**
        (macOS에서 `-f`를 쓰지 마라 — 프로젝트 경로에 `StickMate`가 든 Unity 러너까지 걸린다).
        ★ 하나라도 살아 있으면 안 된다 — 세이브는 인스턴스끼리 공유하고, 저장은 **폴더가 없으면 다시 만든다**(`Core/CharacterSaveStore.cs:652,1637`).
        살아 있는 인스턴스의 다음 저장(기본 60초 주기)이 **원래 자리에 사용자 캐릭터를 다시 써** 첫 실행 컷이 오염되고, **그 사이 쌓인 진행도는 ⑤에서 버려진다**
      - **② ★ Windows · 작업표시줄 자동 숨김을 쓰는 기계 — 흔적 파일을 옮기면 원복 흔적이 사라진다. 앱을 트레이에서 종료한 뒤에 옮긴다.**
        옮기기 전에 흔적이 닫혀 있는지 본다(`docs/TASKBAR_REVEAL.md` 2-3):
        `$f = "$env:USERPROFILE\AppData\LocalLow\Vibelab\StickMate\stickmate_reserved_bar_restore.json"; if (Test-Path $f) { (Get-Content -Raw -Encoding UTF8 $f | ConvertFrom-Json).active } else { "흔적 없음" }`
        → `False` 또는 `흔적 없음`이면 옮긴다. ★ **`True`면 옮기지 않는다** — 지난 실행(크래시·리부팅)이 못 갚은 빚이다. 흔적째 옮기면 **아무도 갚지 않는다**
        (`docs/COMPANY_RENAME_MIGRATION.md` 0절의 고아 흔적과 같은 경로). 앱을 한 번 켜 로그 `[작업표시줄] ★ 복구 —` 줄과 자동 숨김 복귀를 확인 → 트레이에서 종료 → 다시 `False` 확인.
        (켜는 순간 `Player.log`가 `Player-prev.log`로 밀린다 — 위 로그 회수 주의)
      - **③ 옮긴다** — 백업은 **같은 부모 폴더에 다른 이름으로**(같은 볼륨이라 이름 바꾸기 한 번이다). ★ 바탕화면에 두지 마라 — 2-1 「바탕화면 아이콘 0개」가 깨지고 촬영에 찍힌다
        - macOS: `mv "$HOME/Library/Application Support/Vibelab/StickMate" "$HOME/Library/Application Support/Vibelab/StickMate.capture-backup-$(date +%Y%m%d-%H%M%S)"`
        - Windows: `Rename-Item "$env:USERPROFILE\AppData\LocalLow\Vibelab\StickMate" "StickMate.capture-backup-$(Get-Date -Format yyyyMMdd-HHmmss)"`
        - **실제로 생긴 백업 폴더 이름을 캡처 메모(4절)에 적는다**
      - **④ 켜기 전 확인 — 대조 둘** — 원래 경로가 **없다**(`ls` 실패 / `Test-Path` → `False`) **그리고** 백업 폴더 안에 `stickmate_character.json`이 **보인다**.
        ★ 둘 다 없으면 **경로를 잘못 본 것이다** — 「없음」 하나만 보고 통과시키지 마라(죽은 확인이 산 확인과 똑같이 생긴다)
      - **⑤ 촬영 뒤 되돌리기**
        1. 촬영 인스턴스를 ①과 같이 종료 → 0줄 확인
        2. ★ Windows · 자동 숨김 기계: **촬영 폴더의** 흔적을 ②의 명령으로 본다. `True`면(촬영 중 정지·강제 종료) **되돌리기 전에** 앱을 한 번 켜 갚게 하고 종료 → `False` 확인.
           **촬영 폴더를 치운 뒤에는 그 빚을 갚을 실행이 없다** — 되돌린 사용자 폴더의 흔적은 닫혀 있어 다음 실행이 복구하지 않는다
        3. 촬영 폴더는 **지우지 말고** 이름을 바꿔 치운다(`StickMate.capture-shot-<시각>`) — 촬영 중 멈춤이 있었다면 그 안의 `FreezeForensics/`가 판정 자료다. 필요 없다고 판정된 뒤 사용자가 지운다
        4. 백업 폴더 이름을 `StickMate`로 되돌린다
        5. 확인: `stickmate_character.json`의 크기·수정 시각이 옮기기 전과 같은가
        6. 아래 R5 `PlayerPrefs`도 되돌린다(R5 macOS·Windows 절차 4·5번) — **폴더만 되돌리면 온보딩 「봤음」 기록이 빠진 채 남는다**
      - ★ **레벨 30 / 중간 진행 세이브를 쓰는 컷도 같은 방식이다** — ③까지 한 뒤 **촬영용 폴더를 통째로** `StickMate` 자리에 둔다.
        사용자 폴더 안에서 본체만 바꿔 끼우지 마라 — 사용자 본체를 덮게 되고, 사용자의 `prev.json`·임시 파일이 촬영용 본체 옆에 남아 섞인다.
        ★ **촬영용 폴더를 만들거나 다른 기계·다른 사용자 계정에서 가져올 때 넣지 않는 것** 〔R11-b 2026-09-14 — `game-architect` 「작업표시줄 원복 흔적의 기계 경계」 판정 반영〕
        - `*.writing` 파일 — 중단된 저장의 잔해다(`docs/security/ENTITLEMENT_CONTRACT.md` S-7-6 3번)
        - ★★ **`stickmate_reserved_bar_restore.json`** — **받는 기계의 작업표시줄 자동 숨김 설정을 바꿀 수 있다(원칙 3).**
          흔적에는 기계·계정 식별자가 없고 플랫폼 태그만 대조한다(`Platform/ReservedBarRestoreLedger.cs:34-53,150-154`) — 다른 Windows 기계·계정에서 온 **열린 흔적**을 받은 쪽이 자기 빚으로 읽고 「원복」한다.
          ★ `active` 값을 보고 골라 넣지 마라 — **닫힌 흔적도 넣지 않는다.** 넣어서 얻는 것이 없고(없으면 촬영 인스턴스가 필요할 때 새로 쓴다), 「닫힌 것을 확인했다」가 틀리는 순간 남의 PC 설정이 바뀐다
        - `FreezeForensics/` · `Player.log` · `Player-prev.log` — 원래 기계의 기록이다. 가져온 표지가 `state=running`이면 촬영 인스턴스가 **비정상 종료로 판정해 `previous-abnormal-player-NN.log` 복사본을 만든다**(`Platform/SessionExitMarker.cs:103-114`) —
          없던 사건 기록이 생겨 ⑤-3의 판정 자료가 오염된다. 로그 경로에는 원래 계정 이름이 찍혀 있을 수 있다
        - ★ **⑤-3에서 치운 `StickMate.capture-shot-<시각>`을 다음 촬영의 원본으로 다시 쓸 때도 같다** — 그 안에는 촬영 인스턴스가 쓴 흔적·표지·로그가 들어 있다
        - **넣은 뒤 켜기 전 대조 둘**(④와 같은 형태) — 새 `StickMate` 자리에 `stickmate_character.json`이 **보이고** `stickmate_reserved_bar_restore.json`은 **없다.**
          Windows: `Test-Path "$env:USERPROFILE\AppData\LocalLow\Vibelab\StickMate\stickmate_character.json"` → `True` · 같은 폴더 `stickmate_reserved_bar_restore.json` → `False`.
          ★ 둘 다 `False`면 경로를 잘못 본 것이다 — 「흔적 없음」 하나만 보고 통과시키지 마라
        ★ 여기서 「복사」가 아니라 「옮기기」를 쓰는 것은 일부러다(같은 문서 S-7-6 1번은 사용자 이전 안내라 「복사」다) — 원래 경로가 **비어야** 첫 실행이 되고, 이름 바꾸기는 형제 폴더로 복구 지점을 남긴다
- [ ] ★★ **R5 — 「첫 실행」은 저장 폴더만 옮겨서는 안 된다.** 온보딩 안내 「봤음」 기록은
      **`PlayerPrefs`**에 있다(`Interaction/PlayerPrefsGearMenuOnboardingSeenStore.cs:38` `Key` =
      `StickMate.GearMenu.OnboardingSeen.v1`).
      〔R12-9 (2026-09-14, HEAD `6173b6e` 실측): `36e0a5e`에서 위젯의 옛 상수 `OnboardingSeenKey`가 **저장소 경계로 옮겨졌다.** 키 이름 · 값 형식은 불변이다(coder 골든 파일 · verify-change 확인) — 아래 절차의 키 이름은 그대로 쓴다〕 이 머신 실측: **이미 `= 1`로 기록돼 있다.**
      〔★★ **R12 (2026-09-14) 전면 교체** — R5~R11의 *"`unity.Vibelab.StickMate.plist`를 `.bak`으로 옮긴다"*는 **폐기.**
      이유 셋: ① 빌드된 앱이 읽는 파일이 **그게 아니다**(아래 (가)) ② plist 파일을 직접 옮기는 것은 **Apple이 경고한 형태**다(아래 (나))
      ③ 안내는 **뜨는 순간** 기록돼 **테이크마다 다시 소모된다**(아래 (다)). 발견: `test-engineer`(`docs/verify/RESERVED_BAR_OWNER_TOKEN_TEST_SPEC.md` G-1, 리더가 이 맥에서 두 파일 실재 확인)〕
      - **(가) 어느 자리인가 — Unity 6 1차 문서**(`ScriptReference/PlayerPrefs`, 6000.0 · 이 프로젝트 `6000.0.82f1`)

        | 누가 | 문서가 적은 자리 | 이 프로젝트 |
        |---|---|---|
        | ★ **빌드된 앱 · macOS** | `~/Library/Preferences/<BundleIdentifier>.plist` (기본값 `com.<회사>.<제품>`, Player settings에서 바꿀 수 있다) | **`com.Vibelab.StickMate`** |
        | ★ **빌드된 앱 · Windows** | `HKCU\Software\<회사>\<제품>` | **`HKCU\Software\Vibelab\StickMate`** |
        | 에디터 Play 모드 · macOS | `~/Library/Preferences/com.<회사>.<제품>.plist` | 문서대로면 `com.Vibelab.StickMate` — **빌드와 같은 이름.** ★ **실측은 `unity.Vibelab.StickMate`를 가리킨다**(아래) |
        | 에디터 Play 모드 · Windows | `HKCU\Software\Unity\UnityEditor\<회사>\<제품>` | 빌드와 **다른 자리** |

        - **촬영 대상 = `com.Vibelab.StickMate`** — 문서 + 이 머신 실측 셋이 맞는다: `Builds/macOS/StickMate.app`의 `CFBundleIdentifier`와
          `codesign` Identifier가 둘 다 `com.Vibelab.StickMate`(빌드 09-09 13:32:28) · 그 plist 수정 시각 09-09 13:33:07 ·
          창 설정 계열 `Screenmanager …` 키가 **이 파일에만** 있다
        - ★ **`unity.Vibelab.StickMate.plist` = 에디터 것으로 추정 — 촬영과 무관.** 〔문서와 실측이 갈린다. **실측을 우선하고 둘 다 적는다**(리더 판정)〕
          - 문서: 에디터 Play 모드 = `com.<회사>.<제품>.plist`. `unity.*`라는 이름은 문서에 **아예 없다**
          - 실측(`test-engineer` 제출 · marketing이 다른 방법으로 재확인): `unity.*` 수정 시각 **09-14 10:36:58** = 리더의 **에디터 배치모드 Windows 빌드**
            출력 `Builds/Windows/StickMate_Data/Managed`의 **10:37:00** 2초 전(Windows 빌드라 macOS 플레이어가 돌 수 없는 시각) · 그 에디터 세션 동안 `com.*`은 09-09 그대로 ·
            `unity.*`에는 창 설정 키(`Screenmanager …`)가 없다 · 에디터 바이너리에는 `unity.%s.%s` 형식 문자열이 있고 빌드의 `UnityPlayer.dylib`에는 없다
          - 등급은 **추정**이다 — Unity 1차 문서의 확인이 아니다. 그래서 대상 판정은 문서가 **명시한** 빌드 쪽(`com.*`)으로만 내리고, `unity.*`는 **건드리지 않는다**
            (에디터가 켜진 동안 건드리면 진행 중인 테스트 러너의 설정 자리를 흔든다)
        - ⇒ **백업·비우기·되돌리기 대상은 `com.Vibelab.StickMate` 하나.**
        - **빌드마다 번들 ID부터 읽는다**(읽기만): `/usr/libexec/PlistBuddy -c "Print :CFBundleIdentifier" "<앱>/Contents/Info.plist"`.
          `com.Vibelab.StickMate`가 아니면 **이 절의 도메인 이름이 전부 틀린 것이다** — 멈추고 리더에게.
          〔R11까지의 *"현행 macOS 빌드는 `codesign`상 아직 `com.DefaultCompany.StickMate`"*는 **낡았다** — 09-09 빌드부터 `com.Vibelab.StickMate`.
          `com.DefaultCompany.*`·`unity.DefaultCompany.*`는 회사명 변경 전 빌드·에디터의 자리다〕
      - **(나) ★ macOS — plist 파일을 옮기거나 지우지 않는다. `defaults`로만 다룬다**
        - 1차 출처 — Apple `UserDefaults` 문서의 Warning: *"Don't access the files of the defaults database directly from the file system.
          Modifying one of the underlying files directly may cause data loss, a delay in changes being available, or an app crash.
          In macOS, use the `defaults` command-line utility to safely view or modify the defaults database outside of your app."*
        - 우리에게 무슨 뜻인가: 파일을 `.bak`으로 옮겨도 **옛 값(`= 1`)이 다음 실행에 넘어갈 수 있다**(delay) → 알약 없는 「첫 실행」 = **조용한 거짓 소재.**
          또는 **사용자 기록이 사라진다**(data loss)
        - ★ 경고 문장은 **앱이 켜져 있는지를 조건으로 두지 않는다** — 앱을 종료한 뒤에도 파일 직접 조작은 금지다.
          (캐시를 쥔 것이 시스템 데몬 `cfprefsd`라는 설명은 **1차 문서 미확인** — 이 머신에 `cfprefsd` 프로세스가 떠 있는 것만 봤다)
        - 우리 빌드가 이 데이터베이스를 타는가: **Unity 문서 명시 없음(`NSUserDefaults`라고 적은 것은 iOS뿐) — 추론.**
          근거는 바이너리다: 이 빌드의 `UnityPlayer.dylib`가 `NSUserDefaults` 클래스와 `CFPreferencesCopyAppValue` 등을 가져다 쓴다(`nm -u`)
        - **1차 출처로 확인된 방법은 `defaults` 하나다**(위 Warning). ★ `killall cfprefsd` 류는 **1차 문서에서 찾지 못했다 — 쓰지 않는다**(시스템 전체 데몬이라 다른 앱에도 걸린다)
        - ★ **미확인**: `defaults delete`가 **다음 실행에 곧바로 반영되는지**는 문서가 약속하지 않고 이 기계에서도 재지 않았다
          (`delete domain key`: *"Removes the default named key from domain"*뿐). → 켜기 전 `defaults read` 대조(아래 3번)는 **파일이 아니라 같은 데이터베이스를 읽는 확인**이고,
          **최종 판정은 테이크 안에서 알약이 눈에 보이고 로그가 1줄인가**로 한다(아래 (다))
        - `defaults import`가 도메인을 **통째로 바꾸는지 합치는지 미확인**(`defaults help`: *"writes the plist at path to domain"*뿐) → **평상시 되돌리기에 쓰지 않는다.** 비상 복구는 리더 승인 뒤
      - **(다) ★ 테이크마다 다시 비운다 — 안내는 뜨는 순간 기록된다**
        - `Interaction/GearRadialMenuWidget.cs:917-925` — 위젯 주석 「뜨는 그 순간 기록한다」. 부채꼴을 **처음 펼치는 순간** 알약이 뜨고 **그 자리에서** 기록한다: `TryStartOnboardingHint` `:917` → `MarkSeen()` 호출 `:923` → 저장소 `Interaction/PlayerPrefsGearMenuOnboardingSeenStore.cs:81-86`(`SetInt(Key, SeenValue)` `:84` + `Save()` `:86`). 〔R12-9: `36e0a5e`에서 쓰기가 위젯에서 저장소 `MarkSeen`으로 옮겨졌다 — 형식 · 시점 불변. 줄은 HEAD `6173b6e` 실측〕
        - ⇒ 2-4 「최소 3테이크」를 **한 번 비우고** 찍으면 **2·3테이크는 알약 없는 화면**이다. 첫 테이크가 만든 캐릭터도 남는다.
          **테이크마다** 2-3 ①(종료) → (Windows 자동 숨김 기계면 촬영 폴더 흔적 ⑤-2) → 촬영 폴더를 `capture-shot-<시각>`으로 치움(⑤-3) → 아래 절차 2·3번 → ④ 를 다시 돈다
        - **판정은 눈 + 로그 둘 다**(2-5): 테이크마다 **알약이 화면에 보이고** `[부채꼴] 최초 1회 안내를 띄웁니다`(`GearRadialMenuWidget.cs:925` @`6173b6e` — 이 문장 바이트는 `7900ad0`과 같다)가 **정확히 1줄.**
          둘 중 하나라도 아니면 첫 실행 테이크가 아니다 → 폐기. 〔눈은 「비우기가 실제로 반영됐는가」(위 (나) 미확인)의 대조이고, 로그는 눈이 놓친 두 번 발동·0회를 잡는다〕
          ★ 부채꼴을 **펼치기 전에는** 알약이 원래 없다 — 「켰는데 안내가 없다」만으로 절차 실패를 판정하지 않는다
          ★ 〔R12-9, HEAD `6173b6e`〕 **부채꼴은 캐릭터 우클릭으로 편다**(옛 「톱니 부채꼴」 아님 — `0229f52`부터 평소 톱니 없음). 우클릭 경로도 알약을 띄운다: `AppControlDirector.cs:892` `ExpandOrReanchor` → `GearRadialMenuWidget.cs:895` `Expand` → `:838` `TryStartOnboardingHint`
          ★★ **함정(수정 E-1 전) — 게임이 아닌 전체화면 앱이 떠 있는 동안 찍으면 알약이 0줄이 된다.** 그 상태에서는 우클릭 게이트(`AppControlDirector.cs:854-857`)가 허가 발급(`:886`)보다 먼저 막아 부채꼴이 **열리지 않고**, 알약은 `Expand` 안에서만 뜨기 때문이다. 이 0줄은 **비우기 실패가 아니라 P1 회귀**다 — 「0줄 = 폐기」로 읽고 절차 2·3번을 반복해도 계속 0줄이다. ⇒ **첫 실행 테이크는 전체화면 앱을 모두 닫은 화면에서 찍는다.** 0줄이 나오면 폐기하기 전에 전체화면 앱 여부부터 본다(`docs/ux/SETTINGS_ENTRY_NARROW_WIDTH.md` §15-1)
          ★★ **「촬영 전에 한 번 펼쳐 알약이 뜨는지 먼저 본다」는 하지 않는다** — 그 확인이 기록을 소모해 **바로 다음 녹화가 알약 없는 화면**이 된다.
          확인은 **녹화가 이미 돌고 있는 테이크 안에서** 한다. 미리 보고 싶으면 본 뒤 절차 2·3번을 다시 돈다
      - **macOS 절차** (2-3 ① 종료 · `pgrep -x StickMate` 0줄 뒤)
        1. **백업** — 파일 복사가 아니라 `defaults`로 내보낸다. 자리는 2-3 ③과 같은 부모 폴더(바탕화면 금지):
           `B="$HOME/Library/Application Support/Vibelab"; T=$(date +%Y%m%d-%H%M%S)`
           `defaults export com.Vibelab.StickMate "$B/prefs-com.capture-backup-$T.plist"`
           키 **이름만** 기록: `python3 -c 'import plistlib,sys; print("\n".join(sorted(plistlib.load(open(sys.argv[1],"rb")))))' "$B/prefs-com.capture-backup-$T.plist"`
           (`unity.Vibelab.StickMate`는 내보내지도 비우지도 않는다 — (가))
           ★ 백업 안에는 `unity_connect.installation_id` 같은 **식별자 값**이 있다 — 메모·채팅에 **내용을 붙이지 않는다.** 이름만 적는다
        2. **비우기 — 키 하나만**: `defaults delete com.Vibelab.StickMate StickMate.GearMenu.OnboardingSeen.v1`
           (도메인 통째 `defaults delete com.Vibelab.StickMate`를 쓰지 않는다 — 아래 Windows는 통째로 지우면 안 되므로 **같은 모양의 절차**로 둔다. 플랫폼마다 절차를 고르면 언젠가 틀린 쪽을 고른다)
        3. **켜기 전 대조 둘**: `defaults read com.Vibelab.StickMate StickMate.GearMenu.OnboardingSeen.v1` → **실패**(does not exist)
           **그리고** `defaults read-type com.Vibelab.StickMate UnityGraphicsQuality` → **성공**(도메인은 살아 있다).
           ★ 둘 다 실패면 **도메인 이름을 잘못 친 것이다** — 「없음」 하나만 보고 통과시키지 마라
        4. **촬영 뒤**: 마지막 테이크가 알약을 띄웠으면 앱이 이미 다시 써 두었다 → `defaults read com.Vibelab.StickMate StickMate.GearMenu.OnboardingSeen.v1` → `1`.
           **없으면** 사용자 폴더를 되돌린(⑤-4) 뒤 앱을 한 번 켜 **캐릭터를 우클릭해** 부채꼴을 한 번 펼치고(로그 1줄) 앱 안에서 종료한다.
           〔R12-9: 옛 「부채꼴을 펼치고」는 입구가 적혀 있지 않았다. **전체화면 앱이 없는 화면에서** 우클릭한다 — 수정 E-1 전에는 전체화면 앱 중 우클릭이 열리지 않아 기록이 채워지지 않는다(위 (다) 함정)〕
           ★ **손으로 `defaults write`하지 않는다** — Unity가 쓰는 값 형식을 1차 문서로 확인하지 못했다
        5. **대조**: `com.Vibelab.StickMate`의 키 이름 목록이 1번 기록과 **같다.**
           다르면 **`defaults import`로 덮지 말고** 리더에게(import 의미 미확인)
      - **Windows 절차** — `HKCU\Software\Vibelab\StickMate` (Unity 6 문서로 확인 · **Windows 실기 미확인**)
        - ★★★ **이 키를 통째로 지우지 않는다. 다른 기계·다른 계정에서 만든 `.reg`를 가져오지 않는다.**
          곧 들어갈 작업표시줄 원복 **소유 표식(무작위 설치 토큰)이 바로 이 키에 저장된다**
          (`Tasklist.md` 「[game-architect] 무작위 설치 토큰안 구조 확인」 · 리더 판정 「원칙 3 원복 조건 변경 채택」).
          **지우면 그 설치의 원복 소유 판정이 끊기고**(그 PC의 열린 흔적을 아무도 갚지 않는다), **남의 `.reg`로 덮으면 다른 설치의 표식을 이 PC가 제 것으로 읽는다.**
          토큰이 아직 없는 빌드도 **같은 절차로** 찍는다
          - 금지 형태: `Remove-Item HKCU:\Software\Vibelab\StickMate` · `reg delete HKCU\Software\Vibelab\StickMate /f` · `reg delete … /va` ·
            다른 기계 `.reg`의 `reg import` · 레지스트리 정리 도구 · 촬영 중 `docs/COMPANY_RENAME_MIGRATION.md` §2-(2) 레지스트리 복사
          - ★ **촬영 중 새로 생긴 값도 지우지 않는다**(토큰일 수 있다) — 촬영 폴더의 흔적이 그 토큰으로 만들어졌을 수 있다(⑤-2)
          - 에디터 자리 `HKCU\Software\Unity\UnityEditor\Vibelab\StickMate`는 빌드가 읽지 않는다(문서) — 건드리지 않는다
        - ★ 값 이름에 **`_h<숫자>` 꼬리**가 붙는다(Unity 6 문서 예: *"DeckBase_h3232628825"*) — 정확한 이름으로는 못 찾는다. **패턴으로** 찾는다
        0. `$k = 'HKCU:\Software\Vibelab\StickMate'; Test-Path -LiteralPath $k` → `False`면 이 계정에 기록이 없다(이미 첫 실행 상태 — `docs/verify/WINDOWS_CHECK_SESSION.md` 0-2). 비울 것이 없다.
           ★ 단 **회사명 칸부터 확인한다** — 2-3 확인 줄(`[동결기록] 활성 — 폴더 …\Vibelab\StickMate`)이 `Vibelab`이 아니면 경로를 잘못 본 「없음」이다
        1. **백업**(비상용 — 평상시 되돌리기에 쓰지 않는다): `$B = "$env:USERPROFILE\AppData\LocalLow\Vibelab"; $T = Get-Date -Format yyyyMMdd-HHmmss; reg export "HKCU\Software\Vibelab\StickMate" "$B\prefs.capture-backup-$T.reg"`
           ★ 이 파일에는 **토큰 값이 들어간다** — **이 PC 밖으로 옮기지 않는다**, 내용을 메모·채팅에 붙이지 않는다.
           값 **이름만** 기록: `(Get-Item -LiteralPath $k).Property | Sort-Object`
        2. **비우기 — 온보딩 값만**: `(Get-Item -LiteralPath $k).Property | Where-Object { $_ -like 'StickMate.GearMenu.OnboardingSeen.v1_h*' } | ForEach-Object { Remove-ItemProperty -LiteralPath $k -Name $_ }`
        3. **켜기 전 대조 둘**: 같은 `Where-Object` 일치 수 **= 0** **그리고** `Screenmanager*_h*` 일치 수 **≥ 1**(같은 키가 살아 있다).
           ★ `Screenmanager` 계열이 Windows에도 있는지는 **미확인**(macOS 빌드 plist에서만 실측) — 0이면 판독 무효로 리더에게
        4. **촬영 뒤**: 일치 수 **= 1**(마지막 테이크가 다시 씀). 0이면 macOS 4번처럼 **앱 경로로** 채운다 — `New-ItemProperty`로 손으로 만들지 않는다(값 형식 미확인)
        5. **대조**: 값 이름 목록이 1번과 **같다.** 새 이름이 생겼으면(토큰일 수 있다) **지우지 말고** 메모에 「새 이름 N개」로 적어 리더에게
      - **캡처 메모(4절)에 더 적는다**: macOS 번들 ID · 백업 파일 이름 · 테이크별 `[부채꼴] 최초 1회 안내` 로그 줄 수
      - 남는 차이(미확인): 온보딩 값만 비우므로 Unity 창 설정 계열(`Screenmanager …`)은 남는다. 첫 실행의 창 배치에 영향이 있는지 모른다 →
        **첫 실행 컷으로 「창 크기·위치」를 주장하지 않는다**
      - ★ **이 절차 없이 찍은 「첫 실행」은 거짓 소재다** — 신규 사용자는 온보딩 알약을 보는데
        우리 화면에는 안 뜬다. **다른 제품을 찍는 것이다**
- [ ] 캐릭터 배율: 배포 기본값 사용. 개인 저장값(과거 0.35 사례)이 섞이면 실측이 어긋난다
- [ ] `verboseDiagnosticsLogging` **OFF**(진단 문자열이 화면/로그에 남는다)
- [ ] 디버그 잠금 해제(`EquipmentDebugUnlock`) **OFF** — 릴리즈 게이트 확인
- [ ] 배포 에셋 그대로. **확률값을 임시로 올려 찍었다면 반드시 원복하고 `git diff` 0을 확인**
      (선례: 2026-08-29 그라피티 영역을 96→56px로 낮춰 찍고 **에셋 완전 원복 diff 0** 확인)
- [ ] ★ **R2 — 장비를 착용시키지 않는다.** 출하 27색 중 21색이 몸에서 대비 미달이고(최악 1.60:1)
      수정 라운드가 진행 중이다. **장비 색이 화면에 나오는 컷은 홍보용으로 찍지 않는다**
      (`TRUTH_INVENTORY.md` 4절 D등급). 디자인 판정용 캡처는 별개이고, **파일명에 용도를 적어 섞지 않는다**
- [ ] ★ **R2 — [행동] 명령창을 찍는다면 반드시 새로 찍는다.** 격파 타일 삭제로 **타일 6 → 5,
      창 높이 560 → 508**이 됐다. 이전 캡처를 재사용하면 **없는 기능이 찍힌 거짓 소재**가 된다

### 2-4. 캡처 자체
- [ ] **실제 빌드**에서. Unity 에디터 플레이 모드 화면 금지(에디터에서는 `NullPlatformWindowService`라
      **창=지형이 아예 동작하지 않는다** — 에디터 캡처는 원리적으로 거짓 소재다)
- [ ] Retina 배율 기록. 스토어 규격으로 리사이즈할 때 필요
- [ ] 프레임레이트: `MOMENTS.md` 4-0 참고. **Away 등급(15fps)에서 찍힌 클립은 폐기**
- [ ] 같은 순간을 **최소 3테이크**. M2(던지기)는 회전수 편차가 3.3배라 **5테이크 이상**

### 2-5. ★ R2 신설 — 테이크 검수는 **눈이 아니라 로그로** 판정한다

**"프레임 등급이 Away였다"와 "Active였다"는 화면으로 구분이 안 된다.** macOS의 Away 등급은
`renderFrameInterval`만 바꿔 게임 루프는 60Hz로 남는다 — 즉 **보는 사람 눈에는 멀쩡하고
녹화 파일에서만 15fps로 망가진다.** 이 저장소가 반복해서 밟은
"실패한 측정과 성공한 측정이 똑같이 생겼다"의 정확한 형태다. 그래서 로그로 잰다.

| 검사 | 합격 조건 | 근거 |
|---|---|---|
| `[화면클램프]` | **0건** | 나왔다면 캐릭터가 화면 밖으로 나가려 해 되돌려진 것 = 구도 실패. **이 로그는 진단 스위치와 무관하게 항상 남는다**(`StickConfig.cs:1534`) — 릴리즈 빌드에서도 검수가 된다 |
| 프레임 등급 | 구간 내내 **Active**(`renderFrameInterval == 1`) | 위 문단 |
| 연출 태그 | 해당 태그가 **정확히 1회**(`[활쏘기]` / `[윈도우크래시]` / `[그라피티]` …) | 두 번 발동하면 잘린 극이 찍힌다 |
| 〔R12〕 첫 실행 컷만 — `[부채꼴] 최초 1회 안내를 띄웁니다` | **테이크마다 정확히 1줄** | `GearRadialMenuWidget.cs:917-925`(`MarkSeen()` 호출 `:923` → 저장소 `PlayerPrefsGearMenuOnboardingSeenStore.cs:81-86` · 로그 `:925`, HEAD `6173b6e`) — 뜨는 순간 기록돼 **다음 테이크에서는 안 뜬다**(2-3 R5 (다)). 0줄 = 알약 없는 「첫 실행」 = 거짓 소재. ★ 〔R12-9〕 **0줄이면 폐기하기 전에 전체화면 앱이 떠 있었는지 먼저 본다** — 수정 E-1 전에는 게임이 아닌 전체화면 앱 중 캐릭터 우클릭이 부채꼴을 열지 않아 알약도 0줄이 된다(2-3 R5 (다) 경고) |
| 예외 | **0건** | — |

★ **이 검수기에는 양성 대조를 반드시 붙인다.** 일부러 화면 끝으로 밀어 `[화면클램프]`가 찍힌
테이크를 만들고, **검수기가 그걸 빨간불로 거부하는지** 확인한다. 확인 전에는 이 검수기의
"0건 = 통과" 판정을 **전부 무효로 취급**한다(TEAM.md 4절 #4·#5).

---

## 3. 승인 절차 — 순서를 지킨다

```
 ① 마케팅이 컷 리스트 제출 (무엇을, 어느 세트로, 몇 초)
        ▼
 ② 리더 승인  ← 앱 실행·캡처는 여기 없이 시작하지 않는다
        ▼
 ③ 환경 준비 (2절 체크리스트) — 화면을 사람이 준비해야 하므로 사용자 협조 필요
        ▼
 ④ 캡처 — driver.sh 실행은 리더가 정한 방식으로.
          ★ stop / 전역 Q 절대 금지. 사용자 인스턴스(2026-09-02 오후 기준 PID 30572)가 함께 죽는다
          ※ PID는 매번 바뀐다. **숫자를 믿지 말고 `driver.sh status`의 "그 밖의 인스턴스"를 그때 확인**한다
        ▼
 ⑤ 마케팅 1차 검수: 2절 체크리스트 전항목 + "실제로 되는 것만 찍혔는가"
        ▼
 ⑥ 리더 최종 승인
        ▼
 ⑦ ★ 외부 게시는 **사용자가** 한다. 마케팅은 어디에도 올리지 않는다
```

---

## 4. 캡처 결과물 보관 규칙

> ### ★★ R6 (2026-09-05) — **창고를 「스토어용 / 그 밖」으로 접두어에서 가른다**
> 스토어 슬롯은 이제 **Windows 컷만** 받는다(`CAPTURE_REQUESTS_R6.md` 1절). 그런데 macOS 컷은
> 개발일지용으로 계속 찍힌다. **한 폴더에 섞이면 언젠가 누가 macOS 컷을 스토어에 올린다** —
> 이 문서 마지막 줄(*"남겨 두면 언젠가 누가 쓴다"*)이 경고하는 바로 그 형태다.
>
> | 접두어 | 쓰는 곳 | 규칙 |
> |---|---|---|
> | `store-win-` | ★ **스팀 스토어 슬롯 전용** | **Windows 실기 캡처만.** 2-0 체크리스트 전항 통과 |
> | `devlog-` | 개발일지·커뮤니티 | macOS 컷 허용. **스토어에 올리지 않는다** |
> | `judge-` | 디자인·QA 판정용 | ★ **홍보에 쓰지 않는다**(D등급 항목도 여기) |
>
> ★ **접두어가 없는 파일은 스토어에 못 쓴다**로 취급한다. 애매하면 못 쓰는 쪽이다.

- 경로: `docs/marketing/captures/<접두어><YYYYMMDD>-<소재코드>-<테이크>.png|mp4`
- **각 파일에 짝이 되는 메모 1줄**: 빌드 시각 / 플랫폼 / 세트 / 프레임 등급 / 체크리스트 통과 여부
- **빌드 동일성 확인**: 우리 코드는 `StickMate.exe`가 아니라
  `StickMate_Data/Managed/StickMate.Runtime.dll`의 시각으로 판단한다(TEAM.md 4절 #7).
  낡은 빌드로 찍고 새 기능이라 말하는 사고를 막는다
- 체크리스트 미통과분은 **지운다.** 남겨 두면 언젠가 누가 쓴다

---

## 5. 플랫폼

> ### ★★ R6 정정 (2026-09-05)
> 아래 *"직장인 타겟의 주 채널은 MS 스토어"*라는 근거는 **낡았다** — 채널은
> **스팀 단독**으로 확정됐고, 이번 전환으로 **1.0의 유일한 플랫폼이 Windows**가 됐다.
> ⇒ **결론(「Windows 스크린샷이 더 중요하다」)은 그대로 참이고, 근거만 교체된다:**
> **스토어 슬롯에 걸 수 있는 그림이 Windows 컷뿐이기 때문이다.**
> 절차는 **2-0절(Windows 캡처 환경)**이 정본이고, 아래 3줄은 그 안에 흡수됐다.
>
> - **Windows 영향: 함께 검토함.** 2-0 신설 + 4절 접두어 규칙 신설.
> - **macOS 영향: 함께 검토함 — 강등하되 유지.** 2-1~2-4는 `devlog-`/`judge-` 캡처에 계속 쓴다.

- **macOS 영향**: 이 문서의 절차가 macOS 기준으로 작성됨(`screencapture`, Space, 메뉴바).
- **Windows 영향**: **별도 절차 필요** → ★ **R6에서 2-0절로 신설됨.** 다음 항목이 Windows 전용으로 다르다:
  - 작업표시줄 위에 서는 그림 — **Windows에만 있는 소재**(macOS는 Dock)
  - 승인된 예외(작업표시줄 자동 숨김 해제)가 **화면에 보인다**. 캡처 시 그 상태가 찍힌다는 것을
    인지하고, 스토어 문구의 고지와 그림이 어긋나지 않게 한다
  - 창 제목 노출 위험이 macOS보다 **높다** — Windows는 창 제목을 실제로 읽고(`Win32WindowService`),
    작업표시줄 버튼에 제목이 그대로 뜬다. 체크리스트 2-2를 더 엄격히 적용
