<#
  measure-dwm.ps1 — StickMate가 Windows 컴포지터(dwm.exe)에 지우는 부담을 실측한다.
  perf-doc, 2026-08-31. macOS에서 쓴 것과 같은 방법(누적 CPU 시간 델타 + 교차 페어드 설계)을
  Windows로 옮긴 것이다. 결과 CSV를 그대로 팀에 붙여넣으면 된다.

  왜 작업관리자로는 안 되는가:
    · 자세히 탭의 CPU 열은 "논리 프로세서 전체 합계 기준 정수 %"다. 16스레드 PC에서
      코어 1개의 10%는 0.6%로 표시되고, 정수 반올림으로 "00%"가 된다.
      찾으려는 신호 전체가 계측기 분해능 아래에 있다.
    · 이 스크립트는 100ns 분해능 누적 카운터를 직접 읽는다(작업관리자보다 약 8000배 정밀).

  왜 Get-Counter를 안 쓰는가:
    · 한국어 Windows는 성능 카운터 경로가 현지화된다("\Process(dwm)\% 프로세서 시간").
      Win32_PerfRawData_* 클래스는 언어와 무관하므로 그쪽을 쓴다.

  실행법 (PowerShell을 "관리자 권한으로 실행"):
      cd <이 파일이 있는 폴더>
      powershell -ExecutionPolicy Bypass -File .\measure-dwm.ps1
  자동 모드(스크립트가 앱을 직접 켜고 끄며 4사이클 반복):
      powershell -ExecutionPolicy Bypass -File .\measure-dwm.ps1 -Auto -ExePath "C:\경로\StickMate.exe"
  ※ 자동 모드는 앱을 강제 종료하므로 최대 60초치 진행도(자동저장 주기)를 잃을 수 있다.
     방금 실행한 직후에 돌리면 잃을 것이 없다.
#>

param(
    [int]$Cycles = 4,
    [int]$PhaseSeconds = 30,
    [switch]$Auto,
    [string]$ExePath = ""
)

$ErrorActionPreference = "Stop"

function Get-Snapshot {
    # dwm / StickMate 의 누적 CPU 시간(100ns)과 메모리를 한 번에 읽는다.
    $rows = Get-CimInstance -ClassName Win32_PerfRawData_PerfProc_Process |
            Where-Object { $_.Name -like "dwm*" -or $_.Name -like "StickMate*" }
    $dwmCpu = 0; $dwmMem = 0; $appCpu = 0; $appMem = 0; $ts = 0
    foreach ($r in $rows) {
        if ($ts -eq 0) { $ts = [double]$r.Timestamp_Sys100NS }
        if ($r.Name -like "dwm*") {
            $dwmCpu += [double]$r.PercentProcessorTime
            $dwmMem += [double]$r.WorkingSetPrivate
        } else {
            $appCpu += [double]$r.PercentProcessorTime
            $appMem += [double]$r.WorkingSetPrivate
        }
    }
    if ($ts -eq 0) { $ts = [double](Get-Date).Ticks }
    return [pscustomobject]@{ Ts=$ts; DwmCpu=$dwmCpu; DwmMem=$dwmMem; AppCpu=$appCpu; AppMem=$appMem }
}

function Sample-Phase([string]$label, [int]$seconds) {
    $out = @()
    $prev = Get-Snapshot
    for ($i = 0; $i -lt $seconds; $i++) {
        Start-Sleep -Seconds 1
        $cur = Get-Snapshot
        $dt = $cur.Ts - $prev.Ts
        if ($dt -le 0) { $prev = $cur; continue }
        # 코어 1개 기준 % (macOS 측정과 같은 단위). 논리 프로세서 수로 나누면 작업관리자 표시값.
        $dwmPct = ($cur.DwmCpu - $prev.DwmCpu) / $dt * 100.0
        $appPct = ($cur.AppCpu - $prev.AppCpu) / $dt * 100.0
        $out += [pscustomobject]@{
            Time    = (Get-Date).ToString("HH:mm:ss")
            Phase   = $label
            DwmCpu1Core = [math]::Round($dwmPct, 2)
            AppCpu1Core = [math]::Round($appPct, 2)
            DwmMemMB    = [math]::Round($cur.DwmMem / 1MB, 2)
            AppMemMB    = [math]::Round($cur.AppMem / 1MB, 2)
        }
        $prev = $cur
        Write-Host ("  [{0}] {1,-6} dwm={2,7:N2}%(1코어) mem={3,7:N1}MB   app={4,7:N2}% mem={5,7:N1}MB" -f `
            $out[-1].Time, $label, $dwmPct, ($cur.DwmMem/1MB), $appPct, ($cur.AppMem/1MB))
    }
    return $out
}

$cores = (Get-CimInstance Win32_ComputerSystem).NumberOfLogicalProcessors
Write-Host ""
Write-Host "=== StickMate DWM 계측 ===" -ForegroundColor Cyan
Write-Host ("논리 프로세서 {0}개  |  코어1개 기준 1.00% = 작업관리자 표시 {1:N2}%" -f $cores, (1.0/$cores))
Write-Host ("사이클 {0}회 x 위상 {1}초 x 2위상 = 약 {2}분" -f $Cycles, $PhaseSeconds, [math]::Round($Cycles*$PhaseSeconds*2/60.0,1))
Write-Host ""

$all = @()
for ($c = 1; $c -le $Cycles; $c++) {
    Write-Host ("--- 사이클 {0}/{1} ---" -f $c, $Cycles) -ForegroundColor Yellow

    # 위상 OFF: StickMate가 떠 있지 않은 상태
    if ($Auto) {
        Get-Process -Name StickMate -ErrorAction SilentlyContinue | Stop-Process -Force
        Start-Sleep -Seconds 3
    } else {
        Read-Host "StickMate를 [종료]한 뒤 Enter"
    }
    $all += Sample-Phase "OFF" $PhaseSeconds

    # 위상 ON: StickMate 실행 + 유휴(마우스/키보드를 건드리지 말 것)
    if ($Auto) {
        if ($ExePath -eq "") { throw "-Auto 모드에는 -ExePath 가 필요합니다." }
        Start-Process -FilePath $ExePath | Out-Null
        Start-Sleep -Seconds 8   # 창 부착 + 전체화면 확장이 끝나기를 기다린다
    } else {
        Read-Host "StickMate를 [실행]하고 8초쯤 기다린 뒤 Enter (그 다음 30초간 마우스를 건드리지 마세요)"
    }
    $all += Sample-Phase "ON" $PhaseSeconds
}

$csv = Join-Path ([Environment]::GetFolderPath("Desktop")) "stickmate_dwm.csv"
$all | Export-Csv -Path $csv -NoTypeInformation -Encoding UTF8

$on  = $all | Where-Object { $_.Phase -eq "ON" }
$off = $all | Where-Object { $_.Phase -eq "OFF" }
function Avg($rows, $prop) { if ($rows.Count -eq 0) { return 0 } ; ($rows | Measure-Object -Property $prop -Average).Average }

Write-Host ""
Write-Host "=== 요약 (코어 1개 기준 %) ===" -ForegroundColor Cyan
Write-Host ("dwm.exe  OFF {0,7:N2}%   ON {1,7:N2}%   차이 {2,7:N2}%p   [작업관리자 표시로는 {3:N2}%p]" -f `
    (Avg $off DwmCpu1Core), (Avg $on DwmCpu1Core), ((Avg $on DwmCpu1Core)-(Avg $off DwmCpu1Core)), (((Avg $on DwmCpu1Core)-(Avg $off DwmCpu1Core))/$cores))
Write-Host ("dwm 메모리 OFF {0,7:N1}MB  ON {1,7:N1}MB  차이 {2,7:N1}MB" -f `
    (Avg $off DwmMemMB), (Avg $on DwmMemMB), ((Avg $on DwmMemMB)-(Avg $off DwmMemMB)))
Write-Host ("StickMate 자체 CPU  ON {0,7:N2}%(1코어)" -f (Avg $on AppCpu1Core))
Write-Host ""
Write-Host ("CSV 저장됨: {0}" -f $csv) -ForegroundColor Green
Write-Host "이 요약 4줄 + CSV를 팀에 그대로 전달해 주세요."
