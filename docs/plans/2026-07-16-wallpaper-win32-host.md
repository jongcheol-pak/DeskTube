# Plan: 배경창 Win32 호스트 전환 (AV 크래시 근본 수정)

**PRD**: docs/prd.md

## 요구 이해
- **원문 요청**: "System.AccessViolationException … 홈 화면에서 주소 입력후 바탕화면에서 재생 버튼을 클릭함" → 조사로 근본 원인 확정 후 사용자가 "Win32 창 + 컨트롤러 전환 (권장)" 수정 방향을 승인.
- **이해한 요구**: 재생 시작 시 발생하는 네이티브 AV 크래시를 근본 수정한다. 원인은 WinUI 3 `WallpaperWindow`를 WS_CHILD로 바꿔 explorer의 WorkerW에 크로스 프로세스 SetParent하는 part1 D3 설계(WinUI 3 컴포지션이 이를 비지원 — `docs/debug-2026-07-16-av-crash.md` 대조 실험으로 확정). 배경창을 **순수 Win32 창**으로 대체하고 WebView2를 **CoreWebView2Controller**(hwnd 호스팅)로 전환한다 — Lively 등 검증된 배경화면 앱과 동일 구조. 사용자 관찰 동작(아이콘 뒤 재생, 다중 모니터, 볼륨·화질·동기)은 전부 불변.
- **포함하지 않는 것**: 단일 인스턴스 보장(부수 관찰 — Deferred), 유휴 메모리 최적화(별도 실측 후).

## Goal
재생 시작 → 60초+ 생존(기존 3/3 사망 재현 시나리오)이 되도록 배경창 호스팅 계층을 Win32+컨트롤러로 교체한다. `IWallpaperHost`/`IPlayerHost` 공개 계약과 사용자 관찰 동작은 불변.

## PRD Coverage
| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-1 (유튜브 재생) | Must | T1 (구현 방식 교체 — 요구 불변) | ✅ 커버 |
| FR-2 (아이콘 뒤 배치) | Must | T1 (구현 방식 교체 — 요구 불변) | ✅ 커버 |
| FR-3~15, NFR-1~6 | Must/— | (없음) | 이번 범위 외 (기구현/후속) |

> PRD 요구 자체는 변경 없음(구현 계층 교체) — PRD 갱신 불요.

## Out of Scope
- 단일 인스턴스 보장, 유휴 메모리 최적화 (아래 Deferred)
- player.html·postMessage 브리지·재생 오케스트레이션 로직 변경 (그대로 재사용)

## Deferred / Follow-up
- 단일 인스턴스 보장 (Named Mutex + 창 전면화 — 위키 single-instance 패턴): 트레이 상주 + 재실행으로 2인스턴스 공존 관찰됨 (debug 문서 부수 관찰)
- 유휴 워킹셋 ~208MB 관찰 — T8 실측 시 NFR-2 목표(150MB)와 대조
- 전역 예외 훅(UnhandledException 로깅) 상시 탑재 검토 — 위키 global-exception-handling 패턴 (이번 조사에서 진단용으로 유효했음)

## Investigation Log
- 근본 원인·대조 실험: `docs/debug-2026-07-16-av-crash.md` (부착 생략 2/2 생존 vs 부착 2/2 사망, H2·H3 기각) — 이 세션 실측
- `IWallpaperHost`/`IPlayerHost`에는 `GetWindow`·UI 타입이 **없음** (Read 확인) — 인터페이스 무변경 가능, `PlaybackCoordinatorTests`의 Fake 구현 무영향 (grep 확인: 테스트는 인터페이스만 목킹)
- `WallpaperWindow` 사용처 전수 (grep): 코드 사용처 — WallpaperHost(생성·Surface·GetWindow), PlayerHost(ctor·_window·AttachContent·DispatcherQueue·retry timer), AppServices(GetWindow → PlayerHost 생성). 그 외 코드 사용처 없음. **주석 참조 2건 갱신 대상**: IWallpaperHost.cs:7(doc 주석)·Assets/player.html:7(문서 주석) — T1 동반 파일로 처리 (plan-reviewer m1)
- `CoreWebView2ControllerWindowReference.CreateFromWindowHandle`·`CreateCoreWebView2ControllerAsync`·`CookieManager`: WinRT 프로젝션 1.0.3719.77에 실재 (part2 T5에서 이미 사용·빌드 검증됨)
- PlayerHost의 DispatcherQueue 사용처는 retry timer 1곳 — AppServices.CreateAsync가 이미 `dispatcherQueue`를 보유(주입 가능, Read 확인)
- Win32 메시지 창 생성 패턴: `MonitorInterop.DisplayChangeWindow`에 RegisterClass/CreateWindowExW/WndProc/DestroyWindow 전례 있음 (재사용 기반)
- 위키 참조: `30_knowledge/patterns/topmost-clickthrough-overlay.md` — Win32 오버레이 창 ex-style(WS_EX_TOOLWINDOW·NOACTIVATE) 관행 확인 / `single-instance.md` — Deferred 근거 / DeskTube 관련 자료 없음(미등록)

## Risks & Unknowns
| 위험 | 영향 | 완화책 |
|---|---|---|
| 컨트롤러 호스팅에서 자동재생·가상 호스트 동작 차이 | 재생 실패 | 동일 CoreWebView2Environment(공유 UDF·autoplay 인자)·동일 설정 코드 재사용, acceptance에 UIA 자동 재현 검증 포함 |
| Win32 창의 DPI/좌표 (물리 픽셀) | 크기 오차 | 기존 MonitorInfo가 이미 물리 픽셀(EnumDisplayMonitors) — CreateWindowExW도 물리 픽셀이라 변환 불필요 (기존 PositionOnWorkerW 로직 유지) |
| 해상도 변경 시 컨트롤러 Bounds 미갱신 | 검은 여백 | WallpaperSurface.Resized 콜백(WM_SIZE) → PlayerHost가 Bounds 재설정 (T1 Design ③) |

## Impact Analysis
### 4-A. 심볼/타입 추적
- `WallpaperWindow` (삭제): 사용처 3파일 — WallpaperHost·PlayerHost·AppServices, 전부 T1 Files에 포함 (전수 grep, Investigation Log)
- `WallpaperHost.GetWindow` (시그니처 변경 → `GetSurface`): 호출자 AppServices 1곳뿐 (인터페이스 아님 — 공개 계약 영향 없음)
- `PlayerHost` ctor (시그니처 변경): 호출자 AppServices 1곳뿐
- `IWallpaperHost`/`IPlayerHost`: **무변경** — PlaybackCoordinator·테스트 무영향

### 4-B. 계약·직렬화 변경
- 없음 (설정·플레이리스트 JSON 무변경, 공개 인터페이스 무변경)

### 4-C. 영향 받는 테스트
- 기존 81개: 인터페이스 목킹이라 무영향 (통과 유지 확인)
- 신규 자동 테스트 불가 영역(네이티브 창·WebView2) → UIA 자동 재현 스크립트로 검증 (acceptance)

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| `WallpaperSurface` (Win32 배경창) | `MonitorInterop.DisplayChangeWindow` (메시지 전용 창) | 창 등록/파괴 패턴 재사용하되, 표시용 창(WS_POPUP→WorkerW 부착·WM_SIZE 콜백)이라 **신규 클래스** (메시지 전용 창과 책임 상이) |
| 컨트롤러 호스팅 | `YouTubeSessionService.WithCookieManagerAsync` (T5 임시 컨트롤러) | 생성 API 동일 — 상시 컨트롤러 소유로 **PlayerHost 내 신규 작성** (수명·이벤트가 다름) |

## Decisions
### D1. Win32 창 생성·부착 방식
- **Options**: A) CreateWindowExW(WS_POPUP) 후 기존 `AttachToWorkerW`(스타일 전환+SetParent) 재사용 / B) 처음부터 WorkerW를 parent로 WS_CHILD 생성
- **Chosen**: A
- **Rationale**: 기존 부착·재배치·해제·EnsureHealthy 경로를 그대로 재사용(최소 변경). 순수 Win32 창은 SetParent 방식이 Lively 등에서 검증됨 — 문제였던 것은 WinUI 3 창이지 SetParent 자체가 아님(debug 문서 H1 메커니즘).
- **Source**: debug 문서 Phase 3, WallpaperInterop 기존 코드

### D2. Win32 창 스타일
- **Chosen**: WS_POPUP + WS_EX_TOOLWINDOW|WS_EX_NOACTIVATE, 배경 브러시 검정(GetStockObject BLACK_BRUSH — 로드 전 깜빡임 방지), 표시용 창이므로 HWND_MESSAGE 부모 아님
- **Rationale**: Alt-Tab·포커스 훔침 방지 (위키 오버레이 패턴 관행). 부착 후 스타일은 AttachToWorkerW가 WS_CHILD로 전환.
- **Source**: 위키 topmost-clickthrough-overlay, 기존 SwShowNoActivate 관행

### D3. 컨트롤러 크기 추종
- **Chosen**: WallpaperSurface가 WM_SIZE에서 `Resized(width,height)` 콜백 발생 → PlayerHost가 구독해 `controller.Bounds` 갱신. 초기 Bounds는 InitializeAsync에서 클라이언트 크기로 설정.
- **Rationale**: Win32 호스팅은 host가 Bounds를 직접 관리해야 함(자동 리사이즈 없음). 창-컨트롤러 연결이 가장 지역적인 지점은 PlayerHost.
- **Source**: WebView2 Win32 호스팅 계약 (controller.Bounds)

### D4. PlayerHost 타이머 소스
- **Chosen**: `DispatcherQueue`를 AppServices에서 PlayerHost ctor로 주입 (retry timer용 — Win32 창엔 DispatcherQueue 없음)
- **Source**: AppServices.CreateAsync가 이미 dispatcherQueue 보유 (Investigation Log)

## Tasks

- [x] T1. 배경창 Win32 호스트 전환 (FR-1·FR-2 구현 계층 교체)
  - **Type**: D
  - **Design**: ① `Interop/WallpaperSurface.cs` 신규 — Win32 표시 창 소유(RegisterClass/CreateWindowExW/DefWindowProc WndProc/DestroyWindow — DisplayChangeWindow 패턴 재사용), `Hwnd`·`ClientSize`·`event Resized`·`Dispose` 책임 ② `WallpaperHost`: Surface 레코드를 WallpaperSurface 기반으로, `GetWindow`→`GetSurface(monitorId): WallpaperSurface?`, 부착/재배치/해제/EnsureHealthy 경로는 기존 유지 ③ `PlayerHost`: ctor `(WallpaperSurface surface, DispatcherQueue dispatcherQueue)`, InitializeAsync에서 `CreateCoreWebView2ControllerAsync(CreateFromWindowHandle(hwnd))`·Bounds=클라이언트 크기·기존 core 설정(가상 호스트·이벤트) 그대로, Resized 구독→Bounds 갱신, Dispose=`controller.Close()` ④ `AppServices.CreatePlayerAsync`: GetSurface+dispatcherQueue 주입 배선 ⑤ `Views/WallpaperWindow.xaml(.cs)` 삭제 ⑥ 이번에 안 함: 추상 창 팩토리·컨트롤러 풀링 등 간접화 없음 (직접 참조 유지)
  - **Acceptance**: ① `dotnet build --no-incremental -p:Platform=x64` 경고·오류 0 ② 기존 테스트 81/81 ③ **UIA 자동 재현(재생 클릭 → 60초 관찰) 2회 연속 생존** — 동일 시나리오가 수정 전 3/3 사망이었음 ④ 재생 화면이 실제 표시(WebView2 프로세스 기동 확인) ⑤ 정지 시 배경 복구 경로(DetachAll→SPI) 동작 — 로그 확인
  - **Files**:
    - 주: `src/DeskTube/Interop/WallpaperSurface.cs`(신규), `src/DeskTube/Services/WallpaperHost.cs`, `src/DeskTube/Services/PlayerHost.cs`, `src/DeskTube/Services/AppServices.cs`
    - 동반: `src/DeskTube/Services/IWallpaperHost.cs`(doc 주석의 WallpaperWindow 참조 갱신), `src/DeskTube/Assets/player.html`(문서 주석 갱신)
    - 삭제: `src/DeskTube/Views/WallpaperWindow.xaml`, `src/DeskTube/Views/WallpaperWindow.xaml.cs`
  - **Edge Cases**:
    - WebView2 런타임 부재 → 기존 HResult 0x80070002 판별·안내 경로 유지
    - Explorer 재시작(WorkerW 무효) → EnsureHealthy 재부착 경로가 Win32 hwnd로 동일 동작 (IsWindow 검사 유지)
    - 컨트롤러 생성 실패 → 기존 Result 실패 경로(원자적 정리 — Coordinator StartAsync)
    - 창 파괴 후 늦은 Resized/메시지 → WndProc는 Dispose 후 DefWindowProc만 (콜백 가드)
  - **Halt Forecast**:
    - (ii-a) 파일 삭제(WallpaperWindow.xaml/.cs) → `## 사전 승인 항목`
    - (i) "컨트롤러 vs 컴포지션 컨트롤러?" → 일반 컨트롤러(CreateCoreWebView2ControllerAsync)로 확정 — T5 검증 API, 배경창은 입력 불필요
  - **Depends on**: -

- [ ] T2. 조사 문서 마무리 (debug 문서 Fix/Verification 기록)
  - **Type**: A
  - **Design**: `docs/debug-2026-07-16-av-crash.md`의 "Phase 4 — Fix"·"Verification"을 실제 수정·검증 결과로 갱신
  - **Acceptance**: 문서에 수정 파일·검증 결과(빌드/테스트/재현 생존)가 기록됨
  - **Files**: 주: `docs/debug-2026-07-16-av-crash.md`
  - **Edge Cases**: (Type A — skip)
  - **Halt Forecast**: 없음 — 순수 문서 갱신, 파괴적·의존성·외부 요소 없음
  - **Depends on**: T1

## 사전 승인 항목 (일괄 승인 대상)
- T1 — 파일 삭제: `src/DeskTube/Views/WallpaperWindow.xaml`, `WallpaperWindow.xaml.cs` (Win32 창으로 대체 — 되돌리기: git)
- 전 task — 로컬 작업 브랜치 커밋 (push 아님)

## 불가피한 Halt (위임 불가)
- git push·태그·릴리즈·PR — 항상 별도 승인
- 수정 후에도 동일 AV가 재현되는 경우 (원인 재분석 — 돌발)

## Verification Strategy
- 빌드/테스트/format: part2와 동일 명령 (`-p:Platform=x64`, `--no-incremental`)
- 실기 재현: 디버그 세션에서 확립한 UIA 자동 시나리오(패키지 활성화 → URL 입력 → 재생 클릭 → 60초 관찰 → 프로세스 정리·배경 복구) 2회 — 수정 전 3/3 사망과 대조
- HUMAN-VERIFY 잔여: 아이콘 뒤 렌더링 시각 확인(빌드·생존으로는 z-order 시각 품질 미보장), 다중 모니터 동기(2모니터 환경)

## Phase Ledger

## Retry Ledger

## Progress Log

## Next Steps
- 승인 시 `pjc:implement-task`로 실행 (경로 명시: `docs/plans/2026-07-16-wallpaper-win32-host.md 구현`)

## Open Questions
- (없음 — 수정 방향은 사용자 승인됨, 구조 결정은 근거로 확정: D1~D4)
