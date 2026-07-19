# Plan: 언어 설정 3종 개선 (문구·빠른재생 로컬라이즈·재시작)

**PRD**: docs/prd.md (NFR-4 다국어 · FR-22 단일 인스턴스 상호작용)
**Plan Location**: docs/plans/ (날짜별 누적)
**브랜치**: 신규 작업 브랜치에서 진행 (main 직접 금지)

## 요구 이해

> 원문: "1. 언어 목록에서 '시스템 언어 따람' 문구를 '시스템 언어' 변경  2. 홈에서 재생을 하면 플레이리스트에서 '빠른 재생' 리스트가 생성 되는데 앱 언어를 영문으로 전환 하면 영문으로 표시 되도록 수정  3. 앱 언어를 변경하면 일부만 변경 되고 앱을 다시 실행해야 모두 변경 되는데 확인해서 수정, 언어 변경시 앱을 다시 실행 할 수 있으면 그렇게 수정"

이해한 요구:
1. 설정 언어 드롭다운 첫 항목 문구 "시스템 언어 따름" → "시스템 언어" (오타 '따람'=따름). 사용자 확정으로 영문 "Follow system language" → "System language"도 통일.
2. 홈 즉시재생이 만드는 "빠른 재생" 플레이리스트는 생성 시점 이름이 영속 저장돼 언어를 영문으로 바꿔도 한글로 고정. 앱 언어에 맞춰 표시 이름이 바뀌도록 수정.
3. 현재 언어 변경은 트레이·셸만 재생성해 일부만 반영되고 전체 반영엔 수동 재실행 필요. 사용자 선호대로 언어 변경 시 앱을 즉시 재시작해 전체 반영. 단일 인스턴스(FR-22) 게이트와 경합하지 않게 처리.

## Goal

언어 설정의 3개 결함/개선을 처리한다: (1) 시스템 기본 옵션 문구 정리(ko/en), (2) 빠른 재생 리스트 이름의 언어 동기화, (3) 언어 변경 시 앱 즉시 재시작으로 전체 UI 반영.

## Investigation Log

- **언어 적용 경로**: `App` 생성자 `ApplySavedStartupOverrides()`가 settings.json의 `Language`를 `ApplicationLanguages.PrimaryLanguageOverride`로 선적용(App.xaml.cs:50-74) → `x:Uid`·`Loc.Get` 문구가 요소 생성 시점 언어로 고정. 확인: App.xaml.cs:25-27, Loc.cs:16-28.
- **현재 언어 변경 동작**: `SettingsViewModel.OnLanguageIndexChanged`(SettingsViewModel.cs:366-383)가 ① Settings.Language 저장(`Apply` = **fire-and-forget**, cs:420-440) ② `PrimaryLanguageOverride` 설정 ③ `App.ApplyLanguageChange()` 호출. `ApplyLanguageChange`(App.xaml.cs:242-257)는 `Loc.Reset()` + 트레이 재생성 + MainWindow 재생성만 함 → 이미 만들어진 다른 리소스·상태는 안 바뀌어 "일부만 반영". 호출부는 이 1곳뿐(grep 확인).
- **빠른 재생 이름 영속**: 홈 즉시재생 `HomeViewModel.PlayAsync`가 `Loc.Get("Home_QuickPlaylistName")`로 리스트 생성(HomeViewModel.cs:227-238) 후 `Settings.QuickPlaylistId`에 안정 ID 저장(cs:257). 리스트는 안정 ID로 식별되며(App.xaml.cs:200-203, HomeViewModel.cs:225) **표시는 `playlist.Name`(저장값)** 을 읽음(RefreshChips cs:190-193, 플레이리스트 목록). 저장된 Name은 언어 전환에도 불변 → 한글 고정.
  - resw 값: `Home_QuickPlaylistName` ko="빠른 재생"(ko-KR:96-97) / en="Quick play"(en-US:96-98).
- **리스트 이름 변경 API**: `PlaylistLibrary.Rename(Guid, string)`(PlaylistLibrary.cs:51-66) + `SaveAsync()`(cs:32). 라이브러리 로드는 `AppServices.CreateAsync`(AppServices.cs:51-58, `library.InitializeAsync`).
- **재시작 API 실재 확인**: `Microsoft.Windows.AppLifecycle.AppInstance.Restart(System.String)` — WinAppSDK foundation 2.3.5 winmd·projection xml에서 확인("Restarts the application instance", 반환=재시작 요청 상태 `AppRestartFailureReason`). **static** 메서드(MS 공식 문서). 패키지(MSIX) 앱 대상.
- **FR-22 단일 인스턴스 게이트**: `Program.Main`(Program.cs:24-40) → `TryRedirectToExistingInstance`가 `FindOrRegisterForKey("main")`(cs:55). 재시작 시 새 프로세스가 이 게이트를 통과 → 기존 키 미해제면 소멸 중 프로세스로 리다이렉트할 위험. `ExitApplication`(App.xaml.cs:260-280)은 종료 전 `UnregisterKey()` 호출(D8 패턴, cs:269) — 재시작에도 동일 적용 필요.
- **재시작 전 저장 완료 필수**: `Apply`는 `_ = RunAsync()` fire-and-forget(cs:427). 즉시 재시작하면 `SaveSettingsAsync`가 미완료 상태로 프로세스 종료 → 재시작 앱이 **옛 언어** 로드. 따라서 T3는 저장을 `await`로 완료한 뒤 재시작해야 함.
- **문서 영향**: `LanguageCard.Description` ko="언어를 바꾸면 설정 창이 다시 열립니다"(ko-KR:402-403)/en="Changing the language reopens the settings window"(en-US:402-403), help.md:149 "바꾸면 설정 창이 다시 열립니다" — 모두 T3로 부정확해져 갱신 대상.
- **위키 참조**: vault 미설정 — 코드 1차 출처로 진행.
- **Deferred 대장**: 이번 작업과 관련 항목 없음(언어 관련 미처리 없음).

## PRD Coverage

| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| NFR-4 (UI 다국어: 언어 전환 후 문구 확인) | Must급 NFR | T1, T2, T3 | ✅ 커버 (문구 정리·빠른재생 로컬라이즈·전환 시 전체 반영) |
| FR-22 (단일 인스턴스) | Must | T3 (회귀 방지) | ✅ 커버 (재시작이 게이트와 정합 — UnregisterKey, 재실행 리다이렉트 유지 검증) |
| FR-1·8·14·16·18·19·20·21 등 그 외 active Must | Must | — | 이번 범위 외 (기구현/후속) |

> 소규모 연결 plan — 이번에 닿는 NFR-4·FR-22만 커버 대상. Phase G는 이 둘만 재대조(나머지 기구현 FR 재구현 강요 아님).

## Tasks

## Task Status
- [x] T1 — 언어 목록 문구 통일 (Type A)
- [x] T2 — "빠른 재생" 리스트 이름 언어 동기화 (Type C)
- [x] T3 — 언어 변경 시 앱 즉시 재시작 (Type D)
- [x] T4 — 문서 갱신 (Type A)

### T1 — 언어 목록 문구 통일 (Type A)

- ko `Language_System`: "시스템 언어 따름" → "시스템 언어" (Strings/ko-KR/Resources.resw:405-407)
- en `Language_System`: "Follow system language" → "System language" (Strings/en-US/Resources.resw:405-407)
- **Files**: src/DeskTube/Strings/ko-KR/Resources.resw, src/DeskTube/Strings/en-US/Resources.resw
- **Acceptance**: 두 resw의 `Language_System` 값이 각각 "시스템 언어"/"System language"로 변경됨(값 대조). 드롭다운 첫 항목 표시는 빌드 후 ⏳ HUMAN-VERIFY.
- **의존**: 없음.

### T2 — "빠른 재생" 리스트 이름 언어 동기화 (Type C)

앱 시작 시 `QuickPlaylistId`가 가리키는 리스트의 이름을 현재 언어의 `Loc.Get("Home_QuickPlaylistName")`로 맞춘다(다를 때만 Rename+Save). 언어 변경은 T3로 앱을 재시작하므로, 다음 기동의 이 동기화가 새 언어 이름을 반영한다.

**M1 해소(테스트 가능한 배치 확정)**: 결정 로직(찾기·비교·개명)은 `App`(런타임 없이는 인스턴스화 불가)에 두지 않고, 테스트 가능한 `PlaylistLibrary` 메서드로 분리한다. `App`은 IO(`Loc.Get`·`SaveAsync`)만 오케스트레이션.

- **Design**:
  - ① 배치: 순수 로직은 `PlaylistLibrary.SyncQuickPlaylistName(Guid? quickId, string desiredName) : bool`(PlaylistLibrary.cs — 기존 `Rename`과 동일 계층, 단위테스트 대상). 오케스트레이션은 `App.SyncQuickPlaylistName()`(App.xaml.cs, `Loc`·`Services` 사용처와 동일 계층, `TryAutoPlayLastAsync` 관례).
  - ② 신규 심볼(2개):
    - `PlaylistLibrary.SyncQuickPlaylistName(Guid? quickId, string desiredName) : bool` — quickId가 null이거나 리스트 부재거나 이름이 이미 같으면 `false`(무변경), 다르면 `Rename` 후 `true`. 순수(IO 없음) → 단위테스트 대상.
    - `App.SyncQuickPlaylistName()`(void, 내부) — `Library.SyncQuickPlaylistName(Settings.QuickPlaylistId, Loc.Get("Home_QuickPlaylistName"))`가 `true`면 `Library.SaveAsync()`를 fire-and-forget(자동재생 흐름 비차단, 실패는 로그).
  - ③ 의존: Library 메서드는 `_playlists`/`Rename` 재사용. App 메서드는 `Services.Settings.QuickPlaylistId`·`Services.Library`·`Loc.Get`. 호출: `InitializeServicesAsync`에서 `Services` 생성 직후·자동재생(`TryAutoPlayLastAsync`) 이전.
  - ④ 비추상화: 언어별 이름 매핑 테이블·전략 패턴 도입 안 함 — 단일 `Loc.Get` + 안정 ID 식별로 충분.
- **Files**: src/DeskTube/Services/PlaylistLibrary.cs, src/DeskTube/App.xaml.cs (신규 심볼 2개 — Library 메서드 호출부는 App 1곳), tests/DeskTube.Tests/ (T2 단위테스트)
- **Acceptance**:
  1. 영문 전환(→재시작) 후 홈 빠른재생 칩·플레이리스트 목록의 해당 리스트 이름이 "Quick play"로 표시(⏳ HUMAN-VERIFY 화면). (표시 3지점 QuickChip·PlaylistEntry 스냅샷·SelectedPlaylist 바인딩이 모두 `playlist.Name`을 읽으므로 시작 시 모델 rename이 전 지점 커버 — plan-reviewer 확인.)
  2. `SyncQuickPlaylistName`은 quickId null·리스트 부재·이름 일치 시 `false`(무동작·무저장), 상이 시 rename 후 `true`(단위테스트로 검증).
  3. 이름이 이미 현재 언어 이름과 같으면 SaveAsync 미호출(불필요 저장 방지).
- **Edge Cases**: QuickPlaylistId=null / 리스트 삭제됨 / 이름 이미 일치 / Library.Playlists 비어 있음 / SaveAsync 실패(로그만, 앱은 계속).
- **Halt Forecast**: 없음(모두 사전 해소 — 무동작/로그 폴백).
- **Decisions**:
  - 개명 정책: 안정 ID로 식별되는 **앱 관리 리스트**라 항상 언어에 맞춰 동기화한다(사용자 수동 개명은 비지원 시나리오 — 홈 재생마다 내용이 통째로 교체되는 리스트). Source: HomeViewModel.cs:223-257, App.xaml.cs:195-208.
- **의존**: 없음(T3와 독립 — T3 없이 매 기동 동기화도 성립).

### T3 — 언어 변경 시 앱 즉시 재시작 (Type D)

언어 선택 즉시 앱을 재시작해 전체 UI를 새 언어로 반영한다. 기존 셸 재생성(`ApplyLanguageChange`)을 재시작으로 대체한다.

- **SettingsViewModel.OnLanguageIndexChanged**(cs:366-383): ① `Settings.Language = code` ② `PrimaryLanguageOverride` 설정 **제거 확정**(m1) — 재시작이 `ApplySavedStartupOverrides`(App.xaml.cs:67)로 새 언어를 재적용하므로 불필요. ③ `SaveSettingsAsync`를 **await 완료**(fire-and-forget 금지) ④ `App.RestartForLanguageChange()` 호출. 기존 `app.ApplyLanguageChange()` 호출 제거.
- **App**: `ApplyLanguageChange`(cs:242-257)를 `RestartForLanguageChange()`로 교체. 재시작 경로: `UnregisterKey()`(FR-22 게이트 정합, 실패는 로그만) → `_tray?.Dispose()` + `Services?.Dispose()`(배경 복구, ExitApplication 패턴) → `AppInstance.Restart("")`. 반환 `AppRestartFailureReason`가 실패면 로그 남기고 계속(무한 대기 금지).
- **resw**: `LanguageCard.Description` ko "언어를 바꾸면 설정 창이 다시 열립니다"→"언어를 바꾸면 앱이 다시 시작됩니다"(ko-KR:402-403), en "Changing the language reopens the settings window"→"Changing the language restarts the app"(en-US:402-403).
- **Design**:
  - ① 배치: `App.RestartForLanguageChange()`(App.xaml.cs, `ApplyLanguageChange` 대체), `SettingsViewModel.OnLanguageIndexChanged` 수정.
  - ② 신규 심볼: `App.RestartForLanguageChange()` — 안전 정리(UnregisterKey·Dispose) 후 `AppInstance.Restart` 호출. 언어 저장 완료는 호출 측(ViewModel)에서 await 보장.
  - ③ 의존: `AppLifecycleInstance.Restart`(static), `AppLifecycleInstance.GetCurrent().UnregisterKey`, `_tray`/`Services` Dispose. 호출: `SettingsViewModel.OnLanguageIndexChanged` 1곳(유일).
  - ④ 비추상화: 재시작 추상 서비스/인터페이스 도입 안 함 — 단일 사용처라 App 직접 호출.
- **Files**: src/DeskTube/ViewModels/SettingsViewModel.cs, src/DeskTube/App.xaml.cs, src/DeskTube/Strings/ko-KR/Resources.resw, src/DeskTube/Strings/en-US/Resources.resw
- **Acceptance**:
  1. 언어 변경 시 프로세스가 종료 후 새로 떠서 트레이 메뉴·모든 화면 문구가 새 언어로 표시(⏳ HUMAN-VERIFY — 실기 재시작).
  2. 재시작 후 settings.json `Language`가 선택값(저장 await 완료로 보장).
  3. 재시작 후 FR-22 재실행 리다이렉트 정상(두 번째 실행이 기존 창 활성화) — 회귀 없음(⏳ HUMAN-VERIFY, MSIX 배포 필요).
  4. `LanguageCard.Description`가 재시작 안내 문구로 표시.
- **Edge Cases**: `AppInstance.Restart` 실패(AppRestartFailureReason≠None) → 로그 후 계속(앱 유지, 사용자 수동 재시작 폴백) / `Services`·`_tray` null(초기화 실패) → null 가드 후 Restart / 재생 중 변경 → 재생 중단(자동재생 설정 켬이면 재시작 후 재개, FR-8/19 경로).
- **Halt Forecast**: `AppInstance.Restart` 실패 → (i) 사전 해소: 로그 남기고 계속(폴백), 위임 불가 아님. 재시작 자체는 파괴적 외부 작업이 아님(앱 자기 재기동, 데이터 저장 완료 후).
- **의존**: T1(resw 같은 파일 편집 — 순차 권장, 충돌 회피).

### T4 — 문서 갱신 (Type A)

- help.md:149: "시스템 언어 따름 / 한국어 / English. 바꾸면 설정 창이 다시 열립니다." → "시스템 언어 / 한국어 / English. 바꾸면 앱이 다시 시작됩니다."
- README.md: **변경 불필요 확정(m2)** — grep 결과 README는 언어 변경 메커니즘("설정 창이 다시 열립니다"·재시작 등)을 서술하지 않음(line 28 "다국어: 한국어/English + 시스템 언어 추종"은 기능 서술, line 56 "언어 선적용"은 정확). T4 Files에서 제외.
- **Files**: help.md
- **Acceptance**: help.md 언어 항목이 새 문구(시스템 언어)·재시작 동작을 반영.
- **의존**: T1·T3 문구 확정 후.

## 4-D. 재사용 확인

| 신규 심볼 | 유사 기존 구현 검색 | 재사용/신규 사유 |
|-----------|--------------------|------------------|
| `PlaylistLibrary.SyncQuickPlaylistName` (T2) | 동기화 로직 grep — 없음. `Rename`/`Find` 존재 | 신규(순수 결정 로직, 테스트 대상). 실제 개명은 기존 `Rename` 재사용 |
| `App.SyncQuickPlaylistName` (T2) | — | 신규(IO 오케스트레이션 진입점). 저장은 기존 `SaveAsync` 재사용 |
| `App.RestartForLanguageChange` (T3) | `ApplyLanguageChange`(교체 대상), `ExitApplication`의 UnregisterKey·Dispose | 기존 `ApplyLanguageChange` 대체. 정리 로직은 `ExitApplication` 패턴 재사용 |

## Verification Strategy

- `dotnet build DeskTube.slnx -p:Platform=x64` — 경고 0·오류 0.
- `dotnet test DeskTube.slnx -p:Platform=x64` — 기존 143 + T2 신규 단위테스트 통과.
  - T2 테스트 대상 = `PlaylistLibrary.SyncQuickPlaylistName`(순수, 테스트 가능): ① 다른 언어 이름 → rename 후 true / ② quickId null → false·무변경 / ③ 리스트 부재 → false / ④ 이미 일치 → false·무변경. (App 오케스트레이션은 런타임 의존이라 ⏳ HUMAN-VERIFY.)
- ⏳ HUMAN-VERIFY: 언어 전환 시 실제 재시작·전체 UI 반영, 빠른재생 이름 영문 표시, FR-22 재실행 리다이렉트 회귀 없음(MSIX 배포 필요).

## Deferred / Follow-up

- [SUGGEST] fire-and-forget "discard + async 로컬함수 + 실패 로그" 패턴이 3곳(PlaybackCoordinator.FireAndForget, SettingsViewModel.Apply, App.SyncQuickPlaylistName)에 유사 중복 — 공용 유틸 승격 검토(현재 계층이 달라 즉시 강제는 아님, T2 quality m1).
- [SUGGEST] App.RestartForLanguageChange와 App.ExitApplication이 "UnregisterKey → tray/Services Dispose → Exit" 정리 블록을 중복 보유 — 공용 `CleanupForShutdown()` 헬퍼 추출 검토(두 경로 정리 로직 어긋남 방지, T3 quality S1).

## Out of Scope

- 언어 종류 추가(현재 ko/en/system 유지).
- 재시작 없는 완전 라이브 언어 전환(사용자가 재시작 방식 선호로 확정).

## 사전 승인 항목 (일괄 승인 대상)

- 신규 작업 브랜치 생성 및 로컬 작업 브랜치 commit(체크포인트·task 완료).
- resw·App·SettingsViewModel·help.md 수정(위 task 범위 내).

## 불가피한 Halt (위임 불가)

- push / main 병합 / PR / 태그·릴리즈 — 구현·검증 완료 후 별도 승인.

## Open Questions

- [x] 문구 변경 범위(en 통일 여부) → **ko/en 모두 통일**("시스템 언어"/"System language").
- [x] 재시작 UX → **즉시 재시작**(확인 다이얼로그·지연 없음).

## Progress Log
- T1 완료 (e8e4e6e): resw Language_System ko/en 문구 통일. Type A.
- T2 완료 (ceddaff): PlaylistLibrary.SyncQuickPlaylistName(순수) + App 오케스트레이션, 시작 시 빠른재생 이름 언어 동기화. 테스트 4건. quality M1(fire-and-forget 예외안전) 수정. 147/147.
  - 결정: 개명은 안정 ID 식별 앱관리 리스트라 항상 동기화(수동개명 비지원). App fire-and-forget SaveAsync는 try/catch로 예외 안전.
- T3-T4 완료 (f39ac6f, 1e21bee): 언어 변경 시 AppInstance.Restart로 앱 재시작(저장 await 완료 후, UnregisterKey로 FR-22 정합, 실패 시 Exit). resw Description·help.md 문구 갱신. MainWindow 고아코드(ForceClose) 제거. quality M1~M3 수정. 147/147.
  - 결정: 재시작 실패(드묾) 시 degraded 유지 대신 Exit()로 확실히 종료. PrimaryLanguageOverride는 재시작이 재적용하므로 제거.

## Phase Ledger

- (implement-task가 갱신)
