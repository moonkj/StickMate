# 오디오 상주 비용 계측기 (macOS)

`docs/PERFORMANCE_NOTES.md` 부록 C가 쓴 계기. 프로덕션과 무관하고 앱을 건드리지 않는다.

```bash
clang -O2 -o probe  audio_device_probe.c       -framework AudioToolbox -framework AudioUnit -framework CoreAudio -framework CoreFoundation
clang -O2 -o probe2 audio_open_latency_probe.c -framework AudioToolbox -framework AudioUnit -framework CoreAudio -framework CoreFoundation

./probe 512 60     # 512프레임 버퍼로 60초 동안 무음 출력 — 1초마다 "초,콜백수,누적프레임"
./probe 4096 60    # 교정용(콜백 8배 감소가 실제로 관측돼야 계기가 살아 있는 것)
./probe2           # 열기 지연 5회 (생성/Start/첫 콜백, 닫힌 뒤 IsRunning 확인)
```

**함께 봐야 하는 것 (우리 프로세스만 보면 25배를 놓친다)**

```bash
ps -o time= -p $(pgrep -x coreaudiod)     # 구간 전후로 두 번 — 이게 진짜 비용이 실리는 곳
pmset -g assertions | grep -i BuiltInSpeaker   # 열려 있으면 PreventUserIdleSystemSleep 이 뜬다
```

`audio_ab_experiment.sh`는 위를 OFF/ON/OFF로 자동 왕복한다(경로가 스크래치패드로 박혀 있으니
`D=` 한 줄을 고쳐 쓴다).
