# Plan: 플레이리스트 항목 재생시간 표시 — 재생 시 수집·영구 캐시 (2026-07-19)

**PRD**: docs/prd.md

## 요구 이해
- **원문 요청**: "플레이리스트 항목에 재생시간 정보 표시" (첨부: 플레이리스트 우측 항목 목록 스크린샷 — 순위·썸네일·제목·채널명 행)
- 우측 항목 목록의 각 곡 행에 재생시간(예: 3:24)을 표시한다.
- 유튜브 oEmbed 메타데이터에는 재생시간이 없어 별도 취득이 필요 — **곡이 실제 재생될 때 플레이어에서 길이를 받아 항목에 영구 캐시**한다 (질문 확정 Q1). 안 틀어본 곡은 **공란**(Q3).
- 표시 위치는 **행 우측 열**(재생 버튼 왼쪽, 회색 작은 글씨 — Q2). 헤더 총 합계는 하지 않는다(Q4).

## Goal
각 곡 행 우측에 재생시간이 표시되고, 재생해 본 곡부터 채워져 재시작 후에도 유지된다.

## PRD Coverage
| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-18 (보강: 항목 재생시간 표시 — 재생 시 수집·캐시) | Must | T1~T5 | ✅ 커버 |
| FR-1~FR-16, FR-19~FR-21, 나머지 FR-18 항목 | Must/Could | (기구현) | 이번 범위 외 (기구현/후속) |

## Out of Scope
- 전 곡 즉시 조회 방식(숨은 프로브 플레이어 백필 / YouTube Data API) — 질문 Q1에서 기각 (NFR-6 "API 키 불필요" 정책 유지, 저부하 NFR 보호)
- 홈 화면·트레이 등 다른 화면의 재생시간 표시 — 요청에 없음

## Deferred / Follow-up
- 리스트 헤더 총 재생시간 합계 표시 — 질문 Q4에서 이번 제외 (수집 방식 특성상 초기 합계가 실제보다 적게 보이는 오해 소지. 요청 시 확보 곡 합계로 후속 가능)

## Investigation Log
- 위키 참조: vault 미설정 — 코드 1차 출처로 진행 (직전 plan과 동일 상태)
- Deferred 대장 `## 대기` 확인 — "홈 화면 현재 재생 정보 제목·썸네일(FR-18 메타 인프라 재사용)" 항목은 홈 화면 건이라 이번 작업(플레이리스트 화면)과 별개 — 대장 유지. 그 외 관련 항목 없음.
- 직전 plan `2026-07-18-leading-stop-glyph.md`: Phase Ledger `Phase G 통과 (Must 100%)` → 완료 확정. Deferred 1건(E71A/E768 공통화)은 대장에 기등재 확인 — 이관 불필요.
- Step 0: 미커밋 직전 작업(정보 화면 개발자 표기 제거)은 사용자 승인으로 `3afd04f` 커밋 — 깨끗한 상태에서 시작.
- `Models/Playlist.cs:4~19` — PlaylistItem 필드 5개(Id·Url·VideoId·Title·ChannelName), duration 없음. **직접 확인.** 공개 프로퍼티 직렬화(JsonSourceGeneration, `JsonStateStore.cs:101~105`) — 기본값 있는 새 필드는 기존 JSON에서 누락돼도 안전(Title/ChannelName의 `string.Empty` 관례와 동일).
- `Services/VideoMetadataService.cs` — oEmbed는 title·author_name만 파싱. oEmbed 응답에 duration 없음(스펙상 미제공) → 별도 취득 필요 확정. **직접 확인.**
- `Assets/player.html:276~286` — onReady의 1초 인터벌이 PLAYING일 때 `{type:'time', current:t}` 보고. `getDuration()` 호출 없음. `PLAYER_REV = 5`(수정 시 +1 관례 — 32행 주석). **직접 확인.**
- `Services/PlayerHost.cs:273~313` — time 메시지 파싱 → `CurrentTime` 갱신 + `TimeUpdated` 발화. `TryGetProperty` 사용 관례라 옛 rev(duration 미포함)와 신 rev 혼재에도 안전하게 확장 가능. **직접 확인.**
- `Services/IPlayerHost.cs:33~80` — 계약. `CurrentTime` "최근 시각 이벤트 캐시" 관례(79행). 구현체 **전수 2곳**: `PlayerHost`(프로덕션), `FakePlayer`(`tests/DeskTube.Tests/PlaybackCoordinatorTests.cs:62`) — grep 전수 확인(hit 30건 미만 전건 대조). 멤버 추가 시 2곳 갱신.
- `Services/PlaybackCoordinator.cs:610~620` — `OnPlayerTime`: `IsMaster` 필터 후 `masterTime >= PlaybackProgressMinSeconds(1.0)`에서 `_failedItemIds` 초기화(실제 진행 확인 지점). `_library: PlaylistLibrary` 필드 보유(41행) — 수집 시 저장 경로 존재. `CurrentItemId`/`CurrentItemChanged`(110~116행) — 항목 상태 알림 관례. **직접 확인.**
- `Services/PlaylistLibrary.cs:6~8` — "상태 접근은 UI 스레드 단일 직렬화 전제 — 내부 잠금 없음. 영속화는 호출자가 SaveAsync". WebView2 이벤트는 컨트롤러 생성 스레드(UI)로 도착하고 `OnPlayerTime`이 이미 무마셜링으로 상태 변이 중 — 같은 전제로 수집·저장 가능. **직접 확인.**
- `ViewModels/PlaylistsViewModel.cs:41~79` — `PlaylistItemEntry` 생성자가 모델 값 복사, 표시 필드는 `[ObservableProperty]`, `DisplayTitle` 계산 속성 + `NotifyPropertyChangedFor` 관례. 154·174~175행 `CurrentItemChanged` 구독·해제 대칭 + UI 마셜링(205~207행) 관례. backfill(364~437행)은 모델+Entry 동시 갱신 후 `SaveAsync` 1회 관례. **직접 확인.**
- `Views/PlaylistsPage.xaml:284~392` — 행 4컬럼(22·64·*·Auto: 순위·썸네일·곡정보·재생 버튼), 채널명 12px `AppTextTertiaryBrush`. **직접 확인.** 열 1개 삽입 지점(곡정보와 버튼 사이) 확인.
- `JsonStateStore` 쓰기 — 임시 파일 후 원자적 교체(`File.Move`, explorer 확인 + 파일 주석) → 코디네이터 저장과 VM backfill 저장이 드물게 겹쳐도 전체 스냅샷 저장이라 최종 일관(마지막 승자, 손상 없음).

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| `PlaylistItem.DurationSeconds` | duration/시간 필드 grep — 없음 | 신규 (기본값 0 = 미수집) |
| `IPlayerHost.CurrentDuration` | `CurrentTime` 최근값 캐시 관례(IPlayerHost.cs:79) | 동일 관례로 신규 (재사용 불가 — 값이 다름) |
| `PlaybackCoordinator.ItemDurationCaptured` | `CurrentItemChanged`·`MutedChanged` 이벤트 관례 | 동일 관례로 신규 (payload만 예외 — D9) |
| `FormatDuration(int)` | `TimeSpan` grep 5건 — 전부 타이머 간격 등 무관, mm:ss 포맷 유틸 없음 | 신규 (순수 함수) |
| 재생시간 표시 열 | 기존 행 TextBlock·토큰(`AppTextTertiaryBrush`) 재사용 | 신규 컨트롤·스타일 없음 |

## 시각 요소 분해
(해당 없음 — 기준 시안 없는 신규 요소. 기존 행의 채널명과 동급 톤(12px·`AppTextTertiaryBrush`)으로 지정, 값은 토큰만 사용)

## Tasks

- [x] T1. PRD FR-18 보강 — 재생시간 표시·수집 방식 명세 (FR-18 충족)
  - **Type**: A
  - **Acceptance**: FR-18에 "각 항목에 재생시간을 표시한다(재생 시 플레이어에서 수집해 영구 캐시 — 미수집 곡은 공란, 표시 위치는 행 우측)" 취지 보강 + 변경 이력 1줄(질문 확정 요지 포함). 역대조 누락·잔존·변형 0.
  - **Files**: 주: `docs/prd.md`
  - **Edge Cases**: (Type A — skip)
  - **Halt Forecast**: 없음 — 질문 확정이 곧 합의 (plan 승인에 포함, 사전 승인 항목 등재).
  - **Depends on**: 없음

- [x] T2. 수집 하부 — 모델 필드 + 브리지 duration 보고 + 플레이어 계약 (FR-18 충족)
  - **Type**: D (인터페이스 변경 — 구현체 2곳 갱신)
  - **Design**:
    - 배치: 영속 필드는 `Models/Playlist.cs`, 보고는 `Assets/player.html`(기존 1초 인터벌), 계약은 `Services/IPlayerHost.cs`, 파싱은 `Services/PlayerHost.cs`(수신부 한 곳).
    - 신규 심볼: `PlaylistItem.DurationSeconds`(int — 재생 시 수집 캐시, 0=미수집), `IPlayerHost.CurrentDuration`(double — 최근 duration 이벤트 캐시, 0=미보고, `CurrentTime` 관례).
    - 의존 방향: player.html → PlayerHost → (이벤트) → Coordinator — 기존 그대로, 신규 의존 없음.
    - 비추상화: duration 전용 메시지 타입·이벤트 신설 안 함(기존 `time` 메시지에 필드 추가), `TimeUpdated` 시그니처 불변(`EventHandler<double>` 유지 — 구독부 보존, D6).
  - **수정 내용**:
    1. `Models/Playlist.cs` — `public int DurationSeconds { get; set; }` 추가 (한글 주석: 재생 시 수집 캐시·0=미수집, FR-18. 기존 JSON 누락 시 기본값 0 — 역호환).
    2. `Assets/player.html` — 1초 인터벌의 post를 `{ type: 'time', current: t, duration: d }`로 확장. `d`는 `player.getDuration()`을 숫자 가드(비숫자·비유한 → 0)로 정규화(시작 감시 타입 가드 관례). `PLAYER_REV = 6` + 상단 이벤트 목록 주석 갱신.
    3. `Services/IPlayerHost.cs` — `double CurrentDuration { get; }` 추가 (문서주석: 최근 duration 캐시·0=미보고/미상 — 라이브 스트림 포함).
    4. `Services/PlayerHost.cs` — time 케이스에서 `TryGetProperty("duration")` 시 `CurrentDuration` 갱신(없으면 기존 값 유지 — 옛 rev 캐시 호환).
    5. `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs` — `FakePlayer`에 `CurrentDuration` 구현 + `RaiseTime`에서 duration을 지정할 수단(오버로드 또는 설정 가능 필드).
  - **Acceptance**: 빌드 경고 0·오류 0 + 기존 테스트 전체 통과 (동작 추가는 T3에서 검증 — 이 task는 계약·보고 계층만).
  - **Files**: 주: `src/DeskTube/Models/Playlist.cs`, `src/DeskTube/Assets/player.html`, `src/DeskTube/Services/IPlayerHost.cs`, `src/DeskTube/Services/PlayerHost.cs`, `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs`
  - **Edge Cases**:
    - 라이브·미로드 상태 `getDuration()` = 0 → 0 그대로 보고 (수집 차단은 T3 가드).
    - WebView2 캐시로 옛 rev(5) 실행 → duration 필드 없음 → `CurrentDuration` 0 유지 — 기능 저하(수집 안 됨)만, 크래시·오동작 없음.
    - `getDuration` 비숫자 반환(죽은 플레이어) → 0 정규화 — post 콜백 사망 방지(2026-07-18 `toFixed` TypeError 사례 재발 방지 관례).
  - **Halt Forecast**: `IPlayerHost` 공개 인터페이스 멤버 추가 — 계획된 공개 API 변경, **사전 승인 항목 등재** (구현체 2곳이 같은 diff에서 갱신). 파괴적·외부 작업 없음.
  - **Depends on**: 없음

- [x] T3. 코디네이터 수집·영속·알림 + 회귀 테스트 (FR-18 충족)
  - **Type**: D (재생 수명주기 크로스커팅 + 공개 이벤트 추가)
  - **Design**:
    - 배치: 수집 판단·영속은 `PlaybackCoordinator.OnPlayerTime`(마스터 판정·큐·라이브러리를 모두 보유한 유일 지점 — 실제 진행 확인 로직과 결합).
    - 신규 심볼: `public event EventHandler<Guid>? ItemDurationCaptured`(수집된 항목 ID 전달 — 값 정본은 라이브러리 모델).
    - 의존 방향: Coordinator → PlaylistLibrary(기존 `_library` 필드)·IPlayerHost(기존) — 신규 의존 없음. VM이 이벤트 구독(T4).
    - 비추상화: 별도 DurationCaptureService 신설 안 함(수집 조건이 재생 진행 판정(`PlaybackProgressMinSeconds`)과 결합 — OnPlayerTime 지역성 유지), 디바운스 인프라 미사용(곡당 최대 1회 저장이라 불필요).
  - **수정 내용**:
    1. `OnPlayerTime`에서 `masterTime >= PlaybackProgressMinSeconds`(실제 진행 확인 — 기존 `_failedItemIds` 초기화 지점)일 때: sender(`IPlayerHost`)의 `CurrentDuration >= 1.0`이고 `_queue?.Current`가 존재하며 그 `Id == CurrentItemId`이면, 저장값과 반올림 차이가 2초 이상(미수집 0 포함)일 때만 `item.DurationSeconds = (int)Math.Round(duration)` 갱신 + `_library.SaveAsync()` fire-and-forget(예외 로그 흡수 — backfill 관례) + `ItemDurationCaptured(item.Id)` 발화.
    2. 테스트 3건 신설: ① 진행 확인 시(RaiseTime ≥ 1.0 + duration 보고) 모델 duration 갱신·이벤트 발화·저장 1회 ② duration 0(라이브·미보고) 미수집 ③ 동일 값 재보고 시 재저장·재발화 없음. "저장 1회" 검증을 위해 `FakeStore`에 playlist 저장 호출 카운터 추가(기존 `SettingsSaveCount` 관례 — plan-reviewer m1).
  - **Acceptance**: 빌드 경고 0·오류 0 + 신규 테스트 3건 포함 전체 통과.
  - **Files**: 주: `src/DeskTube/Services/PlaybackCoordinator.cs`, `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs`
  - **Edge Cases**:
    - 재생 불가 영상(-3 계열): `masterTime < 1.0`이라 수집 조건 자체 미달 — 오수집 자연 차단 (권한 필요 영상의 가짜 PLAYING 포함).
    - 곡 전환 직후: time·duration은 같은 JS 인터벌 틱에서 함께 보고되므로 항상 같은 영상의 쌍 — 이전 곡 duration이 새 곡에 붙는 오귀속 없음. 로드~PLAYING 사이엔 인터벌이 침묵(PLAYING 조건)이라 잔여 이벤트 없음.
    - 저장 경합: UI 스레드 직렬화 전제 + 원자적 파일 교체 — backfill 저장과 겹쳐도 스냅샷 저장이라 손상 없음 (Investigation Log 근거).
    - 다중 모니터: 기존 `IsMaster` 가드 — 마스터만 수집.
    - 일시정지·절전: 인터벌이 PLAYING에서만 보고 — 수집 시도 없음(무해).
    - 재생 중 항목 삭제 경합: `_queue.Current`·`CurrentItemId` 일치 가드로 모델 부재 시 미수집 — 무해.
  - **Halt Forecast**: `PlaybackCoordinator` 공개 이벤트 추가 — 계획된 공개 API 변경, **사전 승인 항목 등재**. 파괴적·외부 작업 없음.
  - **Depends on**: T2

- [x] T4. UI 표시 — Entry 필드·포맷 함수 + 행 우측 열 + VM 이벤트 구독 (FR-18 충족)
  - **Type**: C (2개 파일 + 테스트, 신규 심볼 도입)
  - **Design**:
    - 배치: 표시 상태는 `PlaylistItemEntry`(표시 필드 관례), 포맷은 그 클래스의 정적 순수 함수(`DisplayTitle` 계산 속성 관례와 동일 파일), 표시는 `PlaylistsPage.xaml` 행 템플릿.
    - 신규 심볼: `PlaylistItemEntry.DurationSeconds`(관찰 가능 int), `DurationText`(계산 속성 — 0 이하면 빈 문자열), `public static string FormatDuration(int seconds)`(순수 함수 — 테스트 대상), VM `OnItemDurationCaptured` 핸들러.
    - 의존 방향: View → Entry(x:Bind), VM → Coordinator 이벤트(기존 `CurrentItemChanged` 구독·해제 대칭·UI 마셜링 관례).
    - 비추상화: `IValueConverter` 미도입(x:Bind 계산 속성 관례 유지), 열 폭 고정값 미도입(Auto), 전용 스타일 미신설(기존 토큰 직접 참조 — 행 내 채널명과 동일 방식).
  - **수정 내용**:
    1. `PlaylistsViewModel.cs` — `PlaylistItemEntry`: `[ObservableProperty] [NotifyPropertyChangedFor(nameof(DurationText))] public partial int DurationSeconds { get; set; }` + `public string DurationText => FormatDuration(DurationSeconds)` + `FormatDuration`(0 이하 → `""`, 3600 이상 → `h:mm:ss`, 그 외 → `m:ss`) + 생성자에서 `item.DurationSeconds` 복사. VM: `Populate`(구독)/`Detach`(해제)에서 `ItemDurationCaptured` 구독·해제(기존 `CurrentItemChanged` 대칭 패턴 + UI 마셜링 — plan-reviewer m2 용어 정정), 핸들러는 `Items`에서 Id 일치 entry를 찾아 라이브러리 모델 값으로 `DurationSeconds` 갱신(entry 없으면 no-op).
    2. `PlaylistsPage.xaml` — 행 Grid에 곡정보(*)와 재생 버튼(Auto) 사이 `Auto` 컬럼 추가, `TextBlock {x:Bind DurationText, Mode=OneWay}` FontSize 12·`AppTextTertiaryBrush`·VerticalAlignment Center. 재생 버튼 `Grid.Column` 3→4. 행 구조 주석 갱신.
    3. 테스트 — `FormatDuration` 경계값 단위테스트(0·음수 → 빈, 59 → `0:59`, 60 → `1:00`, 3599 → `59:59`, 3600 → `1:00:00`, 7325 → `2:02:05`).
  - **Acceptance**: 빌드 경고 0·오류 0 + `FormatDuration` 테스트 통과. 표시·실시간 갱신·재시작 유지는 ⏳ HUMAN-VERIFY.
  - **Files**: 주: `src/DeskTube/ViewModels/PlaylistsViewModel.cs`, `src/DeskTube/Views/PlaylistsPage.xaml`, `tests/DeskTube.Tests/`(포맷 테스트 — 신규 파일 `DurationFormatTests.cs` 또는 기존 파일 병합은 구현 시 관례 따름)
  - **Edge Cases**:
    - 미수집(0) → 빈 문자열 — 공란(질문 Q3 확정). Auto 컬럼이라 빈 값이면 폭 0 — 레이아웃 무해.
    - 다른 리스트 보는 중 수집 → entry 없음 → no-op, 다음 진입 시 생성자 복사로 반영.
    - 이벤트 발생 스레드 비보장 가정 → 기존 관례대로 UI 마셜링.
    - 테스트 프로젝트에서 ViewModels 타입 참조 문제 발생 시(순수 함수인데 WinUI 의존이 끌려오는 경우) → `FormatDuration`을 Services 정적 클래스로 이동(순수 함수 — 위치만 변경, 계약 불변).
  - **Halt Forecast**: 없음 — 파괴적·외부 작업 없음.
  - **Depends on**: T2, T3

- [ ] T5. README 갱신 (FR-18 충족)
  - **Type**: A
  - **Acceptance**: 플레이리스트 화면 기능 서술에 "항목 재생시간 표시(재생 시 수집·캐시, 미수집 곡 공란)" 추가. 역대조 누락·잔존·변형 0.
  - **Files**: 주: `README.md`
  - **Edge Cases**: (Type A — skip)
  - **Halt Forecast**: 없음.
  - **Depends on**: T4

## 사전 승인 항목 (일괄 승인 대상)
- T1 — PRD FR-18 보강 (질문 4건 확정 내용의 명세 반영)
- T2 — `IPlayerHost` 공개 인터페이스에 `CurrentDuration` 멤버 추가 (구현체 전수 2곳 — PlayerHost·FakePlayer — 같은 diff에서 갱신)
- T2 — `PlaylistItem` 직렬화 모델에 `DurationSeconds` 필드 추가 (기본값 0 역호환 — 마이그레이션 불요)
- T3 — `PlaybackCoordinator` 공개 이벤트 `ItemDurationCaptured` 추가

## 불가피한 Halt (위임 불가)
- (없음)

## 검증 방법
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` (경고 0·오류 0)
- 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -c Debug -p:Platform=x64` (신규: 코디네이터 수집 3건 + 포맷 경계 6케이스)
- 포맷: `dotnet format` 위반 0
- 시각·동작(⏳ HUMAN-VERIFY): ① 곡 재생 후 해당 행 우측에 시간 표시 ② 안 틀어본 곡은 공란 ③ 앱 재시작 후에도 수집된 시간 유지 ④ 1시간 이상 영상 h:mm:ss 표기

## Decisions
- D1. 취득 방식 = 재생 시 수집·영구 캐시 — 사용자 확정(Q1). 추가 네트워크·API 키 없음(NFR-6 유지), 기존 1초 시각 보고 인프라 재사용.
- D2. 표시 = 행 우측 열(재생 버튼 왼쪽), 12px `AppTextTertiaryBrush`(채널명과 동급 톤) — 사용자 확정(Q2, 음악 앱 관례).
- D3. 미수집 곡 = 공란(Q3), 헤더 합계 없음(Q4 — Deferred).
- D4. 영속 필드 = `int DurationSeconds`, 0=미수집 — 초 단위 반올림. 기본값 있는 필드라 기존 playlists.json 역호환(Title 관례와 동일). Source: JsonStateStore 소스 생성 직렬화.
- D5. 브리지 = 기존 `time` 메시지에 `duration` 필드 추가(신규 메시지 타입 안 함) + `PLAYER_REV` 6 — time·duration이 같은 틱의 쌍이라 곡 전환 오귀속이 구조적으로 없음. 옛 rev 캐시와 `TryGetProperty` 전방 호환. Source: player.html 32행 rev 관례.
- D6. `TimeUpdated` 시그니처 불변 — duration은 `IPlayerHost.CurrentDuration` 캐시로 노출(`CurrentTime` 관례). 구독부(코디네이터 2곳·FakePlayer) 계약 보존. Source: IPlayerHost.cs:79.
- D7. 수집 시점 = 실제 진행 확인(`masterTime >= PlaybackProgressMinSeconds` 재사용) && `duration >= 1.0` — 재생 불가 영상(가짜 PLAYING)·라이브(duration 0) 오수집 차단. Source: PlaybackCoordinator.cs:617 기존 판정 지점.
- D8. 재저장 억제 = 저장값과 반올림 차 2초 미만이면 skip — 곡당 저장·발화 1회, 미세 오차 반복 저장 방지.
- D9. 알림 = `ItemDurationCaptured`는 `EventHandler<Guid>`(항목 ID 전달) — "정본 읽기" 이벤트 관례(CurrentItemChanged)의 예외: 마셜링 지연 중 곡이 전환되면 `CurrentItemId`가 다른 곡을 가리켜 오갱신·미갱신하므로 수집된 항목 ID를 인자로 고정한다(예외 사유를 문서주석에 명시).
- D10. 포맷 = 1시간 미만 `m:ss`, 이상 `h:mm:ss`, 0 이하 빈 문자열 — 유튜브 표기 관례. 지역화 불요(숫자·콜론만 — resw 무변경).
- D11. 포맷 함수 위치 = `PlaylistItemEntry` 정적 순수 함수 — 표시 관심사의 지역성(DisplayTitle 관례) + 테스트 가능. 테스트 참조 문제 시 Services 이동 폴백(T4 Edge).

## Progress Log
- T1-T2 완료 (커밋 d15e20a, 34c17d1): T1 PRD FR-18 보강(+.gitignore에 .playwright-mcp/ 추가 — 무관 로그 커밋 방지). T2 수집 하부(모델 DurationSeconds·player.html duration REV 6·IPlayerHost.CurrentDuration·PlayerHost 파싱·FakePlayer). 빌드 0·0, 테스트 127/127, spec·quality 리뷰 이슈 0.

## Next Steps
- (구현 완료 후) HUMAN-VERIFY 4항목 확인 → 커밋·병합 여부는 별도 승인

## Open Questions
- [x] Q1. 재생시간 취득 방법 → **재생 시 수집·캐시** (추천안 채택 — 숨은 프로브·Data API 기각)
- [x] Q2. 표시 위치 → **행 우측 열** (재생 버튼 왼쪽, 회색 소자)
- [x] Q3. 미수집 곡 표시 → **공란**
- [x] Q4. 헤더 총 합계 → **항목별만** (합계는 Deferred)
