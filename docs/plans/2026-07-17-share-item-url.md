# Plan: 플레이리스트 항목 공유 메뉴 + URL 복사 팝업

**PRD**: docs/prd.md

## 요구 이해
- **원문 요청**: "이동 메뉴 아래쪽에 공유 메뉴 추가하고 클릭시 2번 이미지처럼 url를 확인/복사 할 수 있는 팝업 표시 / 이동 메뉴 아래 라인 추가, 공유 메뉴 아래 라인 추가"
- **이해한 요구**: 플레이리스트 항목 우클릭 메뉴를 `위로 이동 / 아래로 이동 ─ 구분선 ─ 공유 ─ 구분선 ─ 삭제` 구조로 재구성하고, "공유" 클릭 시 해당 항목의 유튜브 URL(저장된 원본)을 읽기 전용으로 보여주고 "복사" 버튼으로 클립보드에 복사할 수 있는 팝업(어두운 라운드 바 — 첨부 이미지는 스타일 참고)을 표시한다. 복사 시 성공 알림.
- **포함하지 않는 것으로 이해**: OS 공유 시트(Share charm) 연동, 리스트(전체) 공유, 첨부 이미지와의 픽셀 단위 시각 충실도(스타일 참고로만 — 토큰 기반 앱 스타일 적용).

## Goal
플레이리스트 항목을 우클릭 → 공유로 URL을 확인·복사할 수 있다 (메뉴는 이동/공유/삭제 그룹 구분선 포함).

## PRD Coverage
| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-18 (항목 컨텍스트 메뉴 — 공유(URL 복사) 추가 보강) | Must | T1, T2 | ✅ 커버 |
| FR-1~FR-16, FR-19 (나머지 active Must) | Must | (없음) | 이번 범위 외 (기구현) |

## Out of Scope
- OS 공유 시트(Windows Share) 연동 — 요청은 URL 확인/복사 팝업
- 플레이리스트(리스트 단위) 공유

## Deferred / Follow-up
- (없음 — 계획 시점)

## Investigation Log
- 항목 컨텍스트 메뉴: `PlaylistsPage.xaml` 236-252행 — `MenuFlyout`(ItemUp E74A / ItemDown E74B / ItemDelete), 구분선 없음. 핸들러는 코드비하인드 `(sender as FrameworkElement)?.DataContext is PlaylistItemEntry` 패턴 (OnRenameClick 161행 등 동일).
- 항목 URL: `PlaylistItemEntry.Url`(PlaylistsViewModel.cs 51행 — 저장된 원본 그대로, 42행 `Url = item.Url`) — 사용자가 입력한 URL(youtu.be·music.youtube.com 등)이라 공유 값으로 적합(첨부 이미지도 원본 URL).
- 알림 인프라: PlaylistsViewModel에 `NoticeMessage`/`IsNoticeOpen`/`NoticeSeverity`(공개 [ObservableProperty]) + private ShowNotice — 페이지에서 재사용하려면 VM 공개 메서드 1개 신설이 기존 관례(페이지→VM 공개 메서드 호출: CreateAsync 등)와 일치.
- 클립보드: 레포 내 기존 사용 0건(grep "Clipboard") — WinRT 표준 `Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(DataPackage)` 사용(공식 API — 구현 시 시그니처는 이미 알려진 표준, 패키지 앱 권한 불요).
- 팝업 방식: WinUI `Flyout`(페이지 수준 1개, `ShowAt(row)`)이 첨부 이미지의 앵커형 바에 부합 — ContentDialog(모달 과잉)·TeachingTip(닫기 버튼·화살표 스타일 상이) 대비 적합. DataTemplate 안 인라인 Flyout은 항목 수만큼 인스턴스가 생겨 페이지 리소스 1개 공유가 낫다.
- 시각 충실도 판단: 첨부 이미지는 "처럼"(스타일 참고) 요청 — 픽셀 정합 요구가 아니므로 Step 2.5(시각 요소 분해) 미발동. 형태 요지(어두운 라운드 바, URL 왼쪽·복사 버튼 오른쪽)는 T1 acceptance에 직접 명시.
- 구분선: WinUI `MenuFlyoutSeparator` 표준 요소 (WinUI Gallery MenuFlyout 패턴).
- 공유 아이콘: Segoe Fluent Icons E72D(Share) — 기존 메뉴가 FontIcon 글리프 사용(E74A/E74B)과 동일 패턴.
- Deferred 대장 확인: 관련 항목 없음.
- 위키 참조: 프로젝트 국소 UI 변경이라 조회 생략 (직전 plan들에서 vault 무매칭/미설정 확인 이력).
- PRD 경량 확인: FR-18(항목 목록 UI)에 닿음 — 공유 기능 신설 → PRD 갱신 제안(T2) + `**PRD**:` 연결.

## Risks & Unknowns
| 위험 | 영향 | 완화책 |
|---|---|---|
| Flyout이 우클릭 메뉴 닫힘과 동시에 열리며 포커스 경합 | 팝업이 즉시 닫히거나 위치 이상 | MenuFlyoutItem Click은 메뉴가 닫힌 뒤 발생(WinUI 표준) — ShowAt(항목 행 요소)로 앵커 고정, HUMAN-VERIFY 확인 |
| 클립보드 접근 실패(드묾 — 다른 앱 점유) | 복사 안 됨 | try/catch + AppLog + 실패 알림(기존 Notice 재사용) |
| 긴 URL이 박스 폭 초과 | 잘려 보임 | TextBox 고정 폭 + IsReadOnly(스크롤·선택 가능) — 전체 값은 복사로 확보 |

## Impact Analysis
### 4-A. 심볼/타입 추적 결과
| 심볼 | 영향 받는 파일 | 영향 종류 |
|---|---|---|
| PlaylistsPage.xaml 항목 MenuFlyout | PlaylistsPage.xaml (236-252행) | 구조 변경 — 구분선 2 + 공유 항목 추가 (기존 3항목·핸들러 불변) |
| `OnShareClick`/`OnShareCopyClick` (신규 private) | PlaylistsPage.xaml.cs | 추가 — 기존 핸들러 패턴 복제 |
| `PlaylistsViewModel.NotifyLinkCopied` (신규 public) | PlaylistsViewModel.cs, PlaylistsPage.xaml.cs(호출) | 추가 (additive) |
| resw `ItemShareMenuItem.Text`·`Playlists_ShareCopy`·`Playlists_LinkCopied`·`Playlists_ShareCopyFailed` (신규 키) | resw ko/en | 추가 |
| `PlaylistItemEntry.Url` | 읽기만 (기존 공개 속성) | 영향 없음 |

### 4-B. 계약·직렬화 변경
- 없음 — additive UI·VM 메서드만, 저장 형식 불변.

### 4-C. 테스트 파일
- 없음 — View·클립보드·Flyout은 테스트 인프라 부재(HUMAN-VERIFY), VM 신규 메서드는 알림 문구 설정 1줄(서비스 로직 없음 — VM 테스트 인프라 부재 관례).

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| 공유 Flyout UI | 레포 내 Flyout 사용 없음(다이얼로그는 ContentDialog 2곳 — 이름 입력·삭제 확인) | 확인/복사 바에는 앵커형 Flyout이 적합 — 신규(페이지 수준 1개) |
| `NotifyLinkCopied` | 기존 private ShowNotice 재사용(내부 위임) | 페이지→VM 공개 진입점만 신설 |
| 클립보드 호출 | 레포 내 기존 사용 0건 | WinRT 표준 API 직접 호출(헬퍼 추상화 안 함 — 사용처 1곳) |
| 구분선·메뉴 항목 | 기존 MenuFlyout 확장 | 표준 MenuFlyoutSeparator 재사용 |

### Verified by
- grep "MenuFlyout|ContextFlyout" (PlaylistsPage.xaml) → 항목 메뉴 236-252행·리스트 메뉴 79-84행 확인 (이번 대상은 항목 메뉴만)
- grep "Clipboard" (src 전체) → 0건 (신규 도입 아님 — OS 내장 WinRT API)
- grep "Url" (PlaylistsViewModel) → PlaylistItemEntry.Url 공개 getter 확인

## Decisions
### D1. 팝업 방식
- **Options**: A) Flyout(앵커형) / B) ContentDialog(모달) / C) TeachingTip
- **Chosen**: A — 페이지 수준 Flyout 1개(`ShareFlyout`), 공유 클릭한 항목 행에 ShowAt
- **Rationale**: 첨부 이미지의 앵커형 바와 부합, 모달 과잉 회피, 바깥 클릭으로 자연 닫힘
- **Source**: WinUI 표준 컨트롤 관례 (자체 확정)

### D2. 공유 URL 값
- **Chosen**: `PlaylistItemEntry.Url` — 사용자가 입력한 저장 원본 그대로 (정규화·단축 변환 없음)
- **Rationale**: 입력한 링크를 그대로 공유하는 것이 기대 동작(첨부 이미지도 원본), 변환 로직 불필요(YAGNI)
- **Source**: PlaylistsViewModel.cs 42·51행 (자체 확정)

### D3. 복사 피드백
- **Chosen**: 복사 성공 → 클립보드 설정 + Flyout 닫기 + 기존 InfoBar 알림("링크가 복사되었습니다", Success) / 실패 → AppLog + InfoBar 오류 알림
- **Rationale**: 기존 알림 인프라 재사용(신규 토스트 UI 불필요), 유튜브 뮤직의 스낵바 피드백과 동등
- **Source**: PlaylistsViewModel Notice 인프라 (자체 확정)

### D4. 메뉴 구조·아이콘
- **Chosen**: `위로 이동 / 아래로 이동 / ─separator─ / 공유(FontIcon E72D) / ─separator─ / 삭제` — 사용자 명시 구조 그대로
- **Source**: 사용자 요청 + 기존 FontIcon 글리프 패턴

## Tasks
- [x] T1. 공유 메뉴 + URL 복사 Flyout
  - **Type**: C
  - **Design**: ① `Views/PlaylistsPage.xaml(.cs)` + `ViewModels/PlaylistsViewModel.cs` ② 신규 심볼: 페이지 리소스 `ShareFlyout`(Flyout — URL 읽기 전용 TextBox `ShareUrlBox` + "복사" Button 가로 배치) / 코드비하인드 `OnShareClick`(entry.Url 배정 + ShowAt — sender는 MenuFlyoutItem이라 행 시각 요소가 아니므로 앵커는 항목 ListView의 `ContainerFromItem(entry)`로 획득, null이면 ListView 자체에 표시 폴백 — 리뷰 m1) · `OnShareCopyClick`(클립보드 SetContent + Hide + VM 알림) / VM `NotifyLinkCopied()`·`NotifyLinkCopyFailed()` public — 내부 ShowNotice 위임 1책임 ③ 페이지 → VM 공개 메서드·Loc·WinRT Clipboard 참조 (VM은 클립보드 미참조 — View 관심사) ④ 클립보드 헬퍼·공유 서비스 추상화 안 함(사용처 1곳), OS 공유 시트 미도입
  - **Acceptance**: Given 항목 우클릭, Then 메뉴가 `위로/아래로 ─ 공유(E72D) ─ 삭제` 구조(구분선 2개 — MenuFlyoutSeparator). When 공유 클릭, Then 해당 행 앵커로 어두운 라운드 바 팝업(읽기 전용 URL 왼쪽 + "복사" 버튼 오른쪽, 앱 토큰 스타일) 표시(⏳ HUMAN-VERIFY). When 복사 클릭, Then 클립보드에 항목 원본 URL + 팝업 닫힘 + InfoBar "링크가 복사되었습니다"(실패 시 오류 알림 + AppLog). resw: ItemShareMenuItem.Text(공유/Share)·Playlists_ShareCopy(복사/Copy)·Playlists_LinkCopied·Playlists_ShareCopyFailed ko/en 존재. 기존 메뉴 3항목·핸들러 동작 불변. 빌드 통과
  - **Files**:
    - 주: `src/DeskTube/Views/PlaylistsPage.xaml`, `src/DeskTube/Views/PlaylistsPage.xaml.cs`
    - 동반: `src/DeskTube/ViewModels/PlaylistsViewModel.cs`, `src/DeskTube/Strings/ko-KR/Resources.resw`, `src/DeskTube/Strings/en-US/Resources.resw`
  - **Edge Cases**:
    - 클립보드 접근 실패 → try/catch + AppLog + 실패 알림 (앱 계속 동작)
    - 긴 URL → TextBox 고정 폭 + 읽기 전용 스크롤 (잘림은 복사로 보완)
    - Flyout 열린 상태에서 항목 삭제/목록 갱신 → Flyout은 표시 시점 URL 스냅숏(TextBox 텍스트)이라 무해
    - 연속 공유(다른 항목) → ShowAt마다 URL 재배정 (페이지 수준 단일 Flyout)
  - **Halt Forecast**:
    - (ii-a) `PlaylistsViewModel.NotifyLinkCopied/NotifyLinkCopyFailed` 공개 메서드 추가 → `## 사전 승인 항목`
  - **Depends on**: -

- [x] T2. 문서 — PRD FR-18 갱신 + README
  - **Type**: A
  - **Acceptance**: docs/prd.md FR-18에 "항목 우클릭 메뉴에서 공유 — URL 확인/클립보드 복사 팝업" 취지 반영 + 변경 이력 1줄. README 플레이리스트 항목에 공유(URL 복사) 반영
  - **Files**:
    - 주: `docs/prd.md`, `README.md`
  - **Edge Cases**: 없음 (문서)
  - **Halt Forecast**: 없음 — PRD 문구는 plan 승인에 포함(승인 프롬프트 제시)
  - **Depends on**: T1

## 사전 승인 항목 (일괄 승인 대상)
- T1 — `PlaylistsViewModel.NotifyLinkCopied()`·`NotifyLinkCopyFailed()` 공개 메서드 추가 (additive — 기존 알림 인프라 위임)
- T2 — docs/prd.md FR-18 문구 보강 (승인 프롬프트 제시 문구 그대로)

## 불가피한 Halt (위임 불가)
- push·main 병합·릴리즈 (이번 plan에 없음 — 최종 보고에서 별도)

## Verification Strategy
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` — 경고/에러 0
- 단위 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -p:Platform=x64` (회귀 확인 — 신규 테스트 없음, 4-C)
- 수동 검증 (HUMAN-VERIFY): ① 우클릭 메뉴 구조(구분선 2·공유 아이콘) ② 공유 팝업 표시·스타일(어두운 라운드 바, URL+복사) ③ 복사 → 클립보드 값 = 원본 URL + 성공 알림 ④ 기존 이동/삭제 동작 불변

## Phase Ledger

## Retry Ledger

## Progress Log

## Next Steps

## Open Questions
- (없음 — 갈림길은 코드 근거·사용자 명시 구조로 자체 확정, D1~D4)
