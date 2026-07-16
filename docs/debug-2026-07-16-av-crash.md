# Debug: 재생 시작 직후 AccessViolationException 크래시

## Symptom
홈에서 URL 입력 → "바탕화면에서 재생" 클릭 → 수 초~수십 초 내 `System.AccessViolationException`
(스택 트레이스·소스 확인 불가, VS 디버거 대화상자). 디버거 없이 실행하면 프로세스가 조용히 사망.

## Reproduction
1. 앱 실행 (패키지 활성화 — shell:AppsFolder 또는 VS F5)
2. 홈에서 유튜브 URL 입력 → "바탕화면에서 재생" 클릭
3. 2~15초 내 프로세스 사망 (이 환경에서 부착 모드 재현율: 사용자 1회 + 자동 재현 3/3)

## Phase 1 — Evidence
- Error: `AccessViolationException` — Event 1026 스택은 `Program.Main → Application.Start`뿐 (네이티브 구간에서 발생, 관리 프레임 없음). Event 1000: 예외 0xC0000005.
- 앱 로그: `배경창 부착` 이후 어떤 로그도 없음 — 진단 훅(AppDomain/Xaml UnhandledException·ProcessExit)까지 **전부 미발화** → 관리 예외 경로·정상 종료 경로가 아닌 네이티브 즉사.
- 사망 시각은 재생 시작 2~15초 후 (WER가 덤프 수집하는 동안 프로세스가 살아 보여 처음에 "영상 종료 시점"으로 오인 — 이벤트 타임스탬프로 교정).
- Recent changes: part2 전체 신규지만, 크래시 경로(배경창+플레이어)는 part1 코드 — part1은 실기 재생이 HUMAN-VERIFY 잔여였고 이번이 최초 실전 재생.
- Failing layer: `WallpaperInterop.AttachToWorkerW` 이후의 네이티브 컴포지션/입력 계층.

## Phase 2 — Hypotheses
- H1: WinUI 3 창을 WS_CHILD로 바꿔 explorer 소유 WorkerW에 크로스 프로세스 SetParent → WinUI 컴포지션/입력 브리지와 충돌해 네이티브 AV — 검증: 부착만 생략하는 진단 플래그 대조 실험 → **✅ 확정**
- H2: 보조 Window에서 WebView2 사용 자체가 문제 — 검증: H1 실험의 부착 생략 모드도 동일하게 보조 창+WebView2 사용 → 생존 → **❌ 기각**
- H3: 두 인스턴스의 WebView2 UDF 공유 충돌 — 검증: 단일 인스턴스에서 재현됨 (12:16 재현) → **❌ 기각**

## Phase 3 — Root Cause
**WinUI 3(WinAppSDK 2.2) 창의 크로스 프로세스 재부모화 비지원.**
`AttachToWorkerW`가 WinUI 3 `Window`의 HWND를 WS_CHILD로 전환하고 explorer의 WorkerW에
`SetParent`한다. WinUI 3 창은 자체 컴포지션 타깃·입력 사이트(InputSite)가 최상위 HWND에 결합돼
있어, 타 프로세스 창의 자식이 되면 컴포지션 커밋/입력 라우팅이 무효 상태를 참조 → 네이티브 AV.
WebView2 시각 트리 합성이 트리거를 가속(재생 시작 수 초 내). Lively 등 기존 배경화면 앱은
이 구조를 **순수 Win32 창**으로 수행한다 — XAML 창을 붙이는 part1 D3 설계 자체의 결함.

대조 실험 (동일 코드·영상·환경, 유일 변수 = WorkerW 부착):
| 조건 | 결과 |
|---|---|
| 부착 생략 (일반 창 표시) | 2/2 생존 (60초+, 영상 종료·정지 경로 포함) |
| 부착 (기존 코드) | 2/2 사망 (각 15초) |

## Phase 4 — Fix
미적용 — 수정이 part1 코어 구조 변경(다중 파일·아키텍처)이라 plan 범위 초과로 Halt, 사용자 승인 대기.
제안: WallpaperWindow(XAML)를 순수 Win32 창으로 대체하고 WebView2를
`CoreWebView2Environment.CreateCoreWebView2ControllerAsync(hwnd)`(컨트롤러 호스팅)로 전환
(T5 세션 프로브에서 이미 사용한 API — 검증된 배경화면 앱 구조와 동일).

## Verification
- 진단 코드는 전부 원복(git checkout), 실험 잔여물(바탕화면 잔상·프로세스) 정리 완료.
- 회귀 테스트는 수정 구현 시 추가 예정 (UI/네이티브 경로라 자동 테스트 불가 — 수동 재현 절차는 위 Reproduction).

## 부수 관찰 (별도 이슈 후보)
- 단일 인스턴스 보장 없음 — 트레이 상주 + VS 재실행으로 2개 인스턴스가 쉽게 공존 (위키 single-instance 패턴 참조). 크래시 원인은 아니지만(H3 기각) 별도 보완 가치.
- 유휴 워킹셋 ~208MB 관찰 (NFR-2 목표 150MB 초과 가능성) — T8 실측 시 확인 필요.
