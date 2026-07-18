# Plan: 셔플듣기/전체듣기 재생 모드 구분 표시 — 문구 유지 + 트레일링 정지 아이콘 (2026-07-18)

**PRD**: docs/prd.md

## 요구 이해
- **원문 요청**: "셔플듣기/전체듣기 구분이 안되는데 셔플듣기 클릭하면 셔플듣기 표시하고 전체듣기 클릭하면 전체듣기에 표시해서 상태을 알수 있게 해줘" + 명확화 "정지 문구로만 표시하지 말고 셔플듣기 문구 뒤에 정지 아이콘 추가해서 표시"
- 현재는 셔플듣기로 시작해도 전체듣기 버튼만 "정지"로 바뀌어 어느 모드로 재생 중인지 구분이 안 된다.
- **재생을 시작한 모드의 버튼**에 상태를 표시하되, 문구를 "정지"로 교체하지 않고 **버튼 문구("셔플듣기"/"전체듣기")는 유지 + 문구 뒤에 정지 아이콘(E71A)을 추가**한다. 그 버튼 클릭 시 정지.
- 직전 stop-toggle 작업의 "전체듣기 ↔ 정지 문구 교체" 방식은 이 방식으로 **대체**된다(문구 교체 제거). 행 재생 버튼 토글은 그대로 유지.
- 다른 모드 버튼은 평소 모습 그대로이며 클릭 시 그 모드로 재생 전환(현행 동작 유지).

## Goal
플레이리스트 페이지에서 셔플듣기/전체듣기 중 어느 모드로 재생 중인지 버튼만 보고 구분할 수 있고, 그 버튼으로 바로 정지할 수 있다.

## PRD Coverage
| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-18 (보강 수정: 모드별 정지 아이콘 표시) | Must | T1, T2 | ✅ 커버 |
| FR-1~FR-16, FR-19~FR-21, 나머지 FR-18 항목 | Must/Could | (기구현) | 이번 범위 외 (기구현/후속) |

## Out of Scope
- 행 재생 버튼 표시 변경 — 직전 작업의 행 토글(E71A) 유지, 이번 요청은 상단 두 버튼의 모드 구분
- 홈·트레이의 모드 표시 — 요청에 없음
- 재생 모드 자체의 변경(랜덤·한곡반복 UI 노출 등) — 내부 잔존 모드는 표시상 전체듣기(비셔플)로 묶음

## Deferred / Follow-up
- [MINOR] `ShuffleAllAsync` 주석의 "멜론 plan D4" 축약 표기를 전체 slug(`2026-07-16-melon-playlist-style`)로 명확화 검토 — 리뷰 2건이 무의미 단어로 오인(레포 관례 표기이나 혼동 소지, 출처: T1 spec M1·quality m1)

## Investigation Log
- 위키 참조: vault 미설정 — 코드 1차 출처로 진행 (동일 세션 직전 plan과 동일 상태)
- Deferred 대장(`docs/plans/deferred.md`) `## 대기` 확인 — 관련 항목 없음 (직전 stop-toggle plan Deferred도 "(없음)")
- 직전 plan `2026-07-18-playlist-stop-toggle.md`: Phase Ledger `Phase G 통과 (Must 100%)` + Deferred 없음 → 완료 확정. 이번 plan은 그 산출물(문구 교체 방식)을 수정하는 후속.
- `PlaybackMode.cs` — Sequential/Shuffle/Random/RepeatOne/RepeatAll 5종. Shuffle 외 나머지는 UI 진입점이 전체듣기·행 재생(순차)뿐이라 표시상 "비셔플" 묶음으로 처리(레거시 저장값 호환).
- `PlaybackCoordinator.cs:350~355` `SetModeAsync` — `_settings.Mode`와 실행 중 `_queue.SetMode`를 **함께** 갱신 → `Coordinator.Settings.Mode`(공개 accessor `:116`)가 "지금 재생 모드" 정본으로 유효. 유일한 호출부는 `PlaylistsViewModel.StartPlaybackAsync`(`:663`, 전 레포 grep) — 모드 변경은 항상 재생 시작 직전에만 발생하고 StartAsync가 StatusChanged를 발화하므로 **기존 StatusChanged→UpdateNowPlaying 갱신 경로 재사용 가능**(신규 이벤트 불필요).
- `PlaybackCoordinator.cs:123~128` StartAsync 재생 중 재호출 시 StopAsync 선행(직전 plan 확인 재사용) — 모드 전환 재시작도 StatusChanged 2회 발화 보장.
- 트레이·자동재생(`StartLastAsync`)은 SetModeAsync 없이 마지막 모드로 시작 → StatusChanged 발화로 표시 자동 정합(모드 정본 기준이라 진입점 무관).
- `PlaylistsPage.xaml:206~229`(현 HEAD) 상단 버튼 — 직전 작업으로 `PlayToggleGlyph` 함수 바인딩 + `PlayAllLabel`/`PlaylistsStopLabel` 가시성 스왑 상태. 이번에 이 스왑을 **제거**하고 트레일링 아이콘 방식으로 교체.
- `PlaylistsPage.xaml:195~205` 셔플듣기 버튼 — `AppPillButtonStyle`, E8B1 글리프, `ShuffleAllLabel`("셔플듣기"/"Shuffle all"), hover는 `OnOutlineButtonPointerEntered/Exited`(`xaml.cs:86~100` — **테두리만 변경, Content 패턴 매칭 없음** → 트레일링 아이콘 추가에 안전).
- `PlaylistsViewModel.cs` 현 상태 — `IsSelectedPlaying`(직전 T1 신설), `PlayAsync` 토글, `TogglePlayItemAsync`, `UpdateNowPlaying` 말미 갱신, `OnSelectedPlaylistChanged` 재판정. 이번에 `IsSelectedPlaying`을 모드 분리 2속성으로 대체.
- `IsSelectedPlaying` 사용처 전수(전 레포 grep, 직전 세션 V-7 결과 + 현 HEAD 동일): VM 내부 3곳 + `PlaylistsPage.xaml` 3곳(글리프 함수·가시성 2) — 전부 이번 수정 범위 안. 외부 소비자 없음.
- `PlaylistsStopLabel.Text`(resw 양 언어) — 직전 작업 신설, 소비자는 XAML 가시성 스왑 TextBlock 1곳뿐 → 스왑 제거 시 고아가 되므로 함께 제거(위생).
- 접근성 선례 — 스피커 글리프에 `AutomationProperties.Name` + `ToolTipService.ToolTip` 부여(`NowPlayingLabel()` 정적 함수, `PlaylistsPage.xaml:119~120` 등). 트레일링 정지 아이콘에 동일 패턴 적용, 문구는 `Tray_Stop`("정지"/"Stop") 재사용.

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| `IsShufflePlaying`·`IsSequentialPlaying` (VM observable 2종) | `IsSelectedPlaying`(모드 무구분 — 이번에 제거·대체) | 모드 구분이 요구라 분리 신설. 판정식은 기존 `IsPlaying(SelectedPlaylist)` + `Coordinator.Settings.Mode` 재사용 |
| `StopLabel()` (정적 x:Bind 함수 — 정지 아이콘 접근성 이름·툴팁) | `NowPlayingLabel()` 동일 패턴 선례 | 선례 복제, `Tray_Stop` 키 재사용 (신규 resw 키 없음) |
| (제거) `PlayToggleGlyph`·`PlaylistsStopLabel.Text` | — | 문구 교체 방식 폐지에 따른 고아 정리 |

## 시각 요소 분해
(해당 없음 — 기준 시안이 없는 상태 표시 변경. 아이콘·문구는 기존 토큰·글리프 체계를 따른다)

## Tasks

- [x] T1. 모드별 트레일링 정지 아이콘 — VM 상태 분리 + 두 버튼 표시 전환 (FR-18 충족)
  - **Type**: D (다중 파일 + 공개 observable 제거·커맨드 계약 변경)
  - **Design**:
    - 배치: 모드 판정·토글 로직은 `PlaylistsViewModel`, 표시는 `PlaylistsPage.xaml`(트레일링 FontIcon + 가시성 바인딩), 접근성 문구는 기존 `Tray_Stop` 재사용. 상태 정본은 Coordinator(`CurrentPlaylistId` + `Settings.Mode`) — VM은 미러만.
    - 신규 심볼: `IsShufflePlaying`(선택 리스트가 Shuffle 모드로 재생(일시정지 포함) 중), `IsSequentialPlaying`(선택 리스트가 비셔플 모드로 재생 중 — Sequential/Random/RepeatOne/RepeatAll 묶음), `PlaylistsPage.StopLabel()`(정지 아이콘 접근성 이름·툴팁 — Loc "Tray_Stop").
    - 의존 방향: View → VM → Coordinator (기존 그대로).
    - 비추상화: 모드별 버튼 상태 enum·컨버터·다중 인자 x:Bind 함수 도입 안 함 — VM이 계산 완료한 bool 2개만 노출(`RankVisibility`류 상호배타 선례). `IsSelectedPlaying`은 대체 제거(3상태 병존 방지).
  - **수정 내용**:
    1. `PlaylistsViewModel.cs` — `IsSelectedPlaying` 제거, `[ObservableProperty] bool IsShufflePlaying`·`IsSequentialPlaying` 신설. 갱신 지점 동일(`UpdateNowPlaying` 말미 + `OnSelectedPlaylistChanged`): `var playing = SelectedPlaylist is not null && SelectedPlaylist.Id == currentId; var shuffle = _services?.Coordinator.Settings.Mode == PlaybackMode.Shuffle; IsShufflePlaying = playing && shuffle; IsSequentialPlaying = playing && !shuffle;`. `PlayAsync`: `IsSequentialPlaying`이면 StopAsync 후 반환(기존 IsSelectedPlaying 분기 교체). `ShuffleAllAsync`: `IsShufflePlaying`이면 StopAsync 후 반환, 아니면 기존 셔플 시작. 주석 갱신.
    2. `PlaylistsPage.xaml` — 전체듣기 버튼: 선행 FontIcon을 정적 `&#xE768;`로 원복(`PlayToggleGlyph` 바인딩 제거), `PlaylistsStopLabel` TextBlock·`PlayAllLabel` Visibility 스왑 제거(문구 상시 표시), 문구 뒤 트레일링 FontIcon(E71A, `Visibility="{x:Bind ViewModel.IsSequentialPlaying, Mode=OneWay}"`, `AutomationProperties.Name`·ToolTip=`StopLabel()`) 추가. 셔플듣기 버튼: 동일 트레일링 FontIcon(E71A, `IsShufflePlaying`) 추가. 주석 갱신.
    3. `PlaylistsPage.xaml.cs` — `PlayToggleGlyph` 제거(고아), `StopLabel()` 신설(`NowPlayingLabel` 옆). `RowPlayGlyph`·`RowPlayName`·행 토글은 불변.
    4. `Strings/en-US·ko-KR/Resources.resw` — `PlaylistsStopLabel.Text` 항목 제거(고아 정리, 직전 작업 신설분).
  - **Acceptance**: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` 경고 0·오류 0 + `dotnet test` 전체 통과 + `dotnet format` 위반 0. 동작(HUMAN-VERIFY 구분): ① 셔플듣기로 재생(일시정지 포함) 중 셔플듣기 버튼 = "셔플듣기" 문구 + 뒤 정지 아이콘(E71A), 클릭 시 정지; 전체듣기 버튼은 평소 모습 ② 전체듣기·행 재생으로 재생 중 전체듣기 버튼 = "전체듣기" + 정지 아이콘, 클릭 시 정지; 셔플듣기는 평소 모습·클릭 시 셔플로 재시작 ③ 정지 상태·다른 리스트 재생 중엔 두 버튼 모두 아이콘 없음 ④ "정지" 문구 교체는 더 이상 없음(문구 상시 유지) ⑤ 문구 하드코딩 0, `PlaylistsStopLabel`·`PlayToggleGlyph`·`IsSelectedPlaying` 잔존 참조 0.
  - **Files**:
    - 주: `src/DeskTube/ViewModels/PlaylistsViewModel.cs`, `src/DeskTube/Views/PlaylistsPage.xaml`, `src/DeskTube/Views/PlaylistsPage.xaml.cs`, `src/DeskTube/Strings/en-US/Resources.resw`, `src/DeskTube/Strings/ko-KR/Resources.resw`
    - 참조(수정 없음): `PlaybackCoordinator.cs`(Settings.Mode·StopAsync), `PlaybackMode.cs`, `Loc.cs`
  - **Edge Cases**:
    - 일시정지 중: CurrentPlaylistId·Mode 유지 → 아이콘 유지 (직전 D3 기준 승계).
    - 셔플 재생 중 전체듣기 클릭: IsSequentialPlaying=false → 정지 아님, SetModeAsync(Sequential)+재시작(현행 유지). 반대 방향 동일.
    - 레거시 모드(Random/RepeatOne/RepeatAll 저장값)로 트레이·자동재생 시작: 비셔플 묶음 → 전체듣기 쪽 아이콘(합리적 폴백, UI 진입점 없음).
    - 다른 리스트 재생 중·선택 없음: 두 bool 모두 false → 아이콘 없음, 버튼은 시작 동작.
    - 정지 연타·재생 시작 실패(빈 리스트): StopAsync 멱등·StartAsync 실패 시 Stopped 유지(직전 plan Edge 승계).
    - 재생 중 리스트 삭제: DeleteAsync→StopAsync→StatusChanged로 두 bool 해제.
    - 셔플 버튼 hover: OnOutlineButtonPointerEntered는 테두리만 변경(Content 매칭 없음) → 아이콘 추가 무영향 확인 완료.
  - **Halt Forecast**: 없음 — `IsSelectedPlaying` 공개 observable 제거(소비자 XAML 3곳 전부 이번 수정 범위)·`ShuffleAllCommand` 동작 계약 변경(호출부 XAML 1곳)·resw 키 제거는 아래 사전 승인 항목으로 일괄 승인. 파괴적·외부 작업 없음.
  - **Depends on**: 없음

- [x] T2. PRD FR-18 토글 문구 조정 + README 갱신 (FR-18 충족)
  - **Type**: A
  - **Acceptance**: FR-18의 직전 토글 서술("정지 버튼으로 토글")을 "재생을 시작한 듣기 버튼(셔플듣기/전체듣기)의 문구는 유지한 채 뒤에 정지 아이콘을 표시하고 클릭 시 정지(행 재생 버튼 토글은 유지)" 취지로 수정 + 변경 이력 1줄(2026-07-18, 사용자 명확화). README "재생/정지 토글" 항목을 새 방식으로 갱신. 문서-코드 역대조 누락·잔존·변형 0.
  - **Files**: 주: `docs/prd.md`, `README.md`
  - **Edge Cases**: (Type A — skip)
  - **Halt Forecast**: 없음 — PRD 수정은 사용자 명확화 지시가 곧 합의(승인 재확인용 사전 승인 항목 명시).
  - **Depends on**: T1

## 사전 승인 항목 (일괄 승인 대상)
- T1 — `PlaylistsViewModel.IsSelectedPlaying` 공개 observable **제거** → `IsShufflePlaying`·`IsSequentialPlaying`으로 대체 (소비자 XAML 3곳 동반 수정 — 계획된 공개 API 변경)
- T1 — `ShuffleAllCommand`(ShuffleAllAsync) 동작 계약 변경: 셔플 재생 중이면 정지 (호출부 XAML 1곳)
- T1 — resw `PlaylistsStopLabel.Text` 키 제거(양 언어 — 직전 작업 신설분의 고아 정리) + XAML 문구 스왑 제거
- T2 — PRD FR-18 토글 서술 수정 (사용자 명확화 반영 — 승인 재확인용 명시)

## 불가피한 Halt (위임 불가)
- (없음)

## 검증 방법
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` (경고 0·오류 0)
- 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -c Debug -p:Platform=x64` (코디네이터 무변경 — 기존 전체 통과 유지)
- 포맷: `dotnet format` 위반 0
- 시각·동작(HUMAN-VERIFY): T1 Acceptance ①~④ — 모드별 아이콘 표시·클릭 정지·전환 재시작

## Decisions
- D1. 표시 방식 = **문구 유지 + 트레일링 정지 아이콘(E71A)** — 사용자 명확화("정지 문구로만 표시하지 말고 셔플듣기 문구 뒤에 정지 아이콘 추가"). 직전 문구 교체 방식은 제거.
- D2. 모드 판정 정본 = `Coordinator.Settings.Mode` — Source: `SetModeAsync`(`PlaybackCoordinator.cs:350`)가 설정·실행 큐 동시 갱신 + 유일 호출부가 재생 시작 직전(`PlaylistsViewModel.cs:663`)이라 재생 중 불변.
- D3. 셔플 외 모드(Sequential/Random/RepeatOne/RepeatAll)는 **비셔플 묶음**으로 전체듣기 버튼에 표시 — Source: `PlaybackMode.cs` 주석(레거시 잔존)·UI 진입점이 셔플/순차뿐.
- D4. 클릭 계약: 활성 모드 버튼 = 정지, 비활성 모드 버튼 = 그 모드로 (재)시작 — 현행 전환 동작 유지(직전 plan D1 승계).
- D5. 일시정지 중 아이콘 유지 — 직전 plan D3(홈 pill 선례) 승계.
- D6. 정지 아이콘 접근성 = `AutomationProperties.Name`+ToolTip을 `Tray_Stop` 재사용(`StopLabel()` 정적 함수 — `NowPlayingLabel` 선례). 행 버튼 접근성(`RowPlayName`)은 불변.
- D7. 미커밋 진단 계측 유지·이번 커밋은 이번 파일만 스테이징 — 직전 plan D7 승계(동일 세션 사용자 확정).

## Next Steps
- 권장 다음 액션: HUMAN-VERIFY 4항목(모드별 아이콘 표시·클릭 정지·전환) 확인 → 이상 없으면 main 병합·진단 계측 커밋 여부 결정 (각각 별도 승인)
- notes.md의 이번 기록은 미커밋(진단 계측 혼재 사유 승계 — stop-toggle plan Next Steps와 동일). 진단 계측 커밋 때 함께 커밋 권장.

## Phase Ledger
- 전 task(T1~T2) 완료
- Phase F 통과 (HEAD 3ee0419) — 빌드 0·0, 테스트 125/125(F-2), 포맷 0, plan-completion-reviewer OK(BLOCKER/MAJOR/MINOR 0)
- Phase G 통과 (Must 100%) — 커버 대상 FR-18 수정분 코드 정합 확인(시각·동작은 ⏳ HUMAN-VERIFY), 범위 외 FR 제외 규정 적용

## Open Questions
- [x] Q1: 표시 방식? → **문구 유지 + 문구 뒤 정지 아이콘** (사용자 명확화 답변 2026-07-18 — 선택지 제시 중 직접 지정)
