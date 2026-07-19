# Debug: 멀티모니터에서 일부 모니터만 재생 / 재생 중 모니터 변경 시 미재생

## Symptom
- 버그1: 설정에서 모니터 2개를 모두 체크해도 **처음부터 계속 1개 모니터에만** 영상이 나온다.
- 버그2: 재생 중 모니터 체크를 **추가·해제**하면 해당(또는 추가한) 모니터가 재생되지 않는다.

## Reproduction
1. 모니터 2개를 설정에서 모두 선택.
2. 정지 상태에서 재생 시작 → 종종 한 모니터만 영상 표시.
3. 재생 중 한 모니터 체크 해제 후 재추가 → 추가한 모니터 미재생.
- 비결정적(레이스): 어떤 실행에서는 2개 다 재생됨(세션 A 16:59:13), 어떤 실행에서는 1개만(세션 B 16:59:30).

## Phase 1 — Evidence
진단 로그(`WallpaperInterop` WorkerW 토폴로지·배치 rect, `PlayerHost` 플레이어별 `[Pxxxx]` 태그, `player.html` exec/onReady diag)를 임시 추가해 실측.

- **배경창 배치는 정상**: 선택 WorkerW=전체 가상 데스크톱(0,0)-(5120,1600). 두 배경창 모두 요청 좌표=실제 배치 좌표 일치 — DISPLAY1(0,0)-(2560,1600), DISPLAY2(2560,0)-(5120,1600). → **WorkerW 클리핑 가설 기각.**
- **재생 이벤트가 한 플레이어만 발생**:
  - 세션 B 버그1: `[P109C] onReady queued=captions,mute,load → exec load → Playing` ✅ / `[P1202] onReady queued=captions`(load 없음) → 재생 안 됨 ❌. C#은 `[P1202] 플레이어 명령: load`를 분명히 전송했으나 JS 큐에 load 미도달.
  - 세션 B 버그2: 재생 중 추가된 `[P0E0A] onReady queued=`(완전히 빈 큐) → load 유실 → 재생 안 됨 ❌.

## Phase 2 — Hypotheses
- H1: WorkerW가 주 모니터만 덮어 두 번째 배경창이 화면 밖 → **기각**(WorkerW=전체 가상 데스크톱, 배치 좌표 정확).
- H2: 나중 합류 플레이어의 Load+Seek 간섭 → **부분 기각**(처음부터 2개인 세션 B에서도 seek 없이 유실 발생).
- H3: **초기화 직후 페이지 준비 전 전송한 명령이 유실** → **확정**(onReady queued에 C#이 보낸 load가 없음).

## Phase 3 — Root Cause
`PlayerHost.InitializeAsync`가 `CreateCoreWebView2ControllerAsync`+`Navigate`까지만 대기하고 **player.html의 message 리스너가 준비되는 시점을 기다리지 않는다.** 초기화 완료 직후 코디네이터가 `PostWebMessageAsJson`으로 `load` 등을 보내면, 페이지가 아직 로딩 중일 때 **메시지가 유실**된다(WebView2는 문서 로드 완료 전 postMessage를 폐기). 여러 모니터를 동시/연속 시작하면 네비게이션 완료 타이밍 경쟁으로 일부 플레이어가 load를 못 받아 재생되지 않는다. → 비결정적 "대개 1개, 가끔 2개".

## Phase 4 — Fix
- `player.html`: message 리스너 등록 직후 `hostReady` 신호 post(`PLAYER_REV` 8). 조사용 exec/onReady diag 제거.
- `PlayerHost.InitializeAsync`: Navigate 후 `hostReady` 수신까지 대기(`HostReadyTimeout` 10초 폴백) 뒤 완료 반환. `_hostReadyTcs`(TaskCompletionSource)로 동기화, `OnWebMessageReceived`에 `hostReady` case 추가. → 이후 전송되는 모든 명령(load 포함)이 리스너 등록 후 도달.
- 진단 로그 정리: `WallpaperInterop` WorkerW 토폴로지·배치 rect 로그·관련 P/Invoke(GetWindowRect/GetClassNameW/RECT) 제거해 원상 복구. 플레이어 `[Pxxxx]` 태그 로그는 멀티모니터 판독에 유용해 유지.

## Verification
- Build: OK (경고 0, 오류 0). Tests: 142/142 통과.
- 수동 재현(2모니터): 정지→재생 반복·재생 중 모니터 추가/해제 모두 **두 모니터 다 재생됨**. 로그 확인 — 첫 시작·나중 합류 플레이어 전부 `load → Buffering → Playing` 도달, `load` 유실 없음(17:14~17:19 세션).
