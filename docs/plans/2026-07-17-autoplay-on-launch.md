# Plan: "앱 시작 후 자동 재생" 설정 토글 + 마지막 항목 재개

**PRD**: docs/prd.md

## 요구 이해
- **원문 요청**: "이미지에 있는 항목이 빠졌는데 자동 실행 아래쪽에 추가" (시안 스크린샷 — 헤더 "앱 시작 후 자동 재생", 설명 "앱을 열면 마지막으로 재생하던 항목을 이어서 재생합니다", 토글 기본 꺼짐)
- **이해한 요구**: 설정 화면 "자동 실행" 카드 아래에 시안 문구 그대로의 토글 카드를 추가하고, 실제 동작을 구현한다 — 토글이 켜져 있으면 사용자가 앱을 직접 실행했을 때(창 표시 실행) 마지막으로 재생하던 **항목(곡)부터** 자동으로 배경 재생을 시작한다(곡은 처음부터 — 초 단위 위치 아님, 사용자 확정). 부팅 자동 시작(FR-8) 경로의 자동 재생도 "마지막 항목부터"로 통일한다(사용자 확정). 직전 plan(design-1a-restyle)에서 기능 신설이라 Deferred했던 항목의 재수용이며 PRD FR 신설을 동반한다.
- **포함하지 않는 것으로 이해**: 곡 내 재생 위치(초 단위) 복원, 트레이 메뉴 "재생"의 마지막 항목 재개(Deferred — 아래 참조), 단일 인스턴스 보장(기존 deferred 별건).

## Goal
앱을 열 때(및 부팅 자동 시작 시) 마지막으로 재생하던 곡부터 자동으로 이어서 재생할 수 있게 하고, 이를 설정 토글(기본 꺼짐)로 제어한다.

## 명세 추출 (시안 문구 — 원문 그대로, 소규모 인라인 대조)
1. 카드 헤더(ko): "앱 시작 후 자동 재생"
2. 카드 설명(ko): "앱을 열면 마지막으로 재생하던 항목을 이어서 재생합니다"
3. 컨트롤: ToggleSwitch, 기본 꺼짐
4. 위치: "자동 실행" 카드(+상태 InfoBar) 바로 아래, "언어" 카드 위

## PRD Coverage
소규모 연결 plan — 커버 대상만 선언, 나머지는 범위 외.

| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-19 (신설 — 앱 시작 후 자동 재생) | Must | T1(신설), T2, T3, T4 | ✅ 커버 |
| FR-8 (부팅 자동 재생 — "마지막 항목부터" 보강) | Must | T1(문구), T2, T3 | ✅ 커버 |
| FR-10 (설정 화면 항목 — 문구에 추가) | Must | T1(문구), T4 | ✅ 커버 |
| 나머지 active Must FR / NFR | Must/Should | (없음) | 이번 범위 외 (기구현/후속) |

## Out of Scope
- 곡 내 재생 위치(초 단위) 복원 — 플레이어 위치 주기 추적(JS 브리지 확장) 필요, 사용자 확정으로 항목 단위만

## Deferred / Follow-up
- 트레이 메뉴 "재생"(정지 상태 → 마지막 리스트 재생)도 마지막 항목부터 재개하도록 통일 — 이번 요청 범위 밖(요청은 앱 시작 토글), 일관성 개선 후보. `TrayIconService.PlayAsync`가 `StartAsync(playlist.Id)`를 항목 지정 없이 호출 중
- "마지막 재생 리스트→StartAsync" 로직 공통 헬퍼 추출 — 기존 대장 항목(App·TrayIconService 2곳, 3회째 등장 시) 유지

## Investigation Log
- Deferred 대장 확인: `docs/plans/deferred.md` 19행 "[2026-07-17] 앱 시작 후 자동 재생 설정 토글 + 동작 — PRD FR 신설 합의 필요 (출처: design-1a-restyle Q4)" — **이번 plan이 재수용** (구현 완료 시 `## 종결`로 이동). 기타 관련 항목: 15행 공통 헬퍼(위 Deferred에 유지 기록), 16행 단일 인스턴스(별건)
- 위키 참조: 프로젝트 국소 기능이라 조회 생략 (직전 plan들에서 vault 무매칭/미설정 확인 이력)
- `App.xaml.cs` 직접 Read — OnLaunched(95~111행): `quietStart`(StartupTask 활성화 또는 인자) 판별 → `InitializeServicesAsync(quietStart)` → 131행 `if (autoPlay) await TryAutoPlayLastAsync()`. `TryAutoPlayLastAsync`(138~156행): `Settings.LastPlaylistId` 리스트 조회(없거나 비면 생략·로그) 후 `StartAsync(playlist.Id)` — **항목 지정 없음**. 일반 실행은 재생 시작 경로 자체가 없음
- `AppSettings.cs` 직접 Read — `LastPlaylistId`(35행)만 존재, 항목 수준 필드 없음. "새 필드는 기본값과 함께 추가"(5행 주석 — additive 관례), `Normalize()`는 범위 보정만
- `PlaybackCoordinator.cs` 직접 Read — 항목 재생 시작점은 `LoadAll(item)` 단일 경로(466~473행), 호출부는 **전수 3곳**: StartAsync(145행)·ReloadCurrentTrack(297행)·AdvanceAsync(463행) (grep 재확인 — plan-reviewer B1 정정). ReloadCurrentTrack은 로그아웃 등 세션 변경 시 **현재 곡**(`_queue.Current`)을 재로드하므로 LastItemId를 이미 저장된 동일 값으로 재설정(무해)하고 저장 호출도 없음. StartAsync는 152~153행에서 `LastPlaylistId` 저장(LoadAll 이후 — LastItemId를 LoadAll에서 기록하면 같은 저장에 포함됨). AdvanceAsync에는 저장 없음 → 추가 필요. 임베드 금지 스킵도 AdvanceAsync 경유(PlaybackCoordinatorTests 220행 테스트 존재)
- `StartAsync(Guid playlistId, Guid? startItemId = null)` 시그니처 기존 존재(FR-18) — 재개는 이 인자 재사용, 시그니처 변경 없음. `PlaybackQueue.Start(startItemId)`는 목록에 없는 Id를 무시하고 기본 시작(50행 + 테스트 `존재하지_않는_시작_항목은_무시하고_기본_시작한다` 기확인)
- `SettingsViewModel.cs` 직접 Read — 비재생 설정 토글 저장 패턴 확인(453~473행): partial OnChanged → `_loading`/`_services` 가드 → settings 직접 mutate → `Apply(() => Store.SaveSettingsAsync(...))` (ApplyPausePolicy). 새 토글은 Reevaluate 없이 동일 패턴
- `SettingsPage.xaml` 직접 Read(T3 이후 상태) — AutoStartCard(x:Uid) + 상태 InfoBar 다음이 LanguageCard. 새 카드 삽입 위치 = InfoBar와 LanguageCard 사이. SettingsCard+ToggleSwitch 조합은 기존 카드(MuteCard 등)와 동일 구조 재사용
- `JsonStateStoreTests.cs` 125행 — 설정 왕복·기본값 테스트 존재(신규 필드 왕복·구형 JSON 하위 호환 검증 추가 지점)
- AGENTS.md 신선도 — Build/Test 명령·Plan Location 실재 확인, 이번 참조 항목 어긋남 0건

## Risks & Unknowns
| 위험 | 영향 | 완화책 |
|---|---|---|
| 곡 전환마다 settings.json 저장(쓰기 빈도 증가) | 곡당 1회 소량 JSON 쓰기 — 미미 | 기존 SaveSettingsAsync 재사용(볼륨 드래그 등 이미 빈발 저장 경로 존재), 문제 시 후속 최적화 |
| 두 번째 인스턴스 실행 시 중복 자동 재생 | 기존 부팅 자동 재생에도 동일하게 존재하는 기지 리스크 | 단일 인스턴스 보장이 기존 deferred 항목 — 이번 범위 밖, 신규 악화 아님(토글 기본 꺼짐) |
| 저장된 LastItemId 항목이 삭제된 경우 | 재개 불가 | PlaybackQueue.Start가 무시하고 기본 시작(기확인·테스트 존재) — 추가 방어 불요 |

## Impact Analysis
### 4-A. 심볼/타입 추적 결과
| 심볼 | 영향 받는 파일 | 영향 종류 |
|---|---|---|
| `AppSettings` (필드 2 추가: AutoPlayOnLaunch, LastItemId) | `Models/AppSettings.cs`, 소비: `App.xaml.cs`(분기·전달), `PlaybackCoordinator.cs`(기록), `SettingsViewModel.cs`(토글), `JsonStateStoreTests.cs` | T2·T3·T4 additive |
| `LoadAll` 호출부 | `PlaybackCoordinator.cs` StartAsync(145)·ReloadCurrentTrack(297 — 현재 곡 재로드, 동일 값 재설정이라 무해)·AdvanceAsync(463) — 전수 3곳 | T2 (LastItemId 기록 지점) |
| `TryAutoPlayLastAsync` | `App.xaml.cs` 1곳(호출부 InitializeServicesAsync 131행) — private | T3 (startItemId 전달 + 호출 조건 확장) |
| `StartAsync(playlistId, startItemId)` | 시그니처 기존 그대로(FR-18) — 호출부 무변경 | 재사용 (변경 없음) |
| 설정 UI 신규 x:Uid `AutoPlayCard` | `SettingsPage.xaml`, `SettingsViewModel.cs`, resw ko/en 2키 | T4 신규 |

### 4-B. 계약·직렬화 변경
- `settings.json`에 `AutoPlayOnLaunch`(bool, 기본 false)·`LastItemId`(Guid?, 기본 null) **additive 추가** — 구형 JSON은 필드 부재 시 기본값으로 역직렬화(AppSettings 관례 5행 주석), SchemaVersion 유지. 제거·개명 없음 → 마이그레이션 불요
- 공개 시그니처 변경 없음 (StartAsync 기존 선택 인자 재사용, LoadAll·TryAutoPlayLastAsync는 private)

### 4-C. 테스트 파일
- `JsonStateStoreTests.cs` — 신규 필드 왕복 + 구형 JSON(필드 없음) 기본값 검증 추가
- `PlaybackCoordinatorTests.cs` — "곡 진행 시 LastItemId 저장"·"시작 시 LastItemId 저장" 추가 (Harness의 store/settings 페이크 재사용)
- App 시작 분기(T3)는 App 클래스라 단위테스트 불가 — HUMAN-VERIFY (레포 관례: 서비스 계층만 테스트)

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| `AppSettings.AutoPlayOnLaunch`·`LastItemId` | 유사 필드 관례(LastPlaylistId·bool 토글류) 존재 | 관례 따라 신규 필드 (additive) |
| 설정 토글 카드 (UI) | `SettingsCard`+`ToggleSwitch` 조합 다수(MuteCard·AutoStartCard 등) | 기존 구조 그대로 재사용 |
| VM `AutoPlayOnLaunch` property + OnChanged | `ApplyPausePolicy` 패턴(mutate+Store 저장) | 동일 패턴 적용 (Reevaluate만 제외) |
| 재개 시작 로직 | `StartAsync(playlistId, startItemId)` + `PlaybackQueue.Start` 기존 | **전부 재사용** — 신규 재생 로직 0 |

### Verified by
- grep `LoadAll` → 정의 1 + 호출 3 (StartAsync 145·ReloadCurrentTrack 297·AdvanceAsync 463), 모두 표에 포함 (B1 재확인 완료)
- grep `TryAutoPlayLastAsync` → 정의 1 + 호출 1 (InitializeServicesAsync)
- grep `LastPlaylistId` → AppSettings·Coordinator(152·265행)·App(141행)·TrayIconService(132행 — 이번 무변경, Deferred 기록)

## Decisions
### D1. 재개 범위 — 항목(곡) 단위
- **Chosen**: 마지막 재생하던 곡부터, 곡은 처음부터 — **사용자 확정 (Q1)**
- **Rationale**: 시안 문구("항목을 이어서")와 일치, FR-18 startItemId 인프라 재사용으로 신규 재생 로직 0. 초 단위는 JS 브리지 확장 필요라 Out of Scope.

### D2. 부팅 자동 시작 경로 통일
- **Chosen**: FR-8 부팅 자동 재생도 마지막 항목부터 — **사용자 확정 (Q2)**
- **Rationale**: `TryAutoPlayLastAsync` 단일 메서드가 두 경로를 공유하므로 분기 없이 자연 통일. FR-8 문구 보강은 T1.

### D3. LastItemId 기록 지점
- **Chosen**: `LoadAll(item)` 안에서 `_settings.LastItemId = item.Id` 기록(항목 재생 시작 단일 경로), 저장은 StartAsync 기존 저장(153행)에 편승 + AdvanceAsync에 `SaveSettingsAsync` 1회 추가
- **Rationale**: 호출부 전수 3곳(145·297·463)이 LoadAll을 경유하므로 기록 누락 불가. ReloadCurrentTrack(297)은 현재 곡 재로드라 동일 값 재설정 — 부수효과 무해(저장도 없음). 스킵(임베드 금지) 경로도 AdvanceAsync 경유로 자동 커버.
- **Source**: PlaybackCoordinator.cs 145·297·463·466~473행 (Investigation Log — B1 정정 반영)

### D4. 토글 저장 패턴
- **Chosen**: SettingsViewModel에서 `_loading`/`_services` 가드 → settings 직접 mutate → `Apply(Store.SaveSettingsAsync)` — ApplyPausePolicy와 동일(Reevaluate 제외). Coordinator 메서드 신설 안 함(재생 동작과 무관한 시작 옵션).
- **Source**: SettingsViewModel.cs 453~473행

### D5. 토글의 적용 경로
- **Chosen**: 토글은 **일반 실행(창 표시)에만** 적용. 부팅 자동 시작(quietStart)은 토글과 무관하게 기존대로 항상 자동 재생(FR-8 기존 계약 유지). 구현: `if (autoPlay || Services.Settings.AutoPlayOnLaunch) await TryAutoPlayLastAsync();`
- **Rationale**: FR-8은 "자동 실행 시 자동 재생"이 요구 자체 — 토글로 끄면 FR-8 위반. 시안 토글은 별도 항목(일반 실행용).

### D6. resw 문구 (외부 계약 — 고정)
- **Chosen**: `AutoPlayCard.Header` ko "앱 시작 후 자동 재생" / en "Auto-play on launch", `AutoPlayCard.Description` ko "앱을 열면 마지막으로 재생하던 항목을 이어서 재생합니다" / en "When you open the app, playback resumes from the item you were last playing" (ko는 시안 원문 그대로 — 명세 추출 1·2)

## Tasks
- [x] T1. PRD 갱신 — FR-19 신설 + FR-8·FR-10 문구 보강
  - **Type**: A
  - **Acceptance**: FR-19 "앱 시작 후 자동 재생(기본 꺼짐): 켜면 일반 실행 시 마지막 재생 항목부터 배경 재생 자동 시작. 항목 단위(곡 처음부터)" Must로 신설. FR-8에 "(자동 재생은 마지막 재생 항목부터 — FR-19와 동일 재개 방식)" 보강. FR-10 설정 항목 목록에 "앱 시작 후 자동 재생" 추가. 변경 이력 1줄.
  - **Files**: 주: `docs/prd.md`
  - **Edge Cases**: (Type A — 해당 없음)
  - **Halt Forecast**: (i) PRD는 승인 후 고정 → 이 plan 승인이 FR-19 신설·FR-8/10 보강안 승인을 포함함을 승인 프롬프트에 명시
  - **Depends on**: -
- [x] T2. LastItemId 추적 (Coordinator + AppSettings)
  - **Type**: C
  - **Design**: ① `AppSettings.LastItemId`(Guid?, 기본 null — LastPlaylistId 아래, FR-19 doc 주석) ② `PlaybackCoordinator.LoadAll`에서 `_settings.LastItemId = item.Id` 기록(D3), `AdvanceAsync`의 `LoadAll(next)` 뒤 `await _store.SaveSettingsAsync(_settings)` 추가 ③ 의존 방향 불변(Coordinator→settings/store 기존 경로) ④ 이번에 추상화하지 않음: 저장 디바운스/배칭(곡당 1회 쓰기는 미미 — Risks 표)
  - **Acceptance**: Given 재생 중, When 곡이 넘어감(Ended→Advance), Then settings의 LastItemId가 새 곡 Id로 저장된다. Given StartAsync(리스트, 항목 지정), Then LastItemId가 그 항목으로 저장된다. 단위테스트 2건 신규 + 구형 JSON(필드 부재) 로드 시 null 기본값 왕복 테스트. 기존 테스트 전건 통과.
  - **Files**:
    - 주: `src/DeskTube/Models/AppSettings.cs`, `src/DeskTube/Services/PlaybackCoordinator.cs`
    - 테스트: `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs`, `tests/DeskTube.Tests/JsonStateStoreTests.cs`
  - **Edge Cases**: 빈 목록 Advance(null)→StopAsync 경로는 LastItemId 기록 없음(정상 — 마지막 곡 유지) / 정지 후 재시작 시 LastItemId는 새 시작 곡으로 갱신 / 임베드 금지 스킵도 AdvanceAsync 경유라 자동 기록
  - **Halt Forecast**: (i) 직렬화 additive 필드 — 4-B에서 하위 호환 확인 완료(구형 JSON 기본값), 마이그레이션 불요
  - **Depends on**: -
- [x] T3. 앱 시작 자동 재생 분기 + 마지막 항목 재개 (App)
  - **Type**: C
  - **Design**: ① `AppSettings.AutoPlayOnLaunch`(bool, 기본 false — FR-19 doc 주석) ② `App.InitializeServicesAsync` 131행 분기를 `if (autoPlay || Services.Settings.AutoPlayOnLaunch)`로 확장(D5), `TryAutoPlayLastAsync`가 `StartAsync(playlist.Id, services.Settings.LastItemId)`로 항목 전달(D1·D2 — 부팅·일반 공용) + doc 주석 갱신(방향 고정: TryAutoPlayLastAsync 요약을 "자동 시작·앱 시작 자동 재생(FR-8·FR-19) — 마지막 재생 항목부터"로, 131행 분기 주석에 "FR-19 토글은 일반 실행에만, 부팅은 FR-8로 항상" 명시) ③ 의존 방향 불변 ④ 이번에 추상화하지 않음: Tray PlayAsync와의 공통 헬퍼(2곳 — 3회 규칙 미달, Deferred 유지)
  - **Acceptance**: Given 토글 켬 + 마지막 재생 기록 존재, When 앱 일반 실행, Then 마지막 항목부터 배경 재생 자동 시작(⏳ HUMAN-VERIFY — App 계층 단위테스트 불가). Given 토글 끔, When 일반 실행, Then 재생 시작 없음(기존 동작). Given 부팅 자동 시작, Then 토글과 무관하게 마지막 항목부터 재생. 재개 항목 삭제 시 리스트 기본 시작(기존 테스트 보장). 빌드 경고 0.
  - **Files**:
    - 주: `src/DeskTube/App.xaml.cs`, `src/DeskTube/Models/AppSettings.cs`(T2와 같은 파일 — AutoPlayOnLaunch 필드는 이 task 몫)
    - 테스트: `tests/DeskTube.Tests/JsonStateStoreTests.cs`(AutoPlayOnLaunch 기본 false 왕복)
  - **Edge Cases**: LastPlaylistId 리스트 삭제/빈 리스트 → 기존 가드(생략+로그) / LastItemId만 있고 LastPlaylistId null → 가드가 먼저 생략 / 토글 켬 + 재생 기록 전무(첫 실행) → 생략+로그(기존 경로)
  - **Halt Forecast**: (i) "부팅 경로도 항목 재개로 바뀌는 동작 변경" → Q2 사용자 확정(D2)으로 해소
  - **Depends on**: T2 (LastItemId 필드·기록)
- [x] T4. 설정 UI — 토글 카드 + resw (FR-19, FR-10)
  - **Type**: C
  - **Design**: ① `SettingsPage.xaml` — AutoStart 상태 InfoBar와 LanguageCard 사이에 `controls:SettingsCard x:Uid="AutoPlayCard"` + `ToggleSwitch IsOn="{x:Bind ViewModel.AutoPlayOnLaunch, Mode=TwoWay}"`(기존 카드 구조 재사용 — 4-D) ② `SettingsViewModel` — `[ObservableProperty] AutoPlayOnLaunch` + Populate 로드 + `OnAutoPlayOnLaunchChanged`(D4 패턴) ③ resw ko/en `AutoPlayCard.Header`·`AutoPlayCard.Description`(D6 고정 문구) ④ 이번에 추상화하지 않음: 토글 저장 공통화(각 토글 1핸들러 관례 유지)
  - **Acceptance**: Given 설정 화면, Then "자동 실행" 아래에 시안 문구의 토글 카드 표시(기본 꺼짐 — ⏳ 시각은 HUMAN-VERIFY). When 토글 변경, Then settings.json에 즉시 저장·재시작 후 유지. 설정 재진입(NavigationCache) 시 저장값 표시. 빌드 경고 0 + resw ko/en 키 정합.
  - **Files**:
    - 주: `src/DeskTube/Views/SettingsPage.xaml`, `src/DeskTube/ViewModels/SettingsViewModel.cs`
    - 동반: `src/DeskTube/Strings/ko-KR/Resources.resw`, `src/DeskTube/Strings/en-US/Resources.resw`
  - **Edge Cases**: `_loading` 가드로 Populate 중 저장 루프 방지(기존 패턴) / 서비스 미준비(IsReady 전) — ContentControl IsEnabled 일괄 비활성(기존 구조)
  - **Halt Forecast**: (없음 — 기존 패턴 반복, 파괴적·의존성·외부 요소 없음)
  - **Depends on**: T3 (AutoPlayOnLaunch 필드)
- [x] T5. README 갱신
  - **Type**: A
  - **Acceptance**: 기능 목록에 "앱 시작 후 자동 재생" 항목 추가(토글 기본 꺼짐·마지막 항목 재개), 부팅 자동 시작 항목에 "마지막 재생 항목부터" 반영. 존재하지 않는 기능 서술 0.
  - **Files**: 주: `README.md`
  - **Edge Cases**: (Type A — 해당 없음)
  - **Halt Forecast**: (없음 — 순수 문서 갱신, 파괴적·의존성·외부 요소 없음)
  - **Depends on**: T1~T4

## 사전 승인 항목 (일괄 승인 대상)
- (없음) — 공개 시그니처 변경·파일 삭제·의존성 변경 없음 (settings.json 필드는 additive 추가)

## 불가피한 Halt (위임 불가)
- push·main 병합·태그·릴리즈·PR — 구현·검증 완료 후 별도 승인
- plan에 근거 없는 돌발 결정 발생 시

## Verification Strategy
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` — 경고 0·오류 0
- 단위 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -p:Platform=x64` — 전건 통과 (신규: LastItemId 저장 2건 + 설정 왕복/하위 호환)
- 포맷: `dotnet format` 위반 0
- 수동 검증 (HUMAN-VERIFY): ① 토글 켬 → 앱 재실행 시 마지막 곡부터 재생 ② 토글 끔 → 재생 없음 ③ 부팅 자동 시작 시 마지막 곡부터 ④ 설정 카드 문구·위치 시안 일치

## Phase Ledger
- Phase F 통과 (HEAD a98fcf7) — F-2 테스트 104/104, F-7 plan-completion-reviewer OK (0/0/0)
- Phase G 통과 (Must 100%) — 커버 대상 FR-19·FR-8·FR-10 전건 충족 (F-7 대조 재사용, 재루프 0회), HUMAN-VERIFY 4건 잔여

## Retry Ledger

## Progress Log
- T1~T2 완료 (커밋 d8aab80, 17e2541): PRD FR-19 신설·FR-8/10 보강 / AppSettings LastItemId·AutoPlayOnLaunch additive + LoadAll 기록·AdvanceAsync 저장(D3). 테스트 104/104. T2 spec MAJOR 1(왕복 Assert 누락) 수정 후 OK.

## Next Steps
- 전 task 완료 + Phase F/G 통과. deferred.md "앱 시작 후 자동 재생" 종결 이관 완료.
- 잔여: HUMAN-VERIFY 4건(토글 켬 재실행 재개·토글 끔 무재생·부팅 자동 재생 항목 재개·설정 카드 문구/위치) + main 병합/push(별도 승인)

## Open Questions
- [x] Q1: "이어서 재생" 범위 → **항목(곡) 단위 (사용자 확정, D1 반영)**
- [x] Q2: 부팅 자동 시작 경로 통일 → **통일 — 마지막 항목부터 (사용자 확정, D2 반영)**
