# Plan: 중복 실행 방지 — 단일 인스턴스 + 활성화 리다이렉션 (2026-07-19)

**PRD**: docs/prd.md

## 요구 이해
- **원문 요청**: "중복 실행 방지 — 트레이에 실행되어 있는 경우 바로가기를 클릭하는 경우 메인 실행 / 메인 실행된 상태에서 바로가기 클릭시 메인창 활성화 / 재부팅 후 자동실행 되기 전에 사용자가 실행해서 트레이로 실행되어 있는 경우 자동실행으로 인해서 메인화면이 뜨면 안됨. 자동실행 전에 사용자가 실행한 경우 자동실행 동작은 무시, 실행된 앱에 아무 영향을 주면 안됨"
- 앱은 항상 **1개 프로세스만** 실행된다. 두 번째 실행 시도는 새 창을 띄우지 않고 기존 인스턴스로 위임 후 조용히 종료한다.
- **바로가기(일반 실행)로 재실행** 시: 기존 인스턴스가 트레이 상주(창 숨김)든 창 표시 중이든, **메인창을 표시·전면 활성화**한다.
- **자동실행(StartupTask)이 두 번째로 도착** 시(사용자가 먼저 실행해 둔 경우): 기존 인스턴스에 **아무 영향 없이 무시**한다 — 창 표시 없음, 자동 재생 트리거 없음.
- 최초 실행(첫 인스턴스)의 기존 동작(FR-8 자동실행 트레이 시작·자동 재생, FR-19 일반 실행 자동 재생)은 변경하지 않는다.

## Goal
어떤 순서·조합으로 실행해도 프로세스는 1개만 남고, 바로가기 재실행은 메인창 활성화로, 자동실행 중복은 무영향 무시로 동작한다.

## PRD Coverage
| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-22 (신설: 단일 인스턴스 — 중복 실행 방지, 사용자 합의 완료) | Must | T1~T4 | ✅ 커버 |
| FR-8·FR-19 (기존 동작 불변 — 최초 인스턴스 경로 무변경) | Must | T2·T3 회귀 확인 | ✅ 커버 (동작 보존) |
| FR-1~FR-21 나머지 | Must/Should/Could | (기구현) | 이번 범위 외 (기구현/후속) |

## Out of Scope
- 다중 사용자 세션(빠른 사용자 전환) 간 단일화 — MSIX 앱은 사용자별 설치·실행이며 `AppInstance` 키도 사용자별 격리(OS 보장). 사용자 간 중복 방지는 요구에 없음.
- 두 번째 실행에 명령줄 인자로 "특정 동작 지시"(URL 열기 등) 전달 — 요청에 없음(활성화만 위임).

## Deferred / Follow-up
- (없음)

## Investigation Log
- 위키 참조: vault 미설정 — 코드 1차 출처로 진행 (직전 plan과 동일 상태)
- Deferred 대장 `## 대기` 확인 — **"단일 인스턴스 보장 (Named Mutex + 창 전면화)" 항목(2026-07-16 등재)이 이번 요청으로 재수용됨** (본 plan 채택 — 완료 시 F-6.5 규정대로 종결 이동). 방식은 Named Mutex 대신 AppInstance 리다이렉션으로 확정(D1 — 요구 3의 활성화 종류 판별에 필수). 그 외 관련 항목 없음.
- 직전 plan `2026-07-19-item-duration.md`: Phase Ledger `Phase G 통과 (Must 100%)` → 완료 확정. Deferred 1건(헤더 합계)은 대장 `## 대기`로 이관 완료 (본 세션).
- Step 0: 미커밋 직전 작업(help.md 도움말+캡처)은 사용자 승인으로 `5b4b955` 커밋 — 깨끗한 상태에서 시작. PRD 반영은 사용자 확정: **FR-22 신설**.
- `src/DeskTube/App.xaml.cs:96~118` — `OnLaunched`: `WasActivatedByStartupTask() || StartupArgs.HasStartupFlag(...)`로 quietStart 판별 → 창 생성 후 quiet면 `Activate()` 생략. `ShowMainWindow(string?)`(184~197행): `AppWindow.Show()` + `Activate()` — 트레이 메뉴·전곡 실패 안내가 재사용하는 창 표시 단일 경로. `MainWindowHandle`(91~94행): hwnd 접근자 기존재. **직접 확인.**
- `src/DeskTube/Services/StartupService.cs` — `StartupArgs.HasStartupFlag(IEnumerable<string?>)`(순수 함수·테스트 기존재), `WasActivatedByStartupTask()`: `AppLifecycleInstance.GetCurrent().GetActivatedEventArgs().Kind == ExtendedActivationKind.StartupTask` + 실패 시 false 폴백(부팅 초기 COM 미준비 대비). `Microsoft.Windows.AppLifecycle.AppInstance` 별칭 관례(CS0104 회피) 기존재. **직접 확인** — AppLifecycle API가 이 레포에서 이미 검증된 상태.
- `src/DeskTube/obj/Debug/net10.0-windows10.0.22621.0/win-x64/App.g.i.cs:14~32` — 생성 Main은 `#if !DISABLE_XAML_GENERATED_MAIN` 가드: `InitializeComWrappers()` → `Application.Start(...)에서 DispatcherQueueSynchronizationContext 설정 + new App()`. 상수 정의 시 커스텀 Main으로 완전 대체 가능. **직접 확인.**
- `src/DeskTube/MainWindow.xaml.cs:139~157` — 닫기→숨김: `OnAppWindowClosing`에서 `sender.Hide()`(트레이 상주 시). 숨김 창의 재표시는 `AppWindow.Show()`로 충분(ShowMainWindow 기존 경로). **직접 확인.**
- `src/DeskTube/App.xaml.cs:120~147` — 자동 재생 트리거는 `InitializeServicesAsync`(최초 인스턴스 초기화 경로)에서만 발생 — 리다이렉트 수신 경로에 자동 재생 없음이 구조적으로 보장됨. **직접 확인.**
- 단일 인스턴스·Mutex 기존 구현 grep(`mutex|singleinstance|single-instance`) — **0건**. 신규 도입 확정.
- `SetForegroundWindow|AllowSetForegroundWindow` grep — 0건. `Interop/` 관례: 도메인별 파일 분리(`ProcessInterop.cs` = kernel32 워킹셋 트림 등), internal static class + DllImport + 한글 문서주석. **직접 확인.**
- `tests/DeskTube.Tests/StartupArgsTests.cs` 기존재 — 시작 판별 순수 로직의 테스트 위치 관례.
- csproj — `WindowsAppSdkAutoInitialize=false` + `App` 생성자에서 `DeploymentManager.Initialize`(테스트 호스트 보호). 커스텀 Main의 AppInstance 호출은 App 생성 전이지만, 패키지 앱은 프레임워크 패키지 의존성으로 런타임이 로드 보장되고 `GetActivatedEventArgs`는 기존에도 `OnLaunched`(Initialize 이후이긴 하나 배포 초기화와 무관한 AppLifecycle API)에서 사용 중. 실패 대비 try/catch 폴백(D4)으로 방어. **직접 확인.**

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| `Program`(커스텀 Main) | 생성 Main(App.g.i.cs)만 존재 | 신규 — 생성 Main은 리다이렉션 삽입 불가(자동 생성), `DISABLE_XAML_GENERATED_MAIN`으로 대체 |
| `StartupArgs.IsQuietActivation` | `HasStartupFlag`·`WasActivatedByStartupTask` | 기존 2개 판별을 조합한 순수 함수 신규 — 리다이렉트 수신 인자용(GetCurrent가 아닌 이벤트 페이로드 대상이라 `WasActivatedByStartupTask` 재사용 불가) |
| `Interop/ActivationInterop.cs` | `ProcessInterop`(kernel32 워킹셋) — 책임 다름 | 신규 파일 — Interop 도메인별 파일 분리 관례. user32 전면화 + 리다이렉트 대기 API |
| 창 표시·활성화 | `App.ShowMainWindow` 기존 경로 | **재사용** (전면화 보강만 추가) |
| 시작 종류 판별(최초 인스턴스) | `WasActivatedByStartupTask`·`HasStartupFlag` | **재사용** (무변경) |

## 시각 요소 분해
(해당 없음 — 시각 충실도 요청 아님, 신규 UI 없음)

## Tasks

- [x] T1. PRD FR-22 신설 (FR-22 충족)
  - **Type**: A
  - **Acceptance**: FR 표에 FR-22 행 신설 — "앱은 단일 인스턴스로 실행된다: 실행 중 재실행 시 새 창을 띄우지 않고 기존 인스턴스의 메인창을 표시·전면 활성화하며(트레이 상주 중에도 동일), 자동실행(StartupTask)이 중복 도착하면 기존 인스턴스에 아무 영향 없이 무시한다(창 표시·자동 재생 없음). 최초 실행의 FR-8·FR-19 동작은 불변" 취지, 우선순위 Must, 검증 "단위테스트: 활성화 종류 판별 로직 + 리다이렉션·창 활성화는 HUMAN-VERIFY". 변경 이력 1줄. 역대조 누락·잔존·변형 0.
  - **Files**: 주: `docs/prd.md`
  - **Edge Cases**: (Type A — skip)
  - **Halt Forecast**: 없음 — FR-22 신설은 사용자 확정 완료 (사전 승인 항목 등재).
  - **Depends on**: 없음

- [x] T2. 단일 인스턴스 진입 게이트 — 커스텀 Main + 리다이렉션 (FR-22 충족)
  - **Type**: D (앱 진입점 구조 변경 + 빌드 상수 — 크로스커팅)
  - **Design**:
    - 배치: 진입점은 신규 `src/DeskTube/Program.cs`(App.xaml.cs와 책임 분리 — 프로세스 게이트 vs 앱 수명주기), P/Invoke는 신규 `src/DeskTube/Interop/ActivationInterop.cs`(도메인별 파일 관례), 빌드 상수는 `DeskTube.csproj`.
    - 신규 심볼: `Program`(static class — `Main`, `TryRedirectToExistingInstance`), `ActivationInterop`(internal static — `AllowSetForegroundWindow`, `SetForegroundWindow`, `CreateEvent`/`SetEvent`/`CoWaitForMultipleObjects` 리다이렉트 대기용).
    - 의존 방향: Program → AppLifecycle AppInstance + ActivationInterop + App(Start만). 역참조는 App→`Program.SingleInstanceFallbackReason` 읽기 1건만(D4 지연 로그 — 수정 내용 4와 정합, spec 리뷰 M1 정정).
    - 비추상화: SingleInstanceService 같은 서비스·인터페이스 신설 안 함(프로세스 게이트는 DI 그래프 밖·App 생성 전 실행 — 정적 진입점 지역성 유지), 키 상수 설정화 안 함(고정 "main" — D6).
  - **수정 내용**:
    1. `DeskTube.csproj` — `<DefineConstants>$(DefineConstants);DISABLE_XAML_GENERATED_MAIN</DefineConstants>` 추가 (한글 주석: 단일 인스턴스 리다이렉션을 위해 생성 Main을 Program.cs로 대체 — FR-22).
    2. `Program.cs` 신설 — `[STAThread] Main`: `InitializeComWrappers()` → `TryRedirectToExistingInstance()`가 true면 즉시 반환(조용히 종료), false면 생성 Main과 동일하게 `Application.Start`(DispatcherQueueSynchronizationContext 설정 + `new App()`).
    3. `TryRedirectToExistingInstance()`: try { `GetCurrent().GetActivatedEventArgs()` + `FindOrRegisterForKey("main")` } — `IsCurrent`면 false(최초 인스턴스, 계속 진행). 아니면 `ActivationInterop.AllowSetForegroundWindow(keyInstance.ProcessId)`(기존 인스턴스에 전면화 권한 위양, 실패 무시) 후 `RedirectActivationToAsync(args)`를 STA 안전 대기(백그라운드 Task에서 완료 → 이벤트 핸들 SetEvent, 메인은 `CoWaitForMultipleObjects` — MS 공식 단일 인스턴스 샘플 패턴)로 완료시키고 true 반환.
    4. 예외 폴백(D4): AppInstance 조회·등록 예외 → false 반환(일반 시작 계속 — 오늘과 동일한 최악으로 저하, 무반응·크래시 방지). 리다이렉트 자체 실패(기존 인스턴스 소멸 직후 race) → false 반환(일반 시작 계속). 폴백 시 로그 — 단 `AppLog.Initialize` 전이므로 `Debug.WriteLine` + 폴백 사유를 필드에 보관해 OnLaunched에서 AppLog 기록(간단 정적 필드 1개).
  - **Acceptance**: 빌드 경고 0·오류 0 + 기존 테스트 전체 통과 + 빌드 산출물에서 생성 Main 미포함(커스텀 Main 단일 진입점 — `App.g.i.cs`의 `#if` 가드로 컴파일 제외 확인). 실제 2중 실행 방지는 ⏳ HUMAN-VERIFY (T3까지 완료 후 시나리오 검증).
  - **Files**: 주: `src/DeskTube/Program.cs`(신규), `src/DeskTube/Interop/ActivationInterop.cs`(신규), `src/DeskTube/DeskTube.csproj`, `src/DeskTube/App.xaml.cs`(D4 폴백 사유 지연 기록 3줄 — 구현 중 Files 추가)
  - **Edge Cases**:
    - 부팅 초기 COM/WinRT 미준비로 AppInstance 조회 실패 → false 폴백(D4) — StartupService의 기존 실패 폴백 관례와 동일 방향(기능 저하 > 크래시).
    - 기존 인스턴스가 트레이 '종료' 진행 중일 때 재실행 race — 리다이렉트 실패 시 일반 시작 폴백으로 새 primary가 됨(무반응 방지). 극단 race로 순간 2프로세스 공존 가능 — 오늘의 상시 2중 실행보다 좁은 창, 수용.
    - 거의 동시 2회 실행(부팅 자동실행 + 사용자 클릭) — `FindOrRegisterForKey`가 OS 수준에서 단일 승자 보장, 패자는 리다이렉트.
    - 테스트 호스트가 앱 어셈블리 로드 — Main은 실행되지 않으므로(호스트가 진입점 미호출) 기존 `WindowsAppSdkAutoInitialize=false` 보호와 충돌 없음.
  - **Halt Forecast**: csproj 빌드 상수 추가(동작 영향 설정) + 진입점 구조 변경 + 신규 파일 2개 — **사전 승인 항목 등재**. 파괴적·외부 작업 없음.
  - **Depends on**: 없음

- [ ] T3. 리다이렉트 수신 처리 — 활성화 종류별 창 표시/무시 + 판별 테스트 (FR-22 충족)
  - **Type**: C (2~3개 파일 + 테스트, 신규 심볼 도입)
  - **Design**:
    - 배치: 수신 구독은 `App.OnLaunched`(창 생성 직후 — 핸들러가 `_window.DispatcherQueue`로 마셜링, D7), 판별 순수 함수는 `Services/StartupService.cs`의 `StartupArgs`(시작 판별 책임 지역성 — HasStartupFlag 옆), 키 해제는 `ExitApplication`.
    - 신규 심볼: `StartupArgs.IsQuietActivation(ExtendedActivationKind kind, IEnumerable<string?> args)`(순수 — StartupTask 종류 또는 -startup 플래그면 true), `App.OnRedirectedActivated` 핸들러.
    - 의존 방향: App → AppLifecycle AppInstance(구독) + StartupArgs(판별) + ShowMainWindow(기존 경로 재사용) + ActivationInterop.SetForegroundWindow. StartupArgs는 WinUI 미의존 유지(enum 인자만 — 테스트 참조 안전).
    - 비추상화: 활성화 라우터·핸들러 체인 신설 안 함(종류 2분기뿐 — if 1개 지역성 유지), `TimeUpdated`류 신규 이벤트 없음.
  - **수정 내용**:
    1. `StartupService.cs` — `StartupArgs.IsQuietActivation` 추가: `kind == ExtendedActivationKind.StartupTask || HasStartupFlag(args)` (문서주석: 리다이렉트 수신 인자 판별용 — GetCurrent 기반 `WasActivatedByStartupTask`와 구분).
    2. `App.xaml.cs` — `OnLaunched`에서 창 생성 직후 `AppLifecycleInstance.GetCurrent().Activated += OnRedirectedActivated` 구독(별칭 using 관례 승계). 핸들러: 페이로드 `AppActivationArguments`에서 kind + (Launch면 `ILaunchActivatedEventArgs.Arguments` 공백 분리) 추출 → `IsQuietActivation`이면 AppLog 기록 후 무시, 아니면 `DispatcherQueue.TryEnqueue`(발생 스레드 비보장 — OnAllItemsFailed 마셜링 관례)로 `ShowMainWindow(null)` + `ActivationInterop.SetForegroundWindow(MainWindowHandle)`(T2에서 위양받은 권한으로 전면화, 실패 무시 — best-effort).
    3. `ExitApplication` — `AppLifecycleInstance.GetCurrent().UnregisterKey()` 추가(트레이 종료 시 키 조기 해제 — 종료 직후 재실행 race 축소. 프로세스 소멸 시 OS 해제가 최종 안전망).
    4. `tests/DeskTube.Tests/StartupArgsTests.cs` — `IsQuietActivation` 테스트 4건+: StartupTask 종류 → true / Launch + "-startup" 인자 → true / Launch + 일반 인자·빈 인자 → false / Launch + null 요소 포함 → false(무해).
  - **Acceptance**: 빌드 경고 0·오류 0 + 신규 판별 테스트 포함 전체 통과. 시나리오 3종(트레이 중 바로가기→창 표시 / 창 표시 중 바로가기→전면 활성화 / 사용자 선실행 후 자동실행 도착→무영향)은 ⏳ HUMAN-VERIFY.
  - **Files**: 주: `src/DeskTube/App.xaml.cs`, `src/DeskTube/Services/StartupService.cs`, `tests/DeskTube.Tests/StartupArgsTests.cs`
  - **Edge Cases**:
    - 리다이렉트가 quiet(StartupTask/-startup) → 무시: 창 표시 없음 + 자동 재생 없음(자동 재생 트리거는 최초 인스턴스 초기화 경로에만 존재 — Investigation Log 구조 확인). 요구 3 충족.
    - 언어 변경으로 창 재생성(`ApplyLanguageChange`) 후 리다이렉트 → `ShowMainWindow`가 현재 `_window` 참조로 동작(기존 접근자) — 안전.
    - 창 등록(Main)~구독(OnLaunched) 사이 ms 단위 공백에 리다이렉트 도착 → 해당 1회 유실(무반응). 재클릭으로 회복 — 수용(D7).
    - Activated 이벤트 스레드 비보장 → DispatcherQueue 마셜링(기존 관례).
    - 전면화 실패(포그라운드 권한 규칙) → 창 표시는 되고 전면만 실패할 수 있음 — AllowSetForegroundWindow 위양(T2)으로 통상 성공, 실패해도 작업 표시줄 점멸로 사용자 인지 가능(best-effort, 예외 미전파).
    - 서비스 초기화 실패로 트레이 미생성 상태의 리다이렉트 → ShowMainWindow는 _window만 필요 — 동작.
  - **Halt Forecast**: `StartupArgs` 공개 정적 메서드 추가(계획된 공개 API 추가 — 호출부 신규뿐, 기존 시그니처 불변) — **사전 승인 항목 등재**. 파괴적·외부 작업 없음.
  - **Depends on**: T2

- [ ] T4. 문서 갱신 — README·help.md (FR-22 충족)
  - **Type**: A
  - **Acceptance**: README 기능 서술에 "중복 실행 방지(단일 인스턴스): 실행 중 재실행 시 기존 앱의 메인창 활성화, 자동실행 중복은 무영향 무시" 추가. help.md의 기본 동작/FAQ에 사용자 관점 서술 1항목 추가("앱이 이미 실행 중일 때 바로가기를 다시 누르면 창이 열립니다" 취지). 역대조 누락·잔존·변형 0.
  - **Files**: 주: `README.md`, `help.md`
  - **Edge Cases**: (Type A — skip)
  - **Halt Forecast**: 없음 — 순수 문서 서술 추가, 파괴적·의존성·외부 요소 없음.
  - **Depends on**: T3

## 사전 승인 항목 (일괄 승인 대상)
- T1 — PRD FR-22 신설 (사용자 질문 확정 반영 — 문안은 T1 Acceptance)
- T2 — `DeskTube.csproj`에 `DISABLE_XAML_GENERATED_MAIN` 컴파일 상수 추가 (생성 Main → 커스텀 Main 대체, 동작 영향 설정)
- T2 — 신규 파일 2개 추가: `Program.cs`(앱 진입점 구조 변경), `Interop/ActivationInterop.cs`(user32/kernel32/ole32 P/Invoke)
- T3 — `StartupArgs` 공개 정적 메서드 `IsQuietActivation` 추가 (기존 멤버 시그니처 불변)
- 작업 브랜치: 현 HEAD(task/item-duration `5b4b955`) 기준 새 브랜치 `task/single-instance` 생성 후 로컬 checkpoint/task 커밋 (implement-task 위임 범위)

## 불가피한 Halt (위임 불가)
- push · main 병합 · 태그/릴리즈 — 구현·검증 완료 후 최종 보고에서 별도 승인

## 검증 방법
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` (경고 0·오류 0)
- 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -c Debug -p:Platform=x64` (신규: IsQuietActivation 판별 4건+)
- 포맷: `dotnet format` 위반 0
- 동작(⏳ HUMAN-VERIFY — MSIX 배포 상태에서): ① 트레이 상주 중 바로가기 실행 → 메인창 표시·전면, 프로세스 1개 유지 ② 창 표시 중 바로가기 실행 → 창 전면 활성화 ③ 앱 실행 상태에서 `-startup` 인자 실행(자동실행 시뮬레이션) → 창·재생 상태 무변화, 두 번째 프로세스 소멸 ④ 재부팅: 사용자 선실행 → 자동실행 도착 시 무영향 (③으로 근사 확인 가능, 실부팅은 선택) ⑤ 단독 자동실행·단독 일반 실행의 기존 동작(FR-8·FR-19) 회귀 없음

## Decisions
- D1. 방식 = **AppLifecycle `AppInstance` 리다이렉션** (Named Mutex 아님) — 요구 3의 "자동실행 중복 무시"는 두 번째 실행의 **활성화 종류(StartupTask 여부)를 기존 인스턴스가 알아야** 성립하는데, 리다이렉션은 활성화 인자를 그대로 전달한다(Mutex는 별도 IPC 필요 — 근본 해결 아님). MSIX 패키지 앱 공식 단일 인스턴스 패턴. Source: StartupService.cs가 동일 API 사용 중(레포 검증), MS Learn "Making the app single-instanced" 샘플.
- D2. 진입 지점 = 커스텀 `Main`(`Program.cs`) + `DISABLE_XAML_GENERATED_MAIN` — 리다이렉션은 XAML·App 생성 **전**에 판정해야 두 번째 프로세스가 창·리소스를 만들지 않는다(깜빡임·낭비 제거). Source: obj `App.g.i.cs` 생성 Main `#if` 가드 직접 확인.
- D3. quiet 판별 = `kind == StartupTask || -startup 플래그` — 최초 인스턴스의 기존 이중 판별 관례(활성화 종류 + 인자 폴백)를 리다이렉트 수신에도 동일 적용. `-startup` 수동 시뮬레이션 실행도 자동실행과 동일하게 무시(일관성). Source: App.xaml.cs:107, StartupService D3.
- D4. 게이트 실패 폴백 = **일반 시작 계속** — AppInstance 예외·리다이렉트 실패 시 새 인스턴스로 그냥 뜬다. 최악이 "오늘과 동일(2중 실행)"이고, 조용한 종료 폴백은 "앱이 안 뜬다"는 더 나쁜 무반응. Source: StartupService의 실패 시 false 폴백 관례(기능 저하 > 크래시).
- D5. 전면화 = 두 번째 프로세스가 `AllowSetForegroundWindow(기존 PID)` 위양 → 기존 인스턴스가 `SetForegroundWindow` — Windows 포그라운드 규칙상 백그라운드 프로세스는 스스로 전면화 불가, 실행 주체(포그라운드 권한 보유)의 위양이 표준 우회. `AppInstance.ProcessId`로 대상 PID 취득. 실패는 무시(best-effort — 창 표시는 별도 성립).
- D6. 인스턴스 키 = `"main"` 고정 — 앱 전역 1개 인스턴스 요구라 구분 키 불필요, MSIX 사용자별 격리는 OS 보장.
- D7. Activated 구독 위치 = `App.OnLaunched`(창 생성 직후) — 핸들러가 창 DispatcherQueue·ShowMainWindow에 접근해야 하며, Main(App 생성 전) 구독은 정적 브리지가 필요해 복잡도만 는다. 등록~구독 사이 ms 공백의 리다이렉트 1회 유실은 재클릭 회복으로 수용(발생 조건: 최초 기동 중 거의 동시 재실행 — 극히 드묾).
- D8. 종료 시 `UnregisterKey` — 트레이 종료에서 키 조기 해제로 "종료 직후 재실행" race 창 축소. 크래시 종료는 OS의 프로세스 소멸 해제가 안전망.
- D9. 리다이렉트 대기 = 백그라운드 Task 완료 → 이벤트 핸들 + `CoWaitForMultipleObjects` — STA 메인 스레드에서 `AsTask().Wait()` 직접 블로킹은 COM 마셜링 교착 위험(MS 샘플이 이 패턴을 명시). Source: MS Learn 단일 인스턴스 샘플.

## Progress Log
- (구현 시작 전)

## Next Steps
- (구현 완료 후) HUMAN-VERIFY 5항목 확인 → 커밋·병합 여부는 별도 승인

## Open Questions
- [x] Q1. 미커밋 help.md 작업 처리 → **현 브랜치에 문서 커밋** (`5b4b955` 완료)
- [x] Q2. PRD 반영 방식 → **FR-22 신설** (FR-8 보강 아님 — 바로가기 활성화는 자동실행과 별개 동작)
