# Plan: 자막 표시 on/off 토글

**PRD**: docs/prd.md

## 요구 이해
- **원문 요청**: "유튜브 재생시 자막이 표시 되는데 자막 기능 on/off 설정할 수 있나?" → "계획 세워서 진행해줘"
- **이해한 요구**: 설정 페이지에 "자막" ToggleSwitch를 추가하고, 켬=자막 강제 표시 / 끔=자막 강제 숨김으로 앱이 직접 제어한다(유튜브 계정의 자막 선호와 무관). 설정은 영속화되어 재시작 후 유지되고, 재생 중 토글하면 전 모니터 플레이어에 즉시 반영된다. 기본값은 끔 — 현재 "자막이 계정 선호 때문에 표시되는" 문제가 업데이트 즉시 해결된다(사용자 확정).
- **포함하지 않는 것으로 이해**: 자막 언어 선택·글꼴/크기 조정은 포함하지 않는다(on/off만 — 사용자 원문 명시).

## Goal
설정의 "자막" 토글 하나로 배경 재생 영상의 자막 표시를 켜고 끌 수 있다 (기본 끔, 영속화, 재생 중 즉시 반영).

## PRD Coverage
| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-20 (T1에서 신설) | Must | T1(신설), T2, T3 | ✅ 커버 |
| FR-10 (설정 화면 항목에 "자막" 추가) | Must | T1(보강), T3 | ✅ 커버 (항목 추가분만) |
| FR-1~FR-9, FR-11~FR-16, FR-18, FR-19, NFR-1~6 (active) | Must/Should | (없음) | 이번 범위 외 (기구현) |

## Out of Scope
- 자막 언어 선택, 자막 스타일(글꼴·크기·배경) 조정 — on/off만 (사용자 원문 명시)
- "계정 선호 따름" 3-상태(자동) 모드 — 2상태 토글로 확정 (D1)

## Deferred / Follow-up
- (없음 — 계획 시점)

## Investigation Log
- 위키 참조: 관련 위키 자료 없음 — 코드 1차 출처로 진행 (직전 plan들에서 vault 무매칭/미설정 확인 이력, 프로젝트 국소 변경).
- Deferred 대장 확인(docs/plans/deferred.md ## 대기): 자막 관련 항목 없음 — 신규 계획.
- 이전 plan(2026-07-17-toast-notices) Deferred: "(없음 — 계획 시점)" 확인 — 이관할 잔여 없음.
- player.html 전문 Read: playerVars에 자막 파라미터 없음(cc_load_policy 미지정) → 자막 표시는 유튜브 계정/쿠키 선호를 따르는 상태. C#→JS 명령은 `cmd.Type` switch(execute)로 처리, ready 전 명령은 `pending` 큐가 보관 후 onReady에서 재생(기존 인프라 재사용 가능).
- IFrame API에 자막 강제 끄기 공식 playerVars 없음(`cc_load_policy: 1`은 강제 켜기만) → `loadModule('captions')`/`unloadModule('captions')`가 사실상 표준 우회(비공식 API — 사용자에게 사전 고지·동의됨). 모듈명은 HTML5 플레이어 'captions', 구형 'cc' 병용이 방어적.
- PlayerHost.cs 전문 Read: 명령은 `PlayerCommand` record → `PostCommand`(JSON 직렬화, `WhenWritingNull`) 경로. `SetFitMode` 등 기존 메서드와 동일 패턴으로 추가 가능.
- PlaybackCoordinator.cs 전문 Read: 플레이어 생성 지점은 3곳 — StartAsync(135행 부근), RecreatePlayerAsync(553행 부근), HandleMonitorsChangedCoreAsync(639행 부근). 각각 `SetQualityScale`+`SetFitMode`를 초기 적용 — 자막도 같은 자리에서 초기 적용. 설정 변경 메서드 패턴은 `SetFitModeAsync`(전 플레이어 전파 + 저장)가 동형.
- AppSettings.cs 전문 Read: "새 필드는 기본값과 함께 추가" 주석 확인. bool 필드는 Normalize 보정 불요. 기존 settings.json에 필드가 없으면 System.Text.Json이 기본값(false)으로 로드 — 하위 호환 안전.
- SettingsViewModel.cs 관련부 Read: 토글 패턴 2종 확인 — ① Coordinator 경유(OnReduceMirrorQualityChanged → `Apply(() => Coordinator.Set...Async(value))`) ② Store 직접(OnAutoPlayOnLaunchChanged). 자막은 재생 중 즉시 반영이 필요하므로 ①과 동형.
- SettingsPage.xaml Read: 그룹 구조 "화면(모니터·크기 모드·화질·미러 하향) / 소리 / 일반 / 계정". 자막 카드는 화면 그룹 끝(ReduceMirrorCard 다음)에 배치 (D6).
- resw 확인: SettingsCard 문구는 `{x:Uid}.Header`/`.Description` 키 쌍(ko-KR·en-US 양쪽) — MuteCard·AutoPlayCard 패턴 확인.
- tests Read: FakePlayer(PlaybackCoordinatorTests.cs 62행)가 IPlayerHost 전 메서드 구현 — 메서드 추가 시 갱신 필수. JsonStateStoreTests 왕복 테스트(33행)가 AppSettings 전 필드 대조 — 필드 추가분 반영.
- PRD 경량 확인: FR-10(설정 화면 항목 나열)에 닿음 → FR-20 신설 + FR-10 보강으로 사용자 확정(질문 라운드). `**PRD**:` 줄 연결로 Phase G 활성화(커버 대상은 FR-20·FR-10 추가분만).

## Risks & Unknowns
| 위험 | 영향 | 완화책 |
|---|---|---|
| `loadModule`/`unloadModule`은 IFrame API 비공식(레퍼런스 미기재) — 유튜브가 예고 없이 변경 가능 | 자막 제어 무동작 (재생 자체는 무영향) | JS에서 함수 존재 확인 + try/catch. 실패해도 재생 흐름에 영향 0. 사용자에게 비공식 API임을 사전 고지·동의됨 |
| `loadVideoById` 후 자막 모듈 상태가 유지되는지 불확실 | 곡 전환 시 자막 설정 풀림 | JS가 `desiredCaptions`를 보관하고 load 명령 처리 시마다 재적용 (D5) |
| 자막 실표시/숨김은 빌드·단위테스트로 검증 불가 | 시각 결함 미검출 | HUMAN-VERIFY로 명시 — 자막 있는 영상에서 토글 확인 |

## Impact Analysis
### 4-A. 심볼/타입 추적 결과
| 심볼 | 영향 받는 파일 | 영향 종류 |
|---|---|---|
| `IPlayerHost` (메서드 추가) | `src/DeskTube/Services/IPlayerHost.cs` | 인터페이스 선언 추가 |
| 〃 구현체 ① | `src/DeskTube/Services/PlayerHost.cs` | `SetCaptionsEnabled` 구현 (PostCommand) |
| 〃 구현체 ② | `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs` (FakePlayer) | `SetCaptionsEnabled` 구현 (Commands 기록) |
| 〃 호출자 | `src/DeskTube/Services/PlaybackCoordinator.cs` | 생성 3곳 초기 적용 + `SetCaptionsEnabledAsync` 신설 |
| `PlayerCommand` (필드 추가) | `src/DeskTube/Services/PlayerHost.cs` (record·직렬화 컨텍스트 동일 파일) | `bool? Enabled` 추가 — `WhenWritingNull`이라 기존 명령 페이로드 불변 |
| `AppSettings` (필드 추가) | `src/DeskTube/Models/AppSettings.cs` | `CaptionsEnabled` 추가 (기본 false) |
| 〃 소비자 | `src/DeskTube/ViewModels/SettingsViewModel.cs`, `tests/DeskTube.Tests/JsonStateStoreTests.cs` | 로드/저장·왕복 대조 추가 |
| player.html 브리지 | `src/DeskTube/Assets/player.html` | `captions` 명령 처리 + load 시 재적용 |
| 설정 화면 | `src/DeskTube/Views/SettingsPage.xaml`, `Strings/ko-KR·en-US/Resources.resw` | CaptionsCard 추가 |

### 4-B. 계약·직렬화 변경
- `IPlayerHost`에 `SetCaptionsEnabled(bool)` 추가 — 구현체 2개(PlayerHost·FakePlayer) 전수 갱신, 그 외 구현체 없음 (Verified by 참조). **계획된 공개 API 변경 → 사전 승인 항목 등록.**
- `PlayerCommand`에 `bool? Enabled` 추가 — `JsonIgnoreCondition.WhenWritingNull`이라 기존 명령의 JSON 형태 불변(하위 호환). JS 측은 새 `captions` 명령에서만 `cmd.Enabled`를 읽음.
- `AppSettings`에 `bool CaptionsEnabled` 추가 — 기존 settings.json에 키가 없으면 기본값 false로 로드(마이그레이션 불요, 클래스 주석의 "새 필드는 기본값과 함께 추가" 방침 그대로).

### 4-C. 테스트 파일
- `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs` — FakePlayer 메서드 추가 + 자막 명령 전파 테스트 추가
- `tests/DeskTube.Tests/JsonStateStoreTests.cs` — 왕복 테스트에 `CaptionsEnabled` 추가

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| `IPlayerHost.SetCaptionsEnabled(bool)` | grep "SetMuted\|SetFitMode" — 동형 명령 메서드 존재 | 명령 종류가 다르므로 신규 (패턴은 SetMuted 복제) |
| `PlaybackCoordinator.SetCaptionsEnabledAsync(bool)` | grep "SetFitModeAsync" — 전 플레이어 전파+저장 동형 | 설정 항목이 다르므로 신규 (패턴은 SetFitModeAsync 복제) |
| `SettingsViewModel.CaptionsEnabled` + `OnCaptionsEnabledChanged` | OnReduceMirrorQualityChanged — Apply+Coordinator 동형 | 신규 속성 (패턴 복제) |
| `CaptionsCard` (x:Uid) | SettingsCard+ToggleSwitch (MuteCard 등) 기존 공통 컨트롤 | **기존 컨트롤 재사용** — 신규 컨트롤 없음 |
| JS `desiredCaptions`/`applyCaptions()` | player.html `desiredMuted`/`desiredVolume` 상태 보관 패턴 | 신규 (패턴 복제 — 워치독 복원 기준과 동일 방식) |

### Verified by
- grep "IPlayerHost" 전 저장소 → 구현체 2개(PlayerHost, FakePlayer)·호출자 PlaybackCoordinator·factory 시그니처(AppServices)뿐 — 표에 전수 포함 (factory는 타입 참조만, 메서드 추가 영향 없음)
- grep "SetFitMode" → IPlayerHost 선언·PlayerHost 구현·FakePlayer 구현·PlaybackCoordinator 4곳(생성 3곳+SetFitModeAsync) — 자막 적용 지점의 기준 패턴으로 확인
- grep "ReduceMirrorQuality|AutoPlayOnLaunch|IsMuted" → 설정 토글의 전 경로(모델→VM→XAML→coordinator→테스트) 확인, 표와 일치
- grep "CaptionsEnabled|SetCaptionsEnabled|captions" src/ → 0건 (신규 심볼 충돌 없음)

## Decisions
### D1. 토글 의미 — 2상태 강제 제어
- **Options**: A) 켬=강제 표시/끔=강제 숨김 (2상태) / B) 켬=계정 선호 따름/끔=강제 숨김 / C) 자동·켬·끔 3상태
- **Chosen**: A
- **Rationale**: 토글 상태=화면 상태로 예측 가능. B는 켬인데 계정 선호가 꺼져 있으면 자막이 안 나와 혼란. C는 사용자가 on/off만 요청(원문). 사용자 질문 라운드에서 A 전제로 확정.
- **Source**: 사용자 답변 (질문 라운드 2026-07-17)

### D2. 기본값 — 끔
- **Options**: A) 끔 / B) 켬
- **Chosen**: A (끔)
- **Rationale**: 배경화면 용도에 부합 + 현재 "계정 선호로 자막이 표시되는" 문제가 업데이트 즉시 해결. 사용자 확정.
- **Source**: 사용자 답변 (질문 라운드 2026-07-17)

### D3. PRD 반영 — FR-20 신설
- **Options**: A) FR-20 신설 + FR-10 보강 / B) FR-10 보강만 / C) PRD 갱신 안 함
- **Chosen**: A
- **Rationale**: 새 설정 기능마다 FR 신설해 온 관례(FR-19 등)와 일치. 사용자 확정.
- **Source**: 사용자 답변 (질문 라운드 2026-07-17), docs/prd.md 변경 이력 관례

### D4. JS 모듈명 — 'captions'와 'cc' 병용
- **Options**: A) 'captions'만 / B) 'captions'+'cc' 둘 다 호출
- **Chosen**: B
- **Rationale**: HTML5 플레이어는 'captions', 구형 경로는 'cc' — 둘 다 호출해도 미존재 모듈은 무시되므로 무해하고 방어적.
- **Source**: Investigation Log (IFrame API 비공식 관행)

### D5. 재적용 시점 — JS 보관 + load 시 재적용 + 생성 시 1회 전송
- **Options**: A) C#이 곡 전환마다 재전송 / B) JS가 `desiredCaptions` 보관하고 load 명령 처리 시 재적용, C#은 플레이어 생성 시(및 토글 변경 시)만 전송
- **Chosen**: B
- **Rationale**: `desiredMuted` 워치독 복원과 동일 패턴 — 상태 보관 책임이 JS에 이미 존재. C# 곡 전환 경로(LoadAll)를 건드리지 않아 변경 최소.
- **Source**: player.html 31행 desiredMuted 패턴

### D6. 설정 카드 위치 — 화면 그룹 끝 (ReduceMirrorCard 다음)
- **Options**: A) 화면 그룹 (FitMode·화질과 동군) / B) 일반 그룹
- **Chosen**: A — ReduceMirrorCard 다음
- **Rationale**: 자막은 영상 위 시각 요소 — 크기 모드·화질과 같은 "화면" 범주. 그룹 끝 추가로 기존 카드 순서 불변.
- **Source**: SettingsPage.xaml 그룹 구조 (Investigation Log)

### D7. 명명 — `CaptionsEnabled` / 명령 `captions`
- **Options**: A) CaptionsEnabled / B) ShowCaptions / C) SubtitlesEnabled
- **Chosen**: A
- **Rationale**: 유튜브 용어(captions)와 일치, 기존 bool 설정 명명(AutoPlayOnLaunch 등 서술형)과 조화. 명령 문자열은 기존 소문자 단일어(mute·fit·scale) 관례.
- **Source**: AppSettings.cs·player.html 기존 명명

## Tasks
- [x] T1. PRD 갱신 — FR-20 신설 + FR-10 보강
  - **Type**: A
  - **Acceptance**: docs/prd.md에 ① FR-20 행(자막 표시 토글 — 켬=강제 표시/끔=강제 숨김, 기본 끔, Must, 검증: 단위테스트(명령 전파·영속화) + 자막 표시는 HUMAN-VERIFY) ② FR-10 항목 나열에 "자막" 추가 ③ 변경 이력 1줄(FR-20 신설 + FR-10 보강, plan 경로 포함) — 3곳 모두 존재
  - **Files**:
    - 주: `docs/prd.md`
  - **Edge Cases**: (Type A — skip)
  - **Halt Forecast**: (i) PRD는 승인 후 고정 문서 → plan 승인이 이 갱신(질문 라운드 확정분)의 승인을 포함 — `## 사전 승인 항목` 등록
  - **Depends on**: -

- [x] T2. 자막 명령 파이프라인 (설정 모델 → 코디네이터 → 플레이어 → JS)
  - **Type**: D
  - **Design**: ① 배치 — 기존 명령 파이프라인 그대로: Models(AppSettings)·Services(IPlayerHost/PlayerHost/PlaybackCoordinator)·Assets(player.html), 새 파일 없음. ② 신규 심볼 — `AppSettings.CaptionsEnabled`(bool, 기본 false — 영속 상태 정본), `IPlayerHost.SetCaptionsEnabled(bool)`(자막 명령 전송), `PlaybackCoordinator.SetCaptionsEnabledAsync(bool)`(설정 갱신+전 플레이어 전파+저장 — SetFitModeAsync 동형), JS `desiredCaptions`+`applyCaptions()`(상태 보관·재적용). ③ 의존 방향 — Coordinator→IPlayerHost→player.html 단방향(기존과 동일), VM은 T3에서 Coordinator만 참조. ④ 비추상화 — 자막 언어·3상태 확장 대비 추상화 없음(bool 직결), 명령 공통화 리팩토링 없음.
  - **Acceptance**: Given 자막 끔(기본) 설정으로 StartAsync 재생 시작, When 플레이어 생성, Then 각 FakePlayer Commands에 `captions:False` 기록. Given 재생 중, When `SetCaptionsEnabledAsync(true)`, Then 전 플레이어에 `captions:True` + `h.Settings.CaptionsEnabled == true`로 저장 확인(SetFitModeAsync "설정에_저장된다" 테스트와 동형 — FakeStore는 호출 기록 없음). Given `CaptionsEnabled=true` 저장, When 재로드, Then 왕복 일치(JsonStateStoreTests). 빌드 경고/에러 0 + 전 테스트 통과. (실제 자막 표시/숨김·곡 전환 후 유지는 HUMAN-VERIFY — T3 완료 후 일괄)
  - **Files**:
    - 주: `src/DeskTube/Models/AppSettings.cs`, `src/DeskTube/Services/IPlayerHost.cs`, `src/DeskTube/Services/PlayerHost.cs`, `src/DeskTube/Services/PlaybackCoordinator.cs`, `src/DeskTube/Assets/player.html`
    - 테스트: `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs` (FakePlayer + 전파 테스트), `tests/DeskTube.Tests/JsonStateStoreTests.cs` (왕복)
  - **구현 명세**:
    - AppSettings: `public bool CaptionsEnabled { get; set; }` (기본 false — D2). Normalize 변경 불요(bool).
    - IPlayerHost: `void SetCaptionsEnabled(bool enabled);` (한글 요약 주석 — FR-20).
    - PlayerHost: `SetCaptionsEnabled` → `PostCommand(new PlayerCommand("captions", Enabled: enabled))`. `PlayerCommand`에 `bool? Enabled = null` 추가.
    - PlaybackCoordinator: `SetCaptionsEnabledAsync(bool)` 신설(SetFitModeAsync 동형 — 설정 갱신·전 플레이어 전파·저장) + 플레이어 생성 3곳(StartAsync·RecreatePlayerAsync·HandleMonitorsChangedCoreAsync)에서 `SetQualityScale`·`SetFitMode` 옆에 `SetCaptionsEnabled(_settings.CaptionsEnabled)` 초기 적용.
    - player.html: 상단 명령 목록 주석(21행 "load / play / ... / fit")에 `captions` 추가(주석-코드 동기화) + `let desiredCaptions = false;` + `applyCaptions()`(player 존재·loadModule 함수 존재 확인 + try/catch, desiredCaptions에 따라 loadModule/unloadModule을 'captions'·'cc' 양쪽에 — D4) + execute switch에 `case 'captions': desiredCaptions = !!cmd.Enabled; applyCaptions(); break;` + `case 'load'`에서 loadVideoById 후 `applyCaptions()` 재적용 (D5).
  - **Edge Cases**:
    - 자막 트랙 없는 영상: loadModule해도 표시할 트랙 없음 — 무동작·무해
    - ready 전 captions 명령: 기존 pending 큐가 보관 후 재생 (인프라 재사용, 추가 처리 불요)
    - loadModule/unloadModule 미존재(API 변경): 함수 존재 확인+try/catch — 자막 제어만 무동작, 재생 무영향
    - 기존 settings.json에 CaptionsEnabled 키 없음: 기본값 false 로드 (System.Text.Json 기본 동작 — 하위 호환)
    - 재생 정지 상태에서 토글: `_players` 비어 있어 전파 루프 0회 — 저장만 수행(정상)
  - **Halt Forecast**:
    - (ii-a) `IPlayerHost` 공개 인터페이스 메서드 추가(계획된 공개 API 변경) → `## 사전 승인 항목`에 등록
  - **Depends on**: T1

- [x] T3. 설정 UI — 자막 토글 카드 + 문구 + README
  - **Type**: C
  - **Design**: ① 배치 — SettingsViewModel(속성·핸들러)·SettingsPage.xaml(카드)·resw 2개(문구)·README(기능 서술). ② 신규 심볼 — `SettingsViewModel.CaptionsEnabled`([ObservableProperty]) + `OnCaptionsEnabledChanged`(OnReduceMirrorQualityChanged 동형 — `Apply(() => Coordinator.SetCaptionsEnabledAsync(value), "자막 표시 적용")`), Populate에서 `CaptionsEnabled = settings.CaptionsEnabled` 로드(_loading 가드 구간). ③ 의존 방향 — VM→Coordinator(T2 신설 메서드), XAML은 x:Bind TwoWay. ④ 비추상화 — 토글 공통화·헬퍼 추출 없음(기존 카드 패턴 복제).
  - **Acceptance**: Given 설정 화면 XAML, When 빌드, Then CaptionsCard(SettingsCard+ToggleSwitch, `IsOn="{x:Bind ViewModel.CaptionsEnabled, Mode=TwoWay}"`)가 화면 그룹 끝(ReduceMirrorCard 다음)에 존재 + resw ko/en에 `CaptionsCard.Header`/`.Description` 키 존재 + 빌드 경고/에러 0. 토글 변경 → SetCaptionsEnabledAsync 호출 경로는 코드 대조로 확인. 화면 표시·실동작은 HUMAN-VERIFY(빌드 통과 ≠ 시각 확인 — AGENTS XAML 규칙 8).
  - **Files**:
    - 주: `src/DeskTube/ViewModels/SettingsViewModel.cs`, `src/DeskTube/Views/SettingsPage.xaml`
    - 동반: `src/DeskTube/Strings/ko-KR/Resources.resw`, `src/DeskTube/Strings/en-US/Resources.resw`, `README.md`
  - **문구 확정** (하드코딩 금지 — resw):
    - ko `CaptionsCard.Header`: `자막` / `CaptionsCard.Description`: `켜면 영상에 자막을 표시합니다 (자막이 제공되는 영상만)`
    - en `CaptionsCard.Header`: `Captions` / `CaptionsCard.Description`: `Shows captions on videos that provide them`
  - **Edge Cases**:
    - 페이지 재진입(NavigationCacheMode.Required): 자막은 설정 화면에서만 바뀌므로 IsMuted식 재동기화 불요 — Populate 1회 로드로 충분
    - _loading 가드: Populate 중 OnCaptionsEnabledChanged 발화 방지 — Apply 공통 경로가 이미 차단(기존 패턴)
  - **Halt Forecast**: (i) 문구·위치·기본값 — D2·D6·문구 확정으로 사전 해소
  - **Depends on**: T2

## 사전 승인 항목 (일괄 승인 대상)
- T1 — PRD 갱신(FR-20 신설 + FR-10 보강): PRD는 승인 후 고정 문서 — 질문 라운드에서 내용 확정됨, plan 승인이 이 갱신 승인을 포함
- T2 — `IPlayerHost` 공개 인터페이스에 `SetCaptionsEnabled(bool)` 추가 (계획된 공개 API 변경 — 구현체 2개 전수 갱신 포함)

## 불가피한 Halt (위임 불가)
- (없음 — 이번 plan에 push·병합·릴리즈·PR·파괴적 작업·신규 외부 서비스 없음. 로컬 작업 브랜치 commit은 implement-task 규약 위임 범위)

## Known Workarounds (있는 경우만)
- `loadModule`/`unloadModule`은 IFrame API 공식 레퍼런스 미기재(비공식) — 공식 playerVars에 "강제 끄기"가 없어 대안 부재. 사용자 사전 고지·동의됨. 추후 공식 API가 생기면 교체 (Risks 참조).

## Verification Strategy
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` (경고/에러 0)
- 단위 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj`
- 수동 검증 (HUMAN-VERIFY — 사용자): 자막 있는 영상 재생 → ① 기본(끔)에서 자막 미표시 ② 토글 켬 → 즉시 자막 표시 ③ 곡 전환 후에도 유지 ④ 앱 재시작 후 설정 유지

## Phase Ledger
- Phase F 통과 (HEAD 75982c6) — F-1~F-7 전체, plan-completion-reviewer BLOCKER/MAJOR/MINOR 0
- Phase G 통과 (Must 100%) — 커버 대상 FR-20·FR-10(자막 추가분) 기계 검증 충족, 실동작 4항목 HUMAN-VERIFY 대기

## Retry Ledger

## Progress Log
- T1-T2 완료 (커밋 7a36989, d01f2bb): PRD FR-20 신설 + 자막 명령 파이프라인(AppSettings→Coordinator→IPlayerHost→player.html). 빌드 경고 0, 테스트 108/108, spec/quality 리뷰 OK.
  - 참고: `dotnet test`는 `-p:Platform=x64` 필수 (미지정 시 MSIX AnyCPU 에러 — Deferred 대장 기지 이슈).

## Next Steps
- T1~T3 완료 + Phase F/G 통과 (브랜치 task/captions-toggle, HEAD 75982c6 이후 문서 커밋 1개)
- 권장 다음 액션: HUMAN-VERIFY 4항목 사용자 확인(기본 끔 미표시 / 켬 즉시 표시 / 곡 전환 후 유지 / 재시작 후 유지) → 확인 후 main 병합(별도 승인)
- Suggested skills: 공식 /code-review (선택)

## Open Questions
- [x] Q1: 토글 기본값? → **끔** (배경화면 용도 부합, 현재 문제 즉시 해결 — 사용자 확정 2026-07-17)
- [x] Q2: PRD 반영 방식? → **FR-20 신설 + FR-10 보강** (FR-19 관례와 동일 — 사용자 확정 2026-07-17)
