# 정보 화면 후원 링크 버튼 추가

**PRD**: docs/prd.md (FR-21 신설 — 이번 plan T1에서 추가)
**Plan Location**: docs/plans/2026-07-18-sponsor-link.md

## 요구 이해

> 원문: "설정 화면에 후원 링크 버튼 추가하는 계획 세워줘" → (질문 확정) 배치는 **정보(About) 페이지**, 단일 채널.

- 앱 안에 개발자 후원(기부) 링크를 클릭할 수 있는 카드/버튼을 추가한다.
- 클릭하면 후원 페이지(**GitHub Sponsors — https://github.com/sponsors/jongcheol-pak**)를 **기본 브라우저**로 연다.
- 배치는 사용자 확정에 따라 **정보(About) 페이지**(설정 창 내 정보 화면). 채널은 1개.
- 문구는 추천 기본안(한/영), 아이콘은 하트(♥). 다국어(ko/en) 리소스로 분리한다.
- 이 앱은 사용자 대면 기능을 PRD FR로 추적하므로, 후원 링크를 신규 **FR-21**로 등록한다(사용자 확정).

## Goal

정보 화면에 "개발 지원(후원)" 카드를 추가해, 클릭 시 GitHub Sponsors 페이지를 기본 브라우저로 여는 기능을 제공한다. 기존 라이선스 카드의 검증된 외부 링크 패턴을 그대로 재사용한다.

## Investigation Log

- **외부 URL 열기 패턴 (재사용)**: `src/DeskTube/Views/AboutPage.xaml.cs:32` — `await Windows.System.Launcher.LaunchUriAsync(new Uri(entry.Url));` (핸들러 `OnLicenseCardClick`, try-catch + `AppLog.Write` 로깅). 신규 NuGet 불필요 — `Windows.System.Launcher`는 WinUI 3 표준 API. (직접 Read 확인)
- **정보 화면 구조**: `src/DeskTube/Views/AboutPage.xaml` — 앱정보 카드(24-46) → 개인정보 카드(49-64) → 라이선스 목록(67-88). 라이선스는 `controls:SettingsCard`(`IsClickEnabled="True"` + `Click="OnLicenseCardClick"` + `ActionIcon` FontIcon `&#xE8A7;`). 공통 스타일 `AppCardBorderStyle`. (직접 Read 확인)
- **다국어**: `Strings/en-US/Resources.resw`·`Strings/ko-KR/Resources.resw` — About 키는 두 파일 모두 378~393행(`AboutHeader`/`PrivacyTitle`/`PrivacySummary`/`LicensesTitle`). `x:Uid` 자동 매핑(`.Header`/`.Description`/`.Text`). (grep 확인)
- **PRD**: `docs/prd.md`에 후원/기부 FR 없음. Out of Scope에도 후원 링크 제외 없음(광고 제거만 제외). → 신규 FR 여지 있음. (grep 확인)
- **Deferred 대장**: `docs/plans/deferred.md`에 후원 링크 관련 항목 없음. 관련 참고: "정보 화면 개인정보처리방침 안내를 호스팅 URL로 교체"(Store 제출 시) — 이번 작업과 무관. (직접 Read 확인)
- **AGENTS.md stale**: Stack 표기 net8.0(실물 net10.0-windows) — 이미 Deferred 등록됨(이번 범위 외).
- 위키 참조: vault 미설정 — 코드 1차 출처로 진행.

### 4-D. 재사용 확인

| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| `OnSupportCardClick` (핸들러) | `OnLicenseCardClick`(AboutPage.xaml.cs:23) 동일 구조 — 다른 URL 소스 | 신규 (URL 소스가 데이터 아닌 고정 후원 링크). 링크 열기 로직은 `Launcher.LaunchUriAsync` 패턴 재사용 |
| `SponsorUrl` (const) | 라이선스 URL은 `Assets/licenses/index.json` 데이터 기반 | 신규 — 단일 정적 링크라 상수가 적절(번역 대상 아님, resw 부적합) |
| 후원 카드 UI | `controls:SettingsCard` 클릭형(라이선스 카드) | **재사용** — 동일 컨트롤·스타일 |

## PRD Coverage

| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-21 (신설) | Could | T1(등록)·T2(구현) | ✅ 커버 |
| FR-1 ~ FR-20, NFR-* | Must/Should | — | 이번 범위 외 (기구현) |

> 소규모 PRD 연결 plan — 이번에 닿는 FR은 신설 FR-21뿐. 나머지 active FR은 기구현이므로 이 plan의 커버 대상 아님(Phase G는 FR-21만 재검증).

## Tasks

- [x] **T1** — PRD에 FR-21(후원 링크) 신설 (Type A)
- [x] **T2** — 정보 화면 후원 카드 구현 (Type C)

### T1 — PRD에 FR-21(후원 링크) 신설 · Type A (Doc)

`docs/prd.md`에 아래 FR을 기능 요구사항 표에 추가하고, 변경 이력에 1줄 기록한다.

- **추가할 FR** (원문 그대로 등록):
  > `| FR-21 | 정보(About) 화면에 개발 지원(후원) 링크를 표시하고, 클릭 시 후원 페이지(GitHub Sponsors)를 기본 브라우저로 연다. 링크는 다국어 문구로 표시한다 | Could | HUMAN-VERIFY: 클릭 시 브라우저에서 후원 페이지 열림 |`
- **변경 이력 추가** (맨 아래):
  > `- 2026-07-18: FR-21(정보 화면 개발 지원 후원 링크 — GitHub Sponsors) 신설 — 사용자 요청. plan: docs/plans/2026-07-18-sponsor-link.md.`
- **Files**: `docs/prd.md`
- **Acceptance**: `docs/prd.md`에 FR-21 행과 2026-07-18 변경 이력 줄이 존재. 표 형식(파이프 열 수)이 기존 행과 일치.
- **Edge Cases**: (Type A — skip)

### T2 — 정보 화면 후원 카드 구현 · Type C

정보(About) 화면 개인정보 카드와 라이선스 목록 **사이**에 "개발 지원" 후원 카드를 추가하고, 클릭 시 후원 URL을 기본 브라우저로 연다.

- **Design**:
  1. **배치**: `AboutPage.xaml`의 개인정보 카드(line 64) 다음, 라이선스 제목(line 67) 앞에 `controls:SettingsCard` 1개 추가. 핸들러·상수는 `AboutPage.xaml.cs`.
  2. **신규 심볼**: `OnSupportCardClick(object, RoutedEventArgs)` — 후원 URL을 `Launcher.LaunchUriAsync`로 연다(책임: 후원 링크 열기). `private const string SponsorUrl = "https://github.com/sponsors/jongcheol-pak";`
  3. **의존 방향**: `AboutPage.xaml.cs`가 `Windows.System.Launcher`·`AppLog` 참조(기존과 동일). ViewModel 변경 없음(정적 링크라 code-behind로 충분 — 라이선스 카드와 동일 방식).
  4. **비추상화 선언**: "후원 채널 목록/컬렉션" 추상화 도입하지 않음(단일 링크 — YAGNI). URL을 설정/서비스로 빼지 않음.
- **XAML** (개인정보 카드와 라이선스 제목 사이):
  ```xml
  <controls:SettingsCard x:Uid="SupportCard" IsClickEnabled="True" Click="OnSupportCardClick">
      <controls:SettingsCard.HeaderIcon>
          <FontIcon Glyph="&#xEB52;" />
      </controls:SettingsCard.HeaderIcon>
      <controls:SettingsCard.ActionIcon>
          <FontIcon Glyph="&#xE8A7;" FontSize="14" />
      </controls:SettingsCard.ActionIcon>
  </controls:SettingsCard>
  ```
  (`&#xEB52;` = 채운 하트 HeartFill, `&#xE8A7;` = 외부 링크 — 라이선스 카드와 동일 ActionIcon)
- **code-behind** (`OnLicenseCardClick`와 동일 try-catch 구조):
  ```csharp
  private async void OnSupportCardClick(object sender, RoutedEventArgs e)
  {
      try { await Windows.System.Launcher.LaunchUriAsync(new Uri(SponsorUrl)); }
      catch (Exception ex) { AppLog.Write($"후원 링크 열기 실패: {ex.GetType().Name} {ex.Message}"); }
  }
  ```
- **.resw** (en-US·ko-KR 두 파일 모두 About 키 구역에 추가):
  - en-US: `SupportCard.Header` = `Support development`, `SupportCard.Description` = `Support the project on GitHub Sponsors`
  - ko-KR: `SupportCard.Header` = `개발 지원`, `SupportCard.Description` = `GitHub Sponsors에서 프로젝트를 응원해주세요`
- **Files**: `src/DeskTube/Views/AboutPage.xaml`, `src/DeskTube/Views/AboutPage.xaml.cs`, `src/DeskTube/Strings/en-US/Resources.resw`, `src/DeskTube/Strings/ko-KR/Resources.resw`, `README.md`(핵심 기능 1줄 추가)
- **Acceptance**:
  - `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` 경고·에러 0.
  - AboutPage에 `SupportCard`(`x:Uid`) 클릭형 카드가 개인정보 카드와 라이선스 제목 사이에 존재.
  - 두 .resw에 `SupportCard.Header`/`SupportCard.Description` 키가 각각 존재(ko/en 값 상이).
  - `OnSupportCardClick`이 `SponsorUrl`을 `LaunchUriAsync`로 열고 실패는 `AppLog`로만 처리(앱 크래시 없음).
  - README "핵심 기능"에 후원 링크 1줄 추가.
  - (시각·클릭 동작: ⏳ HUMAN-VERIFY — 빌드로 확인 불가)
- **Edge Cases**: URL 열기 실패(브라우저 없음·차단) → catch 후 로그만, 앱 정상 유지(라이선스 카드와 동일). 빈/null URL 불가(상수 고정).
- **Halt Forecast**: 없음 — 신규 의존성·파괴적 작업·외부 인증 없음(브라우저 열기만).

## Deferred / Follow-up

- (없음)

## Out of Scope (영구 제외)

- 앱 내 결제·인앱 구매(Store Commerce) — 이번은 외부 링크만.
- 후원 채널 다중화(여러 플랫폼 카드) — 단일 채널 확정.
- 설정 페이지 배치 — 정보 화면으로 확정.

## 유의사항 (HUMAN, 비차단)

- **Microsoft Store 정책**: 외부 웹사이트(후원 페이지) 링크는 일반적으로 허용되나, Store 제출 전 최신 정책(디지털 상품의 Store 커머스 우회 금지 조항과 무관한 순수 후원 링크인지) 확인 권장. 개발·빌드에는 영향 없음.

## Open Questions

- [x] 배치 위치 — **정보(About) 페이지** (사용자 확정)
- [x] 채널 수 — **단일(링크 1개)** (사용자 확정)
- [x] PRD 반영 — **FR-21 신설** (사용자 확정)
- [x] 문구·아이콘 — **추천 기본안 + 하트 아이콘** (사용자 확정)
- [x] 후원 URL — **https://github.com/sponsors/jongcheol-pak** (사용자 제공)

## 통과 체크리스트

- [x] 근거 없는 단정 0 (Investigation Log 매칭)
- [x] `## 요구 이해` 작성됨
- [x] Impact Analysis 4-A~4-D (4-D 재사용 확인 포함)
- [x] plan-reviewer 이슈 0 (BLOCKER/MAJOR 0, MINOR 1(하트 글리프)은 EB52로 반영 완료)
- [x] 각 task acceptance 검증 가능·동시 만족 가능
- [x] Open Questions 모두 해결
- [x] 코드 작성 중 사용자 결정 분기 0
- [x] 각 task Type(A/C) 분류 명시
- [x] Design 필드 작성됨(T2 — 신규 심볼 Type C)
- [x] Edge Cases 명시(T1 skip, T2 명시)
- [x] Halt Forecast 명시(T2)
