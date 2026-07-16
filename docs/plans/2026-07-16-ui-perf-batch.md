# Plan: UI·성능 개선 배치 (전환 속도·음소거·크기 모드·테마·WinUIEx·최적화)

**PRD**: docs/prd.md

## 요구 이해
- **원문 요청**: "1. 설정 화면의 메뉴 전환 속도가 느림 2. 음소거 on/off 기능 추가 3. 채움,원본,맞춤,늘리기 동영상 크기 모드 추가 4. 앱 테마 변경 기능 추가. 4.WinUIEx 사용해서 ui 작성 5. CommunityToolkit.Mvvm 패키지 사용해서 mvvm 패턴으로 작성 6. 동영상 재생시 최대한 cpu,메모리등 최적화 7. WinUI Gallery 스타일로 작업."
- **이해한 요구**: ① 페이지 전환 지연의 근본 원인(페이지 매번 재생성 + 설정 진입마다 WebView2 세션 프로브)을 제거 ② 설정 화면에 음소거 토글(FR-5는 기구현 — UI 노출) ③ 동영상 크기 모드 3종(채움/맞춤/늘리기 — "원본"은 유튜브가 원본 픽셀 크기를 미제공이라 제외 합의, FR-16) ④ 앱 테마 시스템/라이트/다크(FR-17, AGENTS 규칙 3 개정 합의) ⑤ WinUIEx 도입(창 상태 저장·복원 + Mica 백드롭 + 커스텀 타이틀바 + 창 유틸 — 사용자 a~d 전부 선택) ⑥ 재생 최적화는 미러 모니터 부하 절감 + 유휴 메모리 절감(사용자 a+b 선택) ⑦ Mvvm·Gallery 스타일은 기존 컨벤션 준수 확인(신규 작업 아님 — 전 task에 적용되는 크로스커팅 기준).
- **포함하지 않는 것**: WebView2 브라우저 인자 튜닝(사용자 미선택 — 회귀 위험), "원본" 크기 모드(정의 불가 — PRD 변경 이력에 기록).

## Goal
설정 창 페이지 전환이 체감 즉시(NFR-3 보강)가 되고, 음소거·크기 모드 3종·테마 3옵션이 설정에서 동작하며(FR-10·16·17), WinUIEx 기반 창(상태 복원·Mica·타이틀바)으로 전환되고, 미러 부하·유휴 메모리 최적화가 측정치와 함께 적용된 상태.

## PRD Coverage
| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-16 (크기 모드 3종) | Must | T3 | ✅ 커버 |
| FR-17 (앱 테마) | Must | T4 | ✅ 커버 |
| FR-10 (설정 항목 — 음소거·크기·테마 추가분) | Must | T2·T3·T4 | ✅ 커버 |
| FR-5 (음소거 — UI 노출) | Must | T2 (저장·적용은 기구현) | ✅ 커버 |
| NFR-2 (메모리) | — | T6·T7 (절감 옵션+측정) | ✅ 커버 |
| NFR-3 (전환 즉시성 보강분) | — | T1 | ✅ 커버 |
| FR-1~4·6~9·11~15, NFR-1·4~6 | Must/— | (없음) | 이번 범위 외 (기구현/후속) |

## Out of Scope
- WebView2 브라우저 인자 튜닝 (사용자 미선택 — 효과 불확실·회귀 위험)
- "원본" 크기 모드 (유튜브가 원본 픽셀 크기 미제공 — PRD 변경 이력 합의)
- 유튜브 검색·탐색 등 PRD Out of Scope 전부 동일

## Deferred / Follow-up
- 단일 인스턴스 보장·전역 예외 훅 (기존 대장 항목 유지 — 이번 범위 아님)
- T8 실측 3건(WACK·워킹셋·콜드스타트)은 기존 대장 항목 — 이번 T7 측정과 별개로 계속 대기

## Investigation Log
- 전환 지연 원인 (코드 확정): ① `MainWindow.NavigateOnce` → `Frame.Navigate`가 매번 새 페이지 인스턴스 생성(NavigationCacheMode 미설정 — 페이지·VM 전부 재생성) ② `SettingsPage.Loaded → SettingsViewModel.Load()`가 진입마다 StartupTask 조회 + **세션 프로브(임시 CoreWebView2Controller 생성 — YouTubeSessionService.IsSignedInAsync)** 수행. 프로브가 가장 무거움 (이 세션 Read로 확인)
- player.html 구조 (전체 Read): `#stage` 요소 + `applyScale()`(화질 스케일 CSS transform) — 크기 모드는 stage 변형으로 구현 가능하며 화질 스케일과 **하나의 레이아웃 함수로 통합** 필요 (두 설정이 같은 요소를 조작)
- 유튜브 iframe은 컨테이너 안에서 영상비 유지 레터박스 — "맞춤"=현재 fill, "채움"=stage를 16:9 오버스캔(화면 덮기+크롭), "늘리기"=stage 16:9 고정 후 비균등 scale(sx,sy)
- WinUIEx 2.9.2 nuspec: `net8.0-windows10.0.19041` 타깃, `Microsoft.WindowsAppSDK.WinUI >= 1.8` 의존 — 프로젝트(net8.0-windows10.0.22621, WinAppSDK 2.2)와 호환 (curl nuget 확인)
- CommunityToolkit.Mvvm 8.4.2 기사용 (csproj 확인) — 요청 ⑤는 기존 준수 확인으로 충족, 전 VM이 [ObservableProperty] partial property·[RelayCommand] 사용 중
- Gallery 스타일: AGENTS 디자인 규칙 1~7로 기 강제 — 신규 카드도 SettingsCard/Expander 사용 (크로스커팅)
- `IPlayerHost` 확장 영향 (grep 전수): 구현체 PlayerHost 1개 + 테스트 FakePlayer(PlaybackCoordinatorTests) 1개 — T3 Files에 포함
- 테마: 현재 RequestedTheme 오버라이드 0곳 (grep — T7 언어 전환 주석의 전제). 수동 테마 도입 시 ApplyLanguageChange 주석·재적용 로직 갱신 필요 (T4 Files)
- Deferred 대장 대조: "유휴 워킹셋 ~208MB" 항목 → T7이 절감+측정으로 수용. 단일 인스턴스·전역 훅은 무관 유지
- 위키 참조: 이 세션 초 절차 K 수행 — desktop-localization(테마 재적용 함정: 새 Frame이 RequestedTheme 초기화)·topmost-clickthrough-overlay(참고). 크기 모드·WinUIEx 관련 자료 없음 — 코드 1차 출처로 진행
- PRD 갱신 완료 (사용자 합의, 2026-07-16): FR-16·17 신설, FR-10·NFR-3 보강, 변경 이력 기록

## Risks & Unknowns
| 위험 | 영향 | 완화책 |
|---|---|---|
| WinUIEx WindowEx 전환이 기존 AppWindow.Closing(닫기=숨김)·창 재생성(언어 전환)과 충돌 | 트레이 상주 회귀 | T5 acceptance에 닫기=숨김·언어 전환 시나리오 포함, WindowEx도 AppWindow 노출(동일 API) — P-4에서 실API 확인 후 구현 |
| 페이지 캐시로 모니터 목록 stale | 설정 화면 오표시 | Load 분리 — 무거운 것(프로브·StartupTask) 1회, 모니터 목록은 진입마다 재열거(가벼움 — D7) |
| 채움(크롭) 모드에서 화질 스케일과 상호 간섭 | 시각 왜곡 | player.html 레이아웃 함수 단일화(D2) + 조합별 HUMAN-VERIFY 목록 |
| 워킹셋 트림이 재개 시 페이지 폴트 증가 | 재개 순간 지연 | 정지·창 숨김 시점에만 트림(재생 중 금지 — D6), 측정으로 확인 |

## Impact Analysis
### 4-A. 심볼/타입 추적
- `IPlayerHost` 확장(SetFitMode): 구현 PlayerHost·테스트 FakePlayer — 전수 (grep, Investigation Log). **공개 계약 변경 — 사전 승인 항목 등록**
- `AppSettings` 필드 추가(FitMode·Theme·ReduceMirrorQuality): 직렬화 하위 호환(기본값 로드 — part1 D5 SchemaVersion 방침), Normalize 갱신
- `MainWindow` → WindowEx 전환: MainWindow 참조처 = App.xaml.cs(생성·ShowMainWindow·ApplyLanguageChange·MainWindowHandle)·자체 — 전부 T5 Files
- `PlaybackCoordinator` 변경(SetFitModeAsync·미러 스케일): 소비처 SettingsViewModel — T3·T6 Files
- 페이지 3종 NavigationCacheMode: 페이지 자체 속성 — cross-file 없음

### 4-B. 계약·직렬화 변경
- settings.json에 새 필드 3개 — 기존 파일 로드 시 기본값(하위 호환), 마이그레이션 불요
- IPlayerHost 인터페이스 확장 — 어셈블리 내부 소비만(외부 배포 계약 아님), 구현 2곳 동시 갱신

### 4-C. 영향 받는 테스트
- PlaybackCoordinatorTests: FakePlayer에 SetFitMode 구현 추가 (T3)
- 신규: FitMode/Theme 저장·복원은 기존 JsonStateStore 왕복 테스트 패턴에 케이스 추가 (T3·T4)

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| FitMode enum | PlaybackMode enum 패턴 | 동일 패턴 **신규** (Models) |
| 테마 적용 헬퍼 | Loc(정적 헬퍼) 패턴 | 동일 패턴 신규 `ThemeHelper`(Services 정적) — 창 2종에 적용해야 해 App 산재 방지 |
| 워킹셋 트림 | 기존 Interop P/Invoke 패턴 | `SetProcessWorkingSetSize` 1함수 — 기존 Interop 파일 스타일로 신규 |
| 설정 카드들 | SettingsCard/Expander 기존 사용 | **재사용** (신규 컨트롤 없음) |

## Decisions
### D1. 테마 적용 방식
- **Chosen**: `AppSettings.Theme`(enum: System/Light/Dark, 기본 System) + `ThemeHelper`(구현 명명: `Register(window)` 생성 시 적용+등록 / `SetTheme` 열린 창 전부 재적용 / `Initialize` 시작 선반영) — 각 창의 루트 Content(FrameworkElement).RequestedTheme 설정. 적용 지점: MainWindow 생성·LoginWindow 생성·테마 설정 변경 시(열린 창 즉시)·언어 전환 창 재생성 시. AGENTS 규칙 3 개정(T8)과 함께 "전환은 루트 RequestedTheme 한 곳만" 원칙 유지.
- **Source**: AGENTS 규칙 3(원칙 문구), 위키 desktop-localization(재생성 시 테마 초기화 함정)

### D2. 크기 모드 구현 (FR-16)
- **Chosen**: player.html의 화질 스케일과 크기 모드를 **단일 `applyLayout()`** 로 통합 — 모드: 맞춤(현재 fill), 채움(stage를 16:9 오버스캔 중앙 배치 — 화면 덮기·크롭), 늘리기(stage 16:9 기준 비균등 scale). 새 명령 `fit`(FitMode int). C#: `IPlayerHost.SetFitMode(FitMode)` + `PlaybackCoordinator.SetFitModeAsync`(전 플레이어 적용+저장). 기본값 채움(Windows 배경 설정 관례).
- **명명 (plan-reviewer m1)**: enum 멤버는 동작 일치 명명 `Cover`(채움)/`Contain`(맞춤)/`Stretch`(늘리기) — 기존 CSS 클래스 `.fill`은 레터박스(=Contain)를 의미해 `Fill` 멤버명과 충돌하므로 사용하지 않는다. UI 레이블은 리소스로 채움/맞춤/늘리기. player.html 재작성 시 `.fill` 클래스는 `applyLayout` 통합으로 대체.
- **Source**: player.html 전체 Read, Windows 개인 설정 배경 "채움" 기본 관례

### D3. 음소거 토글
- **Chosen**: 설정 볼륨 카드에 ToggleSwitch 추가(SettingsViewModel.IsMuted ↔ Coordinator.SetMutedAsync — 기구현 API). 트레이 토글과 상태 공유(같은 Settings.IsMuted).
- **Source**: FR-5·FR-10, TrayIconService 기존 구현

### D4. WinUIEx 적용 범위 (사용자 a~d)
- **Chosen**: MainWindow를 `WinUIEx.WindowEx`로 전환 — PersistenceId(창 크기·위치 저장·복원), MinWidth/MinHeight(720×480), ExtendsContentIntoTitleBar+커스텀 타이틀바(앱명 표시), Mica 백드롭(SystemBackdrop — WinUI 표준 API, LoginWindow에도 적용). 창 유틸(d)은 기존 트레이 숨김 로직 유지 + WindowEx 편의 API 사용 가능 범위에서 (기능 동작 변경 없음). 구체 API 시그니처는 T5 P-4에서 패키지 문서로 검증 후 사용 (환각 방지).
- **Source**: 사용자 선택 a~d, WinUIEx 2.9.2 nuspec 호환 확인

### D5. 미러 모니터 부하 절감 (NFR-2)
- **Chosen**: 설정 토글 `ReduceMirrorQuality`(기본 **꺼짐** — 화질 차이는 사용자 opt-in) — 켜면 오디오 대상이 아닌 미러 플레이어의 렌더 스케일을 min(설정 화질, 720)으로 하향(SetQualityScale 재사용). 모니터 구성·오디오 대상 변경 시 재적용.
- **구현 강제 (plan-reviewer m2)**: `EffectiveScaleFor(entry)` 프라이빗 헬퍼를 두고 Coordinator의 **모든 SetQualityScale 호출 지점(StartAsync·SetQualityScaleAsync·RecreatePlayerAsync·HandleMonitorsChangedCoreAsync — 4곳 전부)** 이 이 헬퍼를 경유한다 — 한 지점이라도 직접 호출하면 미러가 풀화질로 남는 누락 위험.
- **Source**: PlaybackCoordinator.ApplyAudioRouting 구조, FR-13 스케일 메커니즘 재사용

### D6. 유휴 메모리 절감
- **Chosen**: `SetProcessWorkingSetSize(-1,-1)` 트림을 ① StopAsync 완료 후 ② 창 닫기(트레이 숨김) 시 호출 (재생 중 금지 — 재개 지연 방지). 효과는 before/after 워킹셋 측정으로 기록 (verification 문서).
- **Source**: Win32 표준 API, Deferred "유휴 208MB" 관찰

### D7. 전환 속도 (NFR-3 보강)
- **Chosen**: ① 3페이지(Home/Playlists/Settings — About 포함 4)에 `NavigationCacheMode.Required` ② SettingsViewModel.Load 분리 — 최초 1회: StartupTask 조회·세션 프로브·콤보 옵션 구성 / 매 진입: 모니터 목록 재열거만 ③ 세션 상태는 캐시 유지(로그인/로그아웃 액션 시 자동 갱신 — 기존 RefreshSessionAsync 경로).
- **Source**: 지연 원인 코드 확정 (Investigation Log)

## Tasks

- [x] T1. 페이지 전환 속도 개선 (NFR-3)
  - **Type**: D
  - **Design**: ① 4페이지 ctor에 `NavigationCacheMode = Required` ② SettingsViewModel — `Load()`를 최초 초기화(1회 가드)와 `RefreshMonitors()`(매 진입)로 분리, 세션 프로브·StartupTask는 최초 1회만 ③ PlaylistsViewModel — 캐시 대응(진입마다 라이브러리 재바인딩은 유지 — 가벼움) ④ 신규 심볼 없음, 간접화 없음
  - **Acceptance**: 빌드·테스트 통과 + 코드 검증: 2번째 설정 진입 시 세션 프로브 미실행(로그로 확인 가능하게 최초 1회 로그) — 체감 즉시성은 HUMAN-VERIFY(NFR-3 ≤300ms 목표)
  - **Files**: 주: `Views/HomePage.xaml.cs`, `Views/SettingsPage.xaml.cs`, `Views/PlaylistsPage.xaml.cs`, `Views/AboutPage.xaml.cs`, `ViewModels/SettingsViewModel.cs` / 동반: `ViewModels/PlaylistsViewModel.cs`
  - **Edge Cases**: 캐시 페이지의 모니터 목록 stale → RefreshMonitors 매 진입 / 언어 전환 창 재생성 → 새 Frame이라 캐시 초기화(정상 — 새 언어로 재생성 필요) / 서비스 준비 전 첫 진입 → 기존 ServicesInitialized 경로 유지
  - **Halt Forecast**: 없음 — 내부 구조 변경, 파괴적·의존성·외부 요소 없음
  - **Depends on**: -

- [x] T2. 음소거 토글 (FR-5·10)
  - **Type**: C
  - **Design**: (D3 참조) SettingsPage 볼륨 카드에 ToggleSwitch, SettingsViewModel.IsMuted(partial property) ↔ SetMutedAsync, resw en/ko
  - **Acceptance**: 토글 → Settings.IsMuted 반영·저장, 트레이 볼륨 체크와 상태 일치 — 실동작 HUMAN-VERIFY; 빌드·테스트 통과
  - **Files**: 주: `Views/SettingsPage.xaml`, `ViewModels/SettingsViewModel.cs` / 동반: `Strings/en-US·ko-KR/Resources.resw`
  - **Edge Cases**: 재생 중 토글 → 즉시 반영(기존 ApplyAudioRouting) / 로드 중 콜백 억제(_loading 가드 기존 패턴)
  - **Halt Forecast**: 없음 — 기구현 API 소비, 파괴적·의존성 없음
  - **Depends on**: T1 (SettingsViewModel 동시 수정 충돌 방지)

- [x] T3. 동영상 크기 모드 3종 (FR-16)
  - **Type**: D
  - **Design**: (D2 참조) ① `Models/FitMode.cs` enum(Cover/Contain/Stretch — D2 명명) + `AppSettings.FitMode`(기본 Cover)·Normalize ② player.html — applyScale→`applyLayout(fitMode, scaleHeight)` 통합, `fit` 명령 추가 ③ `IPlayerHost.SetFitMode(FitMode)` + PlayerHost(PlayerCommand Fit 필드) + FakePlayer(테스트) ④ `PlaybackCoordinator.SetFitModeAsync` — 전 플레이어 적용+저장, StartAsync 시 초기 적용 ⑤ 설정 카드(콤보 3옵션)+resw ⑥ 간접화 없음 — enum·switch 직결
  - **Acceptance**: 모드 저장·복원 왕복 테스트 통과 + 재생 중 콤보 변경 시 fit 명령 전송(코드 경로) — 시각 결과 3종은 HUMAN-VERIFY; 빌드·기존 테스트 통과
  - **Files**: 주: `Assets/player.html`, `Services/IPlayerHost.cs`, `Services/PlayerHost.cs`, `Services/PlaybackCoordinator.cs`, `Models/AppSettings.cs` / 동반: `Models/FitMode.cs`(신규), `Views/SettingsPage.xaml`, `ViewModels/SettingsViewModel.cs`, `Strings/…resw`, `tests/…/PlaybackCoordinatorTests.cs`(FakePlayer)
  - **Edge Cases**: 화질 스케일과 조합(채움+스케일 등 6조합 레이아웃 함수 단일 처리) / ready 전 fit 명령 → JS pending 큐 기존 경로 / 구버전 settings.json(필드 없음) → 기본 Fill
  - **Halt Forecast**: (ii-a) IPlayerHost 공개 계약 확장 → `## 사전 승인 항목`
  - **Depends on**: T2

- [x] T4. 앱 테마 변경 (FR-17)
  - **Type**: D
  - **Design**: (D1 참조) ① `AppSettings.Theme`(enum System/Light/Dark — `Models/AppTheme.cs` 신규) ② `Services/ThemeHelper.cs` 신규 — `Apply(Window)`(루트 Content RequestedTheme)·`ApplyToAll()`(열린 창) ③ 설정 카드(콤보 3옵션)+VM ④ 적용 지점: MainWindow 생성·LoginWindow 생성·설정 변경 즉시·ApplyLanguageChange 재생성 시(기존 "테마 재적용 불필요" 주석 갱신 — 이제 필요) ⑤ resw ⑥ 간접화 없음
  - **Acceptance**: 저장·복원 왕복 테스트 + 3옵션 전환 즉시 적용·언어 전환 후 유지 — HUMAN-VERIFY; 빌드·테스트 통과
  - **Files**: 주: `Models/AppTheme.cs`(신규), `Services/ThemeHelper.cs`(신규), `ViewModels/SettingsViewModel.cs`, `Views/SettingsPage.xaml`, `App.xaml.cs` / 동반: `Views/LoginWindow.xaml.cs`, `Models/AppSettings.cs`, `Strings/…resw`
  - **Edge Cases**: 시스템 추종 선택 시 = Default(OS 변경 실시간 추종) / 언어 전환 창 재생성 → 재적용 / Mica 백드롭(T5)과 라이트/다크 대비 확인(HUMAN-VERIFY)
  - **Halt Forecast**: 없음 — (AGENTS 규칙 3 개정은 T8, 이미 사용자 합의)
  - **Depends on**: T5 (타이틀바·백드롭 확정 후 테마 적용 지점 안정화)

- [x] T5. WinUIEx 도입 — 창 상태·Mica·타이틀바 (사용자 선택 a~d)
  - **Type**: D
  - **Design**: (D4 참조) ① csproj에 WinUIEx 2.9.2 ② MainWindow: `Window`→`WinUIEx.WindowEx` — PersistenceId="MainWindow"(크기·위치 저장·복원), MinWidth 720·MinHeight 480, ExtendsContentIntoTitleBar+타이틀바(앱명), Mica(SystemBackdrop) ③ LoginWindow: Mica 적용 ④ 기존 AppWindow.Closing(닫기=숨김)·ForceClose·창 재생성 경로 동작 유지 ⑤ 구체 API는 P-4에서 패키지 문서 검증 후 사용 ⑥ 간접화 없음
  - **Acceptance**: 창 크기·위치가 재시작 후 복원 + 닫기=트레이 숨김·언어 전환 재생성 정상 — HUMAN-VERIFY; 빌드·테스트 통과 (회귀 0)
  - **Files**: 주: `src/DeskTube/DeskTube.csproj`, `MainWindow.xaml`, `MainWindow.xaml.cs` / 동반: `Views/LoginWindow.xaml(.cs)`, `App.xaml.cs`(참조 시그니처 영향 확인)
  - **Edge Cases**: WindowEx의 Closing 이벤트 체계가 AppWindow.Closing과 다를 수 있음 → P-4 확인 후 기존 의미(닫기=숨김) 보존 / 창 상태 파일 손상 → WinUIEx 기본 동작(무시) 확인 / 다중 모니터에서 저장 위치가 분리된 모니터 → WinUIEx 복원 정책 확인
  - **Halt Forecast**: (ii-a) NuGet 의존성 추가(WinUIEx 2.9.2) → `## 사전 승인 항목`
  - **Depends on**: T1

- [x] T6. 미러 모니터 부하 절감 옵션 (NFR-2)
  - **Type**: C
  - **Design**: (D5 참조) `AppSettings.ReduceMirrorQuality`(기본 false) + PlaybackCoordinator — 미러 플레이어 스케일 하향 적용(SetQualityScaleAsync·ApplyAudioRouting·모니터 변경 경로), 설정 토글 카드+resw
  - **Acceptance**: 토글 on 시 미러 플레이어에만 하향 스케일 명령 전송(코드 경로·저장 왕복 테스트) — 부하 절감 실측은 T7 측정과 함께 HUMAN-VERIFY; 빌드·테스트 통과
  - **Files**: 주: `Services/PlaybackCoordinator.cs`, `Models/AppSettings.cs`, `ViewModels/SettingsViewModel.cs`, `Views/SettingsPage.xaml` / 동반: `Strings/…resw`
  - **Edge Cases**: 오디오 대상 변경 시 이전 미러↔마스터 스케일 재적용 / 단일 모니터(미러 없음) → no-op / 화질 설정이 이미 720 이하 → min이라 변화 없음
  - **Halt Forecast**: 없음 — 기존 스케일 메커니즘 재사용
  - **Depends on**: T3 (플레이어 명령 경로 안정화 후)

- [ ] T7. 유휴 메모리 절감 + 측정 (NFR-2)
  - **Type**: C
  - **Design**: (D6 참조) `Interop/ProcessInterop.cs` 신규 — `TrimWorkingSet()`(SetProcessWorkingSetSize(-1,-1)) / 호출: Coordinator.StopAsync 완료 후·MainWindow 숨김 시 / before/after 워킹셋 측정을 `docs/verification-2026-07.md`에 기록
  - **Acceptance**: 정지·숨김 후 워킹셋이 트림 전 대비 감소(실측 수치 기록 — 자동 측정 스크립트) + 측정치를 NFR-2 목표(150MB)와 대조 기록(미달 시 원인·후속 명시) + 재생 중 트림 미호출(코드 경로); 빌드·테스트 통과
  - **Files**: 주: `Interop/ProcessInterop.cs`(신규), `Services/PlaybackCoordinator.cs`, `MainWindow.xaml.cs` / 동반: `docs/verification-2026-07.md`
  - **Edge Cases**: 트림 직후 재생 재시작 → 페이지 폴트로 시작 지연 가능(측정으로 확인, 문제 시 숨김 시점만 유지) / API 실패 → 무시(best-effort)
  - **Halt Forecast**: 없음 — 표준 API, 파괴적 아님
  - **Depends on**: T6

- [ ] T8. 문서 반영 (AGENTS 규칙 3 개정·README)
  - **Type**: A
  - **Design**: ① AGENTS.md 디자인 규칙 3 개정 — "테마는 시스템에 맡긴다" → "테마 기본은 시스템 추종, 수동 전환(라이트/다크) 지원 — 전환은 루트 RequestedTheme 한 곳만(ThemeHelper)" (사용자 합의 완료, FR-17) ② README 기능 목록에 크기 모드·테마·음소거 토글·창 상태 복원 반영
  - **Acceptance**: AGENTS 규칙 3이 FR-17과 정합, README가 실제 기능과 일치
  - **Files**: 주: `AGENTS.md`, `README.md`
  - **Edge Cases**: (Type A — skip)
  - **Halt Forecast**: 없음 — 순수 문서, 사용자 합의 완료분 반영
  - **Depends on**: T4

## 사전 승인 항목 (일괄 승인 대상)
- T5 — NuGet 의존성 추가: `WinUIEx` 2.9.2 (net8·WinAppSDK 2.2 호환 확인 완료)
- T3 — 공개 계약 확장: `IPlayerHost.SetFitMode` 추가 (구현 2곳 동시 갱신 — 파괴적 변경 아님)
- T8 — AGENTS.md 디자인 규칙 3 개정 (사용자 합의 완료분 반영)
- 전 task — 로컬 작업 브랜치 커밋 (push 아님)

## 불가피한 Halt (위임 불가)
- git push·태그·릴리즈·PR — 항상 별도 승인
- WinUIEx 전환으로 닫기=숨김/창 재생성 회귀가 코드로 해소 불가한 경우 (돌발 — 대안 보고)

## Verification Strategy
- 빌드/테스트/format: 기존 명령 (`-p:Platform=x64`, `--no-incremental`)
- 전환 속도: 2번째 설정 진입에서 프로브 미실행 로그 확인 + 체감 HUMAN-VERIFY (≤300ms 목표)
- 크기 모드·테마·Mica: UIA/실기 HUMAN-VERIFY 목록 (모드 3종 × 화질 조합, 테마 3종 × 언어 전환)
- 워킹셋: 자동 측정(Get-Process 워킹셋 before/after) → verification 문서 기록
- 수동 검증 누적: 창 상태 복원, 닫기=트레이, 음소거 토글=트레이 일치

## Phase Ledger

## Retry Ledger

## Progress Log
- T3·T5 완료 (커밋 9f796ad, 3f4da69): FitMode 3종(player.html applyLayout 통합·IPlayerHost.SetFitMode·Coordinator 3지점 초기 적용·설정 콤보·테스트 83/83) + WinUIEx 2.9.2(WindowManager 창 상태 복원·Mica·타이틀바·라이선스 인벤토리).
  - 결정: WindowEx 상속 대신 WindowManager.Get(this) — XamlTypeInfo 생성 코드 CS0618(obsolete Icon) 억제 불가로 경고 0 원칙과 양립하는 동등 API 채택(WinUIEx 문서 검증). D4 기능 a~d 전부 충족.
  - 결정: WinUIEx 추가 시 라이선스 게이트(LicenseInventoryTests)가 실패 — Assets/licenses/WinUIEx.txt(MIT)+index.json 동반 갱신이 의존성 추가의 필수 절차임을 확인.
- T1-T2 완료 (커밋 2bb2a31, 6ce02ae): 4페이지 NavigationCacheMode.Required + SettingsViewModel Load 분리(최초 1회 프로브/재진입 RefreshMonitors), 음소거 토글 UI+resw. 빌드 경고 0·테스트 81/81·리뷰 spec/quality 둘 다 첫 판 OK.
  - 결정: SettingsPage SignInRequested 구독 ctor→Loaded 이동(캐시 재진입 핸들러 소실 방지). 재진입 시 IsMuted 재동기화 추가(트레이 변경 stale 방지 — T2 acceptance 충족 목적, D7 "모니터만"의 예외).

## Next Steps
- 승인 시 **새 세션에서** `docs/plans/2026-07-16-ui-perf-batch.md 구현` 으로 실행 권장 (현재 세션 컨텍스트 과밀)

## Open Questions
- [x] PRD 갱신(FR-16·17 등) — 승인됨 (2026-07-16, PRD 반영 완료)
- [x] WinUIEx 용도 — a창 상태+b Mica+c타이틀바+d창 유틸 전부
- [x] "원본" 모드 — 제외, 3모드 확정
- [x] 최적화 범위 — a 미러 부하 + b 유휴 메모리
