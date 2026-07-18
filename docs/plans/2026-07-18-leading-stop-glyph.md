# Plan: 듣기 버튼 선행 아이콘의 재생 중 정지 전환 — 트레일링 아이콘 대체 (2026-07-18)

**PRD**: docs/prd.md

## 요구 이해
- **원문 요청**: "전체듣기/셔플듣기 버튼의 문구 앞에 있는 아이콘을 재생시 정지 아이콘으로 변경해줘"
- 현재는 재생 중인 모드 버튼의 **문구 뒤에** 정지 아이콘(E71A)이 추가로 붙는다(직전 mode-indicator 작업).
- 이를 **문구 앞의 기존 아이콘**(전체듣기 E768 / 셔플듣기 E8B1)이 해당 모드로 재생(일시정지 포함) 중일 때 **정지 아이콘(E71A)으로 바뀌는** 방식으로 교체한다 — 트레일링 아이콘은 제거.
- 클릭 동작·VM 상태(`IsShufflePlaying`/`IsSequentialPlaying`)·행 버튼 토글은 그대로 유지. 표시 위치만 이동.

## Goal
재생 중인 모드 버튼은 앞쪽 아이콘 자체가 정지 아이콘으로 바뀌어(문구 유지) 상태가 보이고, 그 버튼 클릭 시 정지한다.

## PRD Coverage
| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-18 (보강 수정: 선행 아이콘 정지 전환) | Must | T1, T2 | ✅ 커버 |
| FR-1~FR-16, FR-19~FR-21, 나머지 FR-18 항목 | Must/Could | (기구현) | 이번 범위 외 (기구현/후속) |

## Out of Scope
- 클릭 계약·VM 상태 변경 — 직전 mode-indicator 구조 그대로 (표시만 이동)
- 행 재생 버튼·홈·트레이 표시 변경 — 요청에 없음

## Deferred / Follow-up
- [MINOR] E71A/E768 글리프 매핑이 `RowPlayGlyph`·`PlayToggleGlyph` 2회 반복 — 3회째 등장 시 공통화 (implement-task 규칙 5의 3회 문턱 적용 중 — 발동 스킬 규약이 글로벌 2회 문턱보다 우선. 출처: plan-reviewer m1)

## Investigation Log
- 위키 참조: vault 미설정 — 코드 1차 출처로 진행 (동일 세션 앞선 plan과 동일 상태)
- Deferred 대장 `## 대기` 확인 — 관련 항목 없음 ([MINOR] "멜론 plan D4" 주석 표기 건은 무관 — 이번 수정 파일과 겹치나 별개 사안, 대장 유지)
- 직전 plan `2026-07-18-playlist-mode-indicator.md`: Phase Ledger `Phase G 통과 (Must 100%)` → 완료 확정. 이번 plan은 그 표시 방식을 수정하는 후속(같은 세션 사용자 요청).
- `PlaylistsPage.xaml:195~229`(현 HEAD 251684a) — 셔플듣기 버튼: 선행 FontIcon `&#xE8B1;` 정적 + 문구 + 트레일링 E71A(`IsShufflePlaying` 가시성, `StopLabel()` 이름·툴팁). 전체듣기 버튼: 선행 `&#xE768;` 정적 + 문구 + 트레일링 E71A(`IsSequentialPlaying`). **직접 확인 완료**(직전 세션에서 본인이 작성·V-7 검증).
- `PlaylistsPage.xaml.cs:70~71` `StopLabel()` — 트레일링 아이콘 전용 소비(2곳). 트레일링 제거 시 소비자가 선행 아이콘의 재생 중 이름·툴팁으로 이동 필요(정지 상태에선 이름·툴팁 불필요 — 장식 아이콘).
- VM `IsShufflePlaying`·`IsSequentialPlaying`(`PlaylistsViewModel.cs:129·134`) — 가시성 소비 2곳이 이번에 글리프 함수 인자로 바뀜. VM 무변경.
- `RowPlayGlyph(bool)`(`PlaylistsPage.xaml.cs:74`) — E71A/E768 동일 매핑 기존 구현(행 전용). 헤더 전체듣기용과 매핑 동일하나 의미(행/헤더)가 달라 별도 함수 유지 — 공통화 문턱(3회 반복)은 이번에 2회째라 미달(implement-task 규칙 5).
- x:Bind 함수의 null 반환: `ToolTipService.ToolTip`에 null이면 툴팁 미표시, `AutomationProperties.Name` null이면 기본(콘텐츠 유도)으로 폴백 — WinUI 표준 동작. 정지 상태의 선행 아이콘은 장식(이름 불필요)이므로 null 폴백이 목표 동작과 일치.

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| `PlayToggleGlyph(bool)` (E71A/E768) | `RowPlayGlyph`(동일 매핑, 행 전용) — 직전 plan에서 제거했던 동명 함수 재도입 | 의미(헤더/행) 구분 유지 위해 별도 신규 — 동일 매핑 2회째로 공통화 문턱(3회) 미달 |
| `ShuffleToggleGlyph(bool)` (E71A/E8B1) | 없음 (셔플 글리프 매핑 최초) | 신규 |
| `StopTip(bool)` (재생 중 "정지" 이름·툴팁, 아니면 null) | `StopLabel()`(무조건 "정지" 반환 — 트레일링 전용) | `StopLabel()`을 조건부 반환으로 **교체**(트레일링 제거로 무조건 버전은 고아) |

## 시각 요소 분해
(해당 없음 — 기준 시안 없는 상태 표시 이동. 기존 글리프·토큰 체계 유지)

## Tasks

- [x] T1. 선행 아이콘 정지 전환 — XAML 두 버튼 + 글리프 함수 (FR-18 충족)
  - **Type**: C (2개 파일, XAML caller 갱신 있음, VM 무변경)
  - **Design**:
    - 배치: 글리프·툴팁 함수는 `PlaylistsPage.xaml.cs` 정적 x:Bind 함수(기존 `RowPlayGlyph` 옆), 표시는 `PlaylistsPage.xaml` 두 버튼 선행 FontIcon. VM·resw 무변경.
    - 신규 심볼: `PlayToggleGlyph(bool)`(E71A/E768 — 전체듣기 선행), `ShuffleToggleGlyph(bool)`(E71A/E8B1 — 셔플듣기 선행), `StopTip(bool)`(재생 중 Loc "Tray_Stop", 아니면 null — 선행 아이콘 이름·툴팁). `StopLabel()`은 제거(트레일링 소멸로 고아).
    - 의존 방향: View → VM(bool 2종) → Coordinator (기존 그대로).
    - 비추상화: 두 글리프 함수를 파라미터화한 공통 함수로 묶지 않음(3회 문턱 미달·의미 분리 유지), 컨버터·enum 미도입.
  - **수정 내용**:
    1. `PlaylistsPage.xaml` — 셔플듣기 선행 FontIcon: `Glyph="{x:Bind local:PlaylistsPage.ShuffleToggleGlyph(ViewModel.IsShufflePlaying), Mode=OneWay}"` + `AutomationProperties.Name`·`ToolTipService.ToolTip`=`StopTip(ViewModel.IsShufflePlaying)`(Mode=OneWay). 전체듣기 선행 FontIcon: `PlayToggleGlyph(ViewModel.IsSequentialPlaying)` + `StopTip(ViewModel.IsSequentialPlaying)` 동일 적용. **트레일링 FontIcon 2개 제거.** 주석 갱신(선행 아이콘 전환 방식).
    2. `PlaylistsPage.xaml.cs` — `StopLabel()` 제거, `PlayToggleGlyph`·`ShuffleToggleGlyph`·`StopTip` 신설(한글 주석 — 왜).
  - **Acceptance**: 빌드 경고 0·오류 0 + 테스트 전체 통과 + format 위반 0. 동작(HUMAN-VERIFY 구분): ① 셔플 재생(일시정지 포함) 중 셔플듣기 선행 아이콘 = E71A(문구 "셔플듣기" 유지·트레일링 없음), 클릭 시 정지 ② 비셔플 재생 중 전체듣기 선행 아이콘 = E71A ③ 정지·다른 리스트 선택 시 각각 E8B1/E768 원복 ④ 재생 중 아이콘에 툴팁 "정지", 정지 상태엔 툴팁 없음 ⑤ `StopLabel` 잔존 참조 0·트레일링 아이콘 XAML 잔존 0.
  - **Files**:
    - 주: `src/DeskTube/Views/PlaylistsPage.xaml`, `src/DeskTube/Views/PlaylistsPage.xaml.cs`
    - 참조(수정 없음): `PlaylistsViewModel.cs`(bool 2종), `Loc.cs`, resw(`Tray_Stop` 재사용)
  - **Edge Cases**:
    - 일시정지 중: bool 유지 → 정지 아이콘 유지 (기존 D5 승계).
    - 정지·다른 리스트 선택: bool false → 원래 글리프·툴팁 없음(StopTip null 폴백).
    - hover(셔플 버튼 OnOutlineButtonPointerEntered): 테두리만 변경 — 글리프 함수 바인딩과 무충돌(직전 확인 승계). 전체듣기 AccentButtonStyle은 hover 핸들러 없음.
    - x:Bind 함수 null 반환: ToolTip 미표시·Name 기본 폴백 (Investigation Log 근거).
  - **Halt Forecast**: 없음 — `StopLabel()` 제거는 소비자(트레일링 2곳)가 같은 diff에서 소멸하는 내부 정리(공개 정적 멤버이나 View 전용·계획된 변경 — 사전 승인 항목 등재). 파괴적·외부 작업 없음.
  - **Depends on**: 없음

- [x] T2. PRD FR-18 서술 수정 + README 갱신 (FR-18 충족)
  - **Type**: A
  - **Acceptance**: FR-18의 "문구 뒤에 정지 아이콘 표시" 서술을 "문구 앞 아이콘이 재생 중 정지 아이콘으로 전환(문구 유지)"으로 수정 + 변경 이력 1줄. README 재생/정지 토글 항목 동일 취지 갱신. 역대조 누락·잔존·변형 0.
  - **Files**: 주: `docs/prd.md`, `README.md`
  - **Edge Cases**: (Type A — skip)
  - **Halt Forecast**: 없음 — 사용자 요청이 곧 합의.
  - **Depends on**: T1

## 사전 승인 항목 (일괄 승인 대상)
- T1 — `PlaylistsPage.StopLabel()` 공개 정적 함수 제거 → `StopTip(bool)`로 교체 (소비자 트레일링 아이콘 2곳이 같은 diff에서 제거 — 계획된 변경)
- T1 — 트레일링 정지 아이콘 2개 제거 (직전 작업 산출물의 표시 방식 교체)
- T2 — PRD FR-18 서술 수정 (사용자 요청 반영)

## 불가피한 Halt (위임 불가)
- (없음)

## 검증 방법
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` (경고 0·오류 0)
- 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -c Debug -p:Platform=x64` (VM·서비스 무변경 — 기존 전체 통과 유지)
- 포맷: `dotnet format` 위반 0
- 시각·동작(HUMAN-VERIFY): T1 Acceptance ①~④

## Decisions
- D1. 표시 방식 = 선행 아이콘 자체를 E71A로 전환 + 트레일링 제거 — 사용자 지시("문구 앞에 있는 아이콘을 재생시 정지 아이콘으로 변경").
- D2. 접근성·툴팁 = `StopTip(bool)` — 재생 중에만 "정지"(Tray_Stop 재사용), 정지 상태는 null 폴백(장식 아이콘 — 기존 선행 아이콘과 동일 상태로 복귀). Source: Investigation Log null 폴백 동작.
- D3. 클릭 계약·VM 상태 불변 — 직전 plan D4 승계 (표시만 이동).
- D4. 글리프 함수 2개 분리 유지(공통화 안 함) — implement-task 규칙 5(3회 문턱)·의미 분리. Source: 4-D 표.
- D5. 미커밋 진단 계측·notes.md 커밋 보류 승계 — 기존 D7 (동일 세션 사용자 확정).

## Open Questions
- (없음 — 표시 방식을 사용자가 직접 지정)
