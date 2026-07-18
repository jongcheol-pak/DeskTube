# Plan: 플레이리스트 재생 버튼 재생/정지 토글 (2026-07-18)

**PRD**: docs/prd.md

## 요구 이해
- **원문 요청**: "플레이리스트에 재생은 있는데 정지 버튼이 없어서 재생 중이면 정지 버튼으로 표시 정지 상태면 재생 버튼으로 표시"
- 플레이리스트 페이지에는 재생 진입점(상단 전체듣기 버튼·행 재생 버튼)만 있고 정지 수단이 없다 — 정지하려면 홈이나 트레이로 가야 한다.
- 상단 전체듣기 버튼: **선택한 리스트**가 재생(일시정지 포함) 중이면 "정지" 버튼으로 표시·클릭 시 정지, 그 외에는 현행 "전체듣기"·클릭 시 재생 (질문 확정 — 선택 리스트 기준. 다른 리스트 재생 중이면 전체듣기 표시, 클릭 시 그 리스트로 전환 = 현행 동작 유지).
- 행 재생 버튼도 포함(질문 확정): **재생 중인 곡의 행** 버튼은 정지 버튼으로 표시·클릭 시 정지, 그 외 행은 현행 "이 곡부터 듣기".
- PRD FR-18 보강 진행(질문 확정) — 셔플듣기 버튼·홈·트레이는 범위 외.

## Goal
플레이리스트 페이지 안에서 재생 상태를 보고 바로 정지할 수 있다 — 상단 버튼과 재생 중인 곡의 행 버튼이 재생/정지 토글로 동작한다.

## PRD Coverage
| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-18 (보강: 전체듣기·행 재생 버튼 재생/정지 토글) | Must | T1, T2 | ✅ 커버 |
| FR-1~FR-16, FR-19~FR-21, 나머지 FR-18 항목 | Must/Could | (기구현) | 이번 범위 외 (기구현/후속) |

## Out of Scope
- 셔플듣기 버튼의 토글화 — 셔플듣기는 항상 "셔플로 (재)시작" 진입점으로 유지 (요청에 없음)
- 홈 화면·트레이 메뉴의 재생/정지 UI — 이미 각자 토글·정지 수단 보유 (FR-9 트레이 토글, 홈 정지 pill)
- 일시정지/재개 버튼 — 정지(Stop)만 다룬다 (일시정지는 기존 정책·트레이 영역)

## Deferred / Follow-up
- (없음)

## Investigation Log
- 위키 참조: vault 미설정 — 코드 1차 출처로 진행 (직전 plan과 동일 상태)
- Deferred 대장(`docs/plans/deferred.md`) `## 대기` 확인 — 이번 작업과 겹치는 항목 없음. #25 "배지 토글 접근성 이름 상태 반영형(BadgeToggleName(bool))"은 이번 행 버튼 접근성 이름 함수의 **선례**로만 참고(편입 아님 — 대상이 홈 소리 배지).
- 기존 plan `2026-07-18-now-playing-item.md`: Phase Ledger "Phase G 통과 (Must 100%)" + Deferred 없음 → 완료 확정, 교체 가능.
- `PlaylistsPage.xaml:206~215` 상단 전체듣기 버튼 — AccentButtonStyle, FontIcon E768 + `x:Uid PlayAllLabel`("전체듣기"/"Play all"), `PlayCommand` 바인딩. **호출부(참조)는 XAML 이 1곳뿐** (전 레포 grep 확정).
- `PlaylistsPage.xaml:359~378` 행 재생 버튼 — `x:Uid ItemPlayButton`(AutomationProperties.Name "이 곡부터 듣기"), FontIcon E768 하드코딩, hover 핸들러 `OnRowPlayPointerEntered/Exited`(`Content: FontIcon` 패턴 매칭 — Content 형태 유지 시 토글과 무관), `Click=OnPlayItemClick`.
- `PlaylistsViewModel.cs:612~620` `PlayAsync`(RelayCommand)·`PlayItemAsync` → `StartPlaybackAsync`. **`PlayItemAsync` 호출부는 `PlaylistsPage.xaml.cs:317` 1곳뿐** (전 레포 grep 확정).
- `PlaylistsViewModel.cs:463~464` `IsPlaying(PlaylistEntry)` = `Coordinator.CurrentPlaylistId == entry.Id` — 선택 리스트 재생 판정에 재사용 가능.
- `PlaylistsViewModel.cs:162~165, 192~198` StatusChanged·CurrentItemChanged 구독 + TryEnqueue 마셜링 + `UpdateNowPlaying`/`UpdateNowPlayingItem` 이미 존재 — 토글 상태 갱신을 이 경로에 얹는다 (신규 구독 불필요).
- `PlaybackCoordinator.cs:123~128` StartAsync는 재생 중 재호출 시 **StopAsync를 먼저 실행** → 시작/정지/리스트 전환 모두 StatusChanged 발화 보장 (Stopped→Playing). `:93~101` CurrentPlaylistId·CurrentItemId는 일시정지 중 유지·정지 시 null (재생 중 표시 정본, 주석 명시). `:219~228` StopAsync는 상태 무관 호출 안전(CleanupAll+null 해제, 멱등).
- `HomeViewModel.cs:68~70, 137~155` 홈 정지 선례 — `IsPlaying = status != Stopped`("일시정지 중에도 정지 가능해야 함" 주석), `StopCommand` → `Coordinator.StopAsync()`, 글리프 E71A + `HomeStopLabel`("정지"/"Stop").
- `Strings/*/Resources.resw` — `Tray_Stop`("정지"/"Stop") plain 키 존재(Loc.Get 재사용 가능), `PlayAllLabel.Text`·`ItemPlayButton.[…]AutomationProperties.Name`은 x:Uid 속성 키(Loc.Get의 슬래시 키 조회 선례 레포에 없음 — plain 키 신설로 우회). `Loc.cs` GetString 폴백 확인.
- `PlaylistsPage.xaml.cs:51` `InvertVisibility(bool)` 기존 헬퍼 — 재생 상태 문구 스왑에 재사용. `:63~68` `NowPlayingLabel()`·`RankVisibility()` 정적 x:Bind 함수 선례.
- AGENTS.md 신선도: 빌드·테스트 명령이 참조하는 `DeskTube.slnx`·`tests/DeskTube.Tests` 실재 확인(이번 계획 참조 항목 어긋남 0).
- 미커밋 변경(진단 계측 7파일)은 사용자 확정으로 그대로 유지 — 이번 task 커밋은 이번 변경 파일만 스테이징.

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| `IsSelectedPlaying` (VM observable) | `HomeViewModel.IsPlaying`(전역 재생 여부)·`PlaylistEntry.IsNowPlaying`(리스트별) | 판정 기준이 "선택 리스트 재생 중"이라 둘과 다름 — 신규. 계산은 기존 `IsPlaying(PlaylistEntry)` 재사용 |
| `TogglePlayItemAsync` | `PlayItemAsync`(재생만) | 기존 메서드를 토글 의미로 **개명·확장** (신규 중복 아님, 호출부 1곳 동반 수정) |
| `PlayToggleGlyph`/`RowPlayGlyph`/`RowPlayName` (정적 x:Bind 함수) | `RankVisibility`·`NowPlayingLabel` 동일 패턴 선례 | 선례 패턴 복제 — 상태별 반환값만 다름 |
| resw `PlaylistsStopLabel.Text`·`ItemPlayName` | `Tray_Stop`·`HomeStopLabel.Text`("정지") | 정지 문구는 `Tray_Stop` **재사용**(Loc.Get). 상단 정지 라벨은 x:Uid 방식이라 별도 키 필요, 행 재생 이름은 x:Uid 속성 키 → plain 키 이전 |

## 시각 요소 분해
(해당 없음 — 기준 시안이 없는 동작 토글. 표시 형태는 기존 홈 정지 pill·행 버튼 스타일을 그대로 따른다)

## Tasks

- [x] T1. 플레이리스트 재생/정지 토글 — VM 상태 + 상단·행 버튼 전환 (FR-18 충족)
  - **Type**: D (다중 파일 + 공개 멤버 개명)
  - **Design**:
    - 배치: 토글 상태·정지 로직은 `PlaylistsViewModel`(기존 재생 진입점 파일), 표시 전환은 `PlaylistsPage`(x:Bind 함수·가시성 스왑), 문구는 resw. 상태 정본은 Coordinator(`CurrentPlaylistId`·`CurrentItemId`) — VM은 미러만 유지.
    - 신규 심볼: `IsSelectedPlaying`(observable bool — 상단 버튼 상태), `TogglePlayItemAsync(entry)`(`PlayItemAsync` 개명 — IsNowPlaying이면 StopAsync, 아니면 기존 재생), `PlaylistsPage.PlayToggleGlyph(bool)`·`RowPlayGlyph(bool)`(E71A/E768)·`RowPlayName(bool)`(Loc.Get "Tray_Stop"/"ItemPlayName") 정적 x:Bind 함수.
    - 의존 방향: View → VM → Coordinator (기존 그대로, 역참조 없음).
    - 비추상화: 상태 enum·컨버터·스타일 셀렉터·전용 커맨드 클래스 도입 안 함 — bool + 정적 함수로 충분(`RankVisibility` 선례). 셔플듣기·홈·트레이 코드는 건드리지 않음.
  - **수정 내용**:
    1. `PlaylistsViewModel.cs` — `[ObservableProperty] bool IsSelectedPlaying` 신설. 갱신 지점: `UpdateNowPlaying()` 말미(StatusChanged·페이지 진입·Populate 말미가 기존 호출) + `OnSelectedPlaylistChanged`. 값 = `SelectedPlaylist is not null && IsPlaying(SelectedPlaylist)`. `PlayAsync`: `IsSelectedPlaying`이면 `Coordinator.StopAsync()` 후 반환, 아니면 기존 `StartPlaybackAsync`. `PlayItemAsync` → `TogglePlayItemAsync`로 개명: `entry.IsNowPlaying`이면 `StopAsync`, 아니면 기존 행 재생. 주석 갱신.
    2. `PlaylistsPage.xaml` — 상단 버튼: FontIcon `Glyph="{x:Bind local:PlaylistsPage.PlayToggleGlyph(ViewModel.IsSelectedPlaying), Mode=OneWay}"`, 문구는 기존 `x:Uid PlayAllLabel` TextBlock(`InvertVisibility(ViewModel.IsSelectedPlaying)`) + 신규 `x:Uid PlaylistsStopLabel` TextBlock(`Visibility="{x:Bind ViewModel.IsSelectedPlaying, Mode=OneWay}"`) 가시성 스왑. 행 버튼: `x:Uid ItemPlayButton` 제거, FontIcon Glyph를 `RowPlayGlyph(IsNowPlaying)`, `AutomationProperties.Name`을 `RowPlayName(IsNowPlaying)` (모두 Mode=OneWay).
    3. `PlaylistsPage.xaml.cs` — 정적 함수 3개 추가(`NowPlayingLabel` 옆), `OnPlayItemClick`의 호출을 `TogglePlayItemAsync`로 변경 + 주석 갱신.
    4. `Strings/en-US·ko-KR/Resources.resw` — `PlaylistsStopLabel.Text`("Stop"/"정지") 신설, `ItemPlayName`("Play from this track"/"이 곡부터 듣기") plain 키 신설, 기존 `ItemPlayButton.[…]AutomationProperties.Name` 항목 제거(값은 ItemPlayName으로 이전 — 번역 보존).
  - **Acceptance**: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` 경고 0·오류 0 + `dotnet test` 전체 통과(코디네이터 무변경 — 기존 125 유지). 동작(HUMAN-VERIFY 구분): ① 선택 리스트 재생·일시정지 중 상단 버튼 = E71A "정지", 클릭 시 정지(배경창 정리·좌우 스피커 글리프 해제) ② 정지 상태·다른 리스트 재생 중 = E768 "전체듣기", 클릭 시 선택 리스트 재생(전환) ③ 재생 중인 곡 행 버튼 = E71A·접근성 이름 "정지", 클릭 시 정지; 그 외 행 = E768 "이 곡부터 듣기"·클릭 시 그 곡부터 재생 ④ 곡 전환·정지 시 행 버튼 표시가 스피커 글리프와 함께 이동·복귀 ⑤ 문구 하드코딩 0(resw 경유).
  - **Files**:
    - 주: `src/DeskTube/ViewModels/PlaylistsViewModel.cs`, `src/DeskTube/Views/PlaylistsPage.xaml`, `src/DeskTube/Views/PlaylistsPage.xaml.cs`, `src/DeskTube/Strings/en-US/Resources.resw`, `src/DeskTube/Strings/ko-KR/Resources.resw`
    - 참조(수정 없음): `PlaybackCoordinator.cs`(StopAsync·CurrentPlaylistId), `Loc.cs`, `HomeViewModel.cs`(선례)
  - **Edge Cases**:
    - 선택 리스트 없음: 버튼 영역 자체가 `HasSelection` 가시성 뒤라 비노출 + `PlayAsync` 기존 null 가드 유지 → IsSelectedPlaying false.
    - 일시정지 중: CurrentPlaylistId·CurrentItemId 유지(정본 주석) → 정지 버튼 유지 (홈 pill "일시정지 중에도 정지 가능" 선례와 통일).
    - 다른 리스트 재생 중 전체듣기 클릭: StartAsync가 내부 StopAsync 선행 → StatusChanged 2회(Stopped→Playing)로 토글·글리프 일관 갱신.
    - 정지 버튼 연타: StopAsync 멱등(상태 무관 CleanupAll) — 2회째 no-op.
    - 재생 중 리스트 삭제: 기존 DeleteAsync가 StopAsync 경유 → StatusChanged로 토글 해제 (추가 처리 불요).
    - 빈 리스트 재생 실패: StartAsync 실패 반환 → Stopped 유지 → 전체듣기 표시 유지 + 기존 토스트.
    - 행 정지 후 같은 행 재클릭: IsNowPlaying false로 복귀했으므로 그 곡부터 재생 (토글 왕복 정상).
    - 페이지 재진입·재구성: `UpdateNowPlaying`이 Populate 말미·진입 시 이미 호출 → 초기 상태 반영 (신규 호출점 불요).
  - **Halt Forecast**: 없음 — 공개 멤버 개명(`PlayItemAsync`→`TogglePlayItemAsync`, 호출부 1곳)·`PlayAsync` 계약 변경(호출부 XAML 1곳)·resw 키 재배치는 아래 사전 승인 항목으로 일괄 승인. 파괴적·외부 작업 없음.
  - **Depends on**: 없음

- [x] T2. PRD FR-18 보강 + README 갱신 (FR-18 충족)
  - **Type**: A
  - **Acceptance**: FR-18에 "전체듣기 버튼은 선택 리스트 재생 중(일시정지 포함) 정지 버튼으로, 재생 중인 곡의 행 재생 버튼도 정지 버튼으로 토글(정지 시 원복)" 취지 1줄 보강 + 변경 이력 1줄(2026-07-18, 사용자 합의) / README 플레이리스트 기능 설명 갱신. 문서-코드 역대조 누락·잔존·변형 0.
  - **Files**: 주: `docs/prd.md`, `README.md`
  - **Edge Cases**: (Type A — skip)
  - **Halt Forecast**: 없음 — PRD 보강은 질문 라운드에서 사용자 합의 완료.
  - **Depends on**: T1

## 사전 승인 항목 (일괄 승인 대상)
- T1 — `PlaylistsViewModel.PlayItemAsync` → `TogglePlayItemAsync` 공개 멤버 개명 + 호출부 1곳(`PlaylistsPage.xaml.cs:317`) 동반 수정 (계획된 공개 API 변경)
- T1 — `PlayCommand`(PlayAsync) 동작 계약 변경: 선택 리스트 재생 중이면 정지 (호출부 XAML 1곳 — 이번 요청의 목적 그 자체)
- T1 — resw 키 재배치: `ItemPlayButton.[…]AutomationProperties.Name` 제거 → `ItemPlayName` plain 키 이전(양 언어 값 보존) + `PlaylistsStopLabel.Text` 신설
- T2 — PRD FR-18 보강 (질문 라운드 합의 완료 — 승인 재확인용 명시)

## 불가피한 Halt (위임 불가)
- (없음)

## 검증 방법
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` (경고 0·오류 0)
- 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -c Debug -p:Platform=x64` (기존 전체 통과 — 코디네이터·서비스 계층 무변경이라 신규 단위테스트 없음, VM 표시 로직은 레포 관례상 테스트 비대상)
- 포맷: `dotnet format` 위반 0
- 시각·동작(HUMAN-VERIFY): T1 Acceptance ①~④ — 재생/정지 토글 왕복, 일시정지 유지, 다른 리스트 전환, 행 버튼 토글

## Decisions
- D1. 토글 판정 기준 = **선택 리스트**(`CurrentPlaylistId == SelectedPlaylist.Id`, 일시정지 포함) — 사용자 확정. Source: 질문 라운드 Q1 + `IsPlaying(PlaylistEntry)` 기존 판정식(`PlaylistsViewModel.cs:463`).
- D2. 적용 범위 = 상단 전체듣기 버튼 + **행 재생 버튼**(재생 중인 곡 행만 정지로) — 사용자 확정 (질문 Q2).
- D3. 일시정지 중에도 정지 버튼 유지. Source: `HomeViewModel.cs:68` 주석("일시정지 중에도 정지 가능해야 함") + `PlaybackCoordinator.cs:93` CurrentPlaylistId 일시정지 유지 정본 주석 — 홈 pill과 기준 통일.
- D4. 정지 표시 = 글리프 E71A + 문구 "정지"/"Stop". Source: 홈 정지 pill(`HomePage.xaml:136`)·트레이(`Tray_Stop`)와 동일 — 앱 내 정지 표기 통일.
- D5. 상단 버튼 스타일 = AccentButtonStyle 유지(같은 버튼의 내용만 전환). Source: AGENTS.md 디자인 규칙 6 "Primary 버튼 화면당 1개" — 버튼 수 불변.
- D6. 문구 처리 = 상단 라벨은 x:Uid 가시성 스왑(하드코딩 금지 규칙 준수·`RankVisibility` 상호배타 선례), 행 접근성 이름은 Loc.Get 정적 함수(`NowPlayingLabel` 선례·deferred #25 BadgeToggleName 제안과 동형). Loc의 슬래시 키(x:Uid 속성 키) 조회는 레포 선례가 없어 쓰지 않는다.
- D7. 미커밋 진단 계측 변경은 그대로 유지, 이번 커밋은 이번 변경 파일만 스테이징 — 사용자 확정 (질문 Q4).
- D8. PRD 보강 진행 + plan에 PRD 연결(Phase G 활성) — 사용자 확정 (질문 Q3).

## Next Steps
- 권장 다음 액션: HUMAN-VERIFY 4항목(토글 시각·동작) 확인 → 이상 없으면 main 병합·진단 계측 커밋 여부 결정 (둘 다 별도 승인)
- Suggested skills: 공식 /code-review (원하면), pjc:llm-wiki (vault 미설정 — 해당 없음)
- notes.md의 이번 작업 기록은 미커밋 상태 — 미커밋 진단 계측 항목과 같은 파일이라 스테이징 시 섞임(사용자 Q4 결정에 따른 보류). 진단 계측 커밋 때 함께 커밋 권장.

## Phase Ledger
- 전 task(T1~T2) 완료
- Phase F 통과 (HEAD 3cad707) — 빌드 0·0, 테스트 125/125(F-2), 포맷 0, plan-completion-reviewer OK(BLOCKER/MAJOR/MINOR 0)
- Phase G 통과 (Must 100%) — 커버 대상 FR-18 보강분 코드 정합 확인(시각·동작은 ⏳ HUMAN-VERIFY), 범위 외 FR 제외 규정 적용

## Open Questions
- [x] Q1: 정지 버튼 표시 기준? → **선택 리스트 기준** (사용자 답변 2026-07-18)
- [x] Q2: 적용 범위? → **상단 버튼 + 행 재생 버튼 포함** (사용자 답변 2026-07-18)
- [x] Q3: PRD FR-18 보강? → **보강 진행** (사용자 답변 2026-07-18)
- [x] Q4: 미커밋 진단 계측 변경 처리? → **그대로 두고 진행** (사용자 답변 2026-07-18)
