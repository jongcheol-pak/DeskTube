# Plan: DeskTube UI·통합 (part2/2)

**PRD**: docs/prd.md
**이전 plan**: docs/plans/2026-07-15-desktube-core-part1.md

## 이전 part 핸드오프
- 함정: 존재하지 않는 타입을 소스에 참조하면 XAML 컴파일러가 CS 오류 대신 WMC9999(내부 NRE)로 죽는다. 경고 확인은 반드시 `--no-incremental` 전체 재빌드로 (증분 빌드가 미변경 프로젝트 경고를 숨김).
- 함정: 테스트는 `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -c Debug -p:Platform=x64` — 플랫폼 플래그 없으면 MSIX 타깃 오류. 앱 csproj의 `WindowsAppSdkAutoInitialize=false`는 테스트 호스트 호환용이므로 제거 금지 (App 생성자가 명시 초기화).
- 기각된 접근: `[LibraryImport]`+델리게이트(unsafe 요구 → `DllImport` 사용), `CoreWebView2InitializationException` 타입(미존재 — 런타임 부재는 HResult 0x80070002로 판별).
- 검증 지름길: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64 --no-incremental && dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -c Debug -p:Platform=x64 && dotnet format DeskTube.slnx --verify-no-changes`
- 배선 참조: 서비스 소비는 `App.Services`(AppServices — 수동 컴포지션 루트, D13). 트레이·UI는 `Coordinator`(StartAsync/Pause/Resume/SetVolumeAsync 등)와 `PowerPolicy.Reevaluate()`(설정 토글 후)를 호출하면 된다.

## 요구 이해
- **원문 요청**: "설정 화면 기능 — 모니터 선택, 유튜브 url 입력, 플레이 리스트 생성 관리 … 윈도우 부팅 후 자동 실행(트레이 아이콘으로 실행) … 앱 정보 화면, 오픈소스 라이선스 화면, 화질 설정 / 트레이 아이콘 — 재생, 정지, 종료, 볼륨 on/off 메뉴" (+ 후속: 유튜브 로그인으로 프리미엄 광고 제거)
- **이해한 요구**: part1의 코어 엔진 위에 사용자 대면 기능을 완성한다 — 트레이 아이콘, 설정 화면(모니터·URL·볼륨·오디오 대상·재생 모드·화질·자동 실행), 플레이리스트 관리 UI, 부팅 자동 시작, 유튜브 로그인(Should), 앱 정보·오픈소스 라이선스 화면, 한/영 다국어, Store 제출 가능 상태(WACK) 검증. 상세 요구는 docs/prd.md가 정본.
- **포함하지 않는 것으로 이해**: Store 실제 제출(사용자 수행), 유튜브 탐색 UI.

## Goal
트레이·설정 UI·자동 시작·로그인·정보 화면·다국어를 완성하고 WACK·메모리 목표를 검증해 Store 제출 가능한 앱으로 만든다.
**전체 목표**: 유튜브 영상을 바탕화면 배경으로 재생하는 Store 배포 가능한 WinUI 3 앱 (part1: 코어 엔진 — 완료 전제).

## PRD Coverage
| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-8 | Must | T4 | ✅ 커버 |
| FR-9 | Must | T1 | ✅ 커버 |
| FR-10 | Must | T2(설정 항목), T3(플레이리스트 UI) | ✅ 커버 |
| FR-11 | Must | T6 | ✅ 커버 |
| FR-12 | Must | T6 | ✅ 커버 |
| FR-13 | Must | T2 (설정 UI·안내 문구 — 스케일 로직은 part1 T6 기구현) | ✅ 커버 (part1과 합산 완성) |
| FR-15 | Should | T5 | ✅ 커버 |
| FR-1~7, FR-14 | Must | (part1 기구현) | ✅ 이전 part 기구현 |
| NFR-1 | — | (part1 기구현) | ✅ 이전 part 기구현 |
| NFR-2 | — | T8 (실측) | ✅ 커버 |
| NFR-3 | — | T8 (실측) | ✅ 커버 |
| NFR-4 | — | T7 | ✅ 커버 |
| NFR-5 | — | T8 (WACK) | ✅ 커버 |
| NFR-6 | — | T5(쿠키 로컬), T6(처리방침 문서) | ✅ 커버 |

## Out of Scope
- Microsoft Store 실제 제출·심사 대응 (사용자 수행)
- part1 Out of Scope 전부 동일 (스트림 추출·광고 차단·모니터별 독립 재생·Win10)

## Deferred / Follow-up
- Store 심사 피드백 대응·identity 교체 후 재패키징 — 제출 시점에 별도 진행
- (part1에서 이관되는 미처리 Deferred가 생기면 implement-task가 여기 반영)

## Investigation Log
- part1 Investigation Log의 검증 사실을 전제로 함 (IFrame API·프리미엄 임베드·WebView2 autoplay·24H2·StartupTask·H.NotifyIcon·WinAppSDK 2.2 — 전부 웹 공식 출처 확인 완료, 2026-07-15)
- 구글 로그인 차단 리스크: WebView2에서 "이 브라우저 또는 앱은 안전하지 않을 수 있습니다" 차단 사례 다수 (WebView2Feedback #1584·#2552) — FR-15가 Should인 근거, T5 실패 경로 설계 근거
- MSIX StartupTask: 패키지형 데스크톱 앱은 `RequestEnableAsync` 시 동의 대화상자 없음, manifest `uap5:Extension` 필요 — MS Learn 확인
- H.NotifyIcon.WinUI: WinUI 3 패키지형 지원, MIT — nuget.org·GitHub 확인

## Risks & Unknowns
| 위험 | 영향 | 완화책 |
|---|---|---|
| 구글이 WebView2 로그인 차단 | FR-15 동작 불가 (Should라 출시는 가능) | 차단 감지 시 안내 InfoBar("구글 정책으로 로그인 불가") + 나머지 기능 정상 동작 (PRD FR-15 명시) |
| 트레이 아이콘이 셸 재시작 후 소실 | 제어 수단 상실 | H.NotifyIcon의 TaskbarCreated 재생성 지원 확인·활성화 |
| WACK 실패 항목 발생 | Store 제출 불가 | T8에서 실행 → 실패 항목별 수정 후 재실행 (동일 이슈 3회 실패 시 보고) |
| 메모리 목표(대기 150MB) 미달 | NFR-2 미충족 | 정지 시 WebView2 완전 해제(part1 설계) 실측 확인, 초과 시 원인 프로파일링 후 보고 |

## Impact Analysis
### 4-A. 심볼/타입 추적 결과
- part1 산출물(서비스 공개 API) 소비가 중심 — part1의 `PlaybackCoordinator`·`MonitorService`·`PlaylistLibrary`·`AppSettings` 공개 계약을 변경하지 않고 사용한다. 계약 변경이 필요해지면 돌발 결정으로 Halt (불가피한 Halt 참조).

### 4-B. 계약·직렬화 변경
- `AppSettings`에 UI 전용 필드 추가 가능(자동 시작 여부·언어 등) — part1의 `schemaVersion` 예약으로 하위 호환(새 필드 기본값 로드).

### 4-C. 테스트 파일
- 신규: `tests/DeskTube.Tests/StartupArgsTests.cs`, `tests/DeskTube.Tests/LicenseInventoryTests.cs` (그 외 task는 UI 중심 — HUMAN-VERIFY)

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| 트레이 아이콘 | 자작 P/Invoke 가능 | **H.NotifyIcon.WinUI 재사용** (part1 4-D와 동일 결정) |
| 설정 카드 | 자작 스타일 | **SettingsCard/SettingsExpander 재사용** (AGENTS 디자인 규칙) |
| 화면 셸 | 자작 탭 | **NavigationView 표준 컨트롤 재사용** |
| LoginWindow·StartupService·라이선스 페이지 등 | part1·레포 내 유사 구현 없음 | **신규 작성** (앱 고유) |

### Verified by
- part1 plan의 Files 목록 대조 — 위 신규 심볼과 중복 없음 (이 세션)

## Decisions
### D1. 설정 셸 구조
- **Options**: A) 단일 페이지 스크롤 / B) `NavigationView`(좌측: 홈/플레이리스트/설정/정보)
- **Chosen**: B
- **Rationale**: 화면 4계열(FR-10~12)을 표준 패턴으로 수용, AGENTS 디자인 규칙 1(Gallery 구조).
- **Source**: AGENTS.md 디자인 규칙

### D2. 트레이 메뉴 구성·창 닫기 동작
- **Options**: A) 닫기=종료 / B) 닫기=트레이로 최소화(재생 유지), 종료는 트레이 메뉴에서만
- **Chosen**: B — 메뉴: 재생 / 정지 / 볼륨 켬·끔(체크) / 설정 열기 / 종료 (PRD 4개 + 설정 열기 편의 항목)
- **Rationale**: 배경화면 앱은 상시 실행이 기본(FR-9 "창 닫아도 트레이 유지" 명시). "설정 열기"는 트레이 시작 시 UI 진입 수단으로 필수.
- **Source**: PRD FR-9

### D3. 자동 시작 판별
- **Options**: A) StartupTask 활성 여부만 / B) 시작 인자(`-startup`)로 트레이 모드 판별 + StartupTask는 설정 토글과 동기화
- **Chosen**: B — StartupTask manifest의 `Parameters`가 아닌 활성화 API 상태 조회 + `AppInstance.GetActivatedEventArgs`로 시작 종류 판별, 불가 시 명령줄 인자 폴백
- **Rationale**: 트레이 조용한 시작(FR-8)은 "어떻게 시작됐는지"를 알아야 함.
- **Source**: MS Learn StartupTask (Investigation Log)

### D4. 로그인 창 방식
- **Options**: A) 설정 페이지 내 임베드 / B) 별도 `LoginWindow`(WebView2, youtube.com 탐색) + 로그인 감지(쿠키 존재) 후 자동 닫기
- **Chosen**: B — 동일 UDF(part1 D9) 공유, 로그아웃=CookieManager 쿠키 전체 삭제 + 플레이어 리로드
- **Rationale**: 구글 로그인 흐름은 팝업·리다이렉트가 있어 전용 창이 안전. 쿠키 삭제는 공식 API로 확정적.
- **Source**: WebView2 CookieManager 공식 API, part1 D9

### D5. 오픈소스 라이선스 화면 데이터
- **Options**: A) 런타임 NuGet 메타데이터 수집 / B) 빌드 시 고정 목록(`Assets/licenses/` 텍스트 + 인덱스 JSON)을 수동 관리 + 테스트로 "참조 패키지 ⊆ 목록" 검증
- **Chosen**: B
- **Rationale**: 패키지 수가 적고(6±), 런타임 수집은 과설계. 누락은 테스트(LicenseInventoryTests — csproj PackageReference 대조)로 차단.
- **Source**: PRD FR-12 검증 방법

### D6. 다국어 적용 방식
- **Options**: A) 마지막에 일괄 리소스화(T7) / B) T1~T6에서 문구 하드코딩 금지(x:Uid/Loc.Get 즉시 적용) + T7은 en/ko 리소스 완성·검증만
- **Chosen**: B
- **Rationale**: AGENTS 다국어 규칙 1(하드코딩 금지)이 이미 강제 — 마지막 일괄 치환은 재작업.
- **Source**: AGENTS.md 다국어 규칙

### D7. 개인정보처리방침 문서
- **Options**: A) 외부 호스팅만 / B) `docs/privacy-policy.md`(한/영)로 저장소에 포함, 정보 화면에서 링크·요지 표시 (Store 제출 시 URL은 사용자가 호스팅)
- **Chosen**: B
- **Rationale**: NFR-6 검증 가능 형태. 호스팅 URL 확보는 Store 제출 절차(사용자 몫).
- **Source**: PRD NFR-6

### D8. UI 문구 톤
- **Options**: A) 기술 용어 그대로 / B) 일반 사용자 언어 (예: "WorkerW 부착 실패" ❌ → "배경화면을 표시할 수 없습니다" ✅, 상세는 로그)
- **Chosen**: B
- **Rationale**: Store 일반 사용자 대상.
- **Source**: decision-points UI 동작(문구 톤) 규정

### D9. 보안·자격증명 (T5 로그인)
- 시드/기본 계정: **없음** (사용자 본인 구글 계정만, 앱은 자격증명을 다루지 않음 — 입력은 구글 페이지에서만 발생)
- 자격증명 보관: **앱이 저장하지 않음** — 세션 쿠키만 WebView2 프로필(OS 사용자 폴더)에 남고 외부 전송 없음. 코드·문서·로그에 계정 정보 기록 금지 (AGENTS DO NOT)
- 인증 실패 정책: 구글 측 소관 (앱은 실패 안내만) — 앱 자체 잠금 로직 불필요
- **Source**: PRD NFR-6, AGENTS.md 보안 규칙

## Tasks

- [x] T1. 트레이 아이콘 (FR-9)
  - **Type**: C
  - **Design**: ① `src/DeskTube/Services/TrayIconService.cs` + 아이콘 리소스(`Assets/tray.ico`) ② H.NotifyIcon.WinUI `TaskbarIcon`을 App 수명으로 소유 — 메뉴 5항목(D2), 더블클릭=설정 창 열기 ③ PlaybackCoordinator(재생/정지/음소거)·MainWindow(열기/닫기=숨김) 호출, App.xaml.cs에서 초기화 ④ 이번에 안 함: 풍선 알림, 동적 아이콘(재생 상태 배지)
  - **Acceptance**: Given 앱 실행, When 트레이 메뉴에서 재생/정지/볼륨 켬·끔/종료 각각 실행, Then 대응 동작 수행 + 설정 창 X 클릭 시 앱이 종료되지 않고 트레이 유지 — HUMAN-VERIFY; 빌드·기존 테스트 통과
  - **Files**:
    - 주: `src/DeskTube/Services/TrayIconService.cs`
    - 동반: `src/DeskTube/App.xaml.cs`, `src/DeskTube/MainWindow.xaml.cs`(닫기→숨김), `src/DeskTube/Assets/tray.ico`, (구현 중 추가) `MainWindow.xaml`(안내 InfoBar), `DeskTube.csproj`, `Services/Loc.cs`, `Strings/en-US·ko-KR/Resources.resw` — D6 하드코딩 금지에 따른 선행 생성
  - **Edge Cases**:
    - 재생할 리스트가 없는 상태에서 "재생" → 설정 창 열고 안내 (조용한 무시 금지)
    - Explorer 재시작 → TaskbarCreated 재등록(H.NotifyIcon 기능 활성)
    - 종료 메뉴 → 배경창 정리·상태 저장 후 종료 (part1 T5 원상복구 경로 사용)
  - **Halt Forecast**:
    - (ii-a) NuGet 의존성 추가(H.NotifyIcon.WinUI) → `## 사전 승인 항목`
  - **Depends on**: - (part1 완료 전제)

- [x] T2. 설정 셸 + 재생 설정 페이지 (FR-10·FR-13 UI)
  - **Type**: D
  - **Design**: ① `Views/` `ViewModels/`: `MainWindow`(NavigationView 셸), `HomePage`+`HomeViewModel`(URL 입력·즉시 재생), `SettingsPage`+`SettingsViewModel` ② SettingsPage 카드: 모니터 선택(다중 체크)·오디오 출력 모니터(콤보)·볼륨 슬라이더·재생 모드·화질 스케일(콤보 + 유튜브 자동 결정 안내 InfoBar)·자동 실행 토글·자동 일시정지 정책 3토글·언어 ③ ViewModel → part1 서비스(DI) 호출, View는 x:Bind만 ④ 이번에 안 함: 설정 검색, 테마 수동 전환(시스템 추종 — AGENTS 규칙 3), **자동 실행 토글 카드(T4에서 StartupService와 함께)·언어 카드(T7에서 전환 절차와 함께) — 백엔드 없는 비기능 토글 노출 방지 (T2 리뷰 합의, T4·T7 Files에 반영)**
  - **Acceptance**: Given 설정 화면, When 각 항목 변경, Then 즉시 적용(볼륨·모니터는 재생 중 반영)되고 앱 재시작 후 유지 — HUMAN-VERIFY; ViewModel 로직은 빌드+x:Bind 컴파일 검증
  - **Files**:
    - 주: `src/DeskTube/MainWindow.xaml(.cs)`, `src/DeskTube/Views/SettingsPage.xaml(.cs)`, `src/DeskTube/ViewModels/SettingsViewModel.cs`
    - 동반: `src/DeskTube/Views/HomePage.xaml(.cs)`, `src/DeskTube/ViewModels/HomeViewModel.cs`, `src/DeskTube/App.xaml.cs`(DI)
  - **Edge Cases**:
    - 잘못된 URL 입력 → 파서 Result 실패 시 InfoBar 안내(D8 톤), 입력 유지
    - 모니터 전부 해제 시도 → 최소 1개 강제(마지막 체크 해제 차단 + 안내)
    - 재생 중 오디오 대상 변경 → 즉시 mute 대상 전환(part1 T7 API)
  - **Halt Forecast**:
    - (i) "설정 UI 컨트롤 선택?" → AGENTS 규칙 7(SettingsCard) + D1로 확정
    - (ii-a) NuGet 의존성 추가(CommunityToolkit.WinUI.Controls.SettingsControls) → `## 사전 승인 항목`
  - **Depends on**: T1

- [ ] T3. 플레이리스트 관리 페이지 (FR-6 UI)
  - **Type**: D
  - **Design**: ① `Views/PlaylistsPage.xaml(.cs)` + `ViewModels/PlaylistsViewModel.cs` ② 좌측 리스트 목록(생성·이름변경·삭제) + 우측 항목 목록(URL 추가·삭제·위/아래 이동·드래그 정렬) + "이 리스트 재생" 버튼, 상한 도달 시 추가 버튼 비활성+안내 ③ PlaylistLibrary(part1 T2)·PlaybackCoordinator 호출 ④ 이번에 안 함: 항목 메타데이터(제목·썸네일) 자동 조회 — 유튜브 Data API 키가 필요해 Out(URL 텍스트 표시만), 가져오기/내보내기
  - **Acceptance**: Given 플레이리스트 화면, When 리스트 생성→URL 3개 추가→순서 변경→재생, Then 배경 재생이 그 순서로 시작되고 재시작 후 상태 유지 — HUMAN-VERIFY; CRUD·상한은 part1 테스트가 커버
  - **Files**:
    - 주: `src/DeskTube/Views/PlaylistsPage.xaml(.cs)`, `src/DeskTube/ViewModels/PlaylistsViewModel.cs`
    - 동반: `src/DeskTube/MainWindow.xaml`(내비 항목)
  - **Edge Cases**:
    - 재생 중인 리스트 삭제 → 재생 정지 후 삭제 (확인 대화상자)
    - 재생 중인 리스트에 항목 추가/삭제 → 큐 갱신(현재 곡 유지, 삭제된 현재 곡이면 다음 곡)
    - 1000개 근접 대량 목록 → ListView 가상화 확인(ItemsRepeater/ListView 기본 가상화 유지, 바인딩 컬렉션은 증분 로드 불필요 — 텍스트만)
  - **Halt Forecast**:
    - (i) "항목 표시 형식?" → Design ④ 확정(URL 텍스트 + 사용자 지정 별칭 없음)
  - **Depends on**: T2

- [ ] T4. 부팅 자동 시작 (FR-8)
  - **Type**: C
  - **Design**: ① `Services/StartupService.cs` + `Package.appxmanifest`(uap5 StartupTask, Enabled=false 기본) ② `IsEnabledAsync/SetEnabledAsync`(StartupTask API 래핑) + 시작 종류 판별(D3) — 자동 시작이면 MainWindow 미표시·트레이만·마지막 상태 자동 재생(PRD Q8) ③ SettingsViewModel(토글)·App.xaml.cs(시작 분기) 소비 ④ 이번에 안 함: 시작 지연 옵션
  - **Acceptance**: Given 자동 실행 토글 on + 재부팅(또는 StartupTask 시뮬레이션 실행), When 로그인, Then 창 없이 트레이 상주 + 마지막 재생 설정으로 배경 재생 시작 — HUMAN-VERIFY; 시작 인자→모드 판별 로직은 xUnit
  - **Files**:
    - 주: `src/DeskTube/Services/StartupService.cs`, `src/DeskTube/Package.appxmanifest`
    - 동반: `src/DeskTube/App.xaml.cs`, `src/DeskTube/Views/SettingsPage.xaml`, `src/DeskTube/ViewModels/SettingsViewModel.cs` (자동 실행 토글 카드 — T2에서 이연)
    - 테스트: `tests/DeskTube.Tests/StartupArgsTests.cs`
  - **Edge Cases**:
    - 사용자가 Windows 설정에서 시작 앱을 직접 꺼 둠(StartupTaskState=DisabledByUser) → 토글에 "시스템 설정에서 꺼짐" 상태 표시, RequestEnableAsync는 재요청 불가 안내
    - 자동 시작 시 마지막 리스트가 삭제됨 → 재생 생략, 트레이만 상주
    - 자동 시작 시 네트워크 미연결 → part1 T6 재시도 경로 소비
  - **Halt Forecast**:
    - (i) "StartupTask 표시명 리소스 형식?" → AGENTS 다국어 규칙 4 확정(`ms-resource:///Resources/<키>`)
  - **Depends on**: T2

- [ ] T5. 유튜브 로그인 (FR-15)
  - **Type**: C
  - **Design**: ① `Views/LoginWindow.xaml(.cs)` + `Services/YouTubeSessionService.cs` ② 세션 서비스 — 로그인 상태 확인(youtube.com 쿠키 SAPISID 존재 여부), 로그인 창 열기, 로그아웃(쿠키 전체 삭제+플레이어 리로드), 상태 변경 이벤트 ③ part1 `WebViewEnvironment`(동일 UDF) 사용, SettingsPage에 로그인 카드(상태 표시+버튼) 추가 ④ 이번에 안 함: 계정 프로필 표시(이름·사진), 다중 계정
  - **Acceptance**: Given 프리미엄 계정, When 로그인 창에서 로그인 완료, Then 설정에 "로그인됨" 표시 + 이후 배경 재생에서 광고 미표시(HUMAN-VERIFY — 구글 차단 시: 차단 안내 표시 + 나머지 기능 정상이면 통과, PRD FR-15 명시); 로그아웃 시 쿠키 삭제 확인
  - **Files**:
    - 주: `src/DeskTube/Services/YouTubeSessionService.cs`, `src/DeskTube/Views/LoginWindow.xaml(.cs)`
    - 동반: `src/DeskTube/Views/SettingsPage.xaml`(로그인 카드), `src/DeskTube/ViewModels/SettingsViewModel.cs`
  - **Edge Cases**:
    - 구글 차단("안전하지 않은 브라우저") → 감지 불가한 외부 페이지 상태이므로 로그인 완료 미감지로 귀결 — 창에 안내 문구 상시 표시("로그인이 안 되는 경우 구글 정책 때문일 수 있습니다"), 앱 기능 영향 없음
    - 로그인 도중 창 닫기 → 상태 변화 없음(재시도 가능)
    - 세션 만료 → 상태 확인 시 "로그아웃됨"으로 자동 반영
  - **Halt Forecast**:
    - (i) "로그인 성공 판별?" → Design ② 확정(쿠키 존재 확인)
    - (i) "구글 차단 시 처리?" → PRD FR-15·Risks에 확정(안내+정상 동작)
    - ※ 신규 외부 서비스 인증정보 도입 아님 — 사용자 본인 계정을 구글 페이지에서 직접 입력(앱은 미보관, D9)
  - **Depends on**: T2

- [ ] T6. 앱 정보 + 오픈소스 라이선스 화면 (FR-11·12, NFR-6 문서)
  - **Type**: C
  - **Design**: ① `Views/AboutPage.xaml(.cs)` + `ViewModels/AboutViewModel.cs` + `Assets/licenses/`(패키지별 라이선스 전문 텍스트 + `index.json`) + `docs/privacy-policy.md` ② AboutPage — 앱 이름·버전(Package.Current에서 조회)·개발자·개인정보처리방침 요지, 라이선스 목록(Expander로 전문 표시) ③ NavigationView 정보 항목에서 진입 ④ 이번에 안 함: 업데이트 확인 기능
  - **Acceptance**: Given 정보 화면, When 열람, Then 버전이 manifest와 일치·모든 참조 패키지의 라이선스 전문 표시 — HUMAN-VERIFY; `LicenseInventoryTests`(csproj PackageReference 집합 ⊆ index.json 집합) xUnit 통과
  - **Files**:
    - 주: `src/DeskTube/Views/AboutPage.xaml(.cs)`, `src/DeskTube/Assets/licenses/index.json`
    - 동반: `src/DeskTube/ViewModels/AboutViewModel.cs`, `docs/privacy-policy.md`, `src/DeskTube/MainWindow.xaml`(내비 항목)
    - 테스트: `tests/DeskTube.Tests/LicenseInventoryTests.cs`
  - **Edge Cases**:
    - 라이선스 파일 누락 → 테스트가 차단(빌드 게이트)
    - 긴 라이선스 전문 → 가상화된 스크롤 (Expander 접힘 기본)
  - **Halt Forecast**:
    - (i) "라이선스 목록 관리 방식?" → D5 확정
  - **Depends on**: T2

- [ ] T7. 다국어 리소스 완성 (NFR-4)
  - **Type**: C
  - **Design**: ① `Strings/en-US/Resources.resw`(중립 폴백) + `Strings/ko-KR/Resources.resw` + `Services/Loc.cs`(코드비하인드 조회 헬퍼) ② T1~T6에서 x:Uid/Loc.Get으로 이미 분리된 키를 en/ko 전수 등재, 언어 설정(시스템 추종 기본 + 수동 전환 콤보)은 AGENTS 다국어 규칙 3 절차로 구현 ③ 전 View·ViewModel이 소비 ④ 이번에 안 함: 제3 언어
  - **Acceptance**: Given 언어 전환(ko↔en), When 모든 화면 순회, Then 미번역(키 노출) 문구 0 + 전환 후 테마 유지(AGENTS 규칙 3-⑤) — HUMAN-VERIFY; 하드코딩 검색(grep: XAML `Text="[가-힣A-Za-z]`·code `"..."` UI 문자열) 0건
  - **Files**:
    - 주: `src/DeskTube/Strings/en-US/Resources.resw`, `src/DeskTube/Strings/ko-KR/Resources.resw`
    - 동반: `src/DeskTube/Services/Loc.cs`, `src/DeskTube/Views/SettingsPage.xaml`, `src/DeskTube/ViewModels/SettingsViewModel.cs` (언어 카드 — T2에서 이연), (T1~T6의 View/ViewModel — 키 등재 확인)
  - **Edge Cases**:
    - 누락 키 → Loc.Get이 키 자체 반환(누락 가시화 — AGENTS 규칙 2)
    - OS 언어가 ko/en 외 → en-US 폴백(DefaultLanguage)
  - **Halt Forecast**:
    - (i) "언어 전환 UI?" → T2 Design ②(설정 카드)에 포함, 전환 절차는 AGENTS 규칙 3
  - **Depends on**: T1~T6

- [ ] T8. 최종 통합 검증 + 문서 (NFR-2·3·5)
  - **Type**: C
  - **Design**: ① `README.md`(개요·기능·실행·아키텍처), WACK 실행·결과 기록, 메모리·콜드 스타트 실측 기록(`docs/verification-2026-07.md`) ② 검증 절차 — Release x64 빌드 + MSIX 패키징 → WACK 실행 → 실패 항목 수정 루프, 작업 관리자 워킹셋 측정(재생 2모니터/정지 대기) ③ 신규 심볼 없음 — 문서·검증 중심(코드 수정은 WACK 실패 대응 한정) ④ 이번에 안 함: Store 업로드
  - **Acceptance**: Given Release 패키지, When WACK 실행, Then 통과(실패 0) + 대기 워킹셋 실측치 기록(150MB 이하 목표 — 초과 시 원인 분석 보고) + 콜드 스타트 3초 이내 실측 + README가 실제 기능과 일치
  - **Files**:
    - 주: `README.md`, `docs/verification-2026-07.md`
    - 동반: (WACK 실패 시 해당 파일 수정 — 범위는 실패 항목 한정)
  - **Edge Cases**:
    - WACK 도구 부재(SDK 미설치) → 설치 안내 후 사용자 확인 요청 (Verification Strategy 대안)
    - 메모리 목표 초과 → 근본 원인 분석(프로세스별 분해) 후 보고 — 무리한 임시방편 금지
  - **Halt Forecast**:
    - (i) "WACK 실행 방법?" → Verification Strategy에 명령 명시
    - (ii-b) WACK 도구 부재(Windows SDK 미설치) → 시스템에 SDK 설치가 필요한 상황은 사용자 확인 대상 — `## 불가피한 Halt`에 명시
    - (ii-b) WACK 실패 수정이 아키텍처 변경을 요구하는 경우 → 돌발 결정으로 Halt
  - **Depends on**: T1~T7

## 사전 승인 항목 (일괄 승인 대상)
- T1 — NuGet 의존성 추가: `H.NotifyIcon.WinUI`
- T2 — NuGet 의존성 추가: `CommunityToolkit.WinUI.Controls.SettingsControls`
- T4 — `Package.appxmanifest` 구조 변경(uap5 StartupTask 확장 추가)
- 전 task — 로컬 작업 브랜치 커밋(implement-task 규약, push 아님)

## 불가피한 Halt (위임 불가)
- git push·원격 저장소 연결·태그·릴리즈·PR — 항상 별도 승인
- Microsoft Store 제출·identity 교체 — 사용자 직접 수행
- part1 공개 계약(서비스 API·JSON 스키마)의 파괴적 변경이 필요해지는 돌발 상황
- T8 — WACK 도구 부재 시(Windows SDK 미설치): 설치는 시스템 변경이므로 사용자 안내 후 확인 대기

## Known Workarounds (있는 경우만)
- 구글 로그인 차단은 앱에서 근본 해결 불가(구글 정책) → 안내 UI로 대응, FR-15는 Should (PRD 합의)
- Store identity는 임시(`DeskTube.Dev`) 유지 → 제출 시 교체 (README에 절차 기록)

## Verification Strategy
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` / Release: `-c Release`
- 단위 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj`
- 하드코딩 문구 검사(T7): grep XAML/C# 사용자 노출 문자열 → 0건
- WACK: `appcert.exe reset` 후 `appcert.exe test -appxpackagepath <msix> -reportoutputpath <xml>` (또는 WACK GUI) — 도구 부재 시 사용자 안내 후 HUMAN-VERIFY 전환
- 수동 검증 누적 목록(HUMAN-VERIFY): 트레이 메뉴 4동작(T1), 설정 항목 반영(T2), 플레이리스트 조작→재생(T3), 부팅 자동 시작(T4), 로그인→광고 미표시(T5), 정보·라이선스 화면(T6), 언어 전환(T7)

## Phase Ledger

## Retry Ledger

## Progress Log
- T1-T2 완료: 트레이 아이콘(H.NotifyIcon) + NavigationView 셸·홈(URL 즉시 재생)·설정 페이지. 빌드 경고 0 / 테스트 75/75 / format 0. 각 task spec+quality 이중 리뷰 OK.
  - 결정: H.NotifyIcon.WinUI는 2.4.1이 net10 전용이라 **2.3.2**(net8 호환 최신) 사용.
  - 결정: 자동 실행 토글 카드→T4, 언어 카드→T7로 이연 (백엔드 없는 비기능 토글 노출 방지 — plan Design ④·T4/T7 Files에 반영, spec 리뷰 합의).
  - 결정: 재생 중 모니터 선택 반영 위해 PlaybackCoordinator에 additive 공개 메서드 `ApplySelectedMonitorsAsync()` 추가 (기존 private 경로 위임 — 파괴적 계약 변경 아님).
  - 결정: 홈 "즉시 재생"은 "빠른 재생" 이름의 플레이리스트를 만들어 항목 교체 후 StartAsync (part1 계약 무변경). 서비스 오류 문구는 사용자에게 노출하지 않고 리소스 문구+로그로 분리 (T1 리뷰 교훈).
  - 리뷰 이의: quality 리뷰의 "PlayAsync 재진입 미가드" MAJOR는 AsyncRelayCommand 기본값(동시 실행 차단, 패키지 XML 문서)으로 반증 → 리뷰어 철회.
  - 함정(위키): x:Uid 정적 텍스트 언어는 App 생성자 이전 동기 선읽기 필요(T7 참고), 부팅 직후 COM 미준비로 AppInstance API는 try/catch+인자 폴백(T4 참고).

## Next Steps
- part1(docs/plans/2026-07-15-desktube-core-part1.md) 완료 후 이 plan을 `pjc:implement-task`로 실행 (경로 명시 호출)

## Open Questions
- [x] part1 Open Questions와 동일 — 전부 해소 (docs/prd.md 결정 기록 + part1 Q10·Q11)
