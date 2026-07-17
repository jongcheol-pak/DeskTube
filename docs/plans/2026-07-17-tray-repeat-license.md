# Plan: 트레이 아이콘 교체 · 재생 순서 설정 제거 · 재생 반복 · 라이선스 링크화

**PRD**: docs/prd.md

## 요구 이해
- **원문 요청**: "1. AppIcon.ico 파일로 트레이 아이콘 변경 2. 설정 화면의 재생 순서 항목 삭제 3. 셔플듣기, 전체 듣기, 항목의 재생 버튼 재생 완료가 되면 멈추지 말고 다시 처음부터 반복 4. 정보 화면에서 오픈소스 라이선스 항목을 클릭하면 내용이 표시 되는데 해당 웹 사이트로 이동 하도록 수정"
- **이해한 요구**: ① 트레이 아이콘을 사용자가 준비한 앱 아이콘(루트 `AppIcon.ico`)으로 교체해 앱 아이콘과 통일. ② 설정 화면에서 "재생 순서" 콤보 항목을 UI에서 제거(내부 재생 모드 로직·저장은 셔플듣기가 계속 사용하므로 유지). ③ 어떤 진입점(셔플듣기/전체듣기/행 재생)으로 재생해도 목록 끝에서 정지하지 않고 처음부터 무한 반복 — 전체듣기·행 재생은 항상 목록 순서대로(사용자 확정), 셔플듣기는 셔플 반복(기구현). ④ 정보 화면 라이선스 항목을 펼침(전문 표시) 대신 클릭 시 해당 라이브러리 공식 사이트를 브라우저로 여는 방식으로 변경 — 전문 txt 파일은 패키지에 유지(사용자 확정).
- **포함하지 않는 것으로 이해**: 창·작업표시줄·타일 아이콘 변경(사용자가 manifest 자산 작업으로 별도 진행 중), 재생 모드 기능 자체의 삭제(Random/RepeatOne enum 값 포함 잔존 — 저장 호환).

## Goal
트레이 아이콘을 앱 아이콘으로 통일하고, 설정을 단순화하며, 재생이 끝나도 멈추지 않고 반복되게 하고, 라이선스 항목을 공식 사이트 링크로 바꾼다.

## PRD Coverage
소규모 연결 plan — 이번에 닿는 FR만 커버 대상으로 선언하고, 나머지 active Must FR은 범위 외로 명시한다.

| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-7 (재생 모드) | Must | T1(문구 갱신), T4 | ✅ 커버 |
| FR-10 (설정 화면 항목) | Must | T1(문구 갱신), T3 | ✅ 커버 |
| FR-12 (라이선스 화면) | Must | T1(문구 갱신), T5 | ✅ 커버 |
| FR-9 (트레이) | Must | T2 (아이콘 시각만 — FR 문구 불변, PRD 갱신 불요) | ✅ 커버 |
| FR-1~6, 8, 11, 13~16, 18 / NFR-1~6 | Must/Should | (없음) | 이번 범위 외 (기구현/후속) |

## Out of Scope
- Random(랜덤)·RepeatOne(한곡반복) 모드의 enum 제거 — UI 진입점은 없어지지만 저장된 설정 하위 호환·`AppSettings.Mode` 직렬화 안정성을 위해 잔존 (영구 제외 아님이 아니라 "제거하지 않기로 결정"한 사항)
- 창/작업표시줄/타일/스플래시 아이콘 작업 — 사용자가 직접 진행 중 (이번 plan 대상 아님)

## Deferred / Follow-up
- (없음)

## Investigation Log
- 위키 참조: 이번 4건은 프로젝트 국소 UI·로직 변경으로 위키 조회 생략 (직전 plan에서 feature/recipe 양방향 무매칭 확인 이력)
- Deferred 대장 확인: `docs/plans/deferred.md` `## 대기` 24건 — 이번 4건과 직접 관련 항목 없음. 직전 plan(design-1a-restyle)의 Deferred 5건은 대장 18~22행에 이미 등재 확인 → 이관 불요, plan 교체 가능 (Phase Ledger "Phase F/G 통과" 마커 커밋 c0a0ec6 확인)
- `TrayIconService.cs` 직접 Read — 아이콘 로드 1곳(64행 `ms-appx:///Assets/tray.ico`), csproj Content 등록 1곳(28행). tray.ico 참조 전수 grep — 이 2곳뿐
- `AppIcon.ico` 바이너리 헤더 파싱 — 9 엔트리(16/24/32/48/64/72/96/128/256px), 180KB, 정상 ico → 트레이(16/32px) 사용 적합
- `PlaybackQueue.cs` 직접 Read — 5개 모드 중 Sequential만 끝에서 null 반환(90~94행), Shuffle은 사이클 소진 시 재셔플로 무한(100~105행), Random/RepeatOne/RepeatAll 무한. `PlaybackCoordinator.AdvanceAsync`(452~460행)가 null이면 `StopAsync()` — 정지 경로는 이 1곳
- `PlaybackCoordinator.StartAsync` 135행 — 큐 생성 시 `_settings.Mode` 사용, `SetModeAsync`(245행)가 저장. 진입점 전수: PlaylistsViewModel 557행(셔플듣기), SettingsViewModel 431행(제거 대상)
- `PlaylistsViewModel.cs` 534~569행 직접 Read — 전체듣기 `PlayAsync`·행 재생 `PlayItemAsync`는 모드를 바꾸지 않고 "현재(마지막) 모드"로 재생 (멜론 plan D4 결정) → 설정 콤보 제거 후에는 셔플 고착 발생 → Q1로 사용자 확정: 항상 순서대로
- Mode UI 심볼 전수 grep — SettingsPage.xaml(121~126행 ModeCard, 118행 재생 그룹 헤더 — 이 그룹의 유일 항목), SettingsViewModel(42행 초기화·48~54행 ModeOptions·82행·104행 ModeIndex·226행 Load·427~433행 핸들러), resw ko/en 각 7키(ModeCard.Header, SettingsGroupPlayback.Text, Mode_* 5) — Mode_* 키는 ModeOptions만 소비(고아 확정)
- `AboutViewModel.cs`·`AboutPage.xaml` 직접 Read — `LicenseEntry(Id, License, FullText)` record, index.json(id/license/file) 파싱 + 전문 File.ReadAllText, Expander 표시. URL 정보 없음
- 패키지 5종 공식 URL을 로컬 nuspec `<projectUrl>`에서 확정 — CommunityToolkit.Mvvm=github.com/CommunityToolkit/dotnet, SettingsControls=github.com/CommunityToolkit/Windows, H.NotifyIcon.WinUI=github.com/HavenDV/H.NotifyIcon, Microsoft.WindowsAppSDK=github.com/microsoft/windowsappsdk, WinUIEx=dotmorten.github.io/WinUIEx
- `LicenseInventoryTests.cs` 직접 Read — ① csproj 참조 ⊆ index.json ② file 필드의 전문 파일 존재·비어있지 않음. 전문 파일 유지 확정(Q2)이므로 기존 2개 테스트 불변, url 검증만 추가
- 영향 테스트 확인 — `PlaybackQueueTests.순차_모드는_순서대로_진행하고_끝에서_정지한다`(14~23행), `PlaybackCoordinatorTests.순차_마지막_곡_종료_시_정지하고_배경을_복구한다`(206~217행)가 "끝에서 정지"를 단언 → T4에서 반복 단언으로 교체. `JsonStateStoreTests` 125행(기본 Mode=Sequential)은 불변
- AGENTS.md 신선도 — Build/Test 명령·Plan Location(docs/plans/) 이번 참조 항목 실재 확인, 어긋남 0건

## Risks & Unknowns
| 위험 | 영향 | 완화책 |
|---|---|---|
| loose 배포 시 resources.pri/자산 캐시로 새 아이콘 미반영 | 트레이 아이콘이 구버전으로 보임 | 검증 절차에 Remove-AppxPackage 후 재등록 명시 (notes 2026-07-16 함정 기록 준용) |
| 저장된 설정에 Random/RepeatOne이 남은 사용자 | 트레이 재생 시 그 모드로 재생 (정지는 안 함 — 두 모드 모두 무한) | 동작 무해 — enum 잔존으로 파싱 오류 없음. 문서(PRD FR-7)에 내부 모드 잔존 명시 |
| SettingsCard를 AboutPage에서 처음 사용 | 카드 시각이 SettingsPage와 달라 보일 가능성 | DesignTokens의 SettingsCard lightweight 키가 전역 스코프(직전 plan 확인) — 동일 토큰 적용됨. HUMAN-VERIFY로 확인 |

## Impact Analysis
### 4-A. 심볼/타입 추적 결과
| 심볼 | 영향 받는 파일 | 영향 종류 |
|---|---|---|
| `Assets/tray.ico` 참조 | `DeskTube.csproj`(28행), `TrayIconService.cs`(64행) — 전수 2곳 | T2 교체 |
| `ModeIndex`/`ModeOptions`/`OnModeIndexChanged` | `SettingsViewModel.cs`(42·48~54·82·104·226·427~433행), `SettingsPage.xaml`(121~126행) — 전수 | T3 제거 |
| resw `ModeCard.Header`·`SettingsGroupPlayback.Text`·`Mode_*` 5키 | `Strings/ko-KR·en-US/Resources.resw` — 소비처는 위 제거 대상뿐 | T3 제거 (고아 방지) |
| `AppSettings.Mode` | `PlaybackCoordinator.cs`(135·247행), `JsonStateStoreTests.cs`(125행) | 유지 (T3 이후에도 사용) |
| `SetModeAsync` | `PlaylistsViewModel.cs`(557행 유지), `SettingsViewModel.cs`(431행 제거), T4에서 PlayAsync/PlayItemAsync 호출 추가 | T3 제거·T4 추가 |
| `PlaybackQueue.Next()` Sequential 분기 | 호출부 `PlaybackCoordinator.AdvanceAsync`(454행) 1곳 + 테스트 2파일 | T4 동작 변경 |
| `LicenseEntry` record | `AboutViewModel.cs`(정의·생성), `AboutPage.xaml`(x:Bind Id/License/FullText) — 전수 | T5 시그니처 변경 |
| `Assets/licenses/index.json` 스키마 | `AboutViewModel.LoadLicenses`, `LicenseInventoryTests.cs` | T5 url 필드 추가 |

### 4-B. 계약·직렬화 변경
- `AppSettings.Mode` 직렬화 불변 (enum 값 제거 없음 — 하위 호환 유지)
- `LicenseEntry` record 시그니처 변경 (FullText → Url) — 앱 내부 UI 전용, 직렬화·외부 노출 없음, 사용처 2파일 전수 갱신
- `index.json`에 `url` 필드 추가 — 기존 필드(id/license/file) 유지라 additive, 소비자는 앱·테스트뿐
- `PlaybackQueue.Next()` 동작 계약 변경: Sequential에서 목록 끝 → null 대신 첫 항목 (빈 목록 null은 유지)

### 4-C. 테스트 파일
- `PlaybackQueueTests.cs` — 순차 끝 정지 테스트 교체 + Sequential 사용 테스트 6곳의 끝 단언 여부 점검
- `PlaybackCoordinatorTests.cs` — 순차 마지막 곡 정지 테스트 교체
- `LicenseInventoryTests.cs` — url 필드 검증 추가 (기존 2개 유지)
- `JsonStateStoreTests.cs` — 불변 (확인만)

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| 라이선스 행 클릭 카드 (UI) | `SettingsCard`(CommunityToolkit SettingsControls) — SettingsPage에서 사용 중, DesignTokens lightweight 키 전역 적용 | 기존 컴포넌트 재사용 (`IsClickable` + `ActionIcon`) — 신규 스타일 없음 |
| `OnLicenseCardClick` (AboutPage code-behind 핸들러) | 유사 구현 없음 (앱 내 외부 브라우저 열기 최초) | 신규 — `Windows.System.Launcher.LaunchUriAsync` 1회 호출 수준, 공통화 대상 아님(1곳) |
| `LicenseEntry.Url` | 기존 record 필드 확장 | 신규 필드 (FullText 대체) |

### Verified by
- grep `tray.ico` → 2 hits, 모두 표에 포함
- grep `ModeIndex|ModeOptions|Mode_|ModeCard` → 전 hit이 SettingsViewModel/SettingsPage/resw에 한정됨을 확인
- grep `PlaybackMode` (src 전수) → 표의 파일 외 사용처 없음 (PlaylistsViewModel 557행 포함)
- grep `LicenseEntry`·`Licenses` → AboutViewModel·AboutPage뿐

## Decisions
### D1. 반복 재생 구현 지점
- **Options**: A) `PlaybackQueue.Next()`의 Sequential 분기를 순환으로 변경 / B) 전체듣기·행 재생 진입점을 RepeatAll 모드로 전환(큐 무변경)
- **Chosen**: A
- **Rationale**: 근본 해결 — 정지 경로는 Sequential→null 1곳뿐이므로 이를 제거하면 트레이 재생·부팅 자동 재생·저장된 구 설정(Mode=Sequential) 등 모든 경로에서 "끝나면 반복"이 성립. B는 저장된 Sequential로 재생되는 경로(트레이)에서 여전히 정지. RepeatAll과 로직이 같아지지만 enum은 저장 호환을 위해 잔존.
- **Source**: PlaybackQueue.cs 90~94행, AdvanceAsync 454~459행, StartAsync 135행 (Investigation Log)

### D2. 전체듣기·행 재생의 모드 명시
- **Options**: A) 항상 순서대로(SetModeAsync(Sequential) 명시) / B) 마지막 모드 유지(기존 멜론 plan D4)
- **Chosen**: A — **사용자 확정 (Q1)**
- **Rationale**: 설정의 재생 순서 콤보 제거(T3) 후 모드 변경 수단이 셔플듣기뿐이라, 기존 동작 유지 시 셔플 고착. 버튼 이름과 동작 일치.
- **Source**: PlaylistsViewModel.cs 536~545행 주석·구현

### D3. 라이선스 전문 txt 파일 처리
- **Chosen**: 패키지에 유지 (화면 표시만 제거) — **사용자 확정 (Q2)**
- **Rationale**: MIT 고지 의무 충족(배포물 동봉), LicenseInventoryTests 기존 게이트 유지.

### D4. 트레이 아이콘 파일 배치
- **Options**: A) `Assets/tray.ico`를 AppIcon.ico 내용으로 덮어쓰기(코드 무변경) / B) `Assets/AppIcon.ico` 신규 복사 + 참조 2곳 갱신 + tray.ico 삭제
- **Chosen**: B
- **Rationale**: 파일명이 출처(루트 AppIcon.ico)와 일치해 추적 용이, "tray.ico인데 내용은 앱 아이콘"인 혼란 방지. 삭제는 사전 승인 항목에 등록. 루트 `AppIcon.ico`(원본)는 사용자 자산이므로 건드리지 않음(복사만).
- **Source**: tray.ico 참조 전수 2곳 (4-A)

### D5. 라이선스 행 클릭 UI
- **Chosen**: `SettingsCard IsClickable="True"` 재사용 — Header=패키지명, Description=라이선스명, ActionIcon=OpenInNew(“”), Click 핸들러(code-behind)에서 `Launcher.LaunchUriAsync`
- **Rationale**: 기존 컴포넌트·전역 lightweight 토큰 재사용(4-D), Expander 대비 클릭=이동 의도가 시각적으로 드러남. HyperlinkButton 단독은 카드 룩 상실이라 기각.
- **Source**: SettingsPage.xaml의 SettingsCard 사용례, DesignTokens lightweight 키(직전 plan)

### D6. 웹사이트 URL 정본
- **Chosen**: 각 패키지 nuspec `<projectUrl>` (Investigation Log의 5개 확정값) — index.json `url` 필드에 기록
- **Rationale**: NuGet 메타데이터가 라이브러리 저자가 선언한 공식 출처.

### D7. AppSettings.Mode·PlaybackMode enum 잔존
- **Chosen**: 필드·enum 값 전부 유지 (UI 진입점만 축소)
- **Rationale**: 저장된 JSON 하위 호환(값 제거 시 역직렬화 실패 위험), 셔플듣기·트레이 재생이 계속 사용. FR-7 문구에 반영.

## Tasks
- [x] T1. PRD 갱신 — FR-7·FR-10·FR-12 문구 (FR-9는 불변)
  - **Type**: A
  - **Acceptance**: FR-7에 "모든 모드는 목록 끝에서 정지하지 않고 계속 반복(순차: 끝나면 처음부터), UI 진입점은 셔플듣기/전체듣기/행 재생" 반영. FR-10 설정 항목 목록에서 "재생 모드" 제거. FR-12를 "라이브러리 목록 표시 + 항목 클릭 시 공식 사이트 이동(전문 파일은 패키지 동봉 유지)"로 갱신. 변경 이력에 3건 1줄씩 기록.
  - **Files**: 주: `docs/prd.md`
  - **Edge Cases**: (Type A — 해당 없음)
  - **Halt Forecast**: (i) PRD는 승인 후 고정 → 이 plan 승인이 PRD 변경안 승인을 포함함을 승인 프롬프트에 명시 (Step 10)
  - **Depends on**: -
- [x] T2. 트레이 아이콘을 AppIcon.ico로 교체
  - **Type**: B
  - **Acceptance**: Given 앱 실행, When 트레이 아이콘 표시, Then `Assets/AppIcon.ico`(루트 AppIcon.ico 사본)가 로드된다 — 빌드 경고 0 + ms-appx 참조·csproj Content 정합(HUMAN-VERIFY: 실제 트레이 표시).
  - **Files**:
    - 주: `src/DeskTube/Assets/AppIcon.ico`(신규 — 루트 `AppIcon.ico` 복사), `src/DeskTube/Services/TrayIconService.cs`(64행 URI), `src/DeskTube/DeskTube.csproj`(28행 Content)
    - 삭제: `src/DeskTube/Assets/tray.ico` (사전 승인 항목)
  - **Edge Cases**: loose 배포 자산 캐시 → 검증 시 Remove-AppxPackage 후 재등록 (Risks 표)
  - **Halt Forecast**: (ii-a) 파일 삭제(tray.ico)·csproj Content 항목 교체 → `## 사전 승인 항목`에 등록
  - **Depends on**: -
- [ ] T3. 설정 화면 "재생 순서" 항목 제거
  - **Type**: C
  - **Acceptance**: Given 설정 화면, When 화면 표시, Then "재생 순서" 카드와 "재생" 그룹 헤더가 없다(그룹의 유일 항목이었음). 빌드 경고 0(미사용 심볼·resw 고아 키 없음) + 기존 테스트 전건 통과. `AppSettings.Mode`·`SetModeAsync`는 잔존(D7).
  - **Files**:
    - 주: `src/DeskTube/Views/SettingsPage.xaml`(117~126행 그룹 헤더+ModeCard), `src/DeskTube/ViewModels/SettingsViewModel.cs`(42·48~54·82·104·226·427~433행)
    - 동반: `src/DeskTube/Strings/ko-KR/Resources.resw`·`src/DeskTube/Strings/en-US/Resources.resw`(ModeCard.Header, SettingsGroupPlayback.Text, Mode_Sequential/Shuffle/Random/RepeatOne/RepeatAll — 각 7키)
  - **Edge Cases**: 설정 페이지 NavigationCacheMode 재진입 시 제거 항목 잔상 없음(Load에서 ModeIndex 참조 제거 확인) / Mode_* 키를 다른 화면이 참조하지 않음(전수 grep 완료)
  - **Halt Forecast**: (i) "재생" 그룹이 비면 헤더 처리 애매 → 헤더도 제거로 확정(이 task 정의)
  - **Depends on**: -
- [ ] T4. 재생 완료 시 처음부터 반복 (FR-7)
  - **Type**: D
  - **Design**: 신규 심볼 없음 — ① `PlaybackQueue.Next()`의 Sequential 분기(90~94행)를 `(_currentIndex + 1) % _items.Count` 순환으로 변경(빈 목록 null 유지), 클래스 요약·enum doc·AdvanceAsync 주석 동기화 ② `PlaylistsViewModel.StartPlaybackAsync`에서 shuffle=false 경로에 `SetModeAsync(PlaybackMode.Sequential)` 명시(D2) — Coordinator/큐 의존 방향 불변 ③ 이번에 추상화하지 않음: RepeatAll과의 로직 중복 통합(모드 의미가 달라질 수 있어 분기 유지), 모드 enum 축소(D7)
  - **Acceptance**: Given 순차 재생(전체듣기 또는 행 재생), When 마지막 곡 종료, Then 첫 곡부터 재생 계속(1곡 리스트는 같은 곡 재로드). Given 셔플듣기 직후, When 전체듣기 클릭, Then 목록 순서대로 재생. 단위테스트: 순차 끝 순환·전체듣기 모드 명시·1곡 반복 — 기존 "끝에서 정지" 테스트 2건은 반복 단언으로 교체, 나머지 전건 통과.
  - **Files**:
    - 주: `src/DeskTube/Services/PlaybackQueue.cs`, `src/DeskTube/ViewModels/PlaylistsViewModel.cs`(538·545·547~558행)
    - 동반: `src/DeskTube/Models/PlaybackMode.cs`(Sequential doc 주석), `src/DeskTube/Services/PlaybackCoordinator.cs`(457행 주석 — null 분기 자체는 빈 목록 방어로 유지)
    - 테스트: `tests/DeskTube.Tests/PlaybackQueueTests.cs`, `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs`
  - **Edge Cases**: 빈 목록 `Next()` → null 유지(AdvanceAsync 정지) / 1곡 리스트 → 같은 곡 무한 재로드(RepeatOne과 동일 패턴 — 허용) / 재생 중 전 항목 삭제 → `UpdateItems` 후 Next() null → 정지(기존 동작 유지) / 저장된 구 설정 Mode=Random·RepeatOne → 무한 재생이라 요구 위배 없음
  - **Halt Forecast**: (i) "전체듣기가 순서대로면 기존 '마지막 모드' 동작과 충돌" → Q1 사용자 확정(D2)으로 해소
  - **Depends on**: T3과 독립 (순서 무관하나 plan 순서대로 진행)
- [ ] T5. 라이선스 항목 클릭 → 공식 사이트 이동 (FR-12)
  - **Type**: D
  - **Design**: ① `index.json`에 `url` 필드 추가(D6 확정값 5개), 기존 id/license/file 유지 ② `LicenseEntry`를 `(Id, License, Url)`로 변경(FullText 제거 — 전문 파일 읽기 코드 삭제), AboutViewModel이 url 파싱 ③ AboutPage: Expander → `SettingsCard IsClickable` 행(D5), code-behind `OnLicenseCardClick`이 `(sender as FrameworkElement).DataContext`의 LicenseEntry.Url로 `Launcher.LaunchUriAsync` ④ 이번에 추상화하지 않음: URL 열기 공통 서비스화(사용처 1곳)
  - **Acceptance**: Given 정보 화면, When 라이선스 항목 클릭, Then 기본 브라우저로 해당 패키지 공식 사이트가 열린다(전문 펼침 없음). 단위테스트: index.json 전 패키지에 url 존재·`https://` 시작(LicenseInventoryTests 추가), 기존 2개 테스트(참조 포함·전문 파일 존재) 불변 통과.
  - **Files**:
    - 주: `src/DeskTube/Assets/licenses/index.json`, `src/DeskTube/ViewModels/AboutViewModel.cs`, `src/DeskTube/Views/AboutPage.xaml`(67~112행), `src/DeskTube/Views/AboutPage.xaml.cs`(Click 핸들러)
    - 테스트: `tests/DeskTube.Tests/LicenseInventoryTests.cs`
  - **Edge Cases**: url 누락·파손 index.json → 기존 catch 로그 방어 유지 / `LaunchUriAsync` 실패(기본 브라우저 없음 등) → 예외 삼키지 않고 로그(AppLog) / 전문 txt 5개는 배포물에 잔존(D3 — csproj `Assets\licenses\**` Content 글롭 불변)
  - **Halt Forecast**: (ii-a) `LicenseEntry` record 시그니처 변경(계획된 내부 시그니처 변경, 사용처 2파일 전수 갱신) → `## 사전 승인 항목`에 등록
  - **Depends on**: -
- [ ] T6. README 갱신
  - **Type**: A
  - **Acceptance**: 설정 화면 항목 목록에서 재생 순서 제거, 재생 동작(끝나면 처음부터 반복) 반영, 정보 화면 라이선스 설명을 "클릭 시 공식 사이트 이동"으로 갱신. 존재하지 않는 기능 서술 0.
  - **Files**: 주: `README.md`
  - **Edge Cases**: (Type A — 해당 없음)
  - **Halt Forecast**: (없음 — 순수 문서 갱신, 파괴적·의존성·외부 요소 없음)
  - **Depends on**: T1~T5

## 사전 승인 항목 (일괄 승인 대상)
- T2 — `src/DeskTube/Assets/tray.ico` 삭제 + csproj Content 항목 교체(tray.ico→AppIcon.ico): 트레이 아이콘 출처 단일화(D4). 되돌리기: git revert로 복원 가능
- T5 — `LicenseEntry` record 시그니처 변경(FullText→Url): 계획된 내부 시그니처 변경, 사용처(AboutViewModel·AboutPage) 전수 갱신 포함

## 불가피한 Halt (위임 불가)
- push·main 병합·태그·릴리즈·PR — 구현·검증 완료 후 최종 보고에서 별도 승인
- plan에 근거 없는 돌발 결정 발생 시

## Verification Strategy
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` — 경고 0·오류 0
- 단위 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -p:Platform=x64` — 전건 통과 (T4 교체 2건 + T5 추가분 포함)
- 포맷: `dotnet format` 위반 0
- 수동 검증 (HUMAN-VERIFY): ① 트레이 아이콘이 앱 아이콘으로 표시(재등록 후) ② 설정 화면에 재생 순서 없음 ③ 마지막 곡 종료 후 첫 곡 재생(실청취) ④ 라이선스 항목 클릭 시 브라우저 열림

## Phase Ledger

## Retry Ledger

## Progress Log

## Next Steps
- 승인 시 `pjc:implement-task`로 T1부터 자율 실행

## Open Questions
- [x] Q1: 설정 재생 순서 제거 후 전체듣기·행 재생의 순서 → **항상 순서대로 (사용자 확정, D2 반영)**
- [x] Q2: 라이선스 전문 txt 파일 처리 → **패키지에 유지, 화면 표시만 제거 (사용자 확정, D3 반영)**
