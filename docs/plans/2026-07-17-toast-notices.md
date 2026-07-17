# Plan: 일시 알림을 자동 소멸 토스트로 전환 (전 화면)

## 요구 이해
- **원문 요청**: "이미지처럼 표시되는 문구를 토스트 팝업으로 수정해서 일정 시간후 사라지도록 수정, 모든 화면 확인해서 적용"
- **이해한 요구**: 현재 X로 닫을 때까지 남는 상단 InfoBar 일시 알림("링크가 복사되었습니다" 등)을 **창 하단 중앙의 토스트 팝업**으로 바꾸고 자동 소멸(성공/정보 3초·오류/경고 5초 — 사용자 확정). 전 화면 전수 적용: 홈·플레이리스트·설정 상단 알림 + 메인 창 트레이 진입 안내 4곳. **상시 안내 3곳은 제외**(설정 화질 설명·자동 실행 상태 표시·로그인 창 구글 차단 안내 — 일시 알림이 아님, 사용자 확정).
- **포함하지 않는 것으로 이해**: Windows OS 알림(트레이 풍선/알림 센터) 연동, 상시 안내 InfoBar의 형태 변경.

## Goal
어느 화면에서든 일시 알림이 하단 중앙 토스트로 떠서 자동으로 사라진다 (닫기 버튼 불필요).

## Out of Scope
- Windows OS 알림 센터(AppNotification) 연동 — 앱 내 토스트로 충분
- 상시 안내 3곳(화질 설명·자동 실행 상태·로그인 차단 안내)의 변경 (사용자 확정 — 유지)

## Deferred / Follow-up
- (없음 — 계획 시점)

## Investigation Log
- InfoBar 전수 조사 (grep "InfoBar|ShowNotice|NoticeRequested|IsNoticeOpen" 전건 Read):
  - **전환 대상(일시 알림) 4곳**: ① HomePage.xaml 124행(HomeViewModel Notice — URL 오류·재생 실패/시작·모니터 최소1 경고) ② PlaylistsPage.xaml 20행(PlaylistsViewModel Notice — 이름/URL 오류·복사 성공/실패·상한 경고) ③ SettingsPage.xaml 19행(SettingsViewModel Notice — 모니터 최소1 등) ④ MainWindow.xaml 68행 NoticeBar(트레이 진입 안내 — App.ShowMainWindow → MainWindow.ShowNotice 156행)
  - **유지(상시 안내) 3곳**: SettingsPage 66행 QualityNote(IsClosable=False 상시 설명), 126행 자동 실행 상태(IsOpen=상태 바인딩·IsClosable=False), LoginWindow 12행 LoginNotice(상시) — 사용자 확정으로 제외
- 알림 생산 경로: VM별 Notice 인프라 중복 — Home·Playlists는 NoticeMessage/IsNoticeOpen/NoticeSeverity 3종 + private ShowNotice(226행·605행), Settings는 NoticeMessage/IsNoticeOpen 2종(118·121행 — Severity 없음, 항상 Informational) + MainWindow.ShowNotice — 공용 토스트 서비스로 일원화하면 중복 제거(근본 해결). (리뷰 m2 정정)
- `MonitorPanelViewModel.NoticeRequested`(27행 — 최소 1개 차단 경고, 발화 181행)·`NoticeCleared`(29행 — 유효 선택 시 떠 있던 경고 닫기, 발화 186행 — 리뷰 m1 정정) 이벤트: 소비자는 HomeViewModel(26-27행)·SettingsViewModel(38-39행) 2곳 전수. 토스트는 자동 소멸이라 "닫기 신호"(NoticeCleared)가 무의미 → 이벤트·발화·구독 2곳을 함께 제거(원자적 — 같은 task).
- PlaylistsViewModel 475행 `IsNoticeOpen = false`(추가 성공 시 이전 오류 닫기) — 토스트 전환 시 불필요(자동 소멸), 제거 대상.
- 기존 유사 구현: grep "Toast|TeachingTip" → 0건 (신규). Flyout(공유 팝업)은 앵커형이라 용도 다름.
- MainWindow 구조: 루트 Grid(행 0 타이틀바/행 1 셸) — NoticeBar가 행 1 상단 오버레이(68-76행). 토스트 호스트를 같은 자리(행 1, VerticalAlignment=Bottom, HorizontalAlignment=Center)로 교체 가능. 페이지들은 모두 이 셸(ContentFrame) 안 — **창 수준 호스트 1개로 전 화면 커버**.
- 창 재생성: 언어 전환 시 `App.ApplyLanguageChange`(App.xaml.cs 181행)가 MainWindow 재생성 — 토스트 호스트 등록(Attach)을 MainWindow 생성자에서 하면 재생성 시 자동 재등록(덮어쓰기).
- 스레드: 알림은 UI 스레드에서 발생(VM 이벤트 핸들러·커맨드)이나 안전하게 DispatcherQueue 마셜링(코디네이터 이벤트 경유 가능성 — MonitorsChanged 선례).
- InfoBarSeverity 재사용: VM들이 이미 `Microsoft.UI.Xaml.Controls.InfoBarSeverity`를 사용(계층형 구조 관례) — 토스트 심각도 인자로 재사용하면 호출부 변경 최소(새 enum 매핑 불필요).
- Deferred 대장 확인: 관련 항목 없음.
- 위키 참조: 프로젝트 국소 UI 변경이라 조회 생략 (직전 plan들에서 vault 무매칭/미설정 확인 이력).
- PRD 경량 확인: 알림 표시 방식은 PRD FR/NFR에 명세 없음(화면 안내는 명세 밖 세부) — PRD 미연결(Phase F 최종). README에도 알림 방식 서술 없음(grep "InfoBar|알림" README.md 0건) — README 갱신 불요(생략 사유).

## Risks & Unknowns
| 위험 | 영향 | 완화책 |
|---|---|---|
| 토스트가 콘텐츠(하단 버튼 등)를 가림 | 하단 UI 일시 가림 | 자동 소멸(3/5초) + 히트테스트 무시(IsHitTestVisible=False)로 클릭 통과 |
| 연속 알림 시 겹침·깜빡임 | 표시 혼란 | 단일 호스트 — 최신 메시지로 교체 + 타이머 리셋 (큐 없음, 단순) |
| Attach 전 Show 호출 | 알림 유실 | OnLaunched가 MainWindow를 항상 먼저 생성(조용 시작 포함 — App.xaml.cs 103행)하므로 실경로 없음, 방어적으로 null 무시 |
| 창 숨김(트레이) 중 알림 | 사용자가 못 봄 | 기존 InfoBar도 동일 특성 — 트레이 진입 안내는 ShowMainWindow가 창을 먼저 띄운 뒤 표시(기존 흐름 유지) |
| Notice 프로퍼티 제거 누락 XAML 참조 | 빌드 실패(x:Bind) | 페이지 InfoBar 제거와 VM 프로퍼티 제거를 같은 task에서 원자 처리 + V-7 역방향 |

## Impact Analysis
### 4-A. 심볼/타입 추적 결과
| 심볼 | 영향 받는 파일 | 영향 종류 |
|---|---|---|
| `ToastService` (신규 static) | Services/ToastService.cs(신규), MainWindow.xaml.cs(Attach), VM 3개·App.xaml.cs(Show 호출) | 추가 |
| `MainWindow.NoticeBar`·`ShowNotice` | MainWindow.xaml(.cs), App.xaml.cs 172행(유일 호출자) | 제거 — 호출부는 ToastService.Show로 교체 |
| VM 3개의 `NoticeMessage`/`IsNoticeOpen`/`NoticeSeverity` | HomeViewModel, PlaylistsViewModel, SettingsViewModel + 각 페이지 XAML InfoBar(x:Bind) | 제거 — XAML과 원자 처리 |
| VM 3개의 private `ShowNotice` | 각 VM 내부 호출 전수(Home 5·Playlists 8·Settings 1) | 내부 구현을 ToastService.Show 위임으로 교체(호출부 불변) |
| `MonitorPanelViewModel.NoticeCleared` | MonitorPanelViewModel(발화 159행), HomeViewModel(구독 27행+핸들러 167행), SettingsViewModel(구독 39행+핸들러 284행) | 제거 (소비자 2곳 전수 — 같은 task) |
| `MonitorPanelViewModel.NoticeRequested` | 유지 (소비자가 토스트 호출로 전환) | 소비부 내부만 변경 |
| `PlaylistsViewModel.NotifyLinkCopied/Failed` | 유지 — 내부 ShowNotice 경유라 자동 전환 | 영향 없음 |

### 4-B. 계약·직렬화 변경
- 직렬화 없음. 공개 계약 변경: MainWindow.ShowNotice(internal) 제거 + MonitorPanelViewModel.NoticeCleared(public 이벤트) 제거 — 호출자·구독자 전수 식별 완료(위 표), 같은 task에서 원자 처리.

### 4-C. 테스트 파일
- 없음 — VM Notice·토스트는 UI 영역(VM 테스트 인프라 부재 관례), 기존 테스트는 Notice 미참조(grep Notice tests/ 0건 — 구현 시 재확인). 자동 소멸·표시는 HUMAN-VERIFY.

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| `ToastService` | grep "Toast|TeachingTip" 0건, 유일 유사물은 VM별 중복 Notice 세트(제거 대상) | 신규 — 중복 3벌을 1개 서비스로 대체(근본 해결) |
| 토스트 호스트 UI | 기존 NoticeBar(InfoBar) — 상단 고정·수동 닫기라 요구 불충족 | 신규 Border+FontIcon+TextBlock(토큰 스타일 — 공유 팝업 바와 동일 톤), NoticeBar는 제거 |
| 심각도 인자 | 기존 `InfoBarSeverity` 재사용 | 새 enum 불필요(호출부 변경 최소) |

### Verified by
- grep "InfoBar|ShowNotice|NoticeRequested|IsNoticeOpen" → 전건 Read (위 Investigation Log 분류 — 전환 4·유지 3)
- grep "NoticeCleared" → 발화 1(MonitorPanel 159행)·구독 2(Home·Settings)·핸들러 2 — 전수 표에 포함
- grep "window.ShowNotice|ShowMainWindow" → App.xaml.cs 172행 유일 호출자 확인

## Decisions
### D1. 토스트 아키텍처
- **Options**: A) 각 페이지 InfoBar 유지 + 자동 닫힘 타이머만 추가 / B) 창 수준 공용 토스트 호스트 1개 + 정적 ToastService 라우팅
- **Chosen**: B — MainWindow 루트에 호스트 1개, VM·App은 `ToastService.Show(message, severity)` 호출
- **Rationale**: 사용자가 "토스트 팝업" 형태를 명시 + VM 3벌 중복 Notice 인프라 제거(근본 해결). A는 형태가 그대로(증상 우회)
- **Source**: 사용자 요청 + Investigation Log 중복 확인

### D2. 위치·표시 시간
- **Chosen**: 하단 중앙, 성공/정보 3초·오류/경고 5초
- **Source**: 사용자 확정 (2026-07-17 질문)

### D3. 연속 알림 처리
- **Chosen**: 단일 토스트 — 새 알림이 오면 메시지 교체 + 타이머 리셋 (큐·스택 없음)
- **Rationale**: 이 앱의 알림 빈도가 낮고(사용자 조작 응답), 큐는 과설계(YAGNI)
- **Source**: 알림 발생 지점 전수(전부 단발성 조작 응답) — 자체 확정

### D4. 상시 안내 3곳 유지
- **Chosen**: 설정 화질 설명·자동 실행 상태·로그인 차단 안내는 InfoBar 그대로
- **Source**: 사용자 확정

### D5. 토스트 시각
- **Chosen**: 어두운 라운드 바(배경 `AppInputBackgroundBrush`·테두리 `AppInputBorderBrush`·radius `AppPillCornerRadius` — 공유 팝업 바와 동일 톤) + 심각도별 FontIcon(성공 E73E 체크·정보 E946·경고 E7BA·오류 E783)과 아이콘 색(성공=코럴 `AppAccentBrush` 대신 구분 위해: 성공 `AppAccentBrush`·정보 `AppTextSecondaryBrush`·경고/오류 시스템 구분색 대신 `AppAccentHoverBrush`) — 구현 시 토큰만 사용, 정확한 조합은 가역적 내부 세부(HUMAN-VERIFY로 확인)
- **Source**: 기존 토큰 팔레트 + 공유 팝업 선례 (자체 확정 — 가역)

## Tasks
- [x] T1. ToastService + MainWindow 토스트 호스트
  - **Type**: C
  - **Design**: ① `Services/ToastService.cs`(신규) + `MainWindow.xaml(.cs)` + `App.xaml.cs` ② `ToastService`(static) — `Attach(Action<string, InfoBarSeverity> presenter, DispatcherQueue)`·`Show(message, severity)` 라우팅 1책임(마셜링 포함, presenter 미등록 시 무시) / MainWindow `PresentToast` — 호스트 표시 + DispatcherTimer(3초, 오류/경고 5초) + 교체·리셋(D3) ③ VM·App → ToastService(정적) → MainWindow presenter (VM은 창 미참조 유지) ④ 큐·애니메이션 라이브러리·인터페이스 추출 안 함(정적 1개 — 창이 하나뿐)
  - **Acceptance**: MainWindow에 하단 중앙 토스트 호스트(토큰 스타일 바 + 심각도 아이콘, `IsHitTestVisible=False`) 신설, 기존 NoticeBar(68-76행)·`ShowNotice`(156행) 제거, App.xaml.cs 172행 호출을 `ToastService.Show(notice, Informational)`로 교체. `ToastService.Show`는 성공/정보 3초·오류/경고 5초 후 자동 숨김(⏳ HUMAN-VERIFY), 연속 호출 시 교체+리셋. 빌드 통과
  - **Files**:
    - 주: `src/DeskTube/Services/ToastService.cs`(신규), `src/DeskTube/MainWindow.xaml`, `src/DeskTube/MainWindow.xaml.cs`
    - 동반: `src/DeskTube/App.xaml.cs`
  - **Edge Cases**:
    - Attach 전 Show → 무시(방어) — 실경로 없음(OnLaunched가 창 먼저 생성)
    - 언어 전환 창 재생성 → 새 생성자 Attach가 presenter 덮어씀(옛 창 참조 해제)
    - 비UI 스레드 Show → DispatcherQueue.TryEnqueue 마셜링
    - 타이머 중 재알림 → Stop 후 재시작(교체)
  - **Halt Forecast**:
    - (ii-a) `MainWindow.ShowNotice`(internal) 제거 + `ToastService` 공개 심볼 신설 → `## 사전 승인 항목`
  - **Depends on**: -

- [x] T2. 홈·설정 알림 전환 + NoticeCleared 제거
  - **Type**: D
  - **Design**: ① ViewModels(Home·Settings·MonitorPanel) + Views(HomePage·SettingsPage) ② 신규 심볼 없음 — Home·Settings의 `ShowNotice`/`OnPanelNoticeRequested` 내부를 `ToastService.Show`로, Notice [ObservableProperty] 3종·`OnPanelNoticeCleared` 제거, MonitorPanelViewModel `NoticeCleared` 이벤트·발화 제거(소비자 2곳 전수 — 원자) ③ VM → ToastService ④ Notice 프로퍼티를 감싸는 어댑터 안 만듦(완전 제거)
  - **Acceptance**: HomePage 124-130행·SettingsPage 19-26행 InfoBar 제거, Home·Settings VM에서 NoticeMessage/IsNoticeOpen/NoticeSeverity·OnPanelNoticeCleared 잔존 0(grep), MonitorPanelViewModel NoticeCleared 참조 전수 0, 기존 알림 지점(URL 오류·재생 실패/시작·모니터 최소1·자동실행 실패 등)이 전부 ToastService 경유. 빌드 통과 + 테스트 전건 통과
  - **Files**:
    - 주: `src/DeskTube/ViewModels/HomeViewModel.cs`, `src/DeskTube/ViewModels/SettingsViewModel.cs`, `src/DeskTube/ViewModels/MonitorPanelViewModel.cs`
    - 동반: `src/DeskTube/Views/HomePage.xaml`, `src/DeskTube/Views/SettingsPage.xaml`
  - **Edge Cases**:
    - SettingsViewModel Apply 계열 오류 알림 경로 전수 이관(grep ShowNotice/IsNoticeOpen 잔존 0)
    - 모니터 최소1 경고(Warning) → 5초 유지 시간 적용
    - 상시 안내 2곳(QualityNote·AutoStart 상태)은 미변경 확인
  - **Halt Forecast**:
    - (ii-a) `MonitorPanelViewModel.NoticeCleared` 공개 이벤트 제거 (소비자 2곳 전수 확인 완료) → `## 사전 승인 항목`
  - **Depends on**: T1

- [x] T3. 플레이리스트 알림 전환
  - **Type**: C
  - **Design**: ① PlaylistsViewModel + PlaylistsPage.xaml ② 신규 심볼 없음 — `ShowNotice` 내부를 ToastService.Show로, Notice [ObservableProperty] 3종·`IsNoticeOpen=false`(475행) 제거, 페이지 InfoBar(20-26행) 제거 ③ VM → ToastService ④ NotifyLinkCopied/Failed 공개 시그니처 불변(내부 위임만 교체)
  - **Acceptance**: PlaylistsPage InfoBar 제거 + VM Notice 프로퍼티 잔존 0(grep), 알림 8지점(이름/URL 오류·상한 경고·복사 성공/실패·재생 실패 등) 전부 ToastService 경유, NotifyLinkCopied/Failed 시그니처 불변(호출부 PlaylistsPage 무수정). 빌드 통과
  - **Files**:
    - 주: `src/DeskTube/ViewModels/PlaylistsViewModel.cs`, `src/DeskTube/Views/PlaylistsPage.xaml`
  - **Edge Cases**:
    - 항목 추가 성공 시 이전 오류 닫기(475행) → 토스트 자동 소멸로 대체(코드 제거)
    - 공유 복사 성공(Success) 3초·실패(Error) 5초
  - **Halt Forecast**: 없음 (내부 전환 — T1·T2에서 계약 확정)
  - **Depends on**: T1, T2

## 사전 승인 항목 (일괄 승인 대상)
- T1 — `ToastService` 공개 정적 클래스 신설 + `MainWindow.ShowNotice`(internal) 제거(유일 호출자 App.xaml.cs 172행 동시 교체)
- T2 — `MonitorPanelViewModel.NoticeCleared` 공개 이벤트 제거 (구독자 Home·Settings 2곳 전수 동시 제거 — 토스트 자동 소멸로 "닫기 신호" 무의미)

## 불가피한 Halt (위임 불가)
- push·main 병합·릴리즈 (이번 plan에 없음 — 최종 보고에서 별도)

## Verification Strategy
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` — 경고/에러 0
- 단위 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -p:Platform=x64` (회귀 확인)
- 잔존 검사: grep — VM Notice 프로퍼티·NoticeCleared·NoticeBar·MainWindow.ShowNotice 잔존 0 / 상시 안내 3곳 불변
- 수동 검증 (HUMAN-VERIFY): ① 공유 복사 → 하단 중앙 토스트 3초 후 소멸 ② 잘못된 URL 추가 → 오류 토스트 5초 ③ 모니터 마지막 해제 시도 → 경고 토스트 ④ 트레이 "재생"(리스트 없음) → 창 표시 + 토스트 ⑤ 연속 알림 교체 ⑥ 상시 안내 3곳 그대로

## Phase Ledger
- Phase F 통과 (HEAD 43b3b40) — F-2 테스트 106/106, F-7 plan-completion-reviewer OK (BLOCKER/MAJOR/MINOR 0). PRD 미연결 — Phase F가 최종

## Retry Ledger

## Progress Log
- T1-T2 완료: ToastService+MainWindow 하단 토스트 호스트(3/5초·교체·클릭 통과, NoticeBar/ShowNotice 제거) / 홈·설정 전환(Notice 프로퍼티·NoticeCleared 전수 제거, ToastService 직접 호출 — ShowNotice 위임 없앰). 함정: SettingsViewModel은 InfoBarSeverity using이 없었음(신규 추가 — C# 실패가 XAML 컴파일러 WMC9999 연쇄 오류로 위장됨).

## Next Steps
- 권장 다음 액션: HUMAN-VERIFY 6건 확인(토스트 표시/소멸 시간·연속 교체·상시 안내 불변) 후 main 병합 결정
- Suggested skills: 공식 /code-review (선택)

## Open Questions
- [x] Q1. 토스트 위치 — **하단 중앙** (사용자 확정)
- [x] Q2. 표시 시간 — **성공/정보 3초·오류/경고 5초** (사용자 확정)
- [x] Q3. 상시 안내 3곳 — **유지(전환 제외)** (사용자 확정)
