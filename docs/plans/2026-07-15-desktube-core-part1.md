# Plan: DeskTube 코어 엔진 (part1/2)

**PRD**: docs/prd.md
**다음 plan**: docs/plans/2026-07-15-desktube-ui-part2.md

## 요구 이해
- **원문 요청**: "winui 3으로 윈도우 스토어 앱 개발 — 윈도우 바탕화면에 동영상 배경화면 설정 기능 앱. 동영상은 유투브 url로 재생 … 배경화면으로 동영상을 설정하기 때문에 앱 최적화, 메모리 최적화 필수" (+ 후속: "유튜브 로그인 기능을 추가해서 … 유료 사용자는 광고가 안나오도록")
- **이해한 요구**: 유튜브 URL 영상을 바탕화면 아이콘 뒤에 재생하는 WinUI 3 앱을 만들어 Microsoft Store에 낼 수 있는 상태로 완성한다. 멀티 모니터 선택(다중 시 동일 영상 동기, 소리는 지정 1개만), 플레이리스트(100×1000), 재생 모드, 부팅 자동 실행+트레이, 화질 간접 제어, 앱 정보/라이선스 화면, 프리미엄 광고 제거용 유튜브 로그인(Should)을 포함한다. 상세 요구는 승인된 PRD(docs/prd.md)가 정본.
- **포함하지 않는 것으로 이해**: 광고 차단·스트림 추출 등 ToS 위반 기법, 유튜브 탐색/검색 UI, 모니터별 다른 영상.

## Goal
배경화면 재생의 코어 엔진(데이터·재생 로직·WorkerW 배경창·WebView2 플레이어·오케스트레이션·자동 일시정지)을 빌드·테스트 가능한 상태로 완성한다.
**전체 목표**: 유튜브 영상을 바탕화면 배경으로 재생하는 Store 배포 가능한 WinUI 3 앱 (part2에서 UI·통합 완성).

## PRD Coverage
| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-1 | Must | T3(URL 파싱), T6(재생), T7(통합) | ✅ 커버 |
| FR-2 | Must | T5 | ✅ 커버 |
| FR-3 | Must | T4(열거·선택), T7(다중 동기) | ✅ 커버 |
| FR-4 | Must | T7 | ✅ 커버 |
| FR-5 | Must | T2(저장), T6(적용) | ✅ 커버 |
| FR-6 | Must | T2 (CRUD·상한 로직 — 관리 UI는 part2 T3) | ✅ 커버 |
| FR-7 | Must | T3 | ✅ 커버 |
| FR-13 | Must | T6 (해상도 스케일 로직 — 설정 UI·안내 문구는 part2 T2) | ⏭️ 다음 part에서 완성 |
| FR-14 | Must | T2 | ✅ 커버 |
| FR-8 | Must | (part2 담당) | ⏭️ 다음 part |
| FR-9 | Must | (part2 담당) | ⏭️ 다음 part |
| FR-10 | Must | (part2 담당) | ⏭️ 다음 part |
| FR-11 | Must | (part2 담당) | ⏭️ 다음 part |
| FR-12 | Must | (part2 담당) | ⏭️ 다음 part |
| FR-15 | Should | (part2 담당) | ⏭️ 다음 part |
| NFR-1 | — | T8 | ✅ 커버 |
| NFR-2 | — | T6·T7 설계 반영, 측정은 part2 T8 | ⏭️ 다음 part에서 측정 |
| NFR-3~6 | — | (part2 담당) | ⏭️ 다음 part |

## Out of Scope
- 스트림 추출(yt-dlp 등)·광고 차단 (YouTube ToS 위반 — PRD Out of Scope)
- 모니터별 독립 플레이리스트, 로컬 파일 재생, Windows 10 지원 (PRD 결정 Q4·Q6)

## Deferred / Follow-up
- **다음 분할 plan**: docs/plans/2026-07-15-desktube-ui-part2.md — T1~T8 (전체의 후반부: 트레이·설정 UI·플레이리스트 UI·자동 시작·로그인·정보 화면·다국어·최종 검증, 미실행)
- Microsoft Store 실제 제출(계정·심사)은 앱 완성 후 사용자가 진행 (plan 범위 밖)
- AGENTS.md Test 명령에 `-p:Platform=x64` 추가 제안 — T1에서 확인: 플래그 없으면 앱 의존성이 AnyCPU로 빌드돼 MSIX 타깃 오류 (record-project-fact로 사용자 승인 후 갱신; 이 plan의 Verification Strategy에는 반영 완료)

## Investigation Log
- 폴더 상태: Glob `*` → docs/prd.md, AGENTS.md, docs/plans/만 존재. 기존 코드·기존 plan·git 저장소 없음 (신규 프로젝트 확정)
- AGENTS.md: 이 세션에서 pjc:bootstrap-agents-md로 생성·사용자 승인됨 (계층형/서비스 중심, xUnit, docs/plans/ 누적)
- Deferred 대장(docs/plans/deferred.md): 없음 — 통과
- 위키 참조: vault 미설정(경로 미확인) — 건너뜀, 웹 공식 문서를 1차 출처로 진행
- YouTube IFrame API: `setPlaybackQuality`는 no-op(화질 강제 불가), `onPlaybackQualityChange` 이벤트만 유효 — 공식 문서 확인 (developers.google.com/youtube/iframe_api_reference). FR-13이 "간접 제어"로 확정된 근거
- YouTube Premium: 로그인된 프리미엄 사용자는 임베드에서도 광고 미표시 — 공식 도움말 확인 (support.google.com/youtube/answer/132596)
- WebView2 자동재생: `CoreWebView2EnvironmentOptions.AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required"` (환경 생성 시점에만 적용 가능) — WebView2Feedback #1598, #2364 확인
- Windows 11 24H2: WorkerW 계층 변경으로 기존 기법 깨짐, Lively가 코어 재작성으로 대응 (탐색 로직에 신·구 두 경로 필요) — lively #2415, discussions #2464 확인
- MSIX StartupTask: `uap5:Extension Category="windows.startupTask"` + `StartupTask.GetAsync().RequestEnableAsync()`, 패키지형 데스크톱 앱은 동의 대화상자 없음 — MS Learn StartupTask Class 확인
- H.NotifyIcon.WinUI 2.4.1: 2025-12 갱신, 활발히 유지 (MIT) — nuget.org 확인
- Windows App SDK 안정 버전 2.2.0 — MS Learn downloads 확인

## Risks & Unknowns
| 위험 | 영향 | 완화책 |
|---|---|---|
| Windows 업데이트로 WorkerW 구조 재변경 | 배경 표시 실패 | 탐색 로직을 전략 2개(신/구)로 분리 + 실패 시 사용자 안내 InfoBar, 버전 감지 로그 |
| WebView2 자동재생 플래그가 일부 환경에서 미작동 (알려진 이슈) | 첫 재생이 멈춘 상태로 시작 | JS 측에서 mute 상태로 우선 재생 시작 후 볼륨 복원 (muted autoplay는 항상 허용) |
| 임베드 금지 영상(소유자 설정) | 해당 항목 재생 불가 | IFrame `onError`(101/150) 수신 → 다음 곡 자동 스킵 + 오류 기록 |
| WebView2 메모리 사용량이 목표 초과 | NFR-2 미달 | 단일 environment/프로필 공유(브라우저 프로세스 공유), 정지 시 WebView2 Close(해제), part2 T8에서 실측 |
| Explorer 재시작(크래시) 시 WorkerW 핸들 무효화 | 배경창 소실 | Explorer 프로세스/셸 창 재생성 감지 → 배경창 재부착 로직 (T5) |

## Impact Analysis
### 4-A. 심볼/타입 추적 결과
- 해당 없음 — 신규 프로젝트(기존 코드 0). 변경/영향 받는 기존 심볼 없음.

### 4-B. 계약·직렬화 변경
- 신규 직렬화 계약: `settings.json`·`playlists.json` (System.Text.Json). 최초 도입이므로 마이그레이션 없음. 이후 변경 대비 `schemaVersion` 필드 포함 (D5).

### 4-C. 테스트 파일
- 신규: `tests/DeskTube.Tests/` — T2·T3·T4·T7·T8의 로직 테스트 (아래 각 task Files에 명시)

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| 트레이 아이콘 (part2) | 직접 P/Invoke(Shell_NotifyIcon) 자작 가능 | 자작 대신 **H.NotifyIcon.WinUI 재사용** (검증된 라이브러리, 테마·메뉴 지원) |
| 설정 카드 UI (part2) | 자작 스타일 가능 | **CommunityToolkit SettingsControls 재사용** (AGENTS 디자인 규칙 7) |
| MVVM 기반 | 자작 INotifyPropertyChanged | **CommunityToolkit.Mvvm 재사용** (AGENTS 컨벤션) |
| WorkerW/모니터 interop, 플레이어 브리지, 오케스트레이터 등 앱 고유 로직 | 레포 내 기존 구현 없음(빈 레포), 재사용 가능한 완성 NuGet 없음 | **신규 작성** (앱 핵심 도메인) |

### Verified by
- 빈 레포 확인: Glob 전체 → 코드 파일 0건 (이 세션)

## Decisions
### D1. 프로젝트 구조 생성 방법
- **Options**: A) `dotnet new winui`(서드파티 템플릿) / B) VS 템플릿 구조를 파일로 직접 작성 (slnx 플랫폼 매핑·MsixPackage 프로필 포함)
- **Chosen**: B
- **Rationale**: AGENTS.md가 A를 금지 (VS 기본 구조와 다름). 자율 루프는 CLI 기반이므로 VS 표준 구조를 손으로 재현하고 `dotnet build`로 검증.
- **Source**: AGENTS.md "프로젝트 생성/실행 필수 규칙"

### D2. 대상 프레임워크·패키지 버전
- **Options**: A) .NET 8 + WinAppSDK 2.2 / B) .NET 8 + WinAppSDK 1.8
- **Chosen**: A — TFM `net8.0-windows10.0.22621.0`, `TargetPlatformMinVersion 10.0.22000.0`(Win11)
- **Rationale**: 2.2가 현행 안정 버전. Win11 전용은 PRD 확정.
- **Source**: MS Learn downloads (Investigation Log), PRD Q6

### D3. WorkerW 부착 전략
- **Options**: A) 구 방식만(Progman에 0x052C 후 형제 WorkerW 탐색) / B) 신·구 이중 경로 (24H2+: Progman 자식 WorkerW / 이전: SHELLDLL_DefView 가진 WorkerW의 다음 형제)
- **Chosen**: B — `IWallpaperHostStrategy` 없이 단일 클래스 내 분기 (과한 추상화 방지), 종료 시 SetParent 해제 + `SystemParametersInfo(SPI_SETDESKWALLPAPER)` 새로고침으로 원상복구
- **Rationale**: 24H2 전후 모두 지원이 FR-2 명시 요구.
- **Source**: lively #2415·#2464 (Investigation Log)

### D4. 다중 모니터 동기 재생 방식
- **Options**: A) 각 플레이어 독립 재생(동기 없음) / B) 마스터-미러 (오디오 대상 플레이어가 마스터, 나머지는 load/play/pause/seek 명령 미러 + 5초 주기 drift 1초 초과 시 seekTo 보정)
- **Chosen**: B
- **Rationale**: "동일 영상 동기 재생"(PRD Q4). 독립 재생은 수 초 어긋남 누적.
- **Source**: PRD 결정 기록 Q4

### D5. 영속화 형식
- **Options**: A) SQLite / B) JSON 2파일 (`settings.json`, `playlists.json`) + `schemaVersion` 필드, System.Text.Json 소스 생성기
- **Chosen**: B
- **Rationale**: 최대 100×1000 항목은 JSON으로 충분(수 MB), DB 의존성 제거. 저장은 임시파일 쓰기→교체(원자적)로 손상 방지.
- **Source**: AGENTS.md 데이터 접근 (DB 없음)

### D6. 전체화면 감지 방식
- **Options**: A) `SHQueryUserNotificationState` 2초 폴링 (QUNS_BUSY/QUNS_RUNNING_D3D_FULL_SCREEN/QUNS_PRESENTATION_MODE → 전체화면) / B) WinEvent 훅(EVENT_SYSTEM_FOREGROUND) + 창 rect 비교
- **Chosen**: A (폴링 주기 2초, 상태 변화 시에만 이벤트 발행)
- **Rationale**: 공식 API 하나로 게임·프레젠테이션 모두 판정, 훅 대비 구현·수명 관리 단순. 2초 지연은 배경화면 용도로 허용.
- **Source**: Win32 공식 API (shell32), Lively 등 동일 접근 관행

### D7. 배터리 세이버·세션 잠금 감지
- **Options**: A) `Windows.System.Power.PowerManager.EnergySaverStatusChanged` + `WTSRegisterSessionNotification`(WM_WTSSESSION_CHANGE) / B) 폴링
- **Chosen**: A (이벤트 기반)
- **Rationale**: 공식 이벤트 존재, 폴링 불필요.
- **Source**: WinRT PowerManager 공식 API

### D8. 플레이어 ↔ C# 통신
- **Options**: A) 앱 내장 `player.html`(IFrame API 로드) + `window.chrome.webview.postMessage` 브리지 / B) youtube.com 페이지 직접 탐색 + JS 주입
- **Chosen**: A — 명령(JSON): load/play/pause/setVolume/mute/seek/quality-scale, 이벤트: ready/stateChange/error/time
- **Rationale**: 공식 IFrame API가 제어 계약을 보장, JS 주입은 유튜브 DOM 변경에 취약.
- **Source**: IFrame API 공식 문서 (Investigation Log)

### D9. WebView2 환경 구성
- **Options**: A) 플레이어마다 별도 environment / B) 단일 공유 environment + 단일 사용자 데이터 폴더(UDF: `ApplicationData…\WebView2`) + `--autoplay-policy=no-user-gesture-required`
- **Chosen**: B
- **Rationale**: 브라우저 프로세스 공유로 메모리 절약(NFR-2), 로그인 쿠키 공유(part2 FR-15) 전제 조건.
- **Source**: WebView2Feedback #1598·#2364 (Investigation Log)

### D10. 상태 관리·스레드
- **Options**: A) 서비스 싱글톤(DI) + UI 스레드 마셜링은 DispatcherQueue / B) 자유 접근
- **Chosen**: A — 재생 상태의 단일 소유자는 `PlaybackCoordinator`(싱글톤), 모니터/전원 이벤트는 코디네이터가 DispatcherQueue로 수신
- **Rationale**: WinUI 컨트롤은 UI 스레드 전용. 상태 소유자 단일화로 경합 제거.
- **Source**: WinUI 3 스레딩 모델(공식), AGENTS.md DI 컨벤션

### D11. 에러 처리·로깅
- **Options**: A) 예외 전파 / B) 서비스 경계는 `Result<T>`/이벤트, 백그라운드 루프는 catch 후 로그 + 복구 시도, 로그는 `ApplicationData…\logs\desktube-{날짜}.log` (개인정보·URL 토큰 미기록)
- **Chosen**: B
- **Rationale**: 상시 실행 앱은 백그라운드 예외 1회로 죽으면 안 됨. AGENTS 컨벤션(Result 권장).
- **Source**: AGENTS.md Conventions

### D12. 테스트 전략
- **Options**: A) UI 자동화 포함 / B) 로직만 xUnit (파서·큐·상한·오디오 대상 결정·일시정지 정책·저장 왕복), interop·UI·재생은 HUMAN-VERIFY
- **Chosen**: B
- **Rationale**: WorkerW·WebView2·트레이는 데스크톱 세션 의존이라 CI형 자동화 비용 과다. PRD 검증 방법 열과 일치.
- **Source**: PRD FR 표 검증 방법 열

## Tasks

- [x] T1. 솔루션 스캐폴딩 (WinUI 3 패키지형 + 테스트 프로젝트 + git 초기화)
  - **Type**: D
  - **Design**: ① 루트에 `DeskTube.slnx`(x64/x86/ARM64 매핑+Deploy), 앱은 `src/DeskTube/`, 테스트는 `tests/DeskTube.Tests/` ② 신규: App/MainWindow 뼈대, `Package.appxmanifest`(identity 임시, runFullTrust), `launchSettings.json`(MsixPackage) ③ 앱 → WindowsAppSDK 2.2·CommunityToolkit.Mvvm 참조, 테스트 → 앱 프로젝트 참조(단 WinUI 타입 제외한 로직만 대상) ④ 이번에 안 함: CI 파이프라인, 다국어 리소스(part2 T7), NavigationView 셸(part2 T2)
  - **Acceptance**: Given 빈 레포, When `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` 및 `dotnet test`, Then 경고/에러 0 + 빈 테스트 1개 통과 + git 저장소에 초기 커밋 존재
  - **Files**:
    - 주: `DeskTube.slnx`, `src/DeskTube/DeskTube.csproj`, `src/DeskTube/App.xaml(.cs)`, `src/DeskTube/MainWindow.xaml(.cs)`, `src/DeskTube/Package.appxmanifest`
    - 동반: `src/DeskTube/Properties/launchSettings.json`, `.gitignore`, `src/DeskTube/app.manifest`, `src/DeskTube/Assets/*.png` (manifest 필수 로고 5종 — 구현 중 추가, 리뷰 확인)
    - 테스트: `tests/DeskTube.Tests/DeskTube.Tests.csproj`, `tests/DeskTube.Tests/SmokeTests.cs`
  - **Edge Cases**:
    - 테스트 프로젝트가 WinUI 앱 참조 시 플랫폼 불일치 빌드 실패 → 테스트도 x64 고정 (`Platforms=x64`)
    - slnx 플랫폼 매핑 누락 → AGENTS 규칙 1로 사전 차단 (acceptance에 빌드 포함)
  - **Halt Forecast**:
    - (i) "패키지 identity를 뭘로?" → 임시 identity(`DeskTube.Dev`) 사용, Store identity는 제출 시 교체 (D2 참조·Known Workarounds)
    - (ii-a) NuGet 의존성 추가(WindowsAppSDK 2.2, CommunityToolkit.Mvvm, xUnit 계열) + git init·로컬 커밋 → `## 사전 승인 항목`
  - **Depends on**: -

- [x] T2. 모델 + JSON 영속화 서비스 (FR-5·6·14 로직)
  - **Type**: C
  - **Design**: ① `Models/`: `Playlist`, `PlaylistItem`, `AppSettings`, `PlaybackMode`(enum: Sequential/Shuffle/Random/RepeatOne/RepeatAll) ② `Services/`: `IStateStore` + `JsonStateStore` — 로드/저장(원자적 교체)·기본값 생성, `PlaylistLibrary` — CRUD + 상한(100/1000) 검증(Result 반환) ③ ViewModel(part2)·PlaybackCoordinator(T7)가 참조, 이들은 System.Text.Json에만 의존 ④ 이번에 안 함: 스키마 마이그레이터(schemaVersion 필드만 예약), 백업/복원 기능
  - **Acceptance**: Given 임의 플레이리스트 상태, When 저장 후 재로드, Then 왕복 일치; When 101번째 리스트/1001번째 항목 추가, Then Result 실패(상한 코드) 반환 — xUnit 테스트로 검증
  - **Files**:
    - 주: `src/DeskTube/Models/Playlist.cs`, `src/DeskTube/Models/AppSettings.cs`, `src/DeskTube/Services/JsonStateStore.cs`, `src/DeskTube/Services/PlaylistLibrary.cs`
    - 동반: `src/DeskTube/Models/PlaybackMode.cs`, `src/DeskTube/Models/Result.cs`(AGENTS Result 컨벤션 공용 타입), `src/DeskTube/Services/AppLog.cs`(D11 구현), `src/DeskTube/Services/IStateStore.cs`(DI 인터페이스 분리), `src/DeskTube/App.xaml.cs`+양쪽 csproj(WinAppSDK 자동 초기화 비활성 — 테스트 호스트 0x80040154 근본 해결, 구현 중 추가·리뷰 확인)
    - 테스트: `tests/DeskTube.Tests/JsonStateStoreTests.cs`, `tests/DeskTube.Tests/PlaylistLibraryTests.cs`
  - **Edge Cases**:
    - 손상된 JSON(파싱 실패) → 손상 파일을 `.bak`로 옮기고 기본값으로 시작 (조용한 데이터 소실 금지, 로그 기록)
    - 빈 플레이리스트 이름 → 검증 실패 Result; 중복 이름 허용(ID가 키)
    - 항목 0개 리스트 재생 요청 → 재생 시작 거부 Result (T7에서 소비)
    - 디스크 쓰기 실패(권한/풀) → 이전 파일 유지(원자적 교체), 오류 Result
  - **Halt Forecast**:
    - (i) "저장 파일 위치·이름?" → D5에서 확정
    - (i) "테스트에서 ApplicationData 접근 불가?" → IStateStore에 경로 주입 가능하게 설계(테스트는 임시 폴더) — Verification Strategy 참조
  - **Depends on**: T1

- [x] T3. 유튜브 URL 파서 + 재생 큐 (FR-1 파싱·FR-7)
  - **Type**: C
  - **Design**: ① `Services/YouTubeUrlParser.cs`(static) — watch?v=/youtu.be//shorts//embed/ → 11자 videoId 추출, 실패 시 Result 실패 ② `Services/PlaybackQueue.cs` — 현재 리스트+모드로 다음/이전 항목 결정(셔플=Fisher-Yates 1회 순회, 랜덤=중복 허용, RepeatOne=현재 유지, RepeatAll=끝→처음) ③ PlaybackCoordinator(T7)가 소비, 외부 의존 없음(순수 로직) ④ 이번에 안 함: 재생 이력 통계, URL 리다이렉트 해석(네트워크 접근 없음 — 형식 파싱만)
  - **Acceptance**: Given 대표 URL 형식 6종(+비유튜브·빈 문자열·11자 아님), When 파싱, Then 정상 6종 videoId 일치·비정상 3종 실패 Result; Given 3곡 리스트, When 각 모드로 next 5회, Then 모드별 기대 순서(셔플은 "전곡 1회 소진" 속성 검증) — xUnit
  - **Files**:
    - 주: `src/DeskTube/Services/YouTubeUrlParser.cs`, `src/DeskTube/Services/PlaybackQueue.cs`
    - 테스트: `tests/DeskTube.Tests/YouTubeUrlParserTests.cs`, `tests/DeskTube.Tests/PlaybackQueueTests.cs`
  - **Edge Cases**:
    - URL에 추가 쿼리(`&t=30s`, `&list=`) → videoId만 추출(재생목록 파라미터 무시 — 앱 플레이리스트가 정본)
    - 항목 1개 리스트 + 셔플 → 같은 곡 반복 재생 허용
    - Unicode/공백 포함 입력 → Trim 후 파싱, 실패 시 Result
  - **Halt Forecast**:
    - (i) "셔플과 랜덤의 차이 정의?" → PRD FR-7에 명시 완료
  - **Depends on**: T1

- [x] T4. 모니터 서비스 (FR-3 열거·식별)
  - **Type**: C
  - **Design**: ① `Interop/MonitorInterop.cs`(EnumDisplayMonitors·GetMonitorInfo P/Invoke) + `Services/MonitorService.cs` ② `IMonitorService` — 모니터 목록(안정 ID=디바이스명+위치 가독 조합 문자열 `{device}@{x},{y}` — 해시 대신 판독성 선택, 결정성 동일, 구현 시 확정), `MonitorsChanged` 이벤트(WM_DISPLAYCHANGE 수신 — 메시지 전용 창) ③ WallpaperHost(T5)·PlaybackCoordinator(T7)·설정 UI(part2)가 참조 ④ 이번에 안 함: DPI별 스케일 보정 로직(T5의 창 배치에서 물리 좌표 사용으로 충분), HDR 감지
  - **Acceptance**: Given 현재 시스템, When 모니터 열거, Then 연결된 모니터 수와 주 모니터 플래그가 실제와 일치(수동 1회 확인) + ID 생성·선택 상태 직렬화 로직은 xUnit(가짜 모니터 데이터)으로 검증
  - **Files**:
    - 주: `src/DeskTube/Services/MonitorService.cs`, `src/DeskTube/Interop/MonitorInterop.cs`
    - 테스트: `tests/DeskTube.Tests/MonitorIdTests.cs`
  - **Edge Cases**:
    - 재생 중 모니터 분리 → Changed 이벤트로 해당 플레이어 정리(T7 소비), 오디오 대상 모니터가 사라지면 주 모니터로 폴백
    - 저장된 선택 모니터가 부팅 시 부재 → 주 모니터 폴백 + 로그
    - 모니터 0개(원격 세션 특수 상황) → 재생 시작 거부
  - **Halt Forecast**:
    - (i) "모니터 안정 ID 규칙?" → Design ②에서 확정(디바이스명 기반)
  - **Depends on**: T1

- [ ] T5. WorkerW 배경창 호스트 (FR-2)
  - **Type**: D
  - **Design**: ① `Interop/WallpaperInterop.cs`(FindWindowEx·SendMessageTimeout(0x052C)·SetParent·EnumWindows P/Invoke) + `Services/IWallpaperHost.cs`(인터페이스 — T7 목킹 seam) + `Services/WallpaperHost.cs`(구현) ② `WallpaperHost : IWallpaperHost` — 모니터별 배경 창(WinUIEx 없이 순수 Win32 스타일 차용한 WinUI `Window`) 생성→WorkerW에 SetParent→모니터 rect로 배치·복구·해제 담당. 24H2 이중 경로는 D3 ③ PlaybackCoordinator(T7)는 `IWallpaperHost`만 참조해 생성/파괴 지시, 창 내부 콘텐츠는 T6의 PlayerView를 받음 ④ 이번에 안 함: 창 전환 애니메이션, 아이콘 숨김 대응(24H2 "아이콘 표시 필요" 제약은 Known Workarounds로 문서화만)
  - **Acceptance**: Given Windows 11(24H2 포함), When 배경창 생성, Then 임의 색 콘텐츠가 바탕화면 아이콘 뒤·창 뒤에 표시되고(HUMAN-VERIFY) 앱 종료 시 원래 배경화면 복구; 빌드·기존 테스트 통과
  - **Files**:
    - 주: `src/DeskTube/Services/WallpaperHost.cs`, `src/DeskTube/Interop/WallpaperInterop.cs`
    - 동반: `src/DeskTube/Services/IWallpaperHost.cs`, `src/DeskTube/Views/WallpaperWindow.xaml(.cs)`
  - **Edge Cases**:
    - WorkerW 탐색 실패(양 경로 모두) → Result 실패 반환, 재생 시작 중단 + 사용자 안내(part2 UI 소비), 크래시 금지
    - Explorer 재시작 → 셸 창 핸들 무효 감지(IsWindow) 시 재부착 재시도(최대 3회, 백오프)
    - 모니터 rect 변경(해상도 변경) → MonitorService.Changed 수신 시 재배치
    - 잠자기 복귀 → 핸들 유효성 확인 후 필요 시 재부착
  - **Halt Forecast**:
    - (i) "24H2에서 아이콘 숨김 상태면 표시 안 될 수 있음?" → Known Workarounds에 제약 명시(OS 동작), 앱은 정상 경로만 보장
    - (i) "SetParent 원복 방법?" → D3에서 확정(SetParent 해제 + SPI_SETDESKWALLPAPER)
  - **Depends on**: T1, T4

- [ ] T6. WebView2 플레이어 (FR-1 재생·FR-5 적용·FR-13 스케일)
  - **Type**: D
  - **Design**: ① `Assets/player.html`(IFrame API 로드+브리지 JS, 앱 리소스로 포함) + `Services/IPlayerHost.cs`(인터페이스 — T7 목킹 seam) + `Services/PlayerHost.cs`(구현: WebView2 생성·명령/이벤트 마셜링) + `Services/WebViewEnvironment.cs`(D9 단일 환경) ② `PlayerHost : IPlayerHost` — 창(T5) 안의 WebView2 1개를 소유, load/play/pause/setVolume(0~100)/mute/seek/scale 명령과 ready/stateChange/error/time 이벤트 제공 ③ WallpaperWindow에 부착, PlaybackCoordinator(T7)는 `IPlayerHost`만 참조 ④ 이번에 안 함: 유튜브 공식 playlist 파라미터 사용(앱 큐가 정본), 캐스트·자막 UI
  - **Acceptance**: Given videoId, When load+play 명령, Then 소리 포함 자동 재생 시작(HUMAN-VERIFY: 음소거 아님) + 임베드 금지 영상에서 error 이벤트(101/150) 수신이 로그로 확인됨; 화질 스케일 720p 설정 시 iframe 내부 해상도가 720 기준으로 축소됨(HUMAN-VERIFY)
  - **Files**:
    - 주: `src/DeskTube/Services/PlayerHost.cs`, `src/DeskTube/Assets/player.html`
    - 동반: `src/DeskTube/Services/IPlayerHost.cs`, `src/DeskTube/Services/WebViewEnvironment.cs`, `src/DeskTube/Views/WallpaperWindow.xaml.cs`(WebView2 부착)
  - **Edge Cases**:
    - 네트워크 끊김 → IFrame 로드 실패 시 30초 후 재시도(최대 3회), 이벤트로 상태 노출
    - WebView2 런타임 부재 → Win11은 기본 탑재이나 방어적으로 감지 후 안내 메시지 (Known Workarounds)
    - 자동재생 플래그 미작동 환경 → JS에서 muted 재생 시작 후 unmute 시도 (Risks 완화책)
    - 프로세스 크래시(CoreWebView2 ProcessFailed) → 플레이어 재생성 1회 시도
  - **Halt Forecast**:
    - (i) "autoplay 정책?" → D9 확정 (환경 인자)
    - (i) "화질 스케일 구현?" → player.html에서 iframe width/height를 스케일 값으로 설정 + CSS transform 확대 (D8 계약에 scale 명령 포함)
  - **Depends on**: T1, T5

- [ ] T7. 재생 오케스트레이터 (FR-3 다중 동기·FR-4 오디오·FR-5)
  - **Type**: D
  - **Design**: ① `Services/PlaybackCoordinator.cs`(싱글톤, DI) ② 책임 — 선택 모니터별 host 수명 관리(T5 `IWallpaperHost`·T6 `IPlayerHost` 인터페이스에만 의존, 생성은 팩토리 델리게이트 주입 — 테스트에서 가짜 구현 주입), PlaybackQueue로 곡 진행, 마스터-미러 동기(D4), 오디오 대상 적용(대상만 unmute·볼륨, 나머지 mute — FR-4), 재생/정지/볼륨 공개 API(트레이·UI가 part2에서 호출), 상태 저장(T2) ③ MonitorService·PowerPolicy(T8) 이벤트 소비, Models/Services 참조 ④ 이번에 안 함: 크로스페이드 등 전환 효과, 모니터별 개별 콘텐츠(Out of Scope)
  - **Acceptance**: Given 모니터 2개 선택+오디오 대상 지정(가짜 `IPlayerHost`/`IWallpaperHost` 주입), When 재생 시작·다음 곡·볼륨 변경·대상 모니터 분리, Then 명령 시퀀스가 기대와 일치(마스터 지정·미러 명령·mute 규칙·주 모니터 폴백·부분 실패 시 창 정리) — xUnit(T5·T6에서 정의한 두 인터페이스 목킹); 실기 2모니터 동기 재생은 HUMAN-VERIFY
  - **Files**:
    - 주: `src/DeskTube/Services/PlaybackCoordinator.cs`
    - 동반: `src/DeskTube/App.xaml.cs`(DI 등록·host 팩토리 배선)
    - 테스트: `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs`
  - **Edge Cases**:
    - 영상 종료 이벤트 중복 수신 → 큐 진행 멱등 처리(현재 곡 세대 토큰 비교)
    - 재생 시작 실패(WorkerW 실패·빈 리스트) → Result 실패 전파, 부분 생성된 창 정리(원자적 시작)
    - 동시 명령(트레이 재생 연타) → 코디네이터 내부 직렬화(DispatcherQueue 단일 스레드 처리)
    - 마지막 곡 종료 + 반복 없음 → 정지 상태 전환·창 유지 정책: 창 닫고 배경 복구
  - **Halt Forecast**:
    - (i) "동기 보정 기준?" → D4 확정 (5초 주기, 1초 초과 시 seek)
    - (i) "오디오 대상 부재 시?" → T4 Edge Case에서 주 모니터 폴백 확정
  - **Depends on**: T2, T3, T4, T5, T6

- [ ] T8. 자동 일시정지 정책 (NFR-1)
  - **Type**: C
  - **Design**: ① `Services/PowerPolicyService.cs` + `Interop/SessionInterop.cs`(WTSRegisterSessionNotification, SHQueryUserNotificationState P/Invoke) ② `PowerPolicyService` — 전체화면(D6 폴링)·배터리 세이버(D7)·세션 잠금(D7) 3신호를 합성해 `PauseRequested/ResumeRequested` 이벤트 발행 (재개는 모든 신호 해제 시) ③ PlaybackCoordinator가 구독(수동 정지와 구분되는 "정책 일시정지" 상태) ④ 이번에 안 함: 설정에서 정책별 on/off 토글(part2 T2에서 UI만 추가 — 서비스는 정책별 enable 플래그를 미리 노출)
  - **Acceptance**: Given 신호 조합 시나리오(전체화면 on→off, 세이버 on 중 잠금 on→둘 다 off 등), When 상태 머신에 주입, Then Pause/Resume 이벤트가 기대 시퀀스와 일치 — xUnit(신호를 인터페이스로 주입); 실기 게임 실행·잠금 동작은 HUMAN-VERIFY
  - **Files**:
    - 주: `src/DeskTube/Services/PowerPolicyService.cs`
    - 동반: `src/DeskTube/Interop/SessionInterop.cs`, `src/DeskTube/Services/PlaybackCoordinator.cs`(구독 연결)
    - 테스트: `tests/DeskTube.Tests/PowerPolicyServiceTests.cs`
  - **Edge Cases**:
    - 배경창 자신의 전체화면 오인 없음(확정) — QUNS 판정은 포그라운드 창 기준이고 배경창은 비활성(WorkerW 자식)이라 판정 대상이 아님
    - 정책 일시정지 중 사용자가 수동 재생 → 수동 명령 우선, 정책 상태 리셋
    - 잠금 해제 직후 세이버 여전히 on → 재개 안 함(모든 신호 해제 조건)
  - **Halt Forecast**:
    - (i) "감지 방식?" → D6·D7 확정
  - **Depends on**: T7

## 사전 승인 항목 (일괄 승인 대상)
- T1 — NuGet 의존성 추가: `Microsoft.WindowsAppSDK`(2.2), `CommunityToolkit.Mvvm`, `xunit`+`xunit.runner.visualstudio`+`Microsoft.NET.Test.Sdk` (part2에서 `H.NotifyIcon.WinUI`, `CommunityToolkit.WinUI.Controls.SettingsControls` 추가 — part2 plan에 재기재)
- T1 — git 저장소 초기화(`git init`) + 자율 루프의 로컬 작업 브랜치 커밋(체크포인트·task 완료 커밋, implement-task 규약 형식) — push 아님
- T1 — 신규 프로젝트 구조 생성(솔루션·프로젝트·폴더 대량 신규 파일)
- T5·T6·T8 — Win32 P/Invoke(user32/shell32/wtsapi32) 사용 코드 추가 (외부 서비스 아님, OS API)

## 불가피한 Halt (위임 불가)
- git push·원격 저장소 연결·태그·릴리즈·PR — 항상 별도 승인 (이번 plan에는 계획된 push 없음)
- Microsoft Store 제출·개발자 계정 작업 — 사용자 직접 수행
- plan에 없는 아키텍처 변경·데이터 형식 변경이 필요해지는 돌발 상황

## Known Workarounds (있는 경우만)
- 24H2는 바탕화면 아이콘 "표시" 상태에서만 배경 렌더링이 안정적이라는 보고 있음(OS 동작) → 앱 문서·FAQ에 명시, 코드 대응 없음
- Store 제출 전까지 `Package.appxmanifest`는 임시 identity(`DeskTube.Dev`) 사용 → 제출 시 Store 발급 identity로 교체(part2 T8 문서화)
- WebView2 런타임은 Win11 기본 탑재라 별도 설치 흐름 없음 → 감지 실패 시 안내만

## Verification Strategy
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` (경고/에러 0)
- 단위 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -c Debug -p:Platform=x64` (전체 통과 — 플랫폼 플래그 필수, T1 확인)
- 포맷: `dotnet format --verify-no-changes` (T1에서 편집구성 확정 후)
- 테스트 격리: `IStateStore` 경로 주입(임시 폴더), `IPlayerHost`/신호 인터페이스 목킹 — WinUI·interop 타입은 테스트 대상 제외
- 수동 검증(HUMAN-VERIFY 누적 목록): 아이콘 뒤 렌더링(T5), 소리 포함 자동 재생·화질 스케일(T6), 2모니터 동기+단일 오디오(T7), 전체화면/잠금/세이버 일시정지(T8), **클린 설치 후 최초 실행**(T2 — WinAppSDK 자동 초기화를 App 생성자 명시 호출로 대체해 초기화 시점이 원본보다 늦음, 프레임워크 패키지 최초 설치/복구 경로 실기 확인 필요) — 각 task 완료 보고에 명시

## Phase Ledger

## Retry Ledger

## Progress Log
- T3-T4 완료 (커밋 f0eebce, +T4 완료 커밋): URL 파서(5형식+스킴 생략) + PlaybackQueue(5모드, 셔플 사이클·UpdateItems 앵커 정합 — 리뷰 MAJOR 수정) + MonitorService(EnumDisplayMonitors, WM_DISPLAYCHANGE 메시지 창, UnregisterClassW 해제 — 리뷰 MAJOR 수정, ResolveTargets/ResolveAudioTarget 폴백 로직). 테스트 53/53.
  - 결정: 모니터 ID = `{device}@{x},{y}` 가독 조합 문자열 (해시 대신 — plan Design 동기화 완료). 이벤트명 MonitorsChanged.
  - 결정: 셔플 = 사이클 내 전곡 1회 소진 → 소진 후 재셔플(직전 곡 연속 회피). RepeatOne/RepeatAll/Random 표준 동작.
- T1-T2 완료 (커밋 2be4513, +T2 완료 커밋): WinUI3 패키지형 스캐폴딩(WinAppSDK 2.2, slnx, xUnit x64) + Models/영속화(JsonStateStore 원자적 쓰기·손상 .bak 복구, PlaylistLibrary 상한 100×1000, Result/ErrorCode, AppLog). 빌드 경고 0, 테스트 16/16.
  - 결정: 테스트 명령에 `-p:Platform=x64` 필수 (AnyCPU 시 MSIX 타깃 오류 — AGENTS 갱신은 Deferred).
  - 결정: WinAppSDK 모듈 자동 초기화 비활성(`WindowsAppSdkAutoInitialize=false`) + App() 생성자에서 DeploymentManager.Initialize 명시 호출 — 테스트 호스트가 앱 어셈블리 로드 시 0x80040154로 죽는 문제의 근본 해결. 클린 설치 최초 실행은 HUMAN-VERIFY 목록에 등재.

## Next Steps
- 권장 다음 액션: 사용자 승인 후 `pjc:implement-task`를 이 파일 경로로 호출 (분할 plan이므로 경로 명시)
- part1 완료 후: 남은 분할 plan: docs/plans/2026-07-15-desktube-ui-part2.md — pjc:implement-task로 별도 실행

## Open Questions
- [x] Q1~Q9: PRD 질문 라운드에서 전부 해소 — docs/prd.md `## 결정 기록` 참조 (재생 엔진=IFrame API, 로그인=Should, 화질=간접 제어, 멀티모니터=동일 영상, 오디오=사용자 지정, OS=Win11, 일시정지=3정책 모두, 자동 실행=자동 재생, 언어=한/영)
- [x] Q10: plan 분할 — B(2개 분할) 선택 (이 파일이 part1)
- [x] Q11: 아키텍처 — 계층형(서비스 중심), AGENTS.md 승인으로 확정
