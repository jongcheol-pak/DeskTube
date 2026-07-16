# Plan: UI 전체 재설계 — Claude Design 시안(DeskTube 1a) 반영

**PRD**: docs/prd.md

> 기준 문서(시안 원본): 세션 스크래치패드 `design\DeskTube-1a.dc.html`
> (Claude Design 프로젝트 "WinUI3 앱 UI 전체 재설계"의 `DeskTube 1a.dc.html` 사본, 2026-07-16 취득).
> 스크래치패드는 세션 종료 후 소실될 수 있으므로 시안의 확정 값은 이 plan의 `## 시각 요소 분해`와
> `## Decisions` D3(다크 토큰 팔레트)에 전량 옮겨 적었다 — 구현은 이 plan만으로 가능하다.

## 요구 이해
- **원문 요청**: "/design-sync <Claude Design 공유 URL>" → (design-sync 부적용 확인 후) "시안 가져와 분석 + 계획 수립" 선택. 시안 요지: "기존 앱의 설정 창 중심 구조를 시안의 사이드바+4페이지 구조로 재편, 기능은 기존 FR 그대로, 문구·색·치수는 시안 원문을 따를 것".
- **이해한 요구**: Claude Design에서 만든 DeskTube 1a 시안(다크 배경 + 코럴 #F25C54 포인트, 커스텀 사이드바, 홈·플레이리스트·설정·정보 4화면)의 시각 디자인을 기존 WinUI 3 앱에 충실하게 반영한다. 화면 구조·스타일·문구 재구성 중심이며, 색·치수는 토큰화(AGENTS.md 규칙 2 유지). **후속 지시(2026-07-16)로 테마 변경 기능을 삭제하고 다크 테마 고정으로 전환한다** (FR-17 폐기 — PRD 변경 이력 반영 완료).
- **포함하지 않는 것으로 이해**: 시안 설정에만 있는 "앱 시작 후 자동 재생" 토글은 기능 신설이므로 이번 제외(사용자 확정 — Deferred). 창 제어 버튼은 자체 구현하지 않고 시스템 캡션 버튼 유지(사용자 확정). 라이트 테마 번안(Q1의 당초 결정)은 후속 지시로 폐기 — 라이트 테마 자체가 사라진다.

## Goal
DeskTube 설정 창(4페이지)의 시각 디자인을 시안 DeskTube 1a와 동일한 구조·색·치수·문구로 재구성한다 (다크 테마 고정 — 시안 원값 토큰 기반, 테마 변경 기능 제거).

## PRD Coverage
이번 작업은 시각 재구성이며 요구(FR 텍스트) 변경 없음 — PRD 갱신 불요, 재검증 연결만 한다.

| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-1 | Must | T5 (홈 URL 입력·재생 UI 재스타일 — 파싱·재생 로직 불변) | ✅ 커버 |
| FR-3 | Must | T4, T5, T7 (모니터 선택 UI를 시각 카드로 — 선택 저장 로직 불변) | ✅ 커버 |
| FR-10 | Must | T7 (설정 화면 그룹·카드 재배열 — 항목 전수 유지) | ✅ 커버 |
| FR-11 | Must | T8 (정보 앱 카드) | ✅ 커버 |
| FR-12 | Must | T8 (라이선스 카드형 expander) | ✅ 커버 |
| FR-13 | Must | T7 (화질 안내 배너 — 문구 유지) | ✅ 커버 |
| FR-17 (REMOVED 2026-07-16) | - | T2 (테마 변경 기능 제거 + 다크 고정 — 폐기 이행) | ✅ 커버 (제거 작업) |
| FR-18 | Must | T6 (차트형 행 재스타일 — 메타·재생 로직 불변) | ✅ 커버 |
| FR-2 | Must | (배경창 렌더 — UI 창 무관) | 이번 범위 외 (기구현) |
| FR-4, FR-5, FR-6, FR-7, FR-8, FR-9, FR-14, FR-15, FR-16 | Must | (로직 불변 — 해당 설정 카드·컨트롤은 T7이 시각만 변경) | 이번 범위 외 (기구현) |
| NFR-1~6 | - | (불변 — NFR-3 페이지 전환 즉시성은 NavigationCacheMode 유지로 보존) | 이번 범위 외 (기구현) |

## Out of Scope
- 창 제어 버튼(최소화/최대화/닫기) 자체 구현 — 시스템 캡션 버튼 유지 (사용자 확정, Q3)
- 시안 프로토타입의 커스텀 드래그 고스트/스페이서 재현 — ListView 내장 드래그 정렬 비주얼 유지 (D7)
- 배경 재생 창(WallpaperWindow)·트레이 메뉴·LoginWindow의 시각 변경 — 시안에 정의 없음

## Deferred / Follow-up
- [SUGGEST] 직접 FontSize 지정 TextBlock의 텍스트 램프 스타일 일괄 점검 — T7 리뷰가 SettingsPage 2곳을 잡아 수정했으나 T3~T6 화면(홈 제목·모니터 카드 제목 등)에도 같은 패턴 잔존 가능 (AGENTS.md 디자인 규칙 4)
- T1 범위 노트: AGENTS.md DO NOT의 "언어 전환 후 테마 재적용 누락" 행이 폐기 규칙(구 3-⑤)을 참조해 stale — acceptance의 "다른 섹션 변경 없음"에서 벗어나지만 문서 동기화(규칙 7-1)를 위해 "App.xaml 외 RequestedTheme 지정 금지"로 교체함 (사소·가역 결정, 기록용)
- "앱 시작 후 자동 재생" 설정 토글 + 동작 — 시안에는 있으나 현재 앱에 없는 신규 기능. 추가하려면 PRD FR 신설 합의 필요 (사용자 확정: 이번 제외, 다음에 기능으로 검토)
- 홈 화면 재생 중 pill에 제목·썸네일 표시 — 기존 deferred 항목(2026-07-16, FR-18 메타 인프라 재사용)과 동일 건. 시안 pill은 "디스플레이 N에서 재생 중" 라벨만이므로 이번엔 시안대로 구현
- 시안 색과 시스템 HighContrast의 시각 정합 검토 — 이번엔 HC 사전을 시스템 HC 색으로 매핑만 (D11)

## Investigation Log
- 위키 참조: 관련 위키 자료 없음 — 코드 1차 출처로 진행 (feature/recipe 양방향 검색 무매칭)
- Deferred 대장 확인: `docs/plans/deferred.md` — "홈 화면 현재 재생 정보 제목·썸네일"(이번 plan Deferred에 유지), "MainWindow 커스텀 타이틀바 Grid 높이 48px 고정"(T3에서 시안 44px 토큰으로 대체 — SUGGEST 건 흡수) 관련 2건 반영
- `MainWindow.xaml` 직접 Read — NavigationView(IsSettingsVisible=True, MenuItems 3개: home/playlists/about), 커스텀 타이틀바 48px 텍스트만, InfoBar 오버레이 (1~51행)
- `MainWindow.xaml.cs` explorer 확인 + 구조 직접 대조 — Mica(`SystemBackdrop=MicaBackdrop` 35행), WinUIEx PersistenceId, 최소 720×480, `OnNavSelectionChanged` Tag switch, `NavigateOnce`
- `HomePage.xaml`(26행)·`HomeViewModel.cs`(96행) 직접 Read — 헤더+URL 입력+AccentButton+InfoBar만. PlayAsync는 "빠른 재생" 고정 이름 리스트 교체 방식, `Loc.Get("Home_QuickPlaylistName")`
- `SettingsPage.xaml`(155행) 직접 Read — SettingsExpander(모니터 토글 목록·자동 일시정지) + SettingsCard 11개 평면 나열, 그룹 헤더 없음
- `PlaylistsPage.xaml`(280행) 직접 Read — 2열 마스터-디테일, 차트형 행(40/56/*/48 컬럼, 56×56 썸네일, 원형 재생 버튼), 컬럼 헤더 행 존재(시안에는 없음), pill 버튼 2개, ListView 내장 드래그
- `AboutPage.xaml`(51행) 직접 Read — 앱 정보 StackPanel + 개인정보 + Expander 라이선스
- 리소스 구조 (explorer 보고 + App.xaml 대조): `Resources/` 디렉터리·커스텀 ResourceDictionary·ThemeDictionaries·커스텀 색 토큰 **전무**. App.xaml은 XamlControlsResources만 병합. Accent는 시스템 기본(`AccentButtonStyle`, `AccentFillColorDefaultBrush` 참조 2곳 — PlaylistsPage 267·272행)
- `ThemeHelper` (explorer 보고): `Initialize/Register/SetTheme` — 창 루트 `RequestedTheme` 변경 방식. 토큰을 `ThemeResource`로 참조하면 테마 전환 시 자동 재해석되므로 구조 변경 불요
- `PlaybackCoordinator.cs` grep 직접 확인 — `Status`(74행)·`StatusChanged` 이벤트(76행)·`SetStatus`(704행) 존재 → 홈 재생 중 pill 바인딩 가능. 정지는 기존 Stop 경로 재사용
- `MonitorChoice` grep 전수(7 hits) — SettingsViewModel.cs(10·12·14·116·332·459행)와 SettingsPage.xaml(29행)만 사용. 홈 공유를 위한 분리·확장의 영향 범위는 이 2파일
- `MonitorService` (explorer 보고): `MonitorInfo(Id, DeviceName, X, Y, Width, Height, IsPrimary)`, `GetMonitors()`, `MonitorsChanged` — 시안 카드의 해상도("2560×1600")·주 모니터 라벨 재료 존재
- `Strings/*/Resources.resw` (explorer 보고): ko/en 각 119키, `x:Uid` 컨트롤.속성 + 영역_개념 패턴
- `tests/DeskTube.Tests/` 목록 직접 확인 — 서비스 계층 테스트만 존재(ViewModel 테스트 없음, 총 100개 통과가 최근 기록). UI 재스타일은 신규 테스트 대상 없음 — 기존 100개 회귀 확인으로 갈음
- 테마 기능 grep 전수 — **src/ + tests/ 전 범위** (`ThemeHelper|AppTheme|ThemeIndex|ThemeOptions|ThemeCard|Theme_` 및 tests의 `\.Theme\b|Theme =`) — 사용처: App.xaml.cs(68·70 Initialize, 182~183 스테일 주석), MainWindow.xaml.cs(40 Register), LoginWindow.xaml.cs(22 Register), SettingsViewModel.cs(79·102~106·127~128·157·269·532~541), SettingsPage.xaml(108~113 ThemeCard), Models/AppTheme.cs(enum), Models/AppSettings.cs(48·64 Theme 필드), Services/ThemeHelper.cs(전체), resw ko/en 각 5키(ThemeCard.Header/.Description, Theme_System/Light/Dark), **tests/DeskTube.Tests/JsonStateStoreTests.cs(44행 `Theme = AppTheme.Dark` 대입, 62행 `Assert.Equal(settings.Theme, loaded.Theme)`)** — T2 제거 범위의 전수 목록 (1차 grep을 src/로 한정했다가 리뷰에서 테스트 호출자 누락 지적받아 전 범위 재수행)
- PRD 직접 Read — FR 전수·NFR-3의 "페이지(홈·플레이리스트·설정·정보)" 명시 확인 → 구조는 현행과 동일, 시각 재구성만
- AGENTS.md 신선도 점검 — Build/Test 명령·Plan Location·Repository Structure의 이번 참조 항목 실재 확인. 단 `Resources/ # 디자인 토큰 사전` 항목은 **현재 디렉터리 부재**(문서가 앞서감) — T2가 신설하므로 갱신 불요

## Risks & Unknowns
| 위험 | 영향 | 완화책 |
|---|---|---|
| NavigationView 표준 스타일이 시안(220px 사이드바·코럴 인디케이터·항목 라운드)과 미세하게 다를 수 있음 | 시각 차이 | 표준 속성(OpenPaneLength·FooterMenuItems)+토큰 우선, lightweight styling 리소스 키(`NavigationViewItem*` 계열) 오버라이드. 잔여 차이는 ⏳ HUMAN-VERIFY로 보고 |
| `SystemAccentColor` 재정의가 일부 컨트롤 상태 브러시에 미반영될 수 있음 | 코럴이 아닌 기본 파랑 잔존 | T2 acceptance에 대표 컨트롤(AccentButton·ToggleSwitch On·Slider·NavigationView 인디케이터) 확인 포함, 미반영 키는 개별 브러시 오버라이드 추가 |
| HighContrast 테마에서 커스텀 토큰이 대비 규칙을 깨뜨릴 수 있음 | 접근성 회귀 | ThemeDictionaries에 HighContrast 사전 포함 — 시스템 HC 색 키로 매핑 (D11) |
| `Application.RequestedTheme`는 시작 후 변경 불가(런타임 set 시 예외) | 코드에서 실수로 재설정하면 크래시 | 다크 고정이 목적이므로 App.xaml 마크업에서만 지정, 코드 설정 경로 전부 제거 (T2 — ThemeHelper 삭제로 설정 지점 자체가 사라짐) |
| NavigationCacheMode=Required로 홈의 모니터·칩 상태가 stale | 잘못된 상태 표시 | HomePage `OnNavigatedTo`에서 재갱신 (T5 Edge) |
| Mica 제거·불투명 배경은 의도된 변경이지만 사용자가 회귀로 느낄 수 있음 | UX 인식 | 완료 보고에 명시 (D10 근거) |

## Impact Analysis
### 4-A. 심볼/타입 추적 결과
| 심볼 | 영향 받는 파일 | 영향 종류 |
|---|---|---|
| `MonitorChoice` (분리·확장) | `ViewModels/SettingsViewModel.cs`(10·12·14·116·332·459행), `Views/SettingsPage.xaml`(29행) — grep 전수 7 hits | 파일 분리(신규 `ViewModels/MonitorChoice.cs`) + 표시 속성 추가. 기존 생성자 시그니처 유지 또는 T4에서 호출부 2곳 동시 갱신 |
| `MainWindow.OnNavSelectionChanged` | `MainWindow.xaml(.cs)` 내부 전용 (외부 호출자 없음 — grep 확인) | 설정 일반 항목화·footer 항목 추가에 따른 Tag switch 확장 |
| `MainWindow.ShowNotice` / `App.ShowMainWindow` | `App.xaml.cs`(163~177행), `TrayIconService` | 변경 없음 (셸 재구성 시 시그니처 보존 — acceptance) |
| `HomeViewModel` | `Views/HomePage.xaml(.cs)`만 참조 | 속성·커맨드 추가 (기존 Url/PlayCommand 계약 유지) |
| `PlaybackCoordinator.Status/StatusChanged` | 구독 추가만 (T5 홈 pill) — 기존 구독처 변경 없음 | 읽기 전용 사용 |
| `Strings/*/Resources.resw` | 전 View + `Loc.Get` 소비처 | 키 추가·기존 값 갱신 + 미사용화 키 제거(D6의 Col* 3키, T2의 테마 5키) |
| `App.xaml` | 전 창·페이지 (리소스 병합) | 토큰 사전 병합 + accent 색 재정의 + `RequestedTheme="Dark"` 지정 |
| `ThemeHelper` (삭제) | App.xaml.cs(68·70), MainWindow.xaml.cs(40), LoginWindow.xaml.cs(22), SettingsViewModel.cs(541) — grep 전수 | 호출부 전부 제거 (Application 수준 다크 고정으로 대체) |
| `AppTheme` enum (삭제) | AppSettings.cs(48·64), SettingsViewModel.cs(539), ThemeHelper.cs, App.xaml.cs(68·70), tests/JsonStateStoreTests.cs(44) — grep 전수(src+tests) | 참조 전부 제거 |
| `AppSettings.Theme` 필드 (제거) | AppSettings.cs(48·64), SettingsViewModel.cs(269), App.xaml.cs(68), tests/JsonStateStoreTests.cs(44·62) | 필드·리셋 로직·소비처 제거 + 테스트의 Theme 대입·assert 2줄 제거 (직렬화 영향은 4-B) |

### 4-B. 계약·직렬화 변경
- `AppSettings.Theme` 필드 제거 — 기존 저장 JSON에 남은 `Theme` 속성은 System.Text.Json 역직렬화 시 무시되므로 하위 호환 (마이그레이션 불요). 새 저장본에는 기록되지 않음.
- 그 외 없음 — 플레이리스트 JSON 스키마·서비스 인터페이스 불변. 공개 계약 변화는 `MonitorChoice` 파일 위치 이동 + 테마 심볼 삭제 (모두 사전 승인 항목).

### 4-C. 테스트 파일
- 직접 영향 1개 — `tests/DeskTube.Tests/JsonStateStoreTests.cs`: 설정 영속화 왕복 테스트가 `Theme = AppTheme.Dark`(44행)·`Assert.Equal(settings.Theme, ...)`(62행)을 참조 → T2에서 두 줄 제거 (필드 삭제에 따른 테스트 갱신, 테스트 본질(왕복 일치)은 유지).
- 그 외 없음 — tests/는 서비스 계층만 (`MonitorIdTests`, `PlaybackCoordinatorTests` 등). ViewModel·XAML 테스트 부재가 프로젝트 관례.
- 회귀 확인: 전체 테스트 통과(`dotnet test`)를 각 task 검증에 포함.

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| `Resources/DesignTokens.xaml` (토큰 사전) | 커스텀 ResourceDictionary 전무 (Glob·App.xaml 확인) | 신규 — AGENTS.md 규칙 2(토큰화)의 최초 실체화 |
| `ViewModels/MonitorChoice.cs` (분리) | SettingsViewModel 내 기존 클래스 | **재사용(이동·확장)** — 중복 정의 대신 파일 분리 |
| `ViewModels/MonitorPanelViewModel.cs` | SettingsViewModel의 모니터 열거·토글·최소 1개 강제 로직 (332·459행 일대) | 신규 심볼이지만 **로직은 기존 코드 이동** — 홈·설정 2곳 공유가 목적 (중복 작성 방지) |
| `Controls/MonitorCardsControl.xaml` | 기존 모니터 UI는 SettingsExpander 토글 목록뿐 — 카드형 없음 | 신규 — 홈(300×188)·설정(200×125) 2곳 재사용 전제 |
| pill 버튼·칩·카드 공용 스타일 (토큰 사전 내 Style) | PlaylistsPage 인라인 pill 버튼(112~124행) 존재 | 인라인 값을 공용 Style로 **추출 재사용** (T2), T6에서 참조 전환 |

### Verified by
- grep "MonitorChoice" → 7 hits(2파일), 모두 위 표에 포함
- grep "OnNavSelectionChanged|ShowNotice|ShowMainWindow" → MainWindow/App/TrayIconService만, 위 표에 포함
- grep "IsPlaying|StatusChanged|PlaybackStatus" (Services/) → Coordinator 공개 상태·이벤트 실재 확인
- grep "ThemeHelper|AppTheme|ThemeIndex|ThemeOptions|ThemeCard|Theme_" (src/) + "AppTheme|\.Theme\b|Theme =" (tests/) → 사용처 전수(Investigation Log 목록, 테스트 2 hits 포함), 모두 위 표·T2 제거 범위에 포함
- Glob "Resources/**" + App.xaml Read → 커스텀 리소스 부재 확인

## Decisions
### D1. 토큰 파일 위치·명명
- **Options**: A) `Resources/DesignTokens.xaml` 단일 사전, 키 접두 `App` / B) 색·치수·스타일 3분할
- **Chosen**: A (파일 1개, 내부에 ThemeDictionaries + 공용 Style 섹션)
- **Rationale**: 규모(색 ~30키·치수 ~10키·Style ~6개)에 3분할은 과함. AGENTS.md Repository Structure의 `Resources/` 예정 위치와 일치.
- **Source**: AGENTS.md 75~94행, 리소스 조사 (커스텀 사전 전무)

### D2. 코럴 accent 적용 방식
- **Options**: A) App.xaml에 `SystemAccentColor`(+Light1~3/Dark1~3) Color 재정의 / B) 컨트롤별 브러시 키 개별 오버라이드
- **Chosen**: A 기본 + 미반영 키만 B 보충
- **Rationale**: WinUI 컨트롤(AccentButton·ToggleSwitch·Slider·NavigationView 인디케이터)이 SystemAccentColor 계열을 소비하므로 한 곳 재정의로 전파. 전파 누락은 T2 acceptance에서 검출해 개별 보충.
- **Source**: PlaylistsPage 267행 등 시스템 accent 소비 확인, WinUI 관례 (구현 시 실효 확인이 acceptance)

### D3. 다크 고정 구현 방식 + 토큰 팔레트 (사용자 후속 지시 — 테마 변경 기능 삭제)
- **Options**: A) App.xaml `RequestedTheme="Dark"` 마크업 고정 + 테마 기구(ThemeHelper·AppTheme·설정 카드) 전부 삭제 / B) ThemeHelper를 남기고 항상 Dark만 적용 / C) 창별 RequestedTheme 지정 유지
- **Chosen**: A
- **Rationale**: Application 수준 고정이 근본 해결 — 창·Frame이 몇 번 재생성돼도 테마가 흔들리지 않아 "언어 전환 후 테마 재적용"(AGENTS.md 다국어 규칙 3-⑤) 문제 자체가 소멸한다. B·C는 죽은 배선을 남긴다. `Application.RequestedTheme`는 시작 후 변경 불가 속성이므로 마크업 지정이 정확한 용법.
- **Source**: 테마 grep 전수 (Investigation Log), 사용자 후속 지시(2026-07-16), PRD FR-17 REMOVED

토큰 팔레트 (다크 단일 — 시안 원값):
| 토큰 키 | 값 (시안 원값) |
|---|---|
| AppRootBackgroundColor | #1A1A1C |
| AppContentBackgroundColor | #202023 |
| AppContentBorderColor | #2C2C30 |
| AppCardBackgroundColor | #242427 |
| AppCardBorderColor | #2F2F34 |
| AppInputBackgroundColor | #2A2A2E |
| AppInputBorderColor | #3A3A40 |
| AppInputBottomLineColor | #55555C |
| AppComboBackgroundColor | #2E2E32 |
| AppComboBorderColor | #3D3D43 |
| AppHoverBackgroundColor | #28282C |
| AppActiveBackgroundColor | #2A2A2E |
| AppTextPrimaryColor | #E8E8EA (강조 #FFFFFF) |
| AppTextSecondaryColor | #9A9AA0 |
| AppTextTertiaryColor | #8A8A90 |
| AppTextNavColor | #B8B8BE |
| AppAccentColor | #F25C54 |
| AppAccentHoverColor | #FF6F66 |
| AppAccentSoftHoverColor (모니터 hover 테두리) | #FF8A82 |
| AppTintBackgroundColor (pill·배너 배경) | #2A2326 |
| AppTintBorderColor | #4A3338 |
| AppTintTextColor (pill 라벨) | #E8C9C5 |
| AppTintBodyColor (배너 본문) | #C9B4B1 |
| AppTintButtonBackgroundColor (정지 버튼) | #3A2A2C |
| AppTintButtonTextColor | #F2A49F |
| AppMonitorSelectedBackground (gradient 180°) | #3A2726→#2A1E1D |
| AppMonitorNumberSelectedColor | #F2A49F |
| AppMonitorNumberColor | #6A6A70 |
| AppMonitorSubSelectedColor | #C9948F |
| AppMonitorSubColor | #7A7A80 |
| AppMonitorBorderColor (비선택) | #3A3A40 |
| AppMonitorStandColor (받침) | #3A3A40 |
| AppDividerColor | #2C2C30 |
| AppDashedBorderColor | #4A4A50 |
| AppChipTextColor | #D8D8DC |
| AppThumbPlaceholderBackground (gradient 135°) | #4A3038→#25191D |

### D4. 모니터 카드 공용화 구조
- **Options**: A) SettingsViewModel의 모니터 로직을 `MonitorPanelViewModel`로 추출, 홈·설정이 각자 인스턴스 소유 / B) HomeViewModel에 로직 복제 / C) SettingsViewModel을 홈에서 직접 참조
- **Chosen**: A
- **Rationale**: B는 중복(선택 저장·최소 1개 강제 로직 2벌), C는 설정 VM 전체(테마·언어·계정 등)를 홈에 끌어와 결합 과잉. A는 기존 로직 "이동"이라 동작 보존이 검증 가능.
- **Source**: SettingsViewModel.cs 332·459행 (이동 대상 로직), grep 전수 7 hits

### D5. 빠른 재생 칩 클릭 동작
- **Chosen**: 플레이리스트 페이지로 이동 + 해당 리스트 선택 (즉시 재생 아님)
- **Source**: 시안 프로토타입 스크립트 `chips: ... onClick: () => this.setState({ page: 'playlists', playlist: p.name })` — 기준 문서가 동작을 정의

### D6. 플레이리스트 컬럼 헤더 행 제거
- **Chosen**: 현재 있는 컬럼 헤더 행(순위/곡정보/듣기, PlaylistsPage 135~166행)을 제거
- **Rationale**: 시안 트랙 목록에 헤더 행이 없음 — 재작성 규칙(최신 기준 문서에 없으면 제거 대상). 관련 resw 키(ColRankLabel 등)는 미사용화되므로 함께 정리(키 삭제는 T6 범위 명시)
- **Source**: 시안 HTML 플레이리스트 화면 (행 직접 시작)

### D7. 드래그 정렬 비주얼
- **Chosen**: ListView 내장 드래그(CanReorderItems) 유지 — 시안의 커스텀 고스트·스페이서 미재현
- **Rationale**: 프로토타입 전용 연출. 내장 기능이 접근성·안정성 우수, 기능(순서 변경)은 동일. Out of Scope에 명시.

### D8. 사이드바 아이콘 글리프 매핑
- **Chosen**: ⌂→`E80F`(Home, 기존 Icon="Home" 유지), ≡→`E8FD`(기존 List 유지), ⚙→`E713`(Settings), ⓘ→`E946`(기존 유지)
- **Rationale**: 시안 아이콘은 텍스트 근사 문자 — Segoe Fluent Icons 대응 글리프가 의미 동일. 기존 매핑 최대 유지.

### D9. 폰트
- **Chosen**: FontFamily 미지정 유지 (AGENTS.md 규칙 4)
- **Rationale**: WinUI 3 기본 폰트가 시안 지정값(Segoe UI Variable)과 동일 — 지정 없이 시안과 일치.

### D10. Mica 백드롭 제거
- **Chosen**: `SystemBackdrop = MicaBackdrop` 제거, 루트 배경 = AppRootBackground 토큰(불투명)
- **Rationale**: 시안 배경은 불투명 단색(#1A1A1C) — Mica 반투명과 양립 불가. 시각 충실도 우선.
- **Source**: 시안 body/루트 배경 정의, MainWindow.xaml.cs 35행

### D11. HighContrast 처리
- **Chosen**: DesignTokens의 ThemeDictionaries에 `Default`(다크 원값)·`HighContrast`(시스템 HC 색 키 매핑: 배경=`SystemColorWindowColor`, 텍스트=`SystemColorWindowTextColor`, accent=`SystemColorHighlightColor` 등) 2사전 구성 (`Light` 사전 없음 — 앱이 다크 고정이라 조회 자체가 발생하지 않음, D3)
- **Rationale**: 다크 고정이어도 시스템 HC 모드에서는 HC 리소스가 우선 적용됨 — HC 사전 누락 시 대비 깨짐. 시각 정합의 정밀 조정은 Deferred.

### D12. 시안 신규 문구의 다국어 처리
- **Chosen**: ko 값은 시안 원문 그대로(의역 금지 — 문서 기반 작업 규칙), en 값은 대응 자연 번역을 신규 작성. 기존 키는 값 갱신, 삭제는 D6 관련 키만.
- **Source**: 시안 HTML 문구 전량 (`## 시각 요소 분해` 문구 행), CLAUDE.md 문서 기반 작업 규칙

### D13. 콘텐츠 영역 카드 구조
- **Chosen**: MainWindow에서 NavigationView의 콘텐츠(Frame)를 Border(배경 AppContentBackground·CornerRadius 10,0,0,0·테두리 좌·상 1px)로 감싼다. NavigationView 자체 배경은 투명 처리(pane 배경 = AppRootBackground)
- **Rationale**: 시안의 "사이드바는 창 배경, 콘텐츠는 좌상단만 둥근 카드" 구조를 표준 컨트롤 조합으로 재현.
- **Source**: 시안 콘텐츠 div (`border-radius:10px 0 0 0; border:1px solid #2c2c30; border-right:none; border-bottom:none`)

## 시각 요소 분해

> 확인 방법 열의 "시안"은 기준 문서(DeskTube-1a.dc.html)의 해당 화면 인라인 스타일. 색 토큰은 D3 표의 키로 구현.

### 공통 셸
| 요소 | 속성 | 디자인 값 | 확인 방법 |
|------|------|----------|-----------|
| 창 루트 | 배경 | #1A1A1C 불투명 (Mica 없음) | 시안 루트 div |
| 타이틀바 | 높이 | 44px | 시안 타이틀바 div |
| 타이틀바 | 좌측 패딩·간격 | padding 0 16, 아이콘-텍스트 gap 10 | 시안 |
| 타이틀바 아이콘 | 크기·라운드 | 18×18, radius 5 (앱 아이콘 이미지) | 시안 img |
| 타이틀바 앱명 | 폰트 | 13px, weight 600, #E8E8EA, "DeskTube" | 시안 span |
| 타이틀바 버튼 | 구성 | 시스템 캡션 버튼 (자체 그리지 않음 — Q3 확정) | 사용자 확정 |
| 사이드바 | 폭·패딩·간격 | 220px, padding 8 10 14, 항목 gap 4 | 시안 사이드바 div |
| 내비 항목 | 패딩·라운드·폰트 | padding 10 14, radius 8, 14px, 아이콘 슬롯 18px·gap 12 | 시안 |
| 내비 항목(활성) | 색 | 배경 #2A2A2E, 텍스트 #FFF weight 600 | 시안 |
| 내비 항목(활성) | 인디케이터 | 좌측 3px 코럴 #F25C54, radius 2, 상하 10px 인셋 | 시안 |
| 내비 항목(비활성) | 색 | 텍스트 #B8B8BE weight 400, hover 배경 #28282C | 시안 |
| 내비 구성 | 항목·순서 | 상단: 홈·플레이리스트·설정 / 하단(footer): 정보 | 시안 navTop/navBottom |
| 콘텐츠 영역 | 카드 | 배경 #202023, radius 10 0 0 0, 테두리 1px #2C2C30 (우·하 없음) | 시안 콘텐츠 div |

### 홈
| 요소 | 속성 | 디자인 값 | 확인 방법 |
|------|------|----------|-----------|
| 페이지 | 패딩·간격 | padding 44 56, 섹션 gap 36 | 시안 홈 div |
| 제목 | 텍스트·폰트 | "바탕화면에서 재생", 30px weight 600 #FFF | 시안 h1 |
| 부제 | 텍스트·폰트 | "유튜브 영상 주소를 붙여넣으면 바탕화면 배경에서 바로 재생됩니다", 14px #9A9AA0 | 시안 p |
| URL 입력 | 치수·스타일 | 높이 48, 배경 #2A2A2E, 테두리 #3A3A40 + 하단 2px #55555C, radius 8, padding 좌우 18, 14px, 입력행 max-width 860·gap 12 | 시안 input |
| URL 입력 | placeholder | "유튜브 영상 주소를 입력하세요" | 시안 |
| 재생 버튼 | 스타일 | 높이 48, padding 0 26, 배경 코럴, radius 8, "▶ 재생" 14px weight 600 #FFF, hover #FF6F66 | 시안 |
| 재생 중 pill | 표시 조건 | 재생 중일 때만 (Status != Stopped) | 시안 sc-if playing |
| 재생 중 pill | 스타일 | 배경 #2A2326, 테두리 #4A3338, radius 20, padding 8 8 8 18, gap 12, 코럴 dot 8×8 원형 | 시안 |
| 재생 중 pill | 라벨 | "디스플레이 {선택 모니터 번호·구분 '·'}에서 재생 중", 13px #E8C9C5 | 시안 playingLabel |
| 정지 버튼 | 스타일 | "■ 정지", padding 5 14, radius 14, 배경 #3A2A2C, 텍스트 #F2A49F 12px weight 600, hover #4A3338 | 시안 |
| 모니터 섹션 헤더 | 텍스트 | "재생 모니터" 16px weight 600 + "클릭해서 선택 · 소리는 스피커 표시 모니터에서만" 13px #8A8A90 | 시안 |
| 모니터 카드(홈) | 치수 | 300×188, radius 10, 테두리 2px, 카드 gap 28, 받침 70×5 radius 3 #3A3A40 | 시안 |
| 모니터 카드 | 선택 상태 | 테두리 코럴, 배경 gradient(180° #3A2726→#2A1E1D), 숫자 #F2A49F | 시안 m.border/bg |
| 모니터 카드 | 비선택 상태 | 테두리 #3A3A40, 배경 #242427, 숫자 #6A6A70 | 시안 |
| 모니터 카드 | 숫자 | 36px weight 200 중앙 | 시안 |
| 모니터 카드 | 소리 배지 | 우상단 10,10 — "🔊 소리", 배경 코럴, #FFF 11px weight 700, padding 3 9, radius 20. 표시 = 선택됨 ∧ 오디오 대상 ∧ 비음소거 | 시안 showAudio 식 |
| 모니터 카드 | 서브라벨 | 좌하단 — "{W}×{H}"(+주 모니터면 " · 주 모니터"), 12px, 선택 #C9948F/비선택 #7A7A80 | 시안 m.sub |
| 모니터 카드 | hover | 테두리 #FF8A82 | 시안 style-hover |
| 빠른 재생 헤더 | 텍스트 | "빠른 재생" 16px weight 600, 섹션은 페이지 하단 정렬 | 시안 margin-top:auto |
| 빠른 재생 칩 | 스타일 | "≡ {리스트명} {N}곡" — 배경 #2A2A2E, 테두리 #3A3A40, radius 8, padding 10 16, 13px #D8D8DC(곡수 #8A8A90), hover 테두리 코럴, gap 12 wrap | 시안 chips |
| 빠른 재생 칩 | 동작 | 클릭 → 플레이리스트 페이지 이동 + 해당 리스트 선택 (D5) | 시안 스크립트 |

### 플레이리스트
| 요소 | 속성 | 디자인 값 | 확인 방법 |
|------|------|----------|-----------|
| 좌측 패널 | 치수 | 폭 300 고정, 우측 구분선 1px #2C2C30, padding 32 18 18, gap 16 | 시안 |
| 패널 제목 | 폰트 | "플레이리스트" 22px weight 600 #FFF (패딩 0 8) | 시안 |
| 새 플레이리스트 버튼 | 스타일 | "＋ 새 플레이리스트" — 점선 테두리 1px #4A4A50, radius 8, padding 11, 중앙, 13px #B8B8BE, hover 테두리·텍스트 코럴 | 시안 |
| 리스트 항목 | 스타일 | padding 12 14, radius 8, 13px, 곡수 우측 #8A8A90, hover #28282C | 시안 |
| 리스트 항목(활성) | 색·인디케이터 | 배경 #2A2A2E, 텍스트 #FFF weight 600, 좌측 3px 코럴 인디케이터(상하 12px 인셋) | 시안 |
| 우측 영역 | 패딩·간격 | padding 32 36, gap 20 | 시안 |
| URL 추가 입력 | 위치·스타일 | 우측 최상단 — 높이 42, 홈 입력과 동일 스타일(padding 0 16, 13px), placeholder "추가할 유튜브 영상 주소를 입력하세요" + "추가" 버튼(높이 42, padding 0 20, 테두리 #3A3A40, hover 코럴) | 시안 |
| 리스트 헤더 행 | 구성 | "{리스트명}" 22px weight 700 + "{N}곡" 13px #8A8A90 + 우측 버튼 2개 | 시안 |
| 셔플듣기 버튼 | 스타일 | "⇄ 셔플듣기" — padding 9 18, radius 8, 테두리 #3A3A40, 13px #D8D8DC, hover 테두리 코럴 | 시안 |
| 전체듣기 버튼 | 스타일 | "▶ 전체듣기" — padding 9 18, radius 8, 배경 코럴, 13px weight 600 #FFF, hover #FF6F66 | 시안 |
| 컬럼 헤더 행 | 존재 | **없음** — 현재 구현의 헤더 행 제거 (D6) | 시안 (행 직접 시작) |
| 트랙 행 | 레이아웃 | padding 12 14, 하단 구분선 1px #2C2C30, gap 16, hover #28282C | 시안 |
| 트랙 행 | 순번 | 폭 22 중앙, 14px #8A8A90 | 시안 |
| 트랙 행 | 썸네일 | **64×40** (16:9), radius 6, placeholder gradient(135° #4A3038→#25191D) — 현행 56×56 정사각에서 변경 | 시안 |
| 트랙 행 | 제목 | 13px weight 600 #E8E8EA, ellipsis | 시안 |
| 트랙 행 | 채널명 | 12px #8A8A90 | 시안 |
| 트랙 행 | 재생 버튼 | 우측 34×34 원형, 테두리 1px #3A3A40, 글리프 12px #D8D8DC, hover 테두리·글리프 코럴 — 현행 코럴 상시 테두리에서 변경 | 시안 |
| 드래그 정렬 | 비주얼 | ListView 내장 유지 (시안 고스트 미재현 — D7) | 결정 |

### 설정
| 요소 | 속성 | 디자인 값 | 확인 방법 |
|------|------|----------|-----------|
| 페이지 | 패딩·최대폭 | padding 44 56, 제목 아래 gap 24, 카드 열 max-width 900·gap 10 | 시안 |
| 제목 | 폰트 | "설정" 30px weight 600 #FFF | 시안 h1 |
| 그룹 헤더 | 스타일 | 12.5px weight 700 letter-spacing 0.06em #9A9AA0, margin 16 4 2 (첫 헤더 4 4 2) | 시안 |
| 그룹 구성 | 순서 | **화면**: 재생 모니터(카드형)·동영상 크기·화질(+안내 배너)·소리 없는 모니터 화질 낮추기 / **소리**: 소리 나오는 모니터·볼륨·음소거 / **재생**: 재생 순서 / **일반**: Windows 시작 시 자동 실행·언어·유튜브 계정·자동 일시정지 | 시안 (앱 시작 후 자동 재생은 제외 — Q4 / 테마 카드는 기능 삭제로 제외 — 사용자 후속 지시, D3) |
| 설정 카드 | 스타일 | 배경 #242427, 테두리 1px #2F2F34, radius 8, padding 16 24 (모니터 카드 20 24) | 시안 |
| 카드 제목/설명 | 폰트 | 제목 14px weight 600 #E8E8EA / 설명 12.5px #8A8A90 (gap 3) | 시안 |
| 카드 문구 | 원문 | 시안 원문 그대로 — 예: 재생 모니터 "배경 영상을 표시할 모니터를 클릭해서 선택하세요", 동영상 크기 "영상을 화면에 어떻게 맞출지 선택합니다", 화질 낮추기 "소리가 나지 않는 모니터의 화질을 낮춰 컴퓨터 부담을 줄입니다", 소리 모니터 "이 모니터에서만 소리가 납니다", 음소거 "켜면 모든 모니터의 소리를 끕니다", 자동 실행 "로그인하면 창 없이 트레이에서 마지막 설정으로 재생을 시작합니다", 언어 "언어를 바꾸면 설정 창이 다시 열립니다", 계정 "로그인하면 유튜브 프리미엄 혜택이 배경 재생에도 적용됩니다", 자동 일시정지 "배경 영상을 잠시 멈춰 시스템 자원을 아낍니다" (테마 카드 문구는 제외 — 기능 삭제) | 시안 문구 전량 |
| 모니터 카드(설정) | 치수 | 200×125, radius 8, 숫자 26px, 배지 10px(우상 7,7), 서브라벨 11px(좌하 7,9), 받침 48×4, gap 20 | 시안 |
| 콤보(선택기) | 스타일 | 배경 #2E2E32, 테두리 #3D3D43, radius 6, padding 8 14, 13px, min-width 150, 우측 ⌄ #8A8A90 | 시안 |
| 콤보 옵션 문구 | 원문 | 재생 순서: 순서대로/셔플 (전곡 1회)/무작위/한 곡 반복/전체 반복 · 크기: 채움/맞춤/늘리기 · 화질: 원본/1080p/720p/480p · 언어: 시스템 언어 따름/한국어/English · 소리 모니터: 자동 (주 모니터)/디스플레이 N (테마 옵션은 제외 — 기능 삭제) | 시안 스크립트 배열 |
| 토글 스위치 | 스타일 | 44×22 radius 11 — 켬: 배경·테두리 코럴, knob 흰 14×14 좌 26 / 끔: 투명, 테두리 #55555C, knob #B8B8BE 좌 4. 좌측 상태 라벨 "켬/끔" 12px #9A9AA0 | 시안 toggleStyle |
| 화질 안내 배너 | 스타일 | 배경 #2A2326, 테두리 #4A3338, radius 8, padding 13 20 — "i" 원형 18×18 코럴 아이콘 + "실제 스트리밍 화질은 유튜브가 자동으로 결정합니다. 이 설정은 렌더링 해상도를 낮춰 시스템 부하를 줄입니다." 12.5px #C9B4B1 | 시안 |
| 볼륨 카드 | 구성 | 제목 "볼륨" + 우측 값 라벨({0~100}, 13px #8A8A90 폭 32 우정렬) + 슬라이더 폭 260 코럴 | 시안 |
| 계정 카드 | 구성 | 상태 텍스트("로그인됨/로그인 안 됨" 13px #8A8A90) + 버튼("로그인/로그아웃", padding 8 20, radius 6, 배경 #2E2E32, 테두리 #3D3D43, hover 테두리 코럴) | 시안 |
| 자동 일시정지 | 구조 | 카드형 expander — 헤더(제목+설명+⌄/⌃) hover #28282C, 펼침 시 서브 행 3개(padding 14 24 14 44, 상단 구분선, 라벨 13.5px #D8D8DC + 토글): "전체 화면 앱 사용 중"·"배터리 절약 모드일 때"·"화면이 잠겨 있을 때" | 시안 pauseRows |

### 정보
| 요소 | 속성 | 디자인 값 | 확인 방법 |
|------|------|----------|-----------|
| 페이지 | 구조 | padding 44 56, 제목 "정보" 30px, 카드 열 max-width 900·gap 10 | 시안 |
| 앱 카드 | 구성 | 아이콘 52×52 radius 12 + "DeskTube" 17px weight 700 + "버전 {버전} · 개발자: {개발자}" 12.5px #8A8A90 (한 줄), padding 22 24 gap 18 | 시안 |
| 개인정보 카드 | 구성 | 제목 "개인정보 처리방침" 14px weight 600 + 본문 12.5px #A8A8AE line-height 1.7 + 각주 "전문: 프로젝트 저장소의 docs/privacy-policy.md 문서" 12px #8A8A90 — 본문·각주는 기존 resw 문구 유지(시안과 동일 취지) | 시안 + 기존 resw |
| 라이선스 섹션 | 제목 | "오픈소스 라이선스" 15px weight 600 (상단 margin 10) | 시안 |
| 라이선스 카드 | 구성 | 카드형 expander — 헤더(이름 13.5px weight 600 + 라이선스명 12px #8A8A90 + 우측 ⌄/⌃) padding 15 24 hover #28282C, 펼침 본문 12px #8A8A90 line-height 1.7 상단 구분선 | 시안 |

## Tasks

- [x] T1. AGENTS.md 디자인 규칙 개정 (시안 기준화)
  - **Type**: A
  - **Acceptance**: AGENTS.md "디자인 규칙" 섹션 규칙 1이 다음 취지로 교체됨 — "디자인 기준은 확정 시안(현재: docs/plans/2026-07-16-design-1a-restyle.md의 시각 요소 분해)이다. 시안이 정의하지 않은 UX는 WinUI Gallery 표준을 따른다. 색·치수·라운드는 `Resources/DesignTokens.xaml` 토큰만 참조(하드코딩 금지 유지), 표준 컨트롤 구조(NavigationView·SettingsCard 등) 위에 스타일만 입힌다." 규칙 3(테마)은 "테마는 다크 고정 — App.xaml `RequestedTheme="Dark"` 한 곳에서만 지정, 개별 요소 RequestedTheme 지정 금지"로 교체(FR-17 REMOVED 반영, ThemeHelper 언급 제거). 규칙 5(시스템 키 우선)는 "토큰 키 우선"으로, 규칙 6(페이지 골격)·7은 시안 골격 기준으로 갱신. 다국어 규칙 3-⑤("새 Frame이라 RequestedTheme 초기화 → 테마 재적용 필수")는 Application 수준 고정으로 사유가 소멸하므로 삭제. 다른 섹션 변경 없음
  - **Files**:
    - 주: `AGENTS.md`
  - **Edge Cases**: 해당 없음 (문서)
  - **Halt Forecast**:
    - (ii-a) AGENTS.md(프로젝트 규약 문서) 수정 → `## 사전 승인 항목`에 등록
  - **Depends on**: -

- [x] T2. 다크 테마 고정 전환 + 디자인 토큰 사전 신설 + 코럴 accent 전파
  - **Type**: D
  - **Design**: ① `src/DeskTube/Resources/DesignTokens.xaml` 신규 (D1) + 테마 기구 삭제(D3): App.xaml `RequestedTheme="Dark"` 지정, `Services/ThemeHelper.cs`·`Models/AppTheme.cs` 파일 삭제, `AppSettings.Theme` 필드 제거, SettingsViewModel 테마 멤버(ThemeOptions·ThemeIndex·OnThemeIndexChanged) 제거, SettingsPage ThemeCard 제거, resw 테마 5키(ko/en) 제거 ② 신규 심볼 = 토큰 키(D3 표: Color + 대응 SolidColorBrush/LinearGradientBrush) + 공용 Style(`AppPillButtonStyle`(플레이리스트 pill 재사용 추출), `AppChipButtonStyle`, `AppCardBorderStyle`, `AppGroupHeaderTextStyle`, `AppDashedButtonStyle`) ③ App.xaml이 병합·참조, 모든 View가 `ThemeResource`로 소비. ThemeHelper 호출부 4곳(App.xaml.cs 68·70, MainWindow.xaml.cs 40, LoginWindow.xaml.cs 22, SettingsViewModel.cs 541 — Investigation Log 전수) 제거 ④ 비추상화: 컨트롤 템플릿 전면 재작성 안 함 — lightweight styling(리소스 키 재정의)과 명시 Style만. 테마 전환 재도입 대비 훅 남기지 않음
  - **Acceptance**: Given 시스템이 라이트 모드여도, When 앱 실행, Then 모든 창이 다크(시안 원값)로 표시되고 설정에 테마 카드가 없음 (기계 검증: 빌드 0 경고·0 오류 + 테스트 통과 + DesignTokens.xaml에 D3 전 키 존재 grep + `ThemeHelper|AppTheme|Theme_` 참조 잔존 0 grep / AccentButtonStyle·ToggleSwitch On·Slider·NavigationView 인디케이터의 코럴 표시와 다크 고정 실표시는 ⏳ HUMAN-VERIFY). HighContrast 사전 존재(D11)
  - **Files**:
    - 주: `src/DeskTube/Resources/DesignTokens.xaml` (신규), `src/DeskTube/App.xaml` (병합 + SystemAccentColor 재정의 + RequestedTheme), `src/DeskTube/ViewModels/SettingsViewModel.cs` (테마 멤버 제거)
    - 동반: `src/DeskTube/App.xaml.cs` (Initialize 경로 제거 + 182~183행 ApplyLanguageChange의 FR-17/테마 재적용 스테일 주석 정리), `src/DeskTube/MainWindow.xaml.cs`·`src/DeskTube/Views/LoginWindow.xaml.cs` (Register 제거), `src/DeskTube/Views/SettingsPage.xaml` (ThemeCard 제거), `src/DeskTube/Models/AppSettings.cs` (Theme 필드 제거), `Strings/*/Resources.resw` (테마 5키 제거)
    - 테스트: `tests/DeskTube.Tests/JsonStateStoreTests.cs` (44행 Theme 대입·62행 Theme assert 제거 — 왕복 테스트 본질 유지)
    - 삭제: `src/DeskTube/Services/ThemeHelper.cs`, `src/DeskTube/Models/AppTheme.cs`
  - **Edge Cases**:
    - HighContrast 모드 진입 시 크래시·저대비 없음 (HC 사전 매핑)
    - 기존 저장 JSON에 Theme 속성 잔존 → 역직렬화 무시로 호환 (4-B)
    - `Application.RequestedTheme`는 시작 후 변경 불가 — 코드 설정 지점 전부 제거로 예외 경로 차단 (Risks)
  - **Halt Forecast**:
    - (i) SystemAccentColor 재정의 미전파 컨트롤 발견 → D2에 따라 해당 브러시 키 개별 오버라이드 (plan 내 해결)
    - (ii-a) 신규 파일·디렉터리 생성(Resources/) + 파일 삭제 2개(ThemeHelper.cs·AppTheme.cs) + AppSettings 필드 제거 → `## 사전 승인 항목`
  - **Depends on**: T1

- [x] T3. 셸 재설계 — 타이틀바·사이드바·콘텐츠 카드
  - **Type**: D
  - **Design**: ① `MainWindow.xaml(.cs)` 내 재구성 (신규 파일 없음) ② 신규 심볼 — `OnNavSelectionChanged` switch에 "settings" Tag 분기 추가, `NavigateToPlaylists(Guid)` internal 메서드 신설(T5의 칩 이동 진입점 — 책임: Nav 선택 + Frame.Navigate(파라미터)), `App`에 `internal MainWindow? Main => _window as MainWindow;` 접근자 신설(HomePage의 MainWindow 참조 획득 수단 — 사전 승인 항목 등록), `ApplyCaptionButtonColors`/`GetTokenColor` private 헬퍼(시스템 캡션 버튼 색을 토큰과 정합 — 자체 버튼 구현이 아니라 색만 지정, Q3 범위 내. 구현 중 추가분 소급 기재 — spec 리뷰 MINOR 반영) ③ App.ShowMainWindow·ShowNotice 시그니처 보존, HomePage가 `((App)Application.Current).Main?.NavigateToPlaylists(...)`로 호출(의존 방향: View→App→MainWindow) ④ 비추상화: 내비게이션 서비스/메시징 도입 안 함 — 창 1개·페이지 4개 규모에 과함
  - **Acceptance**: Given 앱 실행, When 각 내비 항목 클릭, Then 4페이지 전환 동작 유지(설정 포함) + 시안 셸 스펙 충족 — 타이틀바 44px·아이콘 18px·앱명 13px, 사이드바 220px(토글 버튼 없음), 정보가 footer, 활성 항목 코럴 인디케이터, 콘텐츠 좌상단 라운드 카드, Mica 제거·불투명 배경 (기계 검증: 빌드 + 기존 테스트 100 통과 / 시각은 ⏳ HUMAN-VERIFY). 트레이 "설정 열기"·창 닫기→숨김 동작 불변
  - **Files**:
    - 주: `src/DeskTube/MainWindow.xaml`, `src/DeskTube/MainWindow.xaml.cs`
    - 동반: `src/DeskTube/Strings/ko-KR/Resources.resw`, `src/DeskTube/Strings/en-US/Resources.resw` (NavSettings 키 신설)
    - 동반: `src/DeskTube/Assets/` 기존 앱 아이콘 참조 (신규 에셋 없음 — 기존 Square44 계열 재사용)
  - **Edge Cases**:
    - IsSettingsVisible=False 전환 후 내비 항목으로 설정 진입 동작. 트레이 "설정 열기"는 창 표시만 하고 페이지 이동은 하지 않음(ShowMainWindow 불변 — 기존 동작 그대로)
    - 창 최소 크기 720×480에서 사이드바+콘텐츠 레이아웃 유지
    - 언어 전환(셸 재로드) 후에도 다크 표시 유지 — Application 수준 고정이라 재적용 코드 불요 (T2·D3, 구 규칙 3-⑤는 T1에서 삭제)
  - **Halt Forecast**:
    - (i) NavigationView pane 폭·인디케이터 스타일 키 상이 → lightweight 리소스 키 오버라이드로 해결 (Risks 1행)
    - (ii-a) MainWindow 내부 구조 변경(계획된 셸 재구성) → `## 사전 승인 항목`
  - **Depends on**: T2

- [x] T4. 모니터 카드 공용 컴포넌트 (MonitorPanelViewModel + MonitorCardsControl)
  - **Type**: D
  - **Design**: ① `ViewModels/MonitorChoice.cs`(분리·확장: `ResolutionLabel`·`IsPrimary`·`ShowAudioBadge` 추가), `ViewModels/MonitorPanelViewModel.cs`(신규 — 모니터 열거·선택 토글·최소 1개 강제·AppSettings 저장·MonitorsChanged 구독·배지 계산. SettingsViewModel 332·459행 로직 이동), `Controls/MonitorCardsControl.xaml(.cs)`(신규 UserControl — ItemsSource 바인딩, `IsLarge` bool 의존 속성으로 홈 300×188/설정 200×125 변형 — 당초 `CardSize` 명명을 bool로 단순화, spec 리뷰 M2 소급 정정) ② 책임 각 1줄 위와 같음 ③ SettingsViewModel·HomeViewModel(T5)이 MonitorPanelViewModel을 소유, Control은 VM만 바인딩(서비스 직접 참조 금지) ④ 비추상화: 모니터 외 범용 카드 컨트롤로 일반화하지 않음
  - **Acceptance**: Given 설정 화면, When 모니터 카드 클릭 토글, Then 기존과 동일하게 선택 저장·최소 1개 강제·오디오 대상 배지 갱신 (기계 검증: 빌드 + 테스트 100 통과 — 단 이동 대상 로직은 자동 테스트가 없으므로 이는 회귀 바닥선일 뿐이며, 이동 로직의 동작 보존·카드 시각은 ⏳ HUMAN-VERIFY). SettingsPage의 기존 SettingsExpander 토글 목록은 카드 컨트롤로 대체됨
  - **Files**:
    - 주: `src/DeskTube/ViewModels/MonitorPanelViewModel.cs` (신규), `src/DeskTube/Controls/MonitorCardsControl.xaml(.cs)` (신규), `src/DeskTube/ViewModels/MonitorChoice.cs` (신규 — SettingsViewModel에서 분리)
    - 동반: `src/DeskTube/ViewModels/SettingsViewModel.cs` (모니터 로직 위임), `src/DeskTube/Views/SettingsPage.xaml` (모니터 영역 컨트롤 교체 — 상세 스타일은 T7)
    - 동반: `Strings/*/Resources.resw` (소리 배지·주 모니터 라벨 키)
  - **Edge Cases**:
    - 모니터 1개(토글 해제 불가 — 최소 1개 강제 유지)·3개 이상(wrap 배치)
    - 재생 중 모니터 분리/연결(MonitorsChanged) 시 카드 목록 갱신
    - 음소거 상태에서 소리 배지 숨김 (시안 식: 선택∧오디오∧비음소거)
  - **Halt Forecast**:
    - (i) 배지의 음소거·오디오 인덱스 상태 동기화 시점 → Populate(페이지 진입)·자체 변경 시 갱신으로 확정 (Edge 3행)
    - (ii-a) MonitorChoice 클래스 파일 분리(구조 변경·계획된 공개 심볼 이동, 호출부 2곳 grep 확정) → `## 사전 승인 항목`
  - **Depends on**: T2

- [x] T5. 홈 화면 재설계
  - **Type**: D
  - **Design**: ① `Views/HomePage.xaml(.cs)`·`ViewModels/HomeViewModel.cs` 확장 ② 신규 심볼 = HomeViewModel 멤버: `MonitorPanel`(MonitorPanelViewModel 소유), `IsPlaying`·`PlayingLabel`(StatusChanged 구독), `StopCommand`(Coordinator 정지 재사용), `QuickChips`(ObservableCollection — Library.Playlists 투영), 칩 클릭은 HomePage 코드비하인드 `OnChipClick`이 MainWindow.NavigateToPlaylists 호출(당초 `OpenPlaylistCommand` 계획을 순수 화면 이동이라 View 담당으로 대체 — spec 리뷰 MINOR 소급 정정) ③ HomeViewModel→Coordinator/Library/MonitorPanelViewModel 참조, HomePage→MainWindow.NavigateToPlaylists(T3 신설) 호출 ④ 비추상화: 칩·pill을 별도 컨트롤로 뽑지 않음(홈 전용)
  - **Acceptance**: Given 홈, When URL 입력·재생/정지·모니터 토글·칩 클릭, Then — 재생 시작(기존 PlayAsync 계약 불변), pill이 재생 중에만 표시되고 라벨 "디스플레이 N에서 재생 중"·정지 동작, 모니터 카드 선택이 설정과 동일 상태 공유, 칩 클릭 시 플레이리스트 페이지로 이동+해당 리스트 선택 (기계 검증: 빌드+테스트 / 시각·이동 동작 ⏳ HUMAN-VERIFY). 시안 홈 스펙(분해 표) 전 항목 반영
  - **Files**:
    - 주: `src/DeskTube/Views/HomePage.xaml(.cs)`, `src/DeskTube/ViewModels/HomeViewModel.cs`
    - 동반: `src/DeskTube/Views/PlaylistsPage.xaml.cs` (`OnNavigatedTo` 파라미터로 리스트 선택 수용), `src/DeskTube/ViewModels/PlaylistsViewModel.cs` (`SelectPlaylist(Guid)` 헬퍼 — 기존 SelectedPlaylist 세터 재사용)
    - 동반: `Strings/*/Resources.resw` (홈 신규 문구: 부제·placeholder·재생 중 라벨 포맷·정지·빠른 재생·곡수 포맷)
  - **Edge Cases**:
    - 플레이리스트 0개 → 빠른 재생 섹션 숨김(헤더 포함)
    - 재생 중 페이지 재진입(NavigationCacheMode) → OnNavigatedTo에서 pill·모니터·칩 재갱신
    - StatusChanged가 비UI 스레드에서 발생 가능 → DispatcherQueue 마샬링
    - 칩의 리스트가 이동 직전 삭제된 경우 → 존재 확인 후 무시(선택 없음 폴백)
  - **Halt Forecast**:
    - (i) pill 라벨의 모니터 번호 산출(선택 ID→표시 번호) → MonitorPanelViewModel의 카드 순번 재사용으로 확정
    - (ii-a) HomeViewModel 공개 멤버 추가(계획된 시그니처 확장) → `## 사전 승인 항목`
  - **Depends on**: T3, T4

- [x] T6. 플레이리스트 화면 재설계
  - **Type**: C
  - **Design**: 해당 없음 — 신규 심볼 없음 (기존 XAML 재구성 + T2 공용 스타일 소비. D6 헤더 행 제거 포함)
  - **Acceptance**: Given 플레이리스트 화면, When 기존 조작(생성·이름변경·삭제·추가·드래그 정렬·셔플듣기·전체듣기·행 재생), Then 기능 전부 불변 + 시안 스펙 반영 — 좌패널 300px·점선 새 리스트 버튼·활성 인디케이터, URL 입력 상단 배치, 컬럼 헤더 행 제거, 썸네일 64×40, 행 hover·재생 버튼 시안 색 (기계 검증: 빌드+테스트, 미사용 키(ColRankLabel·ColInfoLabel·ColListenLabel) resw 제거 및 잔존 참조 0 grep / 시각 ⏳ HUMAN-VERIFY)
  - **Files**:
    - 주: `src/DeskTube/Views/PlaylistsPage.xaml`
    - 동반: `src/DeskTube/Views/PlaylistsPage.xaml.cs` (헤더 행 제거에 따른 참조 정리 시), `src/DeskTube/ViewModels/PlaylistsViewModel.cs` (썸네일 DecodePixelWidth 조정 등 표시 속성만), `Strings/*/Resources.resw` (placeholder·버튼 문구 시안 원문화, Col* 키 제거)
  - **Edge Cases**:
    - 빈 리스트·선택 없음 상태의 기존 안내(NoSelectionText) 유지
    - 제목 긴 항목 ellipsis, 1000개 가상화 유지(ListView 기본)
    - 썸네일 로드 실패 시 placeholder gradient 표시
  - **Halt Forecast**: (없음 — 파괴적·의존성·외부 요소 없음. resw 키 제거는 D6 결정으로 사전 확정)
  - **Depends on**: T2

- [x] T7. 설정 화면 재설계
  - **Type**: C
  - **Design**: 신규 심볼 1 — `SettingsPage.FormatVolume(double)` x:Bind 포맷 헬퍼(볼륨 값 라벨용, spec 리뷰 MINOR 소급 정정). 그 외 그룹 헤더 4개 + 카드 재배열 + T4 컨트롤·T2 스타일 소비
  - **Acceptance**: Given 설정 화면, When 각 설정 조작, Then 기존 항목 전수(FR-10 — 모니터·크기·화질+배너·미러 하향·오디오·볼륨·음소거·재생 순서·자동 실행(+상태 InfoBar)·언어·계정·자동 일시정지 3토글 — 테마 카드는 T2에서 제거됨) 동작 불변 + 시안 그룹 순서·카드 스타일·문구 반영, 볼륨 값 라벨 표시 (기계 검증: 빌드+테스트 + 항목 개수 전후 대조(테마 제외) / 시각 ⏳ HUMAN-VERIFY)
  - **Files**:
    - 주: `src/DeskTube/Views/SettingsPage.xaml`
    - 동반: `src/DeskTube/ViewModels/SettingsViewModel.cs` (콤보 옵션 문구가 resw 경유이므로 코드 변경 최소 — 필요 시 표시 속성만), `Strings/*/Resources.resw` (카드 헤더·설명 시안 원문화, 그룹 헤더 키 신설)
  - **Edge Cases**:
    - IsReady 이전 일괄 비활성(기존 ContentControl 래퍼) 유지
    - AutoStart 상태 InfoBar(시안에 없는 기존 기능 안내) 유지 — 기능 삭제 아님
    - 자동 일시정지 expander 접힘/펼침 상태 셰브론 동기화
  - **Halt Forecast**: 없음 — 그룹 구성·카드 순서·문구는 시각 요소 분해 표에서 사전 확정, 소비하는 신규 컨트롤(T4)·스타일(T2)은 선행 task 산출물, 파괴적·의존성·외부 요소 없음
  - **Depends on**: T2, T4

- [x] T8. 정보 화면 재설계
  - **Type**: C
  - **Design**: 신규 심볼 없음 — 단 토큰 1종(`AppLargeIconCornerRadius`=12, DesignTokens.xaml) 추가(하드코딩 금지 규칙 이행 — spec 리뷰 MINOR 소급 정정). 카드 구조 재구성 + T2 스타일 소비
  - **Acceptance**: Given 정보 화면, Then 앱 카드(아이콘 52·이름·"버전 {v} · 개발자: {dev}" 한 줄)·개인정보 카드·라이선스 카드형 expander가 시안 스펙으로 표시되고 라이선스 전문 펼침·버전 조회 기능 불변 (기계 검증: 빌드+테스트 / 시각 ⏳ HUMAN-VERIFY)
  - **Files**:
    - 주: `src/DeskTube/Views/AboutPage.xaml`
    - 동반: `src/DeskTube/ViewModels/AboutViewModel.cs` (버전·개발자 한 줄 표시 속성 — 기존 AppVersion 재사용), `Strings/*/Resources.resw` (버전 포맷 문구)
  - **Edge Cases**:
    - 라이선스 0개·로드 실패 시 기존 폴백 유지
    - 긴 라이선스 전문 펼침 성능(기존 접힘 기본 유지)
  - **Halt Forecast**: 없음 — 단일 페이지 XAML 재구성 + 기존 AppVersion 재사용, 요소·문구는 시각 요소 분해 표에서 사전 확정, 파괴적·의존성·외부 요소 없음
  - **Depends on**: T2

## 사전 승인 항목 (일괄 승인 대상)
- T1 — AGENTS.md 디자인 규칙 섹션 개정 (개정 취지는 T1 Acceptance에 명시. plan 승인 = 개정 승인)
- T2 — `src/DeskTube/Resources/` 디렉터리·`DesignTokens.xaml` 신규 생성, App.xaml 리소스 병합·SystemAccentColor 재정의·`RequestedTheme="Dark"` 지정 + **테마 기능 제거**: `Services/ThemeHelper.cs`·`Models/AppTheme.cs` 파일 삭제, `AppSettings.Theme` 필드 제거, SettingsViewModel 테마 멤버·SettingsPage ThemeCard·resw 테마 5키(ko/en) 제거 (사용자 지시에 따른 계획된 기능 삭제 — 사용처는 Investigation Log grep 전수로 확정)
- T3 — MainWindow 셸 구조 변경 (NavigationView 구성 변경·`NavigateToPlaylists` internal 메서드 신설·`App.Main` internal 접근자 신설·Mica 제거)
- T4 — `MonitorChoice` 파일 분리(`ViewModels/MonitorChoice.cs`) + `MonitorPanelViewModel`·`Controls/MonitorCardsControl` 신설, SettingsViewModel 모니터 로직 위임 (구조 변경 — 호출부 2곳 grep 확정)
- T5 — HomeViewModel 공개 멤버 추가, PlaylistsViewModel `SelectPlaylist` 헬퍼 추가 (계획된 시그니처 확장)
- T6 — resw 키 3개 제거(ColRankLabel·ColInfoLabel·ColListenLabel — D6 헤더 행 제거에 따른 미사용화)
- 각 task의 로컬 작업 브랜치 checkpoint/완료 commit (implement-task 규약 위임 범위)

## 불가피한 Halt (위임 불가)
- push·main 병합·태그·릴리즈·PR — 구현·검증 완료 후 최종 보고에서 별도 승인
- plan에 근거 없는 돌발 결정(시안·분해 표가 정의하지 않은 새 UX가 필요해지는 경우)

## Verification Strategy
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` → 경고 0·오류 0
- 단위 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -p:Platform=x64` → 전건 통과 (현행 100개)
- 토큰 검증: DesignTokens.xaml에 D3 표 전 키 존재 + View의 색 하드코딩 잔존 0 (`#[0-9A-Fa-f]{6}` grep — Assets·시안 사본 제외) + 테마 심볼 잔존 0 (`ThemeHelper|AppTheme|Theme_` grep)
- 시각 검증: `## 시각 요소 분해` 표 대조 (implement-task V-9) — 빌드로 판정 불가한 레이아웃·색 표시는 ⏳ HUMAN-VERIFY 목록으로 보고
- 수동 검증 (사용자): 시스템 라이트 모드에서도 앱 다크 고정 확인, 4페이지 왕복, 트레이 진입, 재생 중 pill, 모니터 토글 동기(홈↔설정), 칩 이동

## Phase Ledger

## Retry Ledger

## Progress Log
- T1-T2 완료 (커밋 43c70fb, 15cc5ff): T1 AGENTS.md 디자인 규칙 개정(AGENTS.md는 gitignore — 디스크 반영, plan·PRD 변경 동반 커밋). T2 다크 고정 전환 + DesignTokens.xaml 신설(D3 Color 35키+Brush, HC 사전, 공용 Style 5종) + SystemAccentColor 코럴 재정의. 빌드 경고0·테스트 100/100.
  - 결정: DesignTokens는 D3 키명 Color 리소스 + 대응 Brush 이중 구조 (spec 리뷰 B1 — acceptance grep이 D3 키명을 대조). 그라디언트 브러시 키는 AppMonitorSelectedBackgroundBrush·AppThumbPlaceholderBackgroundBrush.
  - 결정: 다크 accent 면(AccentFillColorDefault)은 SystemAccentColorLight2를 소비 → Light2=#F25C54(원색)로 재정의.
- T3-T4 완료 (커밋 e8fdb5c, 이후 T4 완료 커밋): T3 셸 재설계(타이틀바 44·사이드바 220·설정 일반 항목·정보 footer·콘텐츠 카드·Mica 제거·NavigateToPlaylists/App.Main 신설). T4 모니터 카드 공용화(MonitorChoice 분리·MonitorPanelViewModel·MonitorCardsControl Large/Compact, SettingsViewModel 위임, SettingsExpander→카드 교체). 빌드 0경고·테스트 100/100.
  - 결정: MonitorPanel 구독은 Attach 멱등(-=/+=) + Detach 대칭 (Loaded/Unloaded 반복에 구독 1개 유지). NoticeCleared 이벤트로 유효 선택 시 안내 자동 닫힘 보존.
  - 결정: 카드 hover 테두리는 PointerEntered/Exited로 구현 (x:Bind는 IsSelected 변경 때만 재평가).
  - 결정: 변형 DP는 CardSize 대신 IsLarge(bool) — plan Design 소급 정정.
- T5-T6 완료 (커밋 3d8f3bb, 이후 T6 완료 커밋): T5 홈 재설계(pill·모니터 대형 카드·빠른 재생 칩·하단 정렬 MinHeight 기법·SelectPlaylist pending). T6 플레이리스트 재설계(좌 300px·점선 버튼·활성 인디케이터 IsActive·컬럼 헤더 제거 D6·썸네일 64×40·hover 코럴 3쌍·Col* 3키 제거). 빌드 0경고·테스트 100/100.
  - 결정: 칩 클릭은 View(OnChipClick)가 App.Main 경유 — VM 커맨드 대신 (순수 화면 이동).
  - 결정: 활성 표시는 PlaylistEntry.IsActive + x:Bind 정적 함수(ActiveNameBrush/ActiveWeight) — 상하 인셋은 컨테이너 Padding 0,12.
  - 결정: hover 핸들러는 IsEnabled 가드 필수 (PointerEntered는 비활성에도 발생).
- T7-T8 완료: T7 설정 재설계(그룹 4개·순서 재배열·볼륨 값 라벨·콤보 문구 원문화·틴트 배너·SettingsCard lightweight — 램프 스타일 복원). T8 정보 재설계(앱 카드 한 줄 정보 About_InfoLineFormat·개인정보 카드·라이선스 Expander 카드 — AboutDeveloper·About_VersionFormat 키 정리). 빌드 0경고·테스트 100/100.
  - 결정: 텍스트는 항상 램프 스타일 + 크기·굵기·색 오버라이드 (AGENTS 규칙 4 — T7 리뷰 교훈, 잔존 패턴은 Deferred [SUGGEST]).

## Next Steps
- plan 승인 후 `pjc:implement-task`로 T1부터 자율 실행

## Open Questions
- [x] Q1: 라이트 테마 처리 → ~~시안 톤 번안~~ **사용자 후속 지시(2026-07-16)로 대체: 테마 변경 기능 삭제, 다크 고정** (FR-17 REMOVED, D3 개정)
- [x] Q2: AGENTS.md 디자인 규칙 1 충돌 → **규칙 개정 — 시안 기준** (T1)
- [x] Q3: 창 제어 버튼 → **시스템 캡션 버튼 유지** (Out of Scope에 자체 구현 명시)
- [x] Q4: 시안의 "앱 시작 후 자동 재생" 토글 → **이번 제외** (Deferred — 기능 신설은 PRD 합의 필요)
