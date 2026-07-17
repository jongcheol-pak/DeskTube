# Plan: 한글 앱 이름 "데스크튜브" + 플레이리스트 마지막 선택 기억

**PRD**: docs/prd.md

## 요구 이해
- **원문 요청**: "플레이리스트 화면에서 플레이리스트 목록은 마지막에 선택한 항목을 기본 선택하도록 수정 / 홈 화면에서 주소 입력후 재생하면 기본 반복재생 / 앱 이름을 한글에서는 '데스크튜브'로 표시, 설치후 시작메뉴에 표시되는 앱 이름도 한글에서는 한글로 표시"
- **이해한 요구**: ① 플레이리스트 화면 진입 시 마지막에 선택했던 리스트를 기본 선택(없거나 삭제됐으면 첫 리스트 — 사용자 확정). ② 홈 즉시 재생 반복은 이번에 구현된 FR-7로 이미 전 모드 반복 동작 — **코드 변경 없음, 실행 확인만**(사용자 확정 — 최신 빌드에서도 안 되면 별도 버그 조사). ③ 한글 UI 환경에서 앱 이름을 "데스크튜브"로 — 시작메뉴(매니페스트)·창 제목·타이틀바·정보 화면·트레이 툴팁·시작 작업까지 전부 적용(사용자 확정), 영어 환경은 DeskTube 유지, 개발자명 "DeskTube Dev"는 불변.
- **포함하지 않는 것으로 이해**: 재생 반복 로직 변경(기구현), Store 등록 정보(스토어 리스팅 이름 — 제출 시 별도), 개발자명 현지화.

## Goal
한글 환경에서 앱 이름이 어디서나 "데스크튜브"로 보이고, 플레이리스트 화면이 마지막 선택 리스트를 기억해 기본 선택한다.

## PRD Coverage
| PRD ID | 우선순위 | 대응 task | 상태 |
|--------|---------|----------|------|
| FR-18 (플레이리스트 화면 — 마지막 선택 기본 선택 보강) | Must | T1, T2, T5 | ✅ 커버 |
| NFR-4 (다국어 — 앱 이름 현지화 보강) | Must | T3, T4, T5 | ✅ 커버 |
| FR-7 (반복 재생 — 홈 즉시 재생 포함 기구현 확인) | Must | (코드 변경 없음 — ⏳ HUMAN-VERIFY만) | ✅ 기구현 (2026-07-17 tray-repeat-license) |
| FR-1~FR-6, FR-8~FR-16, FR-19 (나머지 active Must) | Must | (없음) | 이번 범위 외 (기구현) |

## Out of Scope
- 개발자명 "DeskTube Dev"의 현지화 (사용자 확정 — 변경 없음)
- Microsoft Store 리스팅의 앱 이름 (Store 제출 시 별도 — deferred 대장 기존 항목)

## Deferred / Follow-up
- (없음 — 계획 시점)

## Investigation Log
- 플레이리스트 선택: `PlaylistsViewModel.SelectedPlaylist`(106행, TwoWay) — `Populate`(150-164행)가 목록 재구성 후 `ApplyPendingSelection`(칩 진입 예약)만 적용, 저장·복원 없음(기본 = 선택 없음). `OnSelectedPlaylistChanged`(194행)가 활성 표시·항목 갱신. 삭제 시 `SelectedPlaylist = null`(411-413행).
- 마지막 "재생" 리스트는 `AppSettings.LastPlaylistId`(재생 시 코디네이터가 기록)로 이미 존재 — "선택"과 의미가 달라(선택≠재생) 별도 필드 필요. 필드 추가는 JSON additive(하위 호환 — LastItemId 선례, autoplay plan T2).
- 홈 즉시 재생 반복: `PlaybackQueue.Next()`(80-123행) — 전 모드가 끝에서 순환(FR-7, 2026-07-17 갱신). 1곡 리스트는 Sequential `(i+1)%1=0`·Shuffle 재셔플·RepeatOne/All 모두 같은 곡 반환 → `AdvanceAsync`(456행) → `LoadAll`(471행, loadVideoById 재시작). **코드상 반복 갭 없음** — 사용자 확정: 코드 변경 없이 확인만.
- 앱 이름 표기 위치 전수 (grep "DeskTube|데스크튜브" — 소스·resw·manifest): ① `Package.appxmanifest` 16행 Properties/DisplayName, 32-33행 VisualElements DisplayName·Description (리터럴 "DeskTube") ② `MainWindow.xaml` 24행 타이틀바 TextBlock(하드코딩 — 22행 주석 "번역 대상 아님"은 이번 결정으로 무효) ③ `MainWindow.xaml.cs` 26행 `Title = "DeskTube"` ④ `AboutPage.xaml` 30행 앱 카드 이름 ⑤ resw `Tray_ToolTip`(ko 15-17행)·`StartupTaskDisplayName`(ko 294-296행) 값 "DeskTube" ⑥ About 개발자 문구 "DeskTube Dev"(불변 확정) ⑦ ko `PrivacySummary` 산문 첫머리 "DeskTube는…"(358행 — 리뷰 m1 검출, "전부 적용" 확정에 따라 "데스크튜브는…"으로 변경 대상) — 이상 전부이며 다른 사용자 노출 지점 없음.
- manifest 다국어: `<Resource Language="x-generate" />`(26행) + csproj `DefaultLanguage=en-US`(16행) — resw ko/en에서 PRI 생성됨. ms-resource 참조 형식은 StartupTask 선례(48행 `ms-resource:///Resources/StartupTaskDisplayName`)가 이 레포에서 검증된 유일 형식 (AGENTS 다국어 규칙 4: 전체 URI ms-resource://앱이름/... 는 resolve 실패 이력).
- AboutPage: 서비스 의존 없는 정적 페이지(code-behind 12-17행 생성자에서 ViewModel.Load) — 앱 이름 배정을 생성자에서 `Loc.Get`으로 가능. MainWindow도 생성자에서 Title 배정 중(26행) — 동일 지점 확장.
- `Loc.Get`: 키 부재 시 키 자체 반환(번역 누락 가시화 — Services/Loc.cs).
- 저장 패턴: fire-and-forget `Store.SaveSettingsAsync` + try/catch AppLog (MonitorPanelViewModel.ApplyAsync 164-175행 선례).
- 테스트 관례: `JsonStateStoreTests`에 설정 왕복 테스트 존재 — LastSelectedPlaylistId 왕복 추가 지점. VM 테스트 인프라 부재(deferred 대장 기지) — PlaylistsViewModel 동작은 HUMAN-VERIFY.
- Deferred 대장 확인: 이번 작업 직접 관련 항목 없음 (Store 리스팅 항목은 Out of Scope에 연동 표기).
- 위키 참조: 프로젝트 국소 UI·설정 변경이라 조회 생략 (직전 plan들에서 vault 무매칭/미설정 확인 이력).
- PRD 경량 확인: FR-18(화면 동작)·NFR-4(다국어)에 닿음 → PRD 갱신 제안(T5) + `**PRD**:` 연결. FR-7은 변경 없음(확인만).

## Risks & Unknowns
| 위험 | 영향 | 완화책 |
|---|---|---|
| manifest ms-resource resolve 실패 | 설치/시작메뉴 이름 깨짐(빈 값·에러) | 레포 검증 선례 형식(`ms-resource:///Resources/<키>`)만 사용, 배포 후 시작메뉴 HUMAN-VERIFY |
| 시작메뉴 이름이 OS 캐시로 즉시 안 바뀜 | 옛 이름 잔존처럼 보임 | 재배포(MSIX 재설치) 후 확인 안내 — HUMAN-VERIFY 절차에 명시 |
| Populate의 일시적 선택 해제(null)로 저장값 덮어씀 | 마지막 선택 유실 | null 선택은 저장하지 않음(비null만 영속 — T2 Edge) |
| 언어 전환(런타임) 시 창 제목·타이틀바가 옛 언어 잔존 | 표시 불일치 | 기존 언어 전환 재로드 경로 확인 — 셸 재로드가 창 제목까지 갱신하는지 T4에서 확인, 안 되면 전환 핸들러에서 Title 재배정 |

## Impact Analysis
### 4-A. 심볼/타입 추적 결과
| 심볼 | 영향 받는 파일 | 영향 종류 |
|---|---|---|
| `AppSettings.LastSelectedPlaylistId` (신규) | Models/AppSettings.cs, PlaylistsViewModel.cs, JsonStateStoreTests.cs | 추가 (JSON additive) |
| `PlaylistsViewModel.Populate/OnSelectedPlaylistChanged` | ViewModels/PlaylistsViewModel.cs (내부) | 동작 확장 — 시그니처 불변, 외부 호출자 없음(Load 경유) |
| resw `AppDisplayName` (신규 키) | resw ko/en, manifest, MainWindow.xaml.cs, AboutPage.xaml.cs | 추가 |
| resw `Tray_ToolTip`·`StartupTaskDisplayName` | ko resw 값만 변경 (키·en 불변, 소비처 TrayIconService 63행·manifest 48행 무수정) | 값 변경 |
| manifest DisplayName/VisualElements | Package.appxmanifest | 리터럴 → ms-resource 참조 |
| `MainWindow` 타이틀바 TextBlock | MainWindow.xaml(x:Name 부여), MainWindow.xaml.cs | 하드코딩 제거 |
| AboutPage 앱 카드 TextBlock | AboutPage.xaml(x:Name 부여), AboutPage.xaml.cs | 하드코딩 제거 |

### 4-B. 계약·직렬화 변경
- `AppSettings.LastSelectedPlaylistId` — JSON additive(기존 파일에 없으면 null 기본값), 하위 호환. 마이그레이션 불요 (LastItemId 선례).

### 4-C. 테스트 파일
- `tests/DeskTube.Tests/JsonStateStoreTests.cs` — 설정 왕복에 신규 필드 추가 (T1)
- PlaylistsViewModel·XAML·manifest는 테스트 인프라 부재 — HUMAN-VERIFY

### 4-D. 재사용 확인
| 신규 심볼 | 유사 기존 구현 검색 결과 | 재사용/신규 사유 |
|---|---|---|
| `AppSettings.LastSelectedPlaylistId` | `LastPlaylistId`(마지막 **재생** 리스트) 존재 — 의미 상이(선택≠재생, 재생 없이 선택만 해도 기억돼야 함) | 별도 필드 신규 (재사용하면 재생 시마다 선택 기록이 덮임) |
| resw `AppDisplayName` | `Tray_ToolTip`·`StartupTaskDisplayName`이 같은 값 보유 — 용도 특화 키라 의미 재사용 부적합 | 앱 이름 단일 키 신규, 코드 표시 3곳(창 제목·타이틀바·정보)이 공유 |
| 기본 선택 로직 | `ApplyPendingSelection`(칩 진입 예약) 존재 | 재사용·확장 — pending > 저장값 > 첫 리스트 우선순위로 같은 지점(Populate)에서 처리 |

### Verified by
- grep "LastPlaylistId" → 6 hits 전건 확인 (App·AppSettings·Coordinator×2·TrayIcon·PlaylistsViewModel 388행은 재생 표시용 — 이번 변경과 충돌 없음)
- grep "DeskTube|데스크튜브" (소스·resw·manifest, 산출물 제외) → 사용자 노출 지점 전수는 Investigation Log 항목 ①~⑥
- grep "SelectedPlaylist" → 17 hits 전건 Read (PlaylistsViewModel 내부 + PlaylistsPage 바인딩)

## Decisions
### D1. 홈 즉시 재생 반복 (요청 ②)
- **Chosen**: 코드 변경 없음 — FR-7 기구현으로 이미 전 모드 반복. 최종 보고 HUMAN-VERIFY에 "홈 즉시 재생 반복 확인" 포함, 안 되면 별도 버그 조사
- **Source**: 사용자 확정 (2026-07-17 질문) + PlaybackQueue.Next() 코드 확인

### D2. 마지막 선택 저장소
- **Options**: A) 기존 LastPlaylistId 재사용 / B) 신규 LastSelectedPlaylistId
- **Chosen**: B — 재생과 선택은 별개 이력 (A는 재생할 때마다 선택 기록이 덮여 요구와 어긋남)
- **Source**: AppSettings.cs·PlaybackCoordinator.cs 155행 확인

### D3. 선택 폴백
- **Chosen**: pending(칩 진입) > 저장된 마지막 선택 > 첫 리스트, 목록 비면 선택 없음. null 선택은 저장 안 함(일시 해제 보호)
- **Source**: 사용자 확정 (첫 리스트 폴백)

### D4. 앱 이름 적용 범위·값
- **Chosen**: ko = "데스크튜브" / en = "DeskTube", 적용: 매니페스트(시작메뉴·시작작업)·창 제목·타이틀바·정보 화면·트레이 툴팁 전부. 개발자명 불변
- **Source**: 사용자 확정 (전부 적용)

### D5. manifest ms-resource 형식
- **Chosen**: `ms-resource:///Resources/AppDisplayName` (Properties/DisplayName·VisualElements DisplayName·Description 3곳)
- **Rationale**: 이 레포에서 resolve 검증된 유일 형식(StartupTask 선례) — 문서상 단축형(ms-resource:키)보다 실증 우선
- **Source**: AGENTS 다국어 규칙 4 + manifest 48행

### D6. 앱 내 앱 이름 배정 방식
- **Chosen**: resw `AppDisplayName` 단일 키 + 각 화면 생성자에서 `Loc.Get("AppDisplayName")` 배정 (MainWindow Title·타이틀바 TextBlock, AboutPage 앱 카드) — x:Uid 복수 키 대신 단일 출처
- **Rationale**: x:Uid는 `{uid}.Text` 키가 화면마다 늘어남 — 같은 값의 키 증식 방지. MainWindow 생성자가 이미 Title을 배정 중(동일 지점 확장)
- **Source**: MainWindow.xaml.cs 26행, Loc.cs

## Tasks
- [x] T1. AppSettings.LastSelectedPlaylistId + 왕복 테스트
  - **Type**: C
  - **Design**: ① `Models/AppSettings.cs` ② `public Guid? LastSelectedPlaylistId { get; set; }` — 플레이리스트 화면의 마지막 선택 기억 1책임(재생 이력 LastPlaylistId와 별개 — D2) ③ PlaylistsViewModel만 읽고 씀 ④ 별도 저장 서비스·마이그레이션 없음(JSON additive)
  - **Acceptance**: Given 신규 필드에 Guid 저장 후 JsonStateStore 저장→재로드, Then 값 왕복 일치 + 필드 없는 기존 JSON 로드 시 null — 테스트 통과, 기존 테스트 전건 통과
  - **Files**:
    - 주: `src/DeskTube/Models/AppSettings.cs`
    - 테스트: `tests/DeskTube.Tests/JsonStateStoreTests.cs`
  - **Edge Cases**:
    - 필드 없는 기존 설정 JSON → null 기본값 (하위 호환)
  - **Halt Forecast**:
    - (ii-a) 직렬화 필드 추가(additive) → `## 사전 승인 항목`
  - **Depends on**: -

- [x] T2. 플레이리스트 화면 — 마지막 선택 저장·기본 선택
  - **Type**: C
  - **Design**: ① `ViewModels/PlaylistsViewModel.cs` 내부 ② 신규 공개 심볼 없음 — `OnSelectedPlaylistChanged`에서 비null 선택을 `LastSelectedPlaylistId`에 기록 + fire-and-forget 저장(MonitorPanelViewModel.ApplyAsync 선례), `Populate`의 기본 선택을 pending > 저장값 > 첫 리스트로 확장(D3) ③ VM → AppSettings·Store만 ④ 선택 이력 스택·MRU 목록은 만들지 않음(마지막 1개만)
  - **Acceptance**: Given 리스트 B 선택 후 앱 재시작(또는 화면 재진입), When 플레이리스트 화면 진입, Then B가 기본 선택·우측 항목 표시(⏳ HUMAN-VERIFY — VM 테스트 인프라 부재). Given 저장된 리스트 삭제 후 재진입, Then 첫 리스트 선택. Given 목록 빔, Then 선택 없음. 칩 진입은 기존대로 해당 리스트 선택(pending 우선). 빌드 통과
  - **Files**:
    - 주: `src/DeskTube/ViewModels/PlaylistsViewModel.cs`
  - **Edge Cases**:
    - Populate 재구성 중 일시적 null 선택 → 저장 안 함(비null만 영속)
    - 저장 실패(IO) → AppLog만, 화면 동작 계속 (선례 패턴)
    - 삭제로 `SelectedPlaylist = null`(411행) → 저장값 유지, 다음 진입 시 폴백(첫 리스트)
    - 서비스 미초기화(_services null) → 저장 생략
  - **Halt Forecast**: 없음 (내부 변경만 — plan에 근거 확정)
  - **Depends on**: T1

- [ ] T3. 앱 이름 리소스·매니페스트 현지화
  - **Type**: C
  - **Design**: ① resw ko/en + `Package.appxmanifest` ② resw 키 `AppDisplayName`(ko "데스크튜브"/en "DeskTube") — 앱 이름 단일 출처(D6) ③ manifest 3곳(Properties/DisplayName·VisualElements DisplayName·Description)이 `ms-resource:///Resources/AppDisplayName` 참조(D5), ko `Tray_ToolTip`·`StartupTaskDisplayName` 값만 "데스크튜브"로(en 불변) ④ PublisherDisplayName·개발자명은 건드리지 않음
  - **Acceptance**: 빌드 통과 + resw 양 언어에 AppDisplayName 존재 + manifest 3곳이 D5 형식 참조(리터럴 "DeskTube" 잔존 0 — Name/Publisher/PublisherDisplayName·Executable 제외) + ko Tray_ToolTip·StartupTaskDisplayName = "데스크튜브" + ko PrivacySummary 첫머리 "데스크튜브는…"(리뷰 m1, en 불변). 시작메뉴 한글 표시는 ⏳ HUMAN-VERIFY(재배포 후)
  - **Files**:
    - 주: `src/DeskTube/Package.appxmanifest`, `src/DeskTube/Strings/ko-KR/Resources.resw`, `src/DeskTube/Strings/en-US/Resources.resw`
  - **Edge Cases**:
    - ms-resource resolve 실패 시 배포 오류로 표면화 — D5 검증 선례 형식만 사용, 실패 시 원복 용이(3곳 국소)
    - 언어 미지원 로캘 → DefaultLanguage(en-US) 폴백
  - **Halt Forecast**:
    - (ii-a) manifest(배포 설정) 변경 → `## 사전 승인 항목`
  - **Depends on**: -

- [ ] T4. 앱 내 앱 이름 표시 — 하드코딩 제거
  - **Type**: C
  - **Design**: ① `MainWindow.xaml(.cs)`, `Views/AboutPage.xaml(.cs)` ② 신규 공개 심볼 없음 — 타이틀바 TextBlock·About 앱 카드 TextBlock에 x:Name 부여, 각 생성자에서 `Loc.Get("AppDisplayName")` 배정, `Title = "DeskTube"` → 동일 키(D6) ③ View → Loc(Services)만 ④ 바인딩·VM 경유로 만들지 않음(정적 1회 배정 — 언어 전환 시 셸 재로드가 재생성)
  - **Acceptance**: 빌드 통과 + 소스에서 사용자 노출 "DeskTube" 리터럴이 MainWindow.xaml(.cs)·AboutPage.xaml에서 제거됨(grep) + MainWindow.xaml 22행 낡은 주석("번역 대상 아님") 갱신. 한글 환경 창 제목·타이틀바·정보 화면 "데스크튜브" 표시는 ⏳ HUMAN-VERIFY. 언어 전환 직후 창 제목 갱신 여부 확인 — 셸 재로드 경로가 MainWindow를 재생성하지 않으면 전환 핸들러에서 Title·타이틀바 재배정 1줄 추가(Risks 4행)
  - **Files**:
    - 주: `src/DeskTube/MainWindow.xaml`, `src/DeskTube/MainWindow.xaml.cs`, `src/DeskTube/Views/AboutPage.xaml`, `src/DeskTube/Views/AboutPage.xaml.cs`
  - **Edge Cases**:
    - Loc 키 부재 → 키 문자열 표시(누락 가시화 — Loc 계약)
    - 런타임 언어 전환 → 창 제목 잔존 가능성(Risks) — acceptance의 확인·보정 포함
  - **Halt Forecast**: 없음 (내부 표시 변경만)
  - **Depends on**: T3

- [ ] T5. 문서 — PRD·README 갱신
  - **Type**: A
  - **Acceptance**: docs/prd.md — FR-18에 "화면 진입 시 마지막 선택 리스트 기본 선택(없으면 첫 리스트)" 보강, NFR-4에 "앱 이름 현지화(ko 데스크튜브 — 시작메뉴 포함)" 보강, 변경 이력 1줄. README — 다국어 항목에 앱 이름 현지화, 플레이리스트 항목에 마지막 선택 기억 반영
  - **Files**:
    - 주: `docs/prd.md`, `README.md`
  - **Edge Cases**: 없음 (문서)
  - **Halt Forecast**: 없음 — PRD 문구는 plan 승인에 포함(승인 프롬프트 제시)
  - **Depends on**: T2, T4

## 사전 승인 항목 (일괄 승인 대상)
- T1 — `AppSettings.LastSelectedPlaylistId` 직렬화 필드 추가 (JSON additive, 하위 호환 — LastItemId 선례)
- T3 — `Package.appxmanifest` 변경 (DisplayName 3곳 ms-resource 참조 전환 — 배포 설정, 비파괴)
- T5 — docs/prd.md FR-18·NFR-4 문구 보강 (승인 프롬프트 제시 문구 그대로)

## 불가피한 Halt (위임 불가)
- push·main 병합·릴리즈 (이번 plan에 없음 — 최종 보고에서 별도)

## Verification Strategy
- 빌드: `dotnet build DeskTube.slnx -c Debug -p:Platform=x64` — 경고/에러 0
- 단위 테스트: `dotnet test tests/DeskTube.Tests/DeskTube.Tests.csproj -p:Platform=x64`
- 수동 검증 (HUMAN-VERIFY): ① 플레이리스트 화면 재진입·재시작 시 마지막 선택 복원(삭제 시 첫 리스트) ② 한글 환경: 시작메뉴(재배포 후)·창 제목·타이틀바·정보 화면·트레이 툴팁 "데스크튜브" / 영어 환경 DeskTube ③ 언어 전환 직후 창 제목 갱신 ④ 홈 즉시 재생 반복(FR-7 기구현 확인 — 안 되면 별도 버그 보고)

## Phase Ledger

## Retry Ledger

## Progress Log
- T1-T2 완료 (커밋 6b68458, cf76bcc): LastSelectedPlaylistId 필드+왕복·하위호환 테스트(106/106, spec MINOR 1 즉시 반영 — 구형 JSON 테스트 추가) / VM 선택 기록+기본 선택(칩 예약 > 저장값 > 첫 리스트, 비null만 저장).

## Next Steps

## Open Questions
- [x] Q1. 홈 즉시 재생 반복 처리 — **확인만, 코드 변경 없음** (사용자 확정)
- [x] Q2. 앱 이름 적용 범위 — **전부 적용** (사용자 확정)
- [x] Q3. 선택 폴백 — **첫 리스트 선택** (사용자 확정)
