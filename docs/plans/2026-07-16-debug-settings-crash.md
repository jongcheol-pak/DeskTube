# Debug: 설정 메뉴 클릭 시 크래시 (c0000409)

> pjc:pjc-systematic-debugging 조사 로그 — 2026-07-16. 표준 경로(Phase 1~3) 수행,
> **17:52 크래시의 단일 근본 원인은 미확정** (재현 불가 — Halt 조건 보고). 수정은 승인 대기.
>
> **[2026-07-18 종결 부록]** 미확정이던 "재생 중 화면 전환 크래시" 계열의 근본 원인이 확정됐다:
> **PlayerHost가 CoreWebView2 RCW를 강참조하지 않아**(컨트롤러만 필드 보유, core는 프로퍼티 접근)
> 페이지 전환 등 할당 급증 시 GC가 RCW를 수집 → CsWinRT 이벤트 구독 상태(ConditionalWeakTable 키)가
> 소멸 → 재생 중 매초 오는 WebMessageReceived 네이티브 콜백이 해제된 스텁 호출 → CLR fatal
> (pre-allocated ExecutionEngineException). 덤프(2026-07-18, 재생 중 메뉴 이동 100% 재현)의
> 네이티브 스택(EmbeddedBrowserWebView!FireWebMessageReceived → 프로젝션 경계 예외 →
> EEPolicy::HandleFatalError)과 힙 증거(PlayerHost·Controller 생존, CoreWebView2 RCW 0개)로 확정.
> 20:44 "무조작 크래시"(백그라운드 GC 타이밍)·17:52 "재생 중 첫 설정 진입 크래시"와 정합 —
> H2(stale 배포)가 아니라 이것이 원인이었을 개연성이 높다. 수정: PlayerHost `_core` 필드 강참조
> (notes.md 2026-07-18 항목 참조).

## Symptom

- 사용자 보고: 설정 메뉴 클릭 시 DeskTube.exe 크래시.
- 이벤트: 2026-07-16 17:52:13 (KST), ExceptionCode **c0000409** (fail-fast), ModuleName unknown,
  FaultingOffset이 ntdll 이미지 끝 바로 뒤(모듈 밖 = 동적 스텁 추정), 서브코드 0x0a.
- 같은 날 크래시 총 6건 발생 (아래 표).

## Reproduction

재현 실패. UIA 자동화로 배포 빌드(18:06)에서 12회 이상 시도:
- 실행→설정 클릭 (1.5s 지연) ×5 → 전부 생존, 로그로 설정 진입 확인
- 실행 직후 최속 클릭 ×4 → 생존
- 재생 시작 후 0.5/1.5/3초 뒤 설정 클릭 ×3 → 생존
- (단, 자동화 도중 20:44에 **무조작 상태**에서 동일 계열 AV 크래시 1건 발생 — 아래)

## Phase 1 — Evidence

오늘(07-16) 크래시 전수:

| 시각 | 코드 | 증거 | 판정 |
|---|---|---|---|
| 12:05 | e0434352 | 덤프: COMException **0x80040154** — Application.Start의 IApplicationStatics 활성화 실패 | 기지 함정(notes.md): 패키지 앱 exe 직접 실행 시 WinAppSDK 활성화 실패. 설정과 무관 |
| 12:16 | c0000005 | 덤프: WebView2 `EBWebViewEnvironment::RetryCreateWebView` → **CompositionController 생성 완료 콜백** → Microsoft.UI.Xaml.Controls(XAML WebView2) → AV → CLR FailFast | **확정**: XAML WebView2(LoginWindow 전용) 수명 경합 |
| 12:22 | c0000005 | 덤프: 12:16과 동일 스택 | 동일 |
| 12:23 | c0000005 | 덤프: AV → ExecutionEngineException, coreclr+0x1d4660 (예외 추적기 부근) | 같은 계열 (네이티브 경계에서 CLR 치명 오류) |
| **17:52** | **c0000409** | 덤프 없음(WER 큐는 관리자 권한 필요). 앱 로그 0줄 = SettingsViewModel.Populate의 로그(287행) **이전** 크래시. 프로세스 수명 53초 | **미확정** — 사용자가 보고한 건 |
| 20:44 | c0000005 | 재현 자동화 중 발생. coreclr+**0x1d4660** (12:23과 동일 지점), UI 무조작, 실행 18초 뒤 | 같은 계열, WebView2·로그인 미개입 → 원인 폭 넓음 |

부가 사실:
- App에 **UnhandledException 핸들러 없음** → 관리 예외가 새면 로그 없이 즉사 (이번 조사가 어려웠던 직접 원인).
- 17:52 크래시 빌드는 음소거 수정(6111b23, 18:53) **이전** 빌드. FR-18(T1~T6)은 모두 크래시 이후 커밋 — FR-18은 용의선상 제외.
- 18:43 세션(18:06 빌드)에서는 설정 진입 성공 로그 존재.
- 배포 방식: 패키지 loose 배포(AppX 폴더) — 개발 중 증분 재배포 반복.

## Phase 2 — Hypotheses

- H1: LoginWindow(XAML WebView2)의 수명 결함 — WebView2 이벤트 핸들러 안에서 자기 Close() + 컨트롤러 생성 완료 전 창 닫힘 레이스 → 해제된 컨트롤로 완료 콜백 진입.
  - 검증: 덤프 2건(12:16·12:22)의 스택과 정확히 부합 → ✅ **확정 (해당 2건에 한해)**. 단 17:52 건은 로그인 창이 열린 적 없는 세션(설정 로드 로그 0)이라 이 경로가 아님.
- H2: loose 배포의 stale/혼합 상태 (캐시된 resources.pri의 XBF·컴파일드 바인딩과 새 DLL 불일치) → 페이지 생성 중 네이티브 점프 오류 → CFG fail-fast.
  - 정황: 17:52는 페이지 생성 단계 크래시. **notes.md 기존 기록과 교차 부합** — 같은 날 음소거 수정 세션이
    "loose 배포는 패키지 버전이 같으면 OS가 resources.pri(xbf 포함)를 캐시해 재빌드가 화면에 반영 안 됨 —
    Remove-AppxPackage 후 재등록 필요" 함정을 발견·기록했고, 실제로 AppxManifest 재등록 18:42 →
    설정 진입 성공 18:43 → 이후 동일 조작 12회+ 무크래시. 즉 17:52 시점 배포는 stale 캐시 상태였을 개연성이 높음.
    → ⚠️ 가장 유력하나 당시 배포 상태가 소실돼 소급 **확정은 불가**.
- H3: 설정 페이지 로드 경로(모니터 열거 P/Invoke·SettingsExpander) 자체 결함.
  - 검증: 동일 경로 12회+ 자동화 통과, 코드 리뷰상 델리게이트 수명·스레드 경계 문제 없음 → ❌ 기각 (결정적 조건이면 매번 죽어야 함).

## Phase 3 — Root Cause

- **12:16·12:22 (확정)**: XAML WebView2(LoginWindow)의 컨트롤러 생성 재시도 완료 콜백이 이미 해제된 컨트롤 상태로 진입 → AV. 코드상 대응 결함 실재:
  - `LoginWindow.OnNavigationCompleted`가 WebView2 이벤트 콜스택 **안에서** `Close()` 호출 (Views/LoginWindow.xaml.cs:59)
  - 창 Closed 시 `LoginWebView.Close()` 미호출 — 컨트롤러 확정 해제 없이 네이티브 콜백 잔존 가능
- **17:52 (사용자 보고 건, 미확정)**: 페이지 생성 단계의 네이티브 fail-fast. H2(stale 배포)가 가장 정합적이나 소급 검증 불가. 현재 배포 빌드에서는 재현되지 않음.

## 사용자 확인 정보 (사후 접수)

- 17:52 크래시 당시: **홈 화면에서 재생 시작 → 설정 화면 진입, 첫 진입에서만 크래시**, 이후 재발 없음.
- 의미: 크래시 시점에 재생용 WebView2(윈도우드 컨트롤러)가 살아 있었음 — 크래시 계열(WebView2/XAML 네이티브 경계)과 정합.
  로그인 창은 열린 적 없으므로 H1(LoginWindow)은 17:52 건의 직접 원인은 아님 (덤프 확정된 12:16·12:22 건의 원인).
- "설정 값들을 가져오다가"라는 사용자 추정에 대해: 크래시가 페이지 생성~값 채우기(Populate) 완료 로그 이전 구간인 것은 맞음.
  단 값 읽기 자체는 메모리 내 JSON 객체 읽기(순수 관리 코드)라 네이티브 AV/fail-fast의 발생원이 되기 어렵고,
  같은 구간(재생 중 첫 진입 포함)을 자동화로 반복해도 재현되지 않아 결정적 코드 결함보다는 상태 경합·stale 배포 쪽 정황이 우세.

## Phase 4 — Fix (1·2 승인·적용 완료, 2026-07-16)

1. **LoginWindow 수명 수정** (`src/DeskTube/Views/LoginWindow.xaml.cs`) — 덤프 확정분 대응:
   - `OnNavigationCompleted`의 직접 `Close()` → `DispatcherQueue.TryEnqueue` 지연 (WebView2 이벤트 콜스택 안에서 창 파괴 금지)
   - `Closed`에서 `NavigationCompleted` 구독 해제 + `LoginWebView.Close()` 확정 해제 (생성 진행 중이면 취소)
2. **미처리 예외 로깅** (`src/DeskTube/App.xaml.cs`) — XAML `UnhandledException` + `AppDomain.CurrentDomain.UnhandledException`
   로깅 핸들러 추가. 예외는 삼키지 않음(Handled 미설정). AppLog 초기화 전 Write는 무시되므로 안전.
3. 운영 습관 (코드 외): 원인 불명 크래시 재발 시 클린 재배포(패키지 제거 후 배포) 우선 시도. (H2 대응)

- 회귀 테스트: UI·네이티브 수명 경합이라 단위 테스트 불가 — 수동 재현 절차로 대체:
  설정 → 로그인 창 열기 → 페이지 로딩 중 즉시 닫기 반복 / 로그인 완료 자동 닫힘 후 정상 동작 확인.

## Verification

- Build: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` → 경고 0, 오류 0
- Tests: `dotnet test ... -p:Platform=x64` → 100/100 통과
- Review: spec-compliance + code-quality subagent (결과는 notes.md 기록)
- 크래시 재발 여부: 수정 빌드 실배포 후 관찰 필요 (⏳ HUMAN-VERIFY — 특히 다음 크래시 시 앱 로그에 예외 기록이 남는지)
- 재현 자동화 스크립트는 세션 스크래치패드에 있음 (프로젝트 밖).
