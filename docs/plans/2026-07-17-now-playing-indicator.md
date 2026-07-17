# Plan: 현재 재생 중 플레이리스트 표시 (플레이리스트 목록 + 홈 빠른 재생 칩)

**PRD**: docs/prd.md

## 요구 이해
- **원문 요청**: "플레이리스트, 홈의 빠른 재생의 플레이리스트 목록에서 현재 재생중인 목록 표시"
- **이해한 요구**: 배경 재생 중인 플레이리스트가 어느 것인지 두 곳에서 시각적으로 보이게 한다 — ① 플레이리스트 페이지 좌측 리스트 목록 ② 홈 화면 "빠른 재생" 칩 목록. 표시 형태는 코럴(accent)색 스피커 글리프(E767 — 소리 배지와 동일)로 확정(질문 라운드). 재생 시작·정지 시 실시간 갱신되고, 일시정지 중에도 표시를 유지한다(리스트는 여전히 로드된 상태 — 홈 pill·트레이 문구 선례).
- **포함하지 않는 것으로 이해**: 재생 중인 "곡(항목)" 단위 표시(우측 항목 목록의 현재 곡 하이라이트)는 이번 범위가 아니다 — 요청은 "목록(리스트)" 단위.

## Goal
사용자가 플레이리스트 페이지와 홈 어디서든 지금 배경 재생 중인 리스트를 한눈에 알 수 있다.

## PRD Coverage
| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-18 (보강: 재생 중 리스트 표시) | Must | T1~T4 | ✅ 커버 |
| FR-1~FR-16, FR-19, FR-20, 나머지 FR-18 항목 | Must | (기구현) | 이번 범위 외 (기구현/후속) |

## Out of Scope
- 재생 중인 곡(항목) 단위 하이라이트 — 요청 범위 아님 (요청 시 별도 작업)
- 애니메이션 이퀄라이저(움직이는 바) — 정적 글리프로 확정, WinUI 표준 컨트롤에 없음

## Deferred / Follow-up
- (없음)

## Investigation Log
- 위키 참조: 관련 위키 자료 없음 — 코드 1차 출처로 진행 (feature/recipe 양방향 검색 무매칭)
- Deferred 대장(`docs/plans/deferred.md`) 확인 — "홈 화면 현재 재생 정보에 제목·썸네일 표시" 항목은 인접 주제지만 별개 기능(현재 재생 카드의 곡 메타 표시)이라 이번 plan에 편입하지 않음. 그 외 관련 항목 없음.
- `PlaybackCoordinator.cs:103~174` StartAsync 직접 Read — 성공 경로에서 `SetStatus(Playing)`(169) **후에** `_settings.LastPlaylistId = playlistId`(171) 기록. StatusChanged 핸들러가 동기 실행되면 stale LastPlaylistId를 읽는 잠재 레이스 확인(현재는 VM들이 TryEnqueue 마셜링이라 우연히 회피).
- `PlaybackCoordinator.cs:196~203` StopAsync 직접 Read — `SetStatus(Stopped)` 위치 확인. Pause/Resume(206~)은 Status만 변경.
- `PlaybackCoordinator.cs:315~335` NotifyPlaylistChangedAsync — 재생 중 리스트 삭제·비움 시 StopAsync 경유 → StatusChanged(Stopped) 발화 확인.
- `PlaylistsViewModel.cs:405~408` IsPlaying(entry) = `Status != Stopped && Settings.LastPlaylistId == entry.Id` — 기존 "재생 중 리스트" 판정 선례. 호출부 전수: `PlaylistsViewModel.cs:418`(DeleteAsync), `PlaylistsPage.xaml.cs:233`(삭제 확인 문구) 2곳.
- `PlaylistsViewModel.cs:11~31` PlaylistEntry — `IsActive`(선택 표시) observable 선례 확인. StatusChanged 구독은 현재 없음(Load/Detach 패턴만).
- `HomeViewModel.cs` 전체 Read — QuickChip은 불변 record(10행), QuickChips는 RefreshChips(149~166)가 페이지 진입·PlayAsync 후 재구성. StatusChanged 구독·TryEnqueue 마셜링(102~103, 131~132) 기존 존재 → UpdatePlaybackState(134~138)에 칩 갱신 편승 가능.
- `HomePage.xaml:182~205` 칩 템플릿(Button > StackPanel: E8FD 글리프+이름+곡수), `HomePage.xaml.cs:63` OnChipClick이 DataContext를 QuickChip으로 캐스트 — record→class 전환해도 캐스트 코드 무변경.
- `PlaylistsPage.xaml:78~119` 좌측 목록 템플릿 — Grid 3열(3px 인디케이터 | 이름* | 곡수 Auto). 글리프 열 추가 여지 확인.
- `StatusChanged` 구독자 전수 grep(src 전체): HomeViewModel(구독/해제 멱등), TrayIconService(74/184 구독·해제). PlaylistsViewModel은 미구독 — 신규 구독 필요.
- `LastPlaylistId` 사용처 전수 grep: App.xaml.cs:154(자동 재생), PlaylistsViewModel:408, PlaybackCoordinator:171/182/317, AppSettings:39 — 모두 유지(제거·의미 변경 없음, CurrentPlaylistId는 병행 신설).
- `QuickChip` 사용처 전수 grep: HomeViewModel(정의·생성), HomePage.xaml(x:DataType·바인딩), HomePage.xaml.cs:63(캐스트) — 3파일 전부 T3 Files에 포함.
- 글리프 선례: `MonitorCardsControl.xaml.cs:103` — E767 Volume(소리)/E74F Mute. 소리 배지와 동일 계열 글리프 재사용 확정.
- `docs/prd.md` FR-18 직접 Read — 플레이리스트 표시 명세 자리. 보강 위치·기존 문구 형식(연월일 병기) 확인.
- 테스트 하니스: `PlaybackCoordinatorTests.cs:100~` Harness(FakePlayer, StartAsync 다수 선례) — CurrentPlaylistId 단언 테스트 추가 용이.
- 빌드·테스트 명령: AGENTS.md + deferred 대장 — `dotnet build DeskTube.slnx -c Debug -p:Platform=x64`, `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -p:Platform=x64`(-p 미지정 시 MSIX AnyCPU 에러 — 기지 함정).

## Risks & Unknowns
| 위험 | 영향 | 완화책 |
|---|---|---|
| StatusChanged 발화 시점에 LastPlaylistId가 아직 이전 값(레이스) | 이벤트 직결 시 이전 리스트에 표시 | T1 — CurrentPlaylistId를 SetStatus(Playing) **전에** 확정하는 정본 속성 신설 (근본 해결) |
| StatusChanged가 UI 스레드 밖에서 발화 | COM 예외/크래시 | T2 — HomeViewModel과 동일한 DispatcherQueue.TryEnqueue 마셜링 |
| PlaylistsViewModel 구독 누수(페이지 이탈 후 잔존) | 이벤트 누적·중복 갱신 | T2 — Detach에서 해제 + Populate 멱등 재구독 (HomeViewModel 선례 복제) |
| QuickChip record→class 전환 시 바인딩 깨짐 | 홈 칩 표시 회귀 | T3 — x:Bind는 타입 기반이라 프로퍼티 시그니처 유지하면 무변경, 빌드가 검증 |

## Impact Analysis
### 4-A. 심볼/타입 추적 결과
| 심볼 | 영향 받는 파일 | 영향 종류 |
|---|---|---|
| `PlaybackCoordinator.CurrentPlaylistId` (신설) | Services/PlaybackCoordinator.cs | 공개 속성 추가 (additive) |
| `PlaylistsViewModel.IsPlaying` (내부 구현 전환) | ViewModels/PlaylistsViewModel.cs | 판정식을 CurrentPlaylistId 비교로 교체 — 입출력 계약 동일 (호출부 2곳 무변경: PlaylistsViewModel.cs:418, PlaylistsPage.xaml.cs:233) |
| `PlaylistEntry.IsNowPlaying` (신설) | ViewModels/PlaylistsViewModel.cs, Views/PlaylistsPage.xaml | observable 속성 추가 + 템플릿 바인딩 |
| `QuickChip` (record → ObservableObject 클래스) | ViewModels/HomeViewModel.cs, Views/HomePage.xaml, Views/HomePage.xaml.cs | 타입 형태 변경 — 사용처 3파일 전수 확인, 캐스트·기존 바인딩은 시그니처 유지로 무변경 |
| `StatusChanged` 구독 추가 | ViewModels/PlaylistsViewModel.cs | 구독자 3번째 (기존: HomeViewModel, TrayIconService — 무변경) |

### 4-B. 계약·직렬화 변경
- 없음 — AppSettings 직렬화 무변경(LastPlaylistId 유지, 신규 영속 필드 없음). PlayerCommand 등 페이로드 무변경.

### 4-C. 테스트 파일
- `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs` — CurrentPlaylistId 수명 주기 테스트 추가 (기존 116건 통과 유지)
- VM(PlaylistEntry/QuickChip) 표시 로직은 레포 관례상 비대상(서비스 계층만 테스트 — deferred 대장 기지 사실)

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| `PlaybackCoordinator.CurrentPlaylistId` | `IsPlaying(entry)`의 Status+LastPlaylistId 조합식 (PlaylistsViewModel:405) | 조합식은 레이스 내포·판정 로직이 VM에 분산 — 코디네이터 정본 속성으로 승격, 기존 조합식 1곳은 이 속성 재사용으로 전환 (사용처 3곳: IsPlaying·T2·T3) |
| `PlaylistEntry.IsNowPlaying` | `PlaylistEntry.IsActive` (동일 클래스 observable 선례) | 같은 패턴 복제 — 의미가 달라(선택 vs 재생) 별도 속성 필수 |
| `QuickChip.IsNowPlaying` | PlaylistEntry observable 패턴 | 동일 패턴 복제 — record는 변경 통지 불가라 클래스 전환 |
| UI 글리프(스피커) | `MonitorCardsControl` E767 선례 + `AppAccentBrush` 토큰 | 기존 글리프 코드·토큰 재사용 — 신규 스타일·토큰 불필요 |
| resw `NowPlayingIndicator` 키 | 기존 "재생 중" 계열 키 없음 (Home_PlayingFormat은 모니터 번호 포맷용) | 접근성·툴팁용 신규 2키(ko/en) |

### Verified by
- grep "StatusChanged" (src 전체) → 구독자 2곳(HomeViewModel·TrayIconService) 전건 Read, 영향 없음 확인
- grep "LastPlaylistId" → 6 hits 전건 문맥 확인, 모두 유지
- grep "QuickChip" → 3파일, 모두 T3 Files에 포함
- grep "IsPlaying" → HomeViewModel.IsPlaying(동명 무관 — pill용 bool)·PlaylistsViewModel.IsPlaying(entry) 호출부 2곳 확인

## Decisions
### D1. "현재 재생 중 리스트" 정본 소스
- **Options**: A) 코디네이터에 `CurrentPlaylistId`(정지 시 null) 공개 속성 신설 / B) 각 VM에서 Status+LastPlaylistId 조합 (기존 방식 복제) / C) StartAsync의 LastPlaylistId 기록을 SetStatus 앞으로 순서만 스왑
- **Chosen**: A
- **Rationale**: B는 SetStatus(169)→LastPlaylistId(171) 순서 탓에 이벤트 시점 stale 값을 읽는 레이스를 표시 지점마다 내포(현재는 TryEnqueue 지연으로 우연히 회피 — 취약). C는 레이스는 없애지만 "정지 후에도 남는 마지막 재생 ID"와 "지금 재생 중 ID"의 의미 혼재가 그대로다. A가 근본 해결 — 의미가 명확한 단일 정본, 표시 2곳+기존 IsPlaying까지 3곳 재사용.
- **Source**: PlaybackCoordinator.cs:169~171 직접 Read, PlaylistsViewModel.cs:405 선례

### D2. 일시정지 중 표시 유지
- **Chosen**: 유지 (Status != Stopped 동안 CurrentPlaylistId 비-null)
- **Rationale**: 리스트는 여전히 로드·재개 대상. 홈 pill(HomeViewModel.cs:136 `status != Stopped`)·트레이 "정지" 문구·IsPlaying 헬퍼 모두 동일 기준 — 앱 전체 일관.
- **Source**: HomeViewModel.cs:136, PlaylistsViewModel.cs:405~408

### D3. 표시 형태
- **Chosen**: 코럴(AppAccentBrush) 스피커 글리프 E767, 이름 옆 표시 (사용자 확정 — 질문 라운드)
- **Rationale**: 소리 배지(MonitorCardsControl)와 동일 글리프 계열 재사용, 언어 무관·공간 절약. 접근성 이름·툴팁은 resw "재생 중" 텍스트로 보완.
- **Source**: 사용자 답변 (2026-07-17), MonitorCardsControl.xaml.cs:103

### D4. 갱신 트리거
- **Chosen**: `StatusChanged` 구독 + 페이지 진입(Populate/AttachCore) 초기 반영. 신규 이벤트 불필요.
- **Rationale**: 재생 리스트가 바뀌는 모든 경로(StartAsync 성공·StopAsync·전곡 실패 정지·재생 중 리스트 삭제)는 반드시 StatusChanged를 발화한다(Stop→Start 연쇄 포함) — 기존 이벤트만으로 전 경로 커버. 폴링·전용 이벤트는 과설계.
- **Source**: PlaybackCoordinator StartAsync/StopAsync/NotifyPlaylistChangedAsync 직접 Read

### D5. QuickChip 갱신 방식
- **Options**: A) record → ObservableObject 클래스 전환(IsNowPlaying observable) / B) StatusChanged마다 RefreshChips()로 컬렉션 재구성
- **Chosen**: A
- **Rationale**: B는 상태 변화마다 칩 전체 재생성(깜빡임·불필요 레이아웃). A는 PlaylistEntry 선례와 동일 패턴, 변경 통지가 속성 단위.
- **Source**: PlaylistsViewModel.cs:11~31 (PlaylistEntry 선례)

## Tasks
- [x] T1. 코디네이터 정본 속성 `CurrentPlaylistId` 신설 + IsPlaying 전환 + 테스트
  - **Type**: C
  - **Design**: ① Services/PlaybackCoordinator.cs ② `public Guid? CurrentPlaylistId { get; private set; }` — "지금 재생(또는 일시정지) 중인 리스트 ID, 정지 시 null" 책임 1개 (D1 참조) ③ StartAsync 성공 경로가 `SetStatus(Playing)` **직전**에 설정(:169 앞), StopAsync가 `SetStatus(Stopped)` **직전**에 null(:200 앞) — 이벤트 핸들러가 항상 확정된 값을 읽는다. 소비자: PlaylistsViewModel.IsPlaying(판정식 교체)·T2·T3 ④ 인터페이스 추출·이벤트 신설은 하지 않음(StatusChanged로 충분 — D4).
  - **Acceptance**: Given 정지 상태, When StartAsync 성공, Then CurrentPlaylistId == 시작한 리스트 ID (StatusChanged(Playing) 발화 시점에 이미 설정됨) / When StopAsync 또는 전곡 재생 불가 정지, Then null / When Pause, Then 값 유지. 기존 테스트 116건 통과 유지 + 신규 단언 테스트 통과.
  - **Files**:
    - 주: `src/DeskTube/Services/PlaybackCoordinator.cs`
    - 동반: `src/DeskTube/ViewModels/PlaylistsViewModel.cs` (IsPlaying 판정식 교체 — 계약 동일)
    - 테스트: `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs`
  - **Edge Cases**:
    - StartAsync 실패(모니터 없음·빈 리스트 등): 설정 지점 이전에 return — null 유지 (초입 StopAsync가 이전 값도 정리)
    - 재생 중 다른 리스트 StartAsync: 초입 StopAsync가 null→새 리스트로 재설정, Stopped→Playing 이벤트 순서로 UI 일관
    - StartLastAsync: 내부 StartAsync 경유 — 자동 커버
  - **Halt Forecast**:
    - (ii-a) 공개 API 추가(CurrentPlaylistId) → `## 사전 승인 항목`에 등록
  - **Depends on**: -
- [x] T2. 플레이리스트 페이지 좌측 목록에 재생 중 글리프 표시
  - **Type**: C
  - **Design**: ① ViewModels/PlaylistsViewModel.cs + Views/PlaylistsPage.xaml ② `PlaylistEntry.IsNowPlaying`(observable bool — IsActive 선례) + `PlaylistsViewModel.UpdateNowPlaying()`(전 entry에 CurrentPlaylistId 비교 반영) ③ Populate가 초기 1회 호출, StatusChanged 구독(HomeViewModel 패턴: DispatcherQueue 마셜링·Populate 멱등 재구독·Detach 해제)이 이후 갱신. XAML은 이름/곡수 사이 Auto 열에 FontIcon E767(AppAccentBrush, Visibility=IsNowPlaying OneWay, AutomationProperties.Name+ToolTip=resw) ④ 항목 단위 표시·애니메이션은 하지 않음.
  - **Acceptance**: Given 페이지 표시 중, When 어떤 리스트 재생 시작(페이지 내 버튼·홈·트레이 무관), Then 해당 행에만 코럴 스피커 글리프 표시 / When 정지(전곡 실패·리스트 삭제 포함), Then 글리프 제거 / Given 재생 중 페이지 진입, Then 진입 즉시 표시. 빌드 경고 0. (시각 표시는 HUMAN-VERIFY)
  - **Files**:
    - 주: `src/DeskTube/ViewModels/PlaylistsViewModel.cs`, `src/DeskTube/Views/PlaylistsPage.xaml`
    - 동반: `src/DeskTube/Strings/ko-KR/Resources.resw`, `src/DeskTube/Strings/en-US/Resources.resw` (NowPlayingIndicator 키), `src/DeskTube/Views/PlaylistsPage.xaml.cs` (NowPlayingLabel 정적 헬퍼 — 구현 중 추가, spec 리뷰 범위 정합 판정)
  - **Edge Cases**:
    - StatusChanged가 UI 스레드 밖 발화 → TryEnqueue 마셜링 (HomeViewModel.cs:131 선례)
    - 페이지 이탈 후 이벤트 → Detach 해제로 미수신 (구독 누수 방지 — deferred 대장 AccountPanel 함정 선례)
    - 재생 중 리스트 삭제 → DeleteAsync가 StopAsync → Stopped 이벤트로 해제
    - 선택 표시(IsActive 코럴 바)와 동시 표시 → 별도 시각 요소(바 vs 글리프)라 충돌 없음
    - 같은 리스트 재시작(StartAsync 초입 Stop→Start 연쇄): 글리프가 순간 꺼졌다 켜질 수 있음 — 허용(상태 사실 그대로 반영, 시각 영향 미미 — plan-reviewer m1, HUMAN-VERIFY에서 확인)
  - **Halt Forecast**: (없음 — 결정 전부 사전 확정)
  - **Depends on**: T1
- [ ] T3. 홈 빠른 재생 칩에 재생 중 글리프 표시
  - **Type**: C
  - **Design**: ① ViewModels/HomeViewModel.cs + Views/HomePage.xaml ② `QuickChip`을 record에서 ObservableObject partial 클래스로 전환(Id·Name·CountText는 불변 유지 + `IsNowPlaying` observable — D5 참조) ③ RefreshChips가 생성 시 초기값 설정, 기존 UpdatePlaybackState(StatusChanged 경유)가 칩 전체 재판정 — 신규 구독 불필요. 칩 템플릿 StackPanel에 FontIcon E767(AppAccentBrush, Visibility=IsNowPlaying OneWay, AutomationProperties.Name+ToolTip=resw 재사용) 추가 ④ 칩 재구성 방식(B안)·칩 스타일 신설은 하지 않음.
  - **Acceptance**: Given 홈 표시 중, When 재생 시작/정지, Then 해당 리스트 칩에만 글리프 표시/제거 / Given 재생 중 홈 진입, Then 즉시 표시 / "빠른 재생" 리스트 재생 시 그 칩에도 동일 표시. 빌드 경고 0. (시각 표시는 HUMAN-VERIFY)
  - **Files**:
    - 주: `src/DeskTube/ViewModels/HomeViewModel.cs`, `src/DeskTube/Views/HomePage.xaml`
    - 동반: `src/DeskTube/Views/HomePage.xaml.cs` (OnChipClick 캐스트 — 시그니처 유지 확인만, 무변경 예상)
  - **Edge Cases**:
    - PlayAsync 직후 RefreshChips 재구성 → 생성 시 초기값 경로가 곧바로 정확한 상태 반영
    - 리스트 0개(HasChips=false) → 섹션 숨김 기존 동작 유지, 갱신 루프는 빈 컬렉션에 무해
    - StatusChanged 마셜링 → 기존 OnStatusChanged TryEnqueue 경유라 추가 조치 불필요
  - **Halt Forecast**:
    - (ii-a) 공개 타입 QuickChip의 형태 변경(record→class) → `## 사전 승인 항목`에 등록 (사용처 3파일 전수 확인 완료)
  - **Depends on**: T1
- [ ] T4. PRD FR-18 보강 + README 갱신
  - **Type**: A
  - **Acceptance**: FR-18에 "현재 재생 중인 리스트를 플레이리스트 목록·홈 빠른 재생 칩에서 스피커 표시로 구분(일시정지 중 유지)" 취지 보강 + 변경 이력 1줄(2026-07-17, 사용자 합의) / README 기능 설명 갱신. 문서-코드 역대조 누락·잔존 0.
  - **Files**:
    - 주: `docs/prd.md`, `README.md`
  - **Edge Cases**: (Type A — skip)
  - **Halt Forecast**: (없음 — PRD 보강은 질문 라운드에서 사용자 합의 완료)
  - **Depends on**: T2, T3

## 사전 승인 항목 (일괄 승인 대상)
- T1 — `PlaybackCoordinator.CurrentPlaylistId` 공개 속성 추가 (additive 공개 API — 계획된 변경)
- T3 — 공개 타입 `QuickChip` record→ObservableObject 클래스 전환 (사용처 3파일 전수 확인, 프로퍼티 시그니처 유지)
- T4 — PRD FR-18 보강 (질문 라운드에서 사용자 합의 완료 — 승인 재확인용 명시)

## 불가피한 Halt (위임 불가)
- (없음)

## Verification Strategy
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` → 경고/에러 0
- 단위 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -p:Platform=x64` → 전건 통과 (기존 116 + 신규)
- 포맷: `dotnet format` 위반 0
- 수동 검증 (HUMAN-VERIFY): ① 재생 시작 → 플레이리스트 목록·홈 칩에 코럴 스피커 표시 ② 정지·일시정지 동작 ③ 재생 중 페이지 진입 시 즉시 표시 ④ 재생 중 리스트 삭제 시 해제 ⑤ 같은 리스트 재시작 시 깜빡임이 거슬리는 수준인지 (m1 — 허용 전제)

## Phase Ledger

## Retry Ledger

## Progress Log

## Next Steps

## Open Questions
- [x] Q1: 표시 형태? → **A) 코럴 스피커 글리프(E767)** 확정 (텍스트 배지·병행안 기각 — 사용자 답변 2026-07-17)
- [x] Q2: PRD FR-18 보강? → **보강 진행** 확정 (사용자 답변 2026-07-17)
