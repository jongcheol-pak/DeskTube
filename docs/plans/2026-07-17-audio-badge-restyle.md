# Plan: 소리 배지 리스타일 + 홈 음소거 토글

**PRD**: docs/prd.md

## 요구 이해
- **원문 요청**: "배지 모양을 사각형에 모서리만 조금 둥글게 하고 소리 글자 색상이 잘 보이지 않아서 가독성이 떨어 지는데 색상 변경하고 아이콘도 잘 보이지 않으니 변경해줘, 그리고 홈 화면에서만 소리 배지를 클릭하 음소거 on/off 되도록 수정"
- **이해한 요구**: 홈·설정 모니터 카드의 소리 배지를 ① 알약형(radius 20) → 모서리 살짝 둥근 사각형으로, ② 글자·아이콘 색을 가독성 있게(코럴 배경 + 진한 글자, 사용자 확정), ③ 컬러 이모지 🔊 → 글자색과 같은 단색 스피커 글리프로 교체하고, ④ 홈 화면에서만 배지 클릭으로 음소거를 on/off 한다. 음소거 시 배지가 사라지는 기존 규칙으로는 ④가 성립 불가하므로, 배지를 오디오 대상 모니터에 항상 표시하고 소리/음소거 상태를 시각 구분한다(홈·설정 공통 표시, 사용자 확정).
- **포함하지 않는 것으로 이해**: 설정 화면 배지의 클릭 토글(홈 한정 명시), 배지 위치·크기 자체의 변경.

## Goal
모니터 카드 소리 배지를 잘 읽히는 사각 배지(소리/음소거 2상태)로 바꾸고, 홈에서는 배지 클릭으로 음소거를 전환할 수 있게 한다.

## PRD Coverage
<!-- 소규모 연결 plan — 이번에 닿는 FR만 커버 대상, 나머지는 범위 외 (plan-feature Step 1 경량 연결) -->
| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-5 (음소거 — 홈 배지 토글 진입점 추가) | Must | T2, T3, T4, T5 | ✅ 커버 |
| FR-4 (오디오 1개 모니터 출력 — 배지 표시 규칙만 변경, 출력 로직 불변) | Must | T3 (표시만) | ✅ 커버 (표시 규칙) |
| FR-1~FR-3, FR-6~FR-16, FR-18~FR-19 (나머지 active Must) | Must | (없음) | 이번 범위 외 (기구현) |

## Out of Scope
- 배지 위치(우상단)·전체 카드 레이아웃 변경
- 볼륨 슬라이더 등 음소거 외 오디오 컨트롤의 홈 노출
- 설정 화면 배지의 클릭 토글 (사용자가 "홈 화면에서만" 명시 — 영구 제외로 이해)

## Deferred / Follow-up
- 설정 화면 음소거 ToggleSwitch가 트레이/홈 배지 토글을 실시간 반영하지 않는 문제(화면 재진입 시에만 동기화) — 기존 동작이며 이번 범위 밖. MutedChanged 이벤트가 생기므로 SettingsViewModel 구독으로 해결 가능 (후속)

## Investigation Log
- 배지 구현 위치: `Controls/MonitorCardsControl.xaml` 두 템플릿(Large 36-49행 / Compact 94-107행)의 Border+TextBlock, 텍스트는 resw `MonitorAudioBadge.Text`("🔊 소리"/"🔊 Sound") — Read로 확인. 아이콘은 이미지가 아니라 문구 내 컬러 이모지.
- 배지 표시 식: `MonitorPanelViewModel.UpdateAudioBadges()` — `!IsMuted ∧ 선택됨 ∧ 오디오 대상` (129-135행). 음소거하면 배지가 사라져 클릭 해제 불가 → 상태 구분 표시로 변경(사용자 확정 Q1-A).
- `AppBadgeCornerRadius`(20) 사용처: grep 전수 — MonitorCardsControl 배지 2곳뿐(다른 알약형은 `AppPillCornerRadius` 별도 키). 값 변경 안전.
- `MonitorAudioBadge` x:Uid 사용처: grep 전수 — MonitorCardsControl 2곳 + resw ko/en. 키 교체 시 함께 제거.
- 음소거 조작 경로: SettingsViewModel.OnIsMutedChanged(398행) / TrayIconService.OnVolumeClick(97행) → 둘 다 `PlaybackCoordinator.SetMutedAsync`(228행) 경유. 코디네이터에 mute 변경 이벤트 없음(StatusChanged만, 76행) — 배지 실시간 동기화용 `MutedChanged` 이벤트 신설 근거.
- MonitorCardsControl 사용처: grep 전수 — HomePage.xaml 117행(IsLarge=True), SettingsPage.xaml 47행(IsLarge=False) 2곳뿐.
- MonitorPanelViewModel 소비자: HomeViewModel(24-27·88·109행), SettingsViewModel(37-39·168·191·207행) — Attach/Detach 대칭 구조 확인.
- 카드 자체가 Button(카드 클릭 = 모니터 선택 토글) — 배지를 홈에서 클릭 가능하게 하려면 내부 Button으로 분리해 이벤트 전파를 끊어야 함(Edge에 반영).
- 색 대비 실측: 코럴 #F25C54 위 흰 글자 ≈ 3.3:1(11px Bold에 부족) / #1A1A1C 글자 ≈ 5.3:1. 음소거 상태 #3A3A40 위 #B8B8BE ≈ 5.6:1.
- 테스트 관례: `tests/DeskTube.Tests` 서비스 계층만(VM 테스트 인프라 부재 — deferred 대장 기록). PlaybackCoordinatorTests가 가짜 주입으로 coordinator 직접 생성 — MutedChanged 이벤트 테스트 가능 확인.
- 홈 힌트 문구 `HomeMonitorsHint.Text` "클릭해서 선택 · 소리는 스피커 표시 모니터에서만" — 이모지 제거·클릭 토글 신설에 맞춰 갱신 필요(ko/en resw 69-71행).
- PRD 경량 확인: FR-5(음소거)·FR-10(설정 항목)에 닿음 — 홈 배지 토글은 FR-5의 새 UI 진입점 → PRD 갱신 제안 + `**PRD**:` 연결 (Phase G는 위 Coverage의 커버 대상만 재검증).
- Deferred 대장 확인: 이번 작업 직접 관련 항목 없음. 인접 항목 "직접 FontSize 지정 TextBlock 램프 스타일 일괄 점검"(design-1a T7 m1)은 별건 유지 — 이번 배지는 11px 캡션 크기라 램프 스타일 대상 아님.
- 위키 참조: 프로젝트 국소 UI 변경이라 조회 생략 (직전 plan들에서 vault 무매칭/미설정 확인 이력).
- 확정 시안과의 관계: 배지 모양·색·표시 규칙은 design-1a 시안(알약형·항상 흰 글자·음소거 시 숨김)에서 벗어남 — 사용자 요청(2026-07-17)이 시안을 대체. 시안 plan 문서는 이력이므로 수정하지 않음.

## Risks & Unknowns
| 위험 | 영향 | 완화책 |
|---|---|---|
| 중첩 Button(카드 Button 안 배지 Button)의 이벤트 전파 | 배지 클릭이 카드 선택 토글까지 발동 | WinUI Button은 pointer를 자체 처리해 부모 Click 미발동이 기대 동작 — 빌드 후 HUMAN-VERIFY 항목으로 명시 확인 |
| resw 키 교체 누락(옛 x:Uid 잔존) | 런타임 빈 텍스트(빌드는 통과) | T4에서 `MonitorAudioBadge` grep 0건 확인을 acceptance에 포함 |
| MutedChanged 발생 스레드 비보장 | UI 갱신 크래시 | MonitorsChanged와 동일하게 DispatcherQueue 마셜링 재사용 |
| HC(고대비) 모드에서 새 토큰 미정의 | 리소스 키 런타임 크래시 | T1에서 HC 사전에 3개 Brush 동반 정의 |

## Impact Analysis
### 4-A. 심볼/타입 추적 결과
| 심볼 | 영향 받는 파일 | 영향 종류 |
|---|---|---|
| `MonitorChoice.ShowAudioBadge` | MonitorCardsControl.xaml(2곳), MonitorPanelViewModel.cs | 의미 변경(비음소거 조건 제거) — 사용처 전수 확인 |
| `MonitorChoice.IsAudioMuted` (신규) | MonitorCardsControl.xaml, MonitorPanelViewModel.cs | 추가 |
| `PlaybackCoordinator.SetMutedAsync` | SettingsViewModel.cs, TrayIconService.cs, MonitorPanelViewModel.cs(신규 호출) | 내부에 이벤트 발생 추가(시그니처 불변) |
| `PlaybackCoordinator.MutedChanged` (신규) | MonitorPanelViewModel.cs(구독), PlaybackCoordinatorTests.cs | 공개 이벤트 추가 (additive) |
| `MonitorPanelViewModel.ToggleMuteCommand` (신규) | HomePage.xaml(바인딩) | 추가 |
| `MonitorCardsControl.MuteToggleCommand` (신규 DP) | HomePage.xaml | 추가 (SettingsPage 미바인딩 = 비활성) |
| `AppBadgeCornerRadius` 토큰 | MonitorCardsControl.xaml 2곳 | 값 변경 20→4 (사용처 전수 grep) |
| resw `MonitorAudioBadge.Text` | MonitorCardsControl.xaml x:Uid 2곳, ko/en resw | 제거·신규 키 대체 |
| resw `HomeMonitorsHint.Text` | HomePage.xaml(x:Uid 참조 불변) | 문구만 변경 |

### 4-B. 계약·직렬화 변경
- 없음 — AppSettings(IsMuted 등) 스키마 불변, 직렬화 영향 없음. 신규 공개 멤버는 전부 additive.

### 4-C. 테스트 파일
- `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs` — SetMutedAsync 경로 기존 테스트 유지 + MutedChanged 발생 테스트 추가 (T2)
- VM(MonitorPanelViewModel)·XAML은 테스트 인프라 부재 — 서비스 계층만 테스트하는 프로젝트 관례 (deferred 대장 기록 준거)

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| `PlaybackCoordinator.MutedChanged` | `StatusChanged` 이벤트(76행) 패턴 존재 | 동일 패턴 복제(EventHandler) — 신규 필요(음소거 변경 알림 부재) |
| `MonitorChoice.IsAudioMuted` | `ShowAudioBadge` [ObservableProperty] 패턴 | 동일 패턴 추가 — 상태 축 분리(표시 여부/음소거)로 신규 |
| `MonitorPanelViewModel.ToggleMuteCommand` | 프로젝트 전반 `[RelayCommand]` 관례 | 관례 재사용, 신규 커맨드 |
| `MonitorCardsControl.MuteToggleCommand` DP | 같은 파일 `Monitors`·`IsLarge` DP 패턴 | 동일 패턴 추가 |
| x:Bind 정적 함수 `BadgeBackground/Foreground/Glyph/Text` | 같은 파일 `CardBorder`·`Lookup` 패턴 | 동일 패턴 확장 (Loc.Get은 기존 헬퍼 재사용) |
| 토큰 `AppBadgeForeground`·`AppBadgeMutedBackground`·`AppBadgeMutedForeground` | 기존 토큰에 동일 역할 키 없음 (#3A3A40·#B8B8BE 값은 팔레트에 존재하나 역할 키 상이) | 역할별 키 원칙(AGENTS 디자인 규칙 2·5)에 따라 신규 키 — 값은 기존 팔레트 색 재사용 |

### Verified by
- grep "ShowAudioBadge" → 4 hits (MonitorChoice 정의, MonitorPanelViewModel 대입, XAML 2) — 전수 위 표 포함
- grep "AppBadgeCornerRadius|MonitorAudioBadge|SetMutedAsync|MonitorCardsControl|IsMuted" → 산출물(bin/obj) 제외 전건 Read로 확인, 위 표에 포함

## Decisions
### D1. 음소거 상태 배지 표시
- **Options**: A) 항상 표시 + 소리/음소거 상태 구분 (홈·설정 공통) / B) 홈만 음소거 배지 / C) 기존 숨김 유지
- **Chosen**: A
- **Rationale**: 클릭 토글의 성립 조건(음소거 후에도 클릭 대상 존재) + 두 화면 표시 규칙 일관
- **Source**: 사용자 확정 (2026-07-17 질문 Q1)

### D2. 배지 색
- **Options**: A) 코럴 배경 + 진한 글자 / B) 어두운 배경 + 코럴 글자 / C) 흰 글자 유지
- **Chosen**: A — 소리: 배경 `AppAccentColor`(기존) + 글자·아이콘 `#1A1A1C`(신규 AppBadgeForeground). 음소거: 배경 `#3A3A40` + 글자 `#B8B8BE` (신규 Muted 토큰 2쌍)
- **Rationale**: 대비 3.3:1 → 5.3:1 (음소거 5.6:1). 음소거 색은 기존 팔레트(AppMonitorBorderColor·AppTextNavColor 값) 재사용으로 톤 일관
- **Source**: 사용자 확정 (Q2) + DesignTokens.xaml 팔레트

### D3. 아이콘
- **Chosen**: 이모지 제거, `FontIcon` 글리프 — 소리 `&#xE767;`(Volume) / 음소거 `&#xE74F;`(Mute), 글자색과 동일 브러시
- **Rationale**: 컬러 이모지는 배경과 충돌·저해상 뭉개짐. FontIcon 기본 폰트(Segoe Fluent Icons)는 FontFamily 하드코딩 아님(AGENTS 규칙 위반 없음)
- **Source**: WinUI 표준 아이콘 관례 (자체 확정 — 근거로 결정 가능)

### D4. 모서리
- **Chosen**: `AppBadgeCornerRadius` 20 → 4 (기존 토큰 값 변경)
- **Rationale**: "조금 둥글게" — 카드 8·컨트롤 6·아이콘 5보다 작은 요소라 4. 사용처가 배지 2곳뿐임을 grep 전수 확인
- **Source**: 사용자 요청 + 토큰 체계 (자체 확정)

### D5. 홈 한정 클릭의 전달 구조
- **Options**: A) IsLarge==홈 암묵 결합으로 Large 템플릿만 클릭 / B) `MuteToggleCommand` DP — 바인딩된 화면만 활성
- **Chosen**: B (Large 템플릿 배지만 Button화하되, 실행은 DP 커맨드 위임 — HomePage만 바인딩)
- **Rationale**: "대형=홈" 암묵 가정 대신 명시적 계약. 설정 화면은 미바인딩으로 자연 비활성
- **Source**: AGENTS "영리한 추상화보다 명시적" + 기존 DP 패턴 (자체 확정)

### D6. 음소거 변경의 화면 동기화
- **Chosen**: `PlaybackCoordinator.MutedChanged` 이벤트 신설, MonitorPanelViewModel이 구독(디스패처 마셜링)해 배지 재계산
- **Rationale**: 트레이·설정·홈 어디서 바꿔도 배지 즉시 일치 — 근본 해결(StatusChanged 기존 패턴). SettingsViewModel의 수동 UpdateAudioBadges 호출(398행)은 중복이 되므로 제거
- **Source**: TrayIconService/SettingsViewModel 호출 경로 조사 (자체 확정)

### D7. 홈 힌트 문구
- **Chosen**: ko "클릭해서 선택 · 소리는 배지 모니터에서만 · 배지 클릭 시 음소거 전환" / en "Click to select · Sound plays only on the badged monitor · Click the badge to toggle mute"
- **Rationale**: 이모지 표현("스피커 표시") 소멸 + 신규 클릭 기능의 발견성
- **Source**: 기존 문구 리소스 (자체 확정)

## Tasks
- [x] T1. 디자인 토큰 — 배지 모서리·상태 색
  - **Type**: C (B→격상: prefilter ESCALATE — prefilter가 플랫폼 미명시 빌드로 실패 관측, 실제 `-p:Platform=x64` 빌드는 경고/오류 0)
  - **Acceptance**: Given DesignTokens.xaml, When 빌드, Then 통과하고 `AppBadgeCornerRadius`=4, Default 사전에 `AppBadgeForegroundColor #1A1A1C`·`AppBadgeMutedBackgroundColor #3A3A40`·`AppBadgeMutedForegroundColor #B8B8BE` + 대응 Brush 3개, HighContrast 사전에 같은 Brush 3개(Foreground=SystemColorHighlightTextColor 아님 — 소리 배지 배경이 Highlight이므로 HighlightText, Muted 배경=ButtonFace·글자=ButtonText) 존재 (grep 확인)
  - **Files**:
    - 주: `src/DeskTube/Resources/DesignTokens.xaml`
  - **Edge Cases**:
    - HC 사전 누락 시 런타임 크래시 → Default·HC 양쪽 정의를 acceptance에 포함
  - **Halt Forecast**: 없음 (단일 파일 값·additive 키)
  - **Depends on**: -

- [x] T2. PlaybackCoordinator.MutedChanged 이벤트 + 테스트
  - **Type**: C
  - **Design**: ① `Services/PlaybackCoordinator.cs` ② `public event EventHandler? MutedChanged` — 음소거 상태 변경 알림 1책임, `SetMutedAsync`에서 상태 반영 후 발생 ③ VM(MonitorPanelViewModel)이 구독하는 방향만(코디네이터는 UI 미참조 유지) ④ 인터페이스 추출·이벤트 인자 페이로드는 만들지 않음(bool은 Settings.IsMuted가 정본)
  - **Acceptance**: Given 가짜 주입 coordinator, When `SetMutedAsync(true)` 호출, Then `MutedChanged` 1회 발생 + `Settings.IsMuted == true` — 신규 단위 테스트 통과, 기존 테스트 전건 통과
  - **Files**:
    - 주: `src/DeskTube/Services/PlaybackCoordinator.cs`
    - 테스트: `tests/DeskTube.Tests/PlaybackCoordinatorTests.cs`
  - **Edge Cases**:
    - 구독자 없음(트레이 단독 경로) → null 조건 발생(`?.Invoke`)
    - 같은 값 재설정(true→true) — 이벤트 발생해도 배지 재계산은 멱등이라 무해(값 비교 가드 불필요, 단순 유지)
  - **Halt Forecast**:
    - (ii-a) 공개 이벤트 추가 → `## 사전 승인 항목`에 등록
  - **Depends on**: -

- [x] T3. 배지 상태 로직 — 2상태 표시 + 토글 커맨드 + 이벤트 구독
  - **Type**: C
  - **Design**: ① ViewModels 계층 ② `MonitorChoice.IsAudioMuted`([ObservableProperty]) — 카드별 음소거 시각 상태 / `MonitorPanelViewModel.ToggleMuteCommand`([RelayCommand] async) — `Coordinator.SetMutedAsync(!Settings.IsMuted)` 호출 1책임 ③ VM → Coordinator 호출·구독(Attach에서 `MutedChanged` 구독, Detach 해제, `_dispatcher.TryEnqueue(UpdateAudioBadges)` 마셜링 — MonitorsChanged와 동일 패턴) ④ 배지 상태를 enum·별도 클래스로 추상화하지 않음(bool 2개로 충분)
  - **Acceptance**: Given 선택된 오디오 대상 모니터, When `UpdateAudioBadges()`, Then `ShowAudioBadge = (오디오 대상 ∧ 선택됨)`(음소거 무관)·`IsAudioMuted = Settings.IsMuted`. When 트레이/설정/커맨드로 음소거 변경, Then MutedChanged 경유로 배지 상태 즉시 갱신. SettingsViewModel.OnIsMutedChanged의 수동 `MonitorPanel.UpdateAudioBadges()` 호출 제거(이벤트로 일원화). 빌드 통과
  - **Files**:
    - 주: `src/DeskTube/ViewModels/MonitorPanelViewModel.cs`, `src/DeskTube/ViewModels/MonitorChoice.cs`
    - 동반: `src/DeskTube/ViewModels/SettingsViewModel.cs` (중복 호출 제거)
  - **Edge Cases**:
    - 오디오 대상 없음(모니터 0개) → 배지 전부 숨김 (audio null 기존 분기 유지)
    - MutedChanged가 비UI 스레드 발생 → 디스패처 마셜링
    - Detach 후 이벤트 → 구독 해제로 미수신 (Attach/Detach 대칭)
    - 토글 연타 → Settings.IsMuted 즉시 반영이라 순서 안전(저장은 후행 async, 기존 SetMutedAsync 계약 그대로)
  - **Halt Forecast**:
    - (ii-a) MonitorPanelViewModel 공개 멤버 추가 → `## 사전 승인 항목`
  - **Depends on**: T2

- [ ] T4. 배지 시각·클릭 — 템플릿·DP·문구
  - **Type**: C
  - **Design**: ① `Controls/MonitorCardsControl.xaml(.cs)` + `Views/HomePage.xaml` + resw ② `MuteToggleCommand` DP(ICommand) — 배지 클릭 실행 위임 1책임 / x:Bind 정적 함수 `BadgeBackground/BadgeForeground/BadgeGlyph/BadgeText(bool muted)` — 상태별 브러시·글리프·문구(기존 `Lookup`·`Loc.Get` 재사용) ③ 컨트롤 → 토큰·Loc만 참조(서비스 미참조 유지), HomePage → `ViewModel.MonitorPanel.ToggleMuteCommand` 바인딩. 배지 Button은 DataTemplate(`x:DataType=MonitorChoice`) 안이라 UserControl DP에 x:Bind로 직접 도달 불가 — 기존 `OnCardClick`과 동형의 Click 핸들러(코드비하인드)에서 DP 커맨드를 Execute(무인자 전역 토글)로 위임 (리뷰 m2) ④ 배지를 별도 UserControl로 추출하지 않음(두 템플릿 내 인라인 유지 — 기존 "템플릿 2벌 명시" 방침)
  - **Acceptance**: Given 홈(Large), When 배지 클릭, Then 음소거 토글되고 배지가 소리(코럴 바탕+#1A1A1C 글자·E767)↔음소거(#3A3A40 바탕+#B8B8BE 글자·E74F)로 전환되며 카드 선택 상태는 불변. Given 설정(Compact), Then 배지는 표시만(Border 유지, 클릭 시 기존 카드 선택 동작). 두 템플릿 모두 radius 토큰=4 적용, 이모지 없음. resw: `MonitorAudioBadge` grep 0건, `Monitor_AudioBadgeOn`("소리"/"Sound")·`Monitor_AudioBadgeMuted`("음소거"/"Muted")·`Monitor_AudioBadgeToggleName`("음소거 전환"/"Toggle mute") ko/en 존재, `HomeMonitorsHint.Text` D7 문구. 배지 Button에 AutomationProperties.Name·ToolTip(ToggleName) 설정. 빌드 통과
  - **Files**:
    - 주: `src/DeskTube/Controls/MonitorCardsControl.xaml`, `src/DeskTube/Controls/MonitorCardsControl.xaml.cs`
    - 동반: `src/DeskTube/Views/HomePage.xaml`, `src/DeskTube/Strings/ko-KR/Resources.resw`, `src/DeskTube/Strings/en-US/Resources.resw`
  - **Edge Cases**:
    - 배지 클릭 이벤트가 카드 Button으로 전파 → 내부 Button이 pointer 자체 처리(전파 차단) — HUMAN-VERIFY 명시. 전파가 관측되면 배지 Click 핸들러에서 `e.Handled = true` 폴백 (리뷰 m1)
    - MuteToggleCommand 미바인딩(설정 화면·null) → Compact는 Border라 해당 없음, Large에서 null이면 no-op 가드
    - 텍스트 길이 차("소리"↔"음소거", "Sound"↔"Muted") → 배지 폭 Auto(콘텐츠 크기) — 고정폭 금지
    - HC 모드 → T1의 HC Brush로 자동 전환
  - **Halt Forecast**:
    - (ii-a) MonitorCardsControl 공개 DP 추가 → `## 사전 승인 항목`
  - **Depends on**: T1, T3

- [ ] T5. 문서 — PRD FR-5 갱신 + README
  - **Type**: A
  - **Acceptance**: docs/prd.md FR-5에 "홈 화면 모니터 카드의 소리 배지 클릭으로도 음소거를 전환할 수 있다(홈 한정). 배지는 오디오 대상 모니터에 항상 표시되며 소리/음소거 상태를 구분 표시" 취지 반영 + 변경 이력 1줄(2026-07-17). README.md 홈 화면·모니터 카드 설명이 새 배지 동작(2상태·홈 클릭 토글)과 일치
  - **Files**:
    - 주: `docs/prd.md`, `README.md`
  - **Edge Cases**: 없음 (문서)
  - **Halt Forecast**: 없음 — PRD 문구는 plan 승인에 포함(승인 프롬프트에 원문 제시)
  - **Depends on**: T4

## 사전 승인 항목 (일괄 승인 대상)
- T2 — `PlaybackCoordinator.MutedChanged` 공개 이벤트 추가 (additive, 음소거 변경 알림 부재 해소)
- T3 — `MonitorPanelViewModel.ToggleMuteCommand`·`MonitorChoice.IsAudioMuted` 공개 멤버 추가 (additive)
- T4 — `MonitorCardsControl.MuteToggleCommand` 공개 DP 추가 (additive)
- T5 — docs/prd.md FR-5 문구 갱신 (승인 프롬프트에 제시된 문구 그대로 — PRD 고정 원칙의 사용자 합의)

## 불가피한 Halt (위임 불가)
- push·PR·릴리즈 (이번 plan에 없음 — 최종 보고에서 별도)

## Verification Strategy
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` — 경고/에러 0
- 단위 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj`
- 수동 검증 (HUMAN-VERIFY): ① 홈 배지 클릭 → 음소거 전환 + 배지 상태 전환 + 카드 선택 불변 ② 설정 배지 클릭 → 카드 선택만(기존) ③ 트레이 볼륨 on/off → 열린 홈/설정 배지 즉시 갱신 ④ 배지 모양·색 가독성 확인

## Phase Ledger

## Retry Ledger

## Progress Log
- T1-T2 완료 (커밋 208d593, 51e9170): 배지 토큰(radius 4 + 상태 색 3쌍 + HC) / MutedChanged 이벤트 + 테스트(105/105). T1은 prefilter ESCALATE(플랫폼 미명시 빌드 오탐)로 C 격상 처리.
  - 참고: dotnet test는 `-p:Platform=x64` 필수 (deferred 대장 기지 항목과 동일 원인).

## Next Steps

## Open Questions
- [x] Q1. 음소거 상태 배지 표시 — **A) 항상 표시 + 상태 구분(홈·설정 공통, 클릭은 홈만)** (사용자 확정)
- [x] Q2. 배지 색상 — **A) 코럴 배경 + 진한 글자(#1A1A1C), 아이콘은 단색 글리프** (사용자 확정)
