# Plan: 플레이리스트 항목 화면 멜론 차트 스타일 개편 (FR-18)

**PRD**: docs/prd.md

## 요구 이해
- **원문 요청**: "플레이리스트를 이미지와 같은 스타일로 해줘" (첨부: 멜론 차트 스크린샷 — 순위·썸네일·곡정보·앨범·좋아요·듣기 컬럼 + 상단 알약 버튼 줄)
- **이해한 요구**: 플레이리스트 페이지의 **우측 항목 목록**을 멜론 차트형 UI로 재구성한다 — 상단 알약(pill) 버튼 줄(셔플듣기·전체듣기), 헤더 행, 행마다 순위 번호·영상 썸네일·곡정보(제목+채널명)·행 재생 버튼, 행 사이 얇은 구분선. 이를 위해 유튜브 메타데이터(제목·채널명·썸네일)를 oEmbed로 확보해 항목에 캐시하고, 특정 항목부터 재생하는 기능을 추가한다. 색은 하드코딩하지 않고 앱 테마(라이트/다크)를 추종하되 레이아웃·구조는 이미지와 동일하게 한다 (질문 라운드 확정).
- **포함하지 않는 것으로 이해**: 멜론 그린 색·흰 배경 고정(테마 추종으로 대체), 앨범·좋아요·체크박스·순위등락 컬럼(데이터·대응 기능 없음), 좌측 리스트 목록 패널 재설계(현행 유지).

## Goal
플레이리스트 항목 목록이 멜론 차트처럼 보이고(순위·썸네일·곡정보·행 재생), 셔플듣기·전체듣기·항목부터 듣기가 된다.

## PRD Coverage
| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-18 | Must | T1~T6 | ✅ 커버 |
| FR-1~FR-17, NFR-1~6 | Must/Should | 이번 범위 외 (기구현) | ✅ 기구현 — 본 plan은 FR-18만 커버 대상으로 선언 |

## Out of Scope
- 멜론 이미지의 담기/다운/FLAC/선물 버튼 — 이 앱에 대응 기능 없음 (AGENTS: 존재하는 기능만)
- 앨범·좋아요 컬럼 — oEmbed는 제목·채널명만 제공, 좋아요 수는 YouTube Data API 키 필요 (PRD: 외부 서비스 계정 없음)
- 체크박스 컬럼·일괄 선택 — 대응 기능(일괄 담기/삭제) 없음
- 순위 등락 표시(－ 아이콘) — 차트가 아니므로 개념 없음
- YouTube Data API 기반 상세 메타데이터 (조회수·길이 등)

## Deferred / Follow-up
- 홈 화면(HomeViewModel)의 현재 재생 정보에도 제목·썸네일 표시 — 이번엔 플레이리스트 페이지만 (메타데이터 인프라(T1·T2)가 생기므로 후속 작업이 쉬워짐)
- [MINOR, T2 quality m1] VideoMetadataService 클래스 요약에 "단, 호출측 취소는 예외로 전파" 구절 보강 (주석 정밀화)
- [MINOR, T2 quality m2] VideoMetadataService의 Result.Fail 메시지를 프로젝트 관례(서술형 한글)로 통일 — 현재 로그 전용·UI 미노출이라 무해, UI 노출 시 필수

## Investigation Log
- 위키 참조: vault 미설정 여부 미확인이나 프로젝트 허브 규약은 세션 훅 컨텍스트로 갈음, 본 plan은 코드 1차 출처로 진행
- `Models/Playlist.cs` Read — PlaylistItem은 Url·VideoId만 보유. 제목·채널·썸네일 필드 없음
- `Views/PlaylistsPage.xaml` Read — 우측 항목은 URL 텍스트 + 위/아래/삭제 버튼 행. 드래그 정렬 지원(CanReorderItems)
- `ViewModels/PlaylistsViewModel.cs` Read — Items는 `ObservableCollection<PlaylistItem>`(뷰 전용, Id로 모델과 동기화), PlaylistEntry 래퍼 패턴 기존재. RefreshItems/SyncOrderFromViewAsync/_syncingItems 가드 구조 확인
- `Views/PlaylistsPage.xaml.cs` Read — 다이얼로그·드래그·행 버튼 위임 구조, DataContext 캐스팅 2곳(PlaylistItem)
- `Services/PlaybackCoordinator.cs` StartAsync(84)·SetModeAsync(244) Read — StartAsync는 리스트 단위만, SetModeAsync는 설정 영속까지 수행(셔플듣기에 재사용 가능)
- `Services/PlaybackQueue.cs` Read — Start()는 모드별 첫 항목 결정. 시작 항목 지정 개념 없음
- grep `StartAsync(` (src 전수) — Coordinator.StartAsync 호출자 4곳: `App.xaml.cs:138`, `ViewModels/HomeViewModel.cs:79`, `ViewModels/PlaylistsViewModel.cs:349`, `Services/TrayIconService.cs:142` (전건 문맥 확인 — 모두 `StartAsync(playlist.Id)` 형태, 선택 매개변수 추가 시 무변경)
- grep `PlaylistItem` (repo 전수 .cs, 19 hits) — 사용처: PlaylistsViewModel·PlaylistsPage.xaml.cs·PlaybackQueue·PlaybackCoordinator.LoadAll·PlaylistLibrary.AddItem·JsonStateStore(직렬화)·테스트 2파일(PlaybackQueueTests, JsonStateStoreTests). 필드 추가는 additive — 기존 참조 무영향
- `Services/JsonStateStore.cs` Read — System.Text.Json 소스 생성(StateJsonContext). 새 프로퍼티는 자동 직렬화 포함, 구 JSON에 필드 없으면 기본값 역직렬화(하위 호환 확인)
- `Services/AppServices.cs` Read — 수동 컴포지션 루트(DI 컨테이너 없음 — deferred.md 대기 항목 유지). 새 서비스는 CreateAsync에 수동 배선
- grep `HttpClient|WebRequest` (src 전수) — 0건. 이 앱 최초의 직접 HTTP 호출이 됨 (승인 필요 → 사전 승인 항목)
- grep `oembed|thumbnail|ytimg` (src) — 메타데이터 관련 기존 구현 없음
- `docs/prd.md` Read — FR-18 신설 승인(질문 라운드) 후 갱신 완료. NFR-6 상충 없음 확인(변경 이력 기재)
- `docs/plans/deferred.md` Read — 이번 작업 관련 항목 없음(DI 컨테이너 항목은 수동 배선 유지로 무관, "마지막 재생→StartAsync 공통 헬퍼 3회째" 항목은 이번에 호출부 시그니처 무변경이라 미발동)
- `tests/DeskTube.Tests/` 목록 확인 — PlaybackQueueTests·PlaybackCoordinatorTests·JsonStateStoreTests·PlaylistLibraryTests 존재
- oEmbed 엔드포인트: `https://www.youtube.com/oembed?url=<영상URL>&format=json` — API 키 불필요, 응답 JSON에 `title`·`author_name` 포함. 썸네일: `https://i.ytimg.com/vi/<videoId>/mqdefault.jpg` (320×180). ⚠️ 응답 스키마는 구현 시(T2) 실제 응답 1건으로 필드명 확인 후 파서 확정 (진행 방식만 바꾸는 확인 — 설계 성립에는 영향 없음: 실패 시 URL 폴백 경로가 이미 설계에 있음)

## Risks & Unknowns
| 위험 | 영향 | 완화책 |
|---|---|---|
| oEmbed 응답 지연·실패(오프라인, 비공개/삭제 영상 401·404) | 곡정보가 비어 보임 | 실패 시 URL 폴백 표시 + 기존 기능(추가·재생) 정상 동작, 타임아웃 5초 |
| 대량 항목(최대 1000개) backfill 시 요청 폭주 | UI 멈춤·네트워크 부하 | 병렬 4개 제한(SemaphoreSlim) + 이미 메타 있는 항목 skip + 페이지 이탈 시 취소 |
| 구 playlists.json(메타 필드 없음) 로드 | 크래시·소실 우려 | STJ 기본값 역직렬화로 하위 호환 (T1 왕복 테스트로 고정) |
| Items 컬렉션 타입 변경이 드래그 정렬 로직과 충돌 | 정렬 꼬임 | 기존 _syncingItems 가드·Id 동기화 구조 유지, Entry가 Id를 노출 |
| 셔플 시작 항목 지정이 "전곡 1회 순회" 계약과 충돌 | 셔플 회귀 | 시작 항목을 순열 첫 위치로 고정하는 방식(기존 BuildShuffleCycle 재활용), 기존 테스트 전건 통과 확인 |

## Impact Analysis
### 4-A. 심볼/타입 추적 결과
| 심볼 | 영향 받는 파일 | 영향 종류 |
|---|---|---|
| `PlaybackCoordinator.StartAsync` | App.xaml.cs:138 · HomeViewModel.cs:79 · PlaylistsViewModel.cs:349 · TrayIconService.cs:142 | 선택 매개변수 추가(기본 null) — 호출부 4곳 전건 무변경 (전건 Read 확인) |
| `PlaybackQueue.Start` | PlaybackCoordinator.cs:136 · PlaybackQueueTests.cs | 선택 매개변수 추가 — 기존 호출 무변경, 테스트 추가 |
| `PlaylistItem` (필드 추가) | PlaylistsViewModel · PlaylistsPage.xaml(.cs) · PlaybackQueue · PlaybackCoordinator.LoadAll · PlaylistLibrary.AddItem · JsonStateStore · PlaybackQueueTests · JsonStateStoreTests | additive 필드(Title·ChannelName) — 기존 참조 무영향, 직렬화 자동 포함 |
| `PlaylistsViewModel.Items` (요소 타입 변경) | PlaylistsPage.xaml(ItemsSource·DataTemplate) · PlaylistsPage.xaml.cs(OnItemsCollectionChanged·MoveItemAsync·OnRemoveItemClick 캐스팅) · PlaylistsViewModel 내부(RefreshItems·SyncOrderFromViewAsync + **`RemoveItemAsync(PlaylistItem)`:260·`MoveItemAsync(PlaylistItem)`:275 파라미터도 Entry로 동반 변경** — View 캐스팅과 쌍) | `PlaylistItem` → `PlaylistItemEntry` — 소비처는 View 1쌍뿐(전건 T4/T5 Files에 포함) |
| `AppServices` (프로퍼티 추가) | App.xaml.cs 등 AppServices 소비처 | additive — 기존 프로퍼티 무변경 |

### 4-B. 계약·직렬화 변경
- `playlists.json`에 항목별 `Title`·`ChannelName` 필드 추가 — 구 파일(필드 없음) 로드 시 STJ가 기본값(string.Empty)으로 역직렬화(하위 호환), 신 파일을 구 빌드가 읽어도 미지 필드 무시(전방 호환). 마이그레이션 불필요.
- `StartAsync`·`Start` 시그니처: 선택 매개변수 추가라 소스 호환(전 호출부 동시 재컴파일되는 단일 앱 — 바이너리 호환 무관).

### 4-C. 테스트 파일
- `tests/DeskTube.Tests/PlaybackQueueTests.cs` — Start(startItemId) 모드별 케이스 추가
- `tests/DeskTube.Tests/JsonStateStoreTests.cs` — 메타 필드 포함 왕복 + 구형 JSON(필드 결손) 로드 케이스 추가
- `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs` — 기존 StartAsync 테스트 회귀 확인(무수정 통과 예상)
- 신규: `tests/DeskTube.Tests/VideoMetadataParsingTests.cs` — oEmbed JSON 파싱(정상/필드 결손/비정상 JSON)

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| `VideoMetadataService` | grep HttpClient/oembed/thumbnail → 0건 | 기존 구현 없음 — 신규 |
| `PlaylistItemEntry` (뷰 래퍼) | `PlaylistEntry`(PlaylistsViewModel.cs:11) 동일 패턴 기존재 | 같은 파일의 기존 래퍼 **패턴을 복제** (대상이 달라 코드 재사용은 불가) |
| pill 버튼 스타일 | `Resources/` 공용 토큰 사전 부재(Glob 0건), 표준 `AccentButtonStyle`만 존재 | 전체듣기는 기존 `AccentButtonStyle` 재사용(+CornerRadius), 페이지 리소스에 pill 스타일 1개만 정의(신규 사유: 공용 사전 자체가 없음 — 화면 1곳 사용이라 페이지 리소스가 적정, AGENTS 규칙 2의 토큰 분리는 색·간격 값에만 해당하며 본 작업은 ThemeResource 표준 키만 사용) |
| `ShuffleAllCommand`/`PlayAllCommand`/행 재생 | 기존 `PlayCommand`(PlaylistsViewModel.cs:341) | PlayCommand 로직(오류 안내 포함)을 공통 헬퍼로 추출해 3진입점이 공유 |

### Verified by
- grep "StartAsync(" src 전수 → 호출자 4곳, 모두 위 표에 포함
- grep "PlaylistItem" repo .cs 전수 → 19 hits, 사용 파일 전부 위 표·Files에 포함
- grep "HttpClient|WebRequest" src → 0건 (외부 HTTP 신설 확인)

## Decisions
### D1. 메타데이터 확보 방식
- **Options**: A) oEmbed+썸네일 URL / B) 썸네일만 / C) 레이아웃만
- **Chosen**: A — oEmbed(제목·채널명, API 키 불필요)를 항목에 캐시(JSON 영속화), 썸네일은 `i.ytimg.com` URL을 Image에 바인딩
- **Rationale**: 이미지 스타일(곡정보·썸네일)을 실제로 채우는 근본 해결. 실패 시 URL 폴백으로 기능 훼손 없음
- **Source**: 사용자 확정 (질문 라운드 1)

### D2. 색·테마
- **Chosen**: 테마 추종 — 레이아웃·구조(순위/썸네일/곡정보/구분선/pill)는 멜론과 동일, 색은 ThemeResource 표준 키 + 재생 버튼은 앱 액센트 색. 색 하드코딩 0
- **Rationale**: FR-17(테마)·AGENTS 디자인 규칙(하드코딩 금지·시스템 키 우선) 준수, 다크 테마 안 깨짐
- **Source**: 사용자 확정 (질문 라운드 1)

### D3. 행 재생 버튼
- **Chosen**: `StartAsync(Guid playlistId, Guid? startItemId = null)` 확장으로 해당 항목부터 재생. 기본값 null = 현행 동작
- **Rationale**: "이 곡 듣기" 기대에 부합하는 근본 해결. 선택 매개변수라 호출부 4곳 무변경
- **Source**: 사용자 확정 (질문 라운드 1) — 사전 승인 항목 등재

### D4. pill 버튼 구성
- **Chosen**: 셔플듣기 + 전체듣기 2개. 셔플듣기 = `SetModeAsync(Shuffle)` 후 `StartAsync` (재생 모드 설정에 영속 — 기존 SetModeAsync가 저장까지 수행, PlaybackCoordinator.cs:244 확인). 전체듣기 = 현재 재생 모드로 `StartAsync` (기존 PlayCommand와 동일). 담기/다운/FLAC/선물은 미구현 (Out of Scope)
- **Source**: 사용자 확정 (질문 라운드 1)

### D5. 적용 범위
- **Chosen**: 좌측 리스트 목록 패널 현행 유지, 우측 항목 영역만 재구성
- **Source**: 사용자 확정 (질문 라운드 2)

### D6. 행 조작 버튼
- **Chosen**: 위/아래/삭제를 행의 우클릭 컨텍스트 메뉴로 이동(좌측 리스트의 기존 패턴과 동일), 행에는 재생 버튼만. 드래그 정렬은 유지
- **Source**: 사용자 확정 (질문 라운드 2)

### D7. 데이터 없는 컬럼 처리
- **Chosen**: 앨범·좋아요·체크박스·순위등락 생략. 컬럼 = 순위 | 썸네일+곡정보(제목·채널) | 듣기
- **Rationale**: oEmbed는 title·author_name만 제공, 좋아요는 Data API 키 필요(외부 서비스 도입 불가), 일괄 선택 기능 없음 — 데이터·기능이 존재하지 않아 자체 확정
- **Source**: grep·oEmbed 스펙 (Investigation Log)

### D8. 썸네일 표현
- **Chosen**: 정사각 56×56, `Stretch=UniformToFill`(16:9 원본 중앙 크롭), CornerRadius 4
- **Rationale**: 이미지(정사각 앨범아트)와 동일한 실루엣 — 유튜브 썸네일은 16:9뿐이라 크롭이 왜곡 없는 유일한 정사각 표현
- **Source**: 첨부 이미지 실측(Step 2.5 표)

### D9. 항목 표시 모델
- **Chosen**: `Items`를 `ObservableCollection<PlaylistItemEntry>`로 전환 — Entry는 Id·Url·VideoId + [ObservableProperty] Rank·Title·ChannelName + 파생 ThumbnailUrl·표시 텍스트(제목 없으면 URL 폴백)
- **Rationale**: 순위 표시(ListView는 인덱스 바인딩 미제공)와 비동기 메타 도착 시 행 갱신(INPC)을 함께 해결. 같은 파일의 PlaylistEntry 래퍼 패턴 복제
- **Source**: PlaylistsViewModel.cs:11 기존 패턴

### D10. 메타데이터 채움 시점·정책
- **Chosen**: ① 항목 추가 직후 해당 1건 조회 → 성공 시 모델 반영+SaveAsync ② 리스트 선택/페이지 로드 시 Title 없는 항목만 backfill(병렬 4 제한, 완료 후 SaveAsync 1회, 페이지 이탈·리스트 전환 시 CancellationToken 취소) ③ 실패는 조용히 URL 폴백(재시도는 다음 진입 시 자연 발생)
- **Rationale**: 추가 즉시 보이는 반응성 + 기존 데이터 소급 + 폭주 방지
- **Source**: FR-6 상한(1000개) 대비 (Risks 표)

### D11. HTTP·파싱 구조
- **Chosen**: `HttpClient`는 서비스 내 static 단일 인스턴스(소켓 고갈 방지), 타임아웃 5초. oEmbed JSON 파싱은 순수 정적 메서드 `TryParse(json, out title, out channel)`로 분리해 네트워크 없이 단위 테스트 — `JsonDocument` 직접 파싱(필드 2개뿐이라 DTO·소스 생성 컨텍스트 등록 불필요, plan-reviewer m2 반영)
- **Rationale**: .NET HttpClient 권장 사용법 + 테스트 가능성 (프로젝트에 목 프레임워크 없음 — 파싱만 검증)
- **Source**: AGENTS 테스트 컨벤션(xUnit) + JsonStateStore의 소스 생성 선례

## 시각 요소 분해
| 요소 | 속성 | 디자인 값 | 확인 방법 |
|------|------|----------|-----------|
| pill 버튼 줄 | 형태 | 완전 라운드(pill, 높이의 1/2 CornerRadius), 아이콘+텍스트, 외곽선 버튼 | 첨부 스크린샷 상단 |
| pill 버튼 줄 | 배치 | 목록 위 가로 1줄, 좌측 정렬, 간격 ~8px | 첨부 스크린샷 |
| 헤더 행 | 구성 | 컬럼 라벨(순위 / 곡정보 / 듣기), 위·아래 얇은 구분선 | 첨부 스크린샷 (앨범·좋아요는 D7로 생략) |
| 헤더 행 | 색 | 본문보다 옅은 보조 텍스트 색 (`TextFillColorSecondaryBrush`) | 첨부 스크린샷 |
| 행 | 세로 패딩 | 넉넉한 상하 여백(행 높이 ~72px 상당) | 첨부 스크린샷 비율 |
| 순위 | 타이포 | 제목보다 큰 숫자(SubtitleTextBlockStyle 상당), 세로 중앙 | 첨부 스크린샷 |
| 썸네일 | 크기·형태 | 정사각 56×56, 라운드 4px, UniformToFill 크롭 | 첨부 스크린샷 + D8 |
| 곡정보 | 구조 | 제목(본문, SemiBold) 위 + 채널명(캡션, 보조 색) 아래 2줄, 말줄임 | 첨부 스크린샷 |
| 구분선 | 형태 | 행 사이 1px 수평선 (`DividerStrokeColorDefaultBrush`) | 첨부 스크린샷 |
| 행 재생 버튼 | 형태 | 행 우측 끝 원형 재생(▷) 아이콘 버튼, 액센트 색(멜론 그린 대체 — D2) | 첨부 스크린샷 |
| 배경 | 색 | ThemeResource 기본(라이트에서 흰 배경 상당 — D2, 하드코딩 없음) | 첨부 스크린샷 + FR-17 |

## Tasks
- [x] T1. PlaylistItem 메타데이터 필드 + 직렬화 하위 호환 (FR-18)
  - **Type**: C
  - **Acceptance**: Given 구형 playlists.json(Title 필드 없음), When 로드, Then 기본값(빈 문자열)으로 역직렬화 성공 / Given Title·ChannelName 채운 항목, When 저장→재로드, Then 왕복 일치 (단위 테스트)
  - **Files**:
    - 주: `src/DeskTube/Models/Playlist.cs` (PlaylistItem에 `Title`·`ChannelName` string 프로퍼티, 기본 string.Empty)
    - 테스트: `tests/DeskTube.Tests/JsonStateStoreTests.cs` (왕복 + 필드 결손 JSON 케이스)
  - **Edge Cases**: 필드 결손 구형 JSON → 기본값 / null 역직렬화 방어(기본값 초기화)
  - **Halt Forecast**: 없음 (additive 필드, 파괴 없음)
  - **Depends on**: -

- [x] T2. VideoMetadataService 신설 (oEmbed) + AppServices 배선 (FR-18)
  - **Type**: D
  - **Design**: ① `src/DeskTube/Services/VideoMetadataService.cs` 단일 파일 ② `VideoMetadataService` — oEmbed GET·타임아웃·`Result<VideoMetadata>` 반환 1책임 + 정적 `TryParse`(JSON→제목·채널) ③ AppServices가 생성·보유, PlaylistsViewModel이 소비. 서비스는 Models(Result)만 참조 ④ 인터페이스(IVideoMetadataService) 추상화·재시도 정책·캐시 계층은 이번에 만들지 않음 (소비자 1곳 — YAGNI, 기존 수동 배선 관례상 구체 타입 직접 보유도 선례 있음: PlaylistLibrary·PlaybackCoordinator)
  - **Acceptance**: Given 정상 oEmbed JSON, When TryParse, Then 제목·채널 추출 / Given 필드 결손·비정상 JSON, When TryParse, Then false(예외 없음) — 단위 테스트. 네트워크 실패·타임아웃 시 Result 실패 반환(예외 전파 없음)은 코드 리뷰로 확인
  - **Files**:
    - 주: `src/DeskTube/Services/VideoMetadataService.cs` (신규)
    - 동반: `src/DeskTube/Services/AppServices.cs` (프로퍼티+배선)
    - 테스트: `tests/DeskTube.Tests/VideoMetadataParsingTests.cs` (신규)
  - **Edge Cases**: 타임아웃(5초)·오프라인 → Result 실패 / 404·401(삭제·비공개 영상) → Result 실패 / 비정상·빈 JSON → TryParse false / 취소 토큰 전달(호출측 취소 지원)
  - **Halt Forecast**:
    - (ii-a) 외부 HTTP GET 신설(youtube.com/oembed) → `## 사전 승인 항목` 1 등재
    - (i) oEmbed 실제 응답 필드명 확인 → 구현 시 1건 실호출로 확인, 어긋나도 파서만 조정(설계 불변 — Investigation Log 말미)
  - **Depends on**: T1

- [ ] T3. StartAsync·PlaybackQueue 시작 항목 지정 (FR-18)
  - **Type**: D
  - **Design**: 해당 없음 — 신규 심볼 없이 기존 공개 메서드 2개(`PlaybackCoordinator.StartAsync`, `PlaybackQueue.Start`)에 선택 매개변수 `Guid? startItemId = null`만 추가 (구조 변화 없음)
  - **Acceptance**: Given 5곡 리스트, When Start(3번째 Id) — 순차/RepeatAll, Then 3번째부터 시작 / 셔플, Then 첫 곡이 지정 항목이고 사이클이 전곡 1회 순회 / Random, Then 첫 곡이 지정 항목 / Given 존재하지 않는 Id, When Start, Then 기존 동작(모드별 기본 첫 곡) — 전부 단위 테스트 + 기존 PlaybackQueue·Coordinator 테스트 전건 무수정 통과
  - **Files**:
    - 주: `src/DeskTube/Services/PlaybackQueue.cs`, `src/DeskTube/Services/PlaybackCoordinator.cs`
    - 테스트: `tests/DeskTube.Tests/PlaybackQueueTests.cs` (추가), `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs` (회귀 확인)
  - **Edge Cases**: startItemId 미존재(직전 삭제) → 무시하고 기본 시작 / 빈 목록 → 기존 실패 경로 유지 / RepeatOne + 시작 지정 → 그 곡 반복
  - **Halt Forecast**:
    - (ii-a) 공개 메서드 시그니처 확장(계획된 변경, 호출부 4곳 무변경) → `## 사전 승인 항목` 2 등재
  - **Depends on**: -

- [ ] T4. PlaylistsViewModel 개편 — Entry 래퍼·메타 채움·재생 커맨드 (FR-18)
  - **Type**: D
  - **Design**: ① `ViewModels/PlaylistsViewModel.cs` 안에 `PlaylistItemEntry` 추가(같은 파일의 PlaylistEntry 선례) ② `PlaylistItemEntry` — 항목 1건의 표시 상태(Rank·Title·ChannelName·ThumbnailUrl·폴백 텍스트) 보유 (D9) ③ VM이 VideoMetadataService(T2)·Coordinator(T3 확장)를 소비, View는 Entry만 바인딩 ④ 메타 캐시 계층·백그라운드 큐 서비스는 만들지 않음 — VM 내 backfill 메서드 1개로 충분 (YAGNI)
  - **Acceptance**: Given 메타 없는 기존 항목, When 리스트 선택, Then Title 없는 항목만 조회돼 행이 순차 갱신되고 저장은 1회 / Given 항목 추가, When 조회 성공, Then 즉시 제목·채널 표시+영속화, 실패해도 항목 추가는 성공(URL 표시) / When 셔플듣기, Then 재생 모드가 Shuffle로 저장되고 재생 시작 / When 행 재생, Then 해당 항목부터 시작 — 로직은 빌드+기존 테스트, 동작은 HUMAN-VERIFY
  - **Files**:
    - 주: `src/DeskTube/ViewModels/PlaylistsViewModel.cs`
    - 동반: `src/DeskTube/Views/PlaylistsPage.xaml.cs` (DataContext 캐스팅 2곳 `PlaylistItem`→`PlaylistItemEntry`, OnItemsCollectionChanged 유지)
  - **Edge Cases**: backfill 중 리스트 전환·페이지 이탈 → CancellationToken 취소(늦은 응답이 다른 리스트에 반영되지 않게 Entry 참조로 갱신) / 1000개 리스트 → 병렬 4 제한 / 드래그 정렬 후 → Rank 재계산(SyncOrderFromViewAsync 말미) / 메타 도착과 항목 삭제 경합 → 삭제된 Entry 갱신은 무해(컬렉션 밖 객체)
  - **Halt Forecast**:
    - (ii-a) 공개 프로퍼티 `Items` 요소 타입 변경(소비처 View 1쌍뿐, 계획된 변경) → `## 사전 승인 항목` 3 등재
  - **Depends on**: T1, T2, T3

- [ ] T5. PlaylistsPage.xaml 멜론 차트 스타일 + resw (FR-18)
  - **Type**: D
  - **Design**: ① `Views/PlaylistsPage.xaml` 우측 영역만 재구성(D5) ② 신규 심볼 없음 — XAML 구조 변경 + 페이지 리소스 pill 스타일 1개(4-D) ③ Entry(T4)에 x:Bind ④ 공용 스타일 사전·커스텀 컨트롤은 만들지 않음 (화면 1곳 — YAGNI)
  - **Acceptance**: 빌드 경고 0 + `## 시각 요소 분해` 표 전 항목이 구현에 존재(V-9 대조) — 시각 적정성은 ⏳ HUMAN-VERIFY / 문구 하드코딩 0(전부 x:Uid·resw en/ko 양쪽) / 위/아래/삭제가 컨텍스트 메뉴로 동작
  - **Files**:
    - 주: `src/DeskTube/Views/PlaylistsPage.xaml`
    - 동반: `src/DeskTube/Views/PlaylistsPage.xaml.cs` (컨텍스트 메뉴 핸들러 연결 — 기존 핸들러 재사용), `src/DeskTube/Strings/en-US/Resources.resw`, `src/DeskTube/Strings/ko-KR/Resources.resw` (셔플듣기·전체듣기·헤더 3라벨·메뉴 3항목·행 재생 접근성 이름)
  - **Edge Cases**: 좁은 창 폭 → 곡정보 열 `*` + TextTrimming / 썸네일 로드 실패·로드 전 → 빈 자리 배경(`ControlFillColorDefaultBrush`) 표시 / 항목 0개 리스트 → 기존 NoSelectionText·빈 목록 동작 유지 / 접근성 — 행 재생 버튼 AutomationProperties.Name 필수(AGENTS)
  - **Halt Forecast**: 없음 (XAML·리소스 — 파괴·외부·시그니처 없음)
  - **Depends on**: T4

- [ ] T6. 문서 갱신 — README (FR-18)
  - **Type**: A
  - **Acceptance**: README 핵심 기능에 차트형 플레이리스트(메타데이터 표시·셔플/전체듣기·항목부터 재생, FR-18) 반영 — 존재하는 기능만 기재
  - **Files**:
    - 주: `README.md`
  - **Depends on**: T5

## 사전 승인 항목 (일괄 승인 대상)
1. T2 — **외부 HTTP GET 신설**: `https://www.youtube.com/oembed`(제목·채널 조회)·`https://i.ytimg.com`(썸네일 이미지). 앱 최초의 직접 HTTP 호출. 영상 ID 외 개인정보 미전송·로그인 쿠키 미사용 (PRD 변경 이력에 NFR-6 상충 없음 기재). 패키지 의존성 추가는 없음(System.Net.Http는 BCL)
2. T3 — **공개 API 시그니처 확장**: `PlaybackCoordinator.StartAsync`·`PlaybackQueue.Start`에 선택 매개변수 `Guid? startItemId = null` 추가 — 호출부 4곳 무변경(비파괴)
3. T4 — **공개 프로퍼티 요소 타입 변경**: `PlaylistsViewModel.Items`를 `ObservableCollection<PlaylistItemEntry>`로 — 소비처는 PlaylistsPage 1쌍뿐(Impact 4-A 전수 확인)

## 불가피한 Halt (위임 불가)
- push·main 병합·태그·릴리즈·PR — 구현·검증 완료 후 최종 보고에서 별도 승인

## Verification Strategy
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` — 경고/에러 0
- 단위 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj`
- 수동 검증 (HUMAN-VERIFY): 플레이리스트 페이지 시각 스타일(시각 요소 분해 표 대조)·메타데이터 표시·셔플/전체/행 재생 동작 — 빌드 통과가 레이아웃 적정성을 보장하지 않음(AGENTS XAML 규칙 8)

## Phase Ledger

## Retry Ledger

## Progress Log

## Next Steps
- 사용자 승인 후 `pjc:implement-task`로 T1부터 자율 실행

## Open Questions
- [x] Q1. 메타데이터 확보 방식 → **A) oEmbed+썸네일** (질문 라운드 1)
- [x] Q2. 색·테마 처리 → **테마 추종** (질문 라운드 1)
- [x] Q3. 행 재생 버튼 동작 → **해당 항목부터 재생 (API 확장)** (질문 라운드 1)
- [x] Q4. pill 버튼 구성 → **셔플듣기+전체듣기 2개** (질문 라운드 1)
- [x] Q5. 좌측 패널 → **현행 유지** (질문 라운드 2)
- [x] Q6. 행 조작 버튼 → **컨텍스트 메뉴로 이동** (질문 라운드 2)
- [x] Q7. PRD 갱신 → **FR-18 신설 승인** (질문 라운드 2, PRD 갱신 완료)
